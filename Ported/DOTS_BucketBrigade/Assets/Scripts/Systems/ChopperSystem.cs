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

    private static uint InvwkRnd(ref uint seed)
    {
        seed += ((seed * seed) | 5u);
        return seed;
    }

    private static float InvwkRndf(ref uint seed)
    {
        return math.asfloat((InvwkRnd(ref seed) >> 9) | 0x3f800000) - 1.0f;
    }

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
        var grid = GetSingleton<Grid>();
        float dt = Time.DeltaTime;
        float et = (float)Time.ElapsedTime;
        
        var handle = Entities
            .WithNone<Destination2D>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Chopper c, ref Translation t, in FromTo ft, in Position2D pos) =>
            {
                uint seed = math.asuint(et);
                InvwkRnd(ref seed);
                seed *= (1 + (uint)entity.Index);

                switch (c.State)
                {
                    case Chopper.ActionState.MovingUp:
                    {
                        c.VerticalPos = math.min(c.MaxVerticalPos, c.VerticalPos + c.VerticalSpeed * dt);
                        if (c.VerticalPos >= c.MaxVerticalPos)
                        {
                            float2 dest;
                            if (c.DropFire)
                                dest = (new float2(InvwkRndf(ref seed), InvwkRndf(ref seed)) * 2 - 1) * 50;
                            else
                                dest = c.IsToDropWaterOnFire ? ft.Target : ft.Source;

                            ecb.AddComponent<Destination2D>(entityInQueryIndex, entity, new Destination2D
                            {
                                Value = dest
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

                        if (c.DropFire && !(grid.Physical.ContainsKey(grid.ToGridPos(pos))))
                        {
                            var fire = ecb.Instantiate(entityInQueryIndex, gameMaster.FirePrefab);
                            ecb.SetComponent(entityInQueryIndex, fire, new GradientState {Value = 1.0f});
                            ecb.SetComponent(entityInQueryIndex, fire, new PositionInGrid {Value = grid.ToGridPos(pos)});
                            ecb.SetComponent(entityInQueryIndex, fire, new Translation {Value = new float3(grid.ToPos2D(grid.ToGridPos(pos)), 0).xzy});
                        }
                        else
                        {
                            if (c.IsToDropWaterOnFire && (grid.Physical.TryGetValue(grid.ToGridPos(pos), out Grid.Cell cell)) && cell.Flags == Grid.Cell.ContentFlags.Fire)
                            {
                                var bucket = ecb.Instantiate(entityInQueryIndex, gameMaster.BucketPrefab);
                                ecb.SetComponent(entityInQueryIndex, bucket, new GradientState {Value = 1.0f});
                                ecb.SetComponent(entityInQueryIndex, bucket, new Position2D {Value = pos.Value});
                                ecb.AddComponent<DestroyBucketWhenEmptyTag>(entityInQueryIndex, bucket);
                            }

                            c.IsToDropWaterOnFire = !c.IsToDropWaterOnFire;
                        }

                        break;
                    }
                }

                t.Value = new float3(pos.Value, c.VerticalPos).xzy;

            }).Schedule(inputDeps);

        var handleMove = Entities
            .WithAll<Destination2D>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Chopper c, ref Translation t, in FromTo ft, in Position2D pos) =>
            {
                if (!c.DropFire)
                {
                    ecb.SetComponent<Destination2D>(entityInQueryIndex, entity, new Destination2D
                    {
                        Value = c.IsToDropWaterOnFire ? ft.Target : ft.Source
                    });
                }

                t.Value = new float3(pos.Value, c.VerticalPos).xzy;
            }).Schedule(handle);

        m_CommandBufferSystem.AddJobHandleForProducer(handle);
        
        return handleMove;
    }
}
