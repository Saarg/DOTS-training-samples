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
        public float DeltaTime;

        public WaterMaster WaterMaster;
        
        [ReadOnly]
        public ComponentDataFromEntity<Carrying> CarryingFromEntity;
        [ReadOnly]
        public ComponentDataFromEntity<GradientState> GradientFromEntity;
        
        public void Execute(Entity entity, int index, ref Position2D pos, ref Destination2D dest, [ReadOnly]ref MovementSpeed speed)
        {
            var dist = speed.Value * DeltaTime;

            if (CarryingFromEntity.HasComponent(entity) && GradientFromEntity[CarryingFromEntity[entity].Value].Value >= 1.0f)
            {
                dist *= WaterMaster.CarryMultiplier;
            }
            
            var diff = dest.Value - pos.Value;
            var len = math.length(diff);
            var invLen = math.rcp(len);
            
            pos.Value += diff * invLen * math.min(dist, len);
            
            if (dist >= len)
            {
                pos.Value = dest.Value;
                EntityCommandBuffer.RemoveComponent<Destination2D>(m_ThreadIndex, entity);
            }
        }
    }

    protected override void OnUpdate()
    {
        var job = new MoveToDestinationJob
        {
            EntityCommandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            DeltaTime = Time.DeltaTime,
            WaterMaster = GetSingleton<WaterMaster>(),
            CarryingFromEntity = GetComponentDataFromEntity<Carrying>(true),
            GradientFromEntity = GetComponentDataFromEntity<GradientState>(true)
        };

        Dependency = job.Schedule(this, Dependency);
        m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        
    }

    protected override void OnCreate()
    {
        m_CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        
        RequireSingletonForUpdate<WaterMaster>();
    }
}
