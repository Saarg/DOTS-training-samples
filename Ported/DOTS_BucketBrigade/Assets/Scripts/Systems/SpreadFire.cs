using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

[UpdateAfter(typeof(ConsolidateFireFront))] 
public class SpreadFire : JobComponentSystem
{
    private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;
    
    // FIXME(tim): should account for grid radius
    private static NativeArray<int2> m_AroundCells;

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
        m_AroundCells = new NativeArray<int2>(new int2[]
        {
            new int2(-1, -1),
            new int2(-1, 0),
            new int2(-1, 1),
            new int2(0, -1),
            // new int2(0, 0),
            new int2(0, 1),
            new int2(1, -1),
            new int2(1, 0),
            new int2(1, 1),
        }, Allocator.Persistent);
        
        m_CommandBufferSystem = World
            .DefaultGameObjectInjectionWorld
            .GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var aroundCells = m_AroundCells;

        EntityCommandBuffer.Concurrent ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var grid = GetSingleton<Grid>();
        var fireMaster = GetSingleton<FireMaster>();
        var waterMaster = GetSingleton<WaterMaster>();

        var gradientStateData = GetComponentDataFromEntity<GradientState>(true);

        float dt = Time.DeltaTime;
        uint seed = math.asuint(dt);
        InvwkRnd(ref seed);
        
        var simFireHandle = Entities
            .WithAll<FireTag>()
            .WithNone<MaxOutFireTag>()
            .ForEach((Entity entity, int entityInQueryIndex, ref GradientState state, ref Translation t, in PositionInGrid posInGrid) =>
                {
                    float acc = 0;
                    uint rseed = seed + (uint)entityInQueryIndex + math.asuint(state.Value);
                    // Compute transfer rate
                    for (var i = 0; i < aroundCells.Length; ++i)
                    {
                        int2 currentPos = aroundCells[i] + posInGrid.Value;
                        if (grid.Physical.ContainsKey(currentPos))
                        {
                            var cell = grid.Physical[currentPos];
                            var gs = gradientStateData[cell.Entity];

                            float sign = (cell.Flags == Grid.Cell.ContentFlags.Fire ? 1 : -1);

                            acc += sign * gs.Value * fireMaster.HeatTransferRate * dt * InvwkRndf(ref rseed);
                        }
                    }

                    // Harry, you're a fire
                    state.Value = math.max(0, state.Value + acc);
                    

                    // When Maxed-out, don't update the fire anymore unless something happens
                    if (state.Value >= 1.0f)
                    {
                        state.Value = 1.0f;
                        ecb.AddComponent<MaxOutFireTag>(entityInQueryIndex, entity);
                        t.Value.y = 1.0f - InvwkRndf(ref rseed) * 0.1f;
                    }
                    else
                    {
                        t.Value.y = (state.Value - fireMaster.Flashpoint) / (1.0f - fireMaster.Flashpoint);
                    }
                })
            .WithReadOnly(aroundCells)
            .WithReadOnly(gradientStateData)
            .WithNativeDisableContainerSafetyRestriction(gradientStateData)
            .Schedule(inputDeps);
        
        var spreadHandle = Entities
            .WithAll<PreFireTag>()
            .ForEach((Entity entity, int entityInQueryIndex, in GradientState state, in PositionInGrid posInGrid) =>
                {
                    // something is already present in this cell, we should be deleted
                    if (grid.Physical.ContainsKey(posInGrid.Value))
                    {
                        ecb.AddComponent<ToDeleteFromGridTag>(entityInQueryIndex, entity);
                        return;
                    }
                    if (state.Value > fireMaster.Flashpoint)
                    {
                        ecb.RemoveComponent<PreFireTag>(entityInQueryIndex, entity);
                        ecb.AddComponent<FireTag>(entityInQueryIndex, entity);
                        ecb.AddComponent<NewFireTag>(entityInQueryIndex, entity);

                        // FIXME: remove
                        float3 pos = new float3(grid.ToPos2D(posInGrid.Value), 0).xzy;
                        ecb.SetComponent<Translation>(entityInQueryIndex, entity, new Translation() {Value = pos});
                    }
                })
            .Schedule(simFireHandle);

        m_CommandBufferSystem.AddJobHandleForProducer(spreadHandle);

        return spreadHandle;
    }

    protected override void OnDestroy()
    {
        m_AroundCells.Dispose();
    }
}