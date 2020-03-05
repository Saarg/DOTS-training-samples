using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
[UpdateAfter(typeof(PickupSystem))]
public class MoveToDestinationSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem m_CommandBufferSystem;
    
    [BurstCompile]
    struct MoveToDestinationJob : IJobForEachWithEntity<Position2D, Destination2D, MovementSpeed>
    {
        [NativeSetThreadIndex]
        int m_ThreadIndex;
        
        public EntityCommandBuffer.Concurrent EntityCommandBuffer;
        
        public void Execute(Entity entity, int index, ref Position2D pos, ref Destination2D dest, [ReadOnly]ref MovementSpeed speed)
        {
            var diff = dest.Value - pos.Value;
            var len = math.length(diff);
            var invLen = math.rcp(len);
            
            pos.Value += diff * invLen * math.min(speed.Value, len);
            
            if (speed.Value >= len)
            {
                EntityCommandBuffer.RemoveComponent<Destination2D>(m_ThreadIndex, entity);
            }
        }
    }
    
    protected override void OnUpdate()
    {
        var job = new MoveToDestinationJob
        {
            EntityCommandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };

        Dependency = job.Schedule(this, Dependency);
        m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        
    }

    protected override void OnCreate()
    {
        m_CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
}
