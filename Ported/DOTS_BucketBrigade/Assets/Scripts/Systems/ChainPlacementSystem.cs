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
    struct ChainBotPlacementJob : IJobForEachWithEntity<InLine, Position2D, Role>
    {
        [NativeSetThreadIndex]
        int m_ThreadIndex;
        
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public float2 Source;
        public float2 Target;
        
        public void Execute(Entity entity, int index, [ReadOnly] ref InLine inLine, [ReadOnly] ref Position2D pos, [ReadOnly] ref Role role)
        {
            var dest = GetChainPosition(inLine.Progress, role.Value);

            if (math.lengthsq(pos.Value - dest) > 0.001f)
            {
                CommandBuffer.AddComponent(m_ThreadIndex, entity, new Destination2D{Value = dest});
            }
        }
        
        float2 GetChainPosition(float progress, BotRole role)
        {
            switch (role)
            {
                case BotRole.Throw:
                    return Target;
                case BotRole.Fill:
                    return Source;
                default:
                    break;
            }

            progress *= 2;
            if(progress > 1.0f)
                progress -= 1;
            
            var curveOffset = Mathf.Sin(progress * Mathf.PI) * 1f;
            
            var diff = Source - Target;
            if (role == BotRole.PassEmpty)
                diff = Target - Source;
            diff = math.normalizesafe(diff);
            var perpendicular = new float2(diff.y, -diff.x);

            var source = role == BotRole.PassFull ?  Source : Target;
            var target = role == BotRole.PassFull ?  Target : Source;
            var newPosition = math.lerp(source, target, progress);
            newPosition += perpendicular * curveOffset;
            return newPosition;
        }
    }
    
    protected override void OnCreate()
    {
        m_EntityQuery = GetEntityQuery(ComponentType.ReadOnly<InLine>(), ComponentType.ReadOnly<Position2D>()
            , ComponentType.ReadOnly<ChainParentComponent>(), ComponentType.Exclude<Destination2D>(), ComponentType.ReadOnly<Role>());
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
