using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

//[UpdateAfter()]
public class ConsolidateFireFront : JobComponentSystem
{
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
    
    private EndSimulationEntityCommandBufferSystem m_CommandBufferSystem;
    
    protected override void OnCreate()
    {
        m_CommandBufferSystem = World
            .DefaultGameObjectInjectionWorld
            .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        EntityCommandBuffer.Concurrent ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var grid = GetSingleton<Grid>();
        var gameMaster = GetSingleton<GameMaster>();
        var aroundCells = m_AroundCells;

        // Spawn Fires
        var physicalParallelGrid = grid.Physical.AsParallelWriter();
        var simParallelGrid = grid.Simulation.AsParallelWriter();
        var spawnFireHandle = Entities.ForEach(
                (Entity entity, int entityInQueryIndex, NewFireTag tag, PositionInGrid posInGrid) =>
                {
                    // Add to the grid
                    if (!physicalParallelGrid.TryAdd(posInGrid.Value,
                        new Grid.Cell {Entity = entity, Flags = Grid.Cell.ContentFlags.Fire}))
                    {
                        // There's already something !
                        ecb.DestroyEntity(entityInQueryIndex, entity);
                        return;
                    }

                    // Remove the new-fire tag, add the fire-front tag
                    ecb.RemoveComponent<NewFireTag>(entityInQueryIndex, entity);
                    ecb.RemoveComponent<FireFrontTag>(entityInQueryIndex, entity);

                    // Spawn pre-fires around the fire
                    for (var i = 0; i < aroundCells.Length; ++i)
                    {
                        var currentPos = aroundCells[i] + posInGrid.Value;

                        if (math.any(math.abs(currentPos) > 120))
                            continue;
                        
                        if (simParallelGrid.TryAdd(currentPos, 0))
                        {
                            var preFireEntity = ecb.Instantiate(entityInQueryIndex, gameMaster.FirePrefab);
                            ecb.AddComponent<PreFireTag>(entityInQueryIndex, preFireEntity);
                            ecb.RemoveComponent<NewFireTag>(entityInQueryIndex, preFireEntity);
                            ecb.AddComponent<PositionInGrid>(entityInQueryIndex, preFireEntity, new PositionInGrid{ Value = currentPos });
                            ecb.SetComponent<GradientState>(entityInQueryIndex, preFireEntity, new GradientState());
                            
                            // FIXME: remove
                            ecb.SetComponent<Translation>(entityInQueryIndex, preFireEntity, new Translation(){ Value = (float3)(new int3(currentPos.x, -1, currentPos.y))});
                        }
                    }
                })
            .WithReadOnly(aroundCells)
            .Schedule(inputDeps);
        
        // Remove the FireFrontTag of fires that aren't in the front of the fire
        var removeFireFrontHandle = Entities.ForEach((Entity entity, int entityInQueryIndex, FireFrontTag tag, PositionInGrid posInGrid) =>
            {
                var isInFront = true;
                for (var i = 0; i < aroundCells.Length; ++i)
                {
                    if (grid.Physical.ContainsKey(posInGrid.Value + aroundCells[i]))
                        isInFront = false;
                }
                
                if (!isInFront)
                    ecb.RemoveComponent<FireFrontTag>(entityInQueryIndex, entity);
                
            })
            .WithReadOnly(aroundCells)
            .Schedule(spawnFireHandle);
        
        m_CommandBufferSystem.AddJobHandleForProducer(removeFireFrontHandle);
        
        return removeFireFrontHandle;
    }
}
