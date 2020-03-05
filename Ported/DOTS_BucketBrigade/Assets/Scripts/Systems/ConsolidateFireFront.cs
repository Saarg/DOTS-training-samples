using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

//[UpdateAfter()]
public class ConsolidateFireFront : JobComponentSystem
{
    private static NativeArray<int2> m_AroundCells;
    
    private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;
    
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
        EntityCommandBuffer.Concurrent ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var grid = GetSingleton<Grid>();
        var gameMaster = GetSingleton<GameMaster>();
        var aroundCells = m_AroundCells;
        var physicalParallelGrid = grid.Physical.AsParallelWriter();
        var simParallelGrid = grid.Simulation.AsParallelWriter();

        // Check sim-fires and remove those that aren't a smi-fire anymore
        var removeSimFireHandle = Entities
            .WithNone<ToDeleteFromGridTag>()
            .WithAll<PreFireTag>()
            .ForEach(
            (Entity entity, int entityInQueryIndex, in PositionInGrid posInGrid) =>
            {
                // Check that a fire is somewhere around the fire sim
                for (var i = 0; i < aroundCells.Length; ++i)
                {
                    var currentPos = aroundCells[i] + posInGrid.Value;
                    if (grid.Physical.TryGetValue(currentPos, out Grid.Cell cell) && cell.Flags == Grid.Cell.ContentFlags.Fire)
                    {
                        return;
                    }
                }
                
                // We got here, so none is present, so remove the fire-sim:
                ecb.AddComponent<ToDeleteFromGridTag>(entityInQueryIndex, entity);
            })
            .WithReadOnly(aroundCells)
            .Schedule(inputDeps);
        
        // Spawn Fires
        var addFireToGridHandle = Entities
            .WithNone<ToDeleteFromGridTag>()
            .WithAll<NewFireTag>()
            .ForEach(
                (Entity entity, int entityInQueryIndex, in PositionInGrid posInGrid) =>
                {
                    // Add to the grid
                    if (!physicalParallelGrid.TryAdd(posInGrid.Value,
                        new Grid.Cell {Entity = entity, Flags = Grid.Cell.ContentFlags.Fire}))
                    {
                        // There's already something ! (we cannot use ToDeleteFromGrid as the grid already has something)
                        ecb.DestroyEntity(entityInQueryIndex, entity);
                        return;
                    }

                    // Remove the new-fire tag, add the fire-front tag
                    ecb.RemoveComponent<NewFireTag>(entityInQueryIndex, entity);
                })
            .Schedule(removeSimFireHandle);
        
        var spawnFireHandle = Entities
            .WithNone<ToDeleteFromGridTag, PreFireTag>()
            .WithAll<SpawnAroundSimFireTag>()
            .ForEach(
                (Entity entity, int entityInQueryIndex, in PositionInGrid posInGrid) =>
                {
                    if (grid.Physical.TryGetValue(posInGrid.Value, out Grid.Cell cell) && cell.Entity != entity)
                        return;
                    // Remove the new-fire tag, add the fire-front tag
                    ecb.RemoveComponent<SpawnAroundSimFireTag>(entityInQueryIndex, entity);

                    bool hasAroundCells = false;
                    // Spawn pre-fires around the fire
                    for (var i = 0; i < aroundCells.Length; ++i)
                    {
                        var currentPos = aroundCells[i] + posInGrid.Value;

                        if (grid.Physical.ContainsKey(currentPos))
                            continue;
                        
                        if (simParallelGrid.TryAdd(currentPos, 0))
                        {
                            hasAroundCells = true;
                            var preFireEntity = ecb.Instantiate(entityInQueryIndex, gameMaster.FirePrefab);
                            ecb.AddComponent<PreFireTag>(entityInQueryIndex, preFireEntity);
                            ecb.RemoveComponent<NewFireTag>(entityInQueryIndex, preFireEntity);
                            ecb.AddComponent<PositionInGrid>(entityInQueryIndex, preFireEntity, new PositionInGrid{ Value = currentPos });
                            ecb.SetComponent<GradientState>(entityInQueryIndex, preFireEntity, new GradientState());
                            
                            // FIXME: remove
                            float3 pos = new float3(grid.ToPos2D(currentPos), -1.0f).xzy;

                            ecb.SetComponent<Translation>(entityInQueryIndex, preFireEntity, new Translation(){ Value = pos});
                        }
                    }
                    if (hasAroundCells)
                        ecb.AddComponent<FireFrontTag>(entityInQueryIndex, entity);
                })
            .WithReadOnly(aroundCells)
            .Schedule(addFireToGridHandle);
        
        // Remove the FireFrontTag of fires that aren't in the front of the fire
        var removeFireFrontHandle = Entities
            .WithNone<ToDeleteFromGridTag>()
            .WithAll<FireFrontTag>()
            .ForEach((Entity entity, int entityInQueryIndex, in PositionInGrid posInGrid) =>
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

    protected override void OnDestroy()
    {
        m_AroundCells.Dispose();
    }
}
