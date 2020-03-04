using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(ConsolidateFireFront))] 
public class SpreadFire : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem m_CommandBufferSystem;
    
    // FIXME(tim): should account for grid radius
    private static readonly NativeArray<int2> m_AroundCells = new NativeArray<int2>(new int2[]
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
    
    protected override void OnCreate()
    {
        m_CommandBufferSystem = World
            .DefaultGameObjectInjectionWorld
            .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
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
        
        var simFireHandle = Entities
            .WithAll<FireTag>()
            .WithNone<MaxOutFireTag>()
            .ForEach((Entity entity, int entityInQueryIndex, ref GradientState state, in PositionInGrid posInGrid) =>
                {
                    float acc = 0;
                    // Compute transfer rate
                    for (var i = 0; i < aroundCells.Length; ++i)
                    {
                        int2 currentPos = aroundCells[i] + posInGrid.Value;
                        if (grid.Physical.ContainsKey(currentPos))
                        {
                            var cell = grid.Physical[currentPos];
                            var gs = gradientStateData[cell.Entity];

                            float sign = (cell.Flags == Grid.Cell.ContentFlags.Fire ? 1 : -1);

                            acc += sign * gs.Value * fireMaster.HeatTransferRate * dt;
                        }
                    }

                    // Harry, you're a fire
                    state.Value += acc;

                    // When Maxed-out, don't update the fire anymore unless something happens
                    if (state.Value >= 1.0f)
                    {
                        state.Value = 1.0f;
                        ecb.AddComponent<MaxOutFireTag>(entityInQueryIndex, entity);
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
                        ecb.SetComponent<Translation>(entityInQueryIndex, entity,
                            new Translation() {Value = new float3(posInGrid.Value.x, 0.5f, posInGrid.Value.y)});
                    }
                })
            .Schedule(simFireHandle);

        m_CommandBufferSystem.AddJobHandleForProducer(spreadHandle);

        return spreadHandle;
    }
}