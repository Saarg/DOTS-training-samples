using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(TargetingSystem))]
public class ChopperSystem : JobComponentSystem
{
    private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;

    protected override void OnCreate()
    {
        m_CommandBufferSystem = World
            .DefaultGameObjectInjectionWorld
            .GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        EntityCommandBuffer.Concurrent ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var gameMaster = GetSingleton<GameMaster>();
        float dt = Time.DeltaTime;
        
        var handle = Entities
            .WithNone<Destination2D>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Chopper c, ref Translation t, in FromTo ft, in Position2D pos) =>
            {
                switch (c.State)
                {
                    case Chopper.ActionState.MovingUp:
                    {
                        c.VerticalPos = math.min(c.MaxVerticalPos, c.VerticalPos + c.VerticalSpeed * dt);
                        if (c.VerticalPos >= c.MaxVerticalPos)
                        {
                            ecb.AddComponent<Destination2D>(entityInQueryIndex, entity, new Destination2D
                            {
                                Value = c.IsToDropWaterOnFire ? ft.Target : ft.Source
                            });
                            c.State = Chopper.ActionState.FlyingToDestination;
                        }

                        break;
                    }
                    case Chopper.ActionState.Dropping:
                    {
                        float minPos = c.IsToDropWaterOnFire ? 3.0f : 0.2f;
                        c.VerticalPos = math.max(minPos, c.VerticalPos - c.VerticalSpeed * dt);
                        if (c.VerticalPos <= minPos)
                            c.State = Chopper.ActionState.PerformingAction;
                        break;
                    }
                    case Chopper.ActionState.FlyingToDestination:
                    {
                        c.State = Chopper.ActionState.Dropping;
                        break;
                    }
                    case Chopper.ActionState.PerformingAction:
                    {
                        c.State = Chopper.ActionState.MovingUp;

                        if (c.IsToDropWaterOnFire)
                        {
                            var bucket = ecb.Instantiate(entityInQueryIndex, gameMaster.BucketPrefab);
                            ecb.SetComponent(entityInQueryIndex, bucket, new GradientState { Value = 1.0f });
                            ecb.SetComponent(entityInQueryIndex, bucket, new Position2D { Value = pos.Value });
                        }
                        
                        c.IsToDropWaterOnFire = !c.IsToDropWaterOnFire;
                        break;
                    }
                }

                t.Value = new float3(pos.Value, c.VerticalPos).xzy;

            }).Schedule(inputDeps);

        var handleMove = Entities
            .WithAll<Destination2D>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Chopper c, ref Translation t, in FromTo ft, in Position2D pos) =>
            {
                t.Value = new float3(pos.Value, c.VerticalPos).xzy;
            }).Schedule(handle);

        m_CommandBufferSystem.AddJobHandleForProducer(handle);
        
        return handleMove;
    }
}
