using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

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
                    // First: grad ownership of the entities to spawn
                    foreach (var pos in aroundCells)
                    {
                        var currentPos = pos + posInGrid.Value;
                        if (simParallelGrid.TryAdd(currentPos, 0))
                        {
                            var preFireEntity = ecb.Instantiate(entityInQueryIndex, gameMaster.FirePrefab);
                            ecb.AddComponent<PreFireTag>(entityInQueryIndex, preFireEntity);
                        }
                    }
                })
            .WithReadOnly(aroundCells)
            .Schedule(inputDeps);
        
        // Remove the FireFrontTag of fires that aren't in the front of the fire
        var removeFireFrontHandle = Entities.ForEach((Entity entity, int entityInQueryIndex, FireFrontTag tag, PositionInGrid posInGrid) =>
            {
                var isInFront = true;
                foreach (var pos in aroundCells)
                {
                    if (grid.Physical.ContainsKey(pos))
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
