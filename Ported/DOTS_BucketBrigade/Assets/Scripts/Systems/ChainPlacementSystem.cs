using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateBefore(typeof(MoveToDestinationSystem))]
public class ChainPlacementSystem : SystemBase
{
    EntityQuery m_EntityQuery;
    private EndSimulationEntityCommandBufferSystem m_CommandBufferSystem;

    [BurstCompile]
    struct ChainBotPlacementJob : IJobForEachWithEntity<InLine, Position2D>
    {
        [NativeSetThreadIndex]
        int m_ThreadIndex;
        
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public float2 Source;
        public float2 Target;
        
        public void Execute(Entity entity, int index, [ReadOnly] ref InLine inLine, [ReadOnly] ref Position2D pos)
        {
            var dest = GetChainPosition(inLine.Progress);

            var same = pos.Value == dest;
            if (!same.x || !same.y)
            {
                CommandBuffer.AddComponent(m_ThreadIndex, entity, new Destination2D{Value = dest});
            }
        }
        
        float2 GetChainPosition(float progress)
        {
            var curveOffset = Mathf.Sin(progress * Mathf.PI) * 1f;
            
            return new float2(math.lerp(Source.x, Target.x, curveOffset), math.lerp(Source.y, Target.y, curveOffset));
        }
    }
    
    protected override void OnCreate()
    {
        m_EntityQuery = GetEntityQuery(ComponentType.ReadOnly<InLine>(), ComponentType.ReadOnly<Position2D>()
            , ComponentType.ReadOnly<ChainParentComponent>(), ComponentType.Exclude<Destination2D>());
        m_CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    
    protected override void OnUpdate()
    {
        var chainList = new List<ChainParentComponent>();
        EntityManager.GetAllUniqueSharedComponentData(chainList);

        var dependencyList = new NativeList<JobHandle>(chainList.Count, Allocator.TempJob);
        
        foreach (var chain in chainList)
        {
            if (chain.Chain == Entity.Null)
                continue;
            var fromTo = EntityManager.GetComponentData<FromTo>(chain.Chain);

            if (fromTo.Source == Entity.Null || fromTo.Target == Entity.Null)
                continue;
            
            var job = new ChainBotPlacementJob
            {
                Source = EntityManager.GetComponentData<PositionInGrid>(fromTo.Source).Value,
                Target = EntityManager.GetComponentData<PositionInGrid>(fromTo.Target).Value,
                CommandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent()
            };
            m_EntityQuery.SetSharedComponentFilter(chain);
            dependencyList.Add( job.Schedule(m_EntityQuery, Dependency));
        }

        Dependency = JobHandle.CombineDependencies(dependencyList);
        
        Dependency = dependencyList.Dispose(Dependency);
        
        m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
