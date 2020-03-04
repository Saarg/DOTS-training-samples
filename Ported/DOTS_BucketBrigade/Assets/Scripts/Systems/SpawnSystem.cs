using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class SpawnSystem : JobComponentSystem
{
    protected override void OnCreate()
    {
        base.OnCreate();

        RequireSingletonForUpdate<SpawnPrefabsTag>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var gameMaster = GetSingleton<GameMaster>();
        var grid = GetSingleton<Grid>();

        // Per chain, we have 2 bots that are used to scoop the water and to throw the bucket, plus all the bots in the chain itself and
        // the number of omnibots 
        var botCount = gameMaster.NbChains * gameMaster.NbBotsPerChain + gameMaster.NbChains * 2 + gameMaster.NbOmnibots;
        // Spawn Bots
        SpawnEntities(grid, botCount, gameMaster.BotPrefab, gameMaster.NbRows, gameMaster.NbCols);

        // Spawn buckets
        SpawnEntities(grid, gameMaster.NbBuckets, gameMaster.BucketPrefab, gameMaster.NbRows, gameMaster.NbCols);

        // Spawn fires
        SpawnEntities(grid, gameMaster.NbFires, gameMaster.FirePrefab, gameMaster.NbRows, gameMaster.NbCols, true);

        var spawnPrefabEntity = GetSingletonEntity<SpawnPrefabsTag>();
        EntityManager.DestroyEntity(spawnPrefabEntity);

        return inputDeps;
    }

    void SpawnEntities(Grid grid, int count, Entity prefab, int nbRows, int nbCols, bool isInGrid = false)
    {
        // using block so we don't forgot to .Dispose()
        using (var spawnedEntities = new NativeArray<Entity>(count, Allocator.TempJob))
        {
            EntityManager.Instantiate(prefab, spawnedEntities);

            // Initialize a random position for each entity, there will be a system that will copy
            // the Position2D we give and transform it to a LocalToWorld
            var randomSeed = new Random((uint)System.DateTime.Now.Ticks);
            for (var i = 0; i < count; ++i)
            {
                if (!isInGrid)
                {
                    EntityManager.SetComponentData(spawnedEntities[i], new Position2D
                    {
                        Value = new float2(randomSeed.NextFloat(0, nbRows), randomSeed.NextFloat(0, nbCols))
                    });
                }
                else
                {
                    var gridPos = new int2(randomSeed.NextInt(0, nbRows), randomSeed.NextInt(0, nbCols));

                    EntityManager.SetComponentData(spawnedEntities[i], new PositionInGrid
                    {
                        Value = gridPos
                    });

                    var pos2D = grid.ToPos2D(gridPos);
                    EntityManager.SetComponentData(spawnedEntities[i], new Translation
                    {
                        Value = new float3(pos2D.x, 0.5f, pos2D.y)
                    });
                }
            }
        }
    }
}