using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class SpawnSystem : JobComponentSystem
{
    GameMaster m_GameMaster;
    Grid m_Grid;
    BotMaster m_BotMaster;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireSingletonForUpdate<SpawnPrefabsTag>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_GameMaster = GetSingleton<GameMaster>();
        m_Grid = GetSingleton<Grid>();
        m_BotMaster = GetSingleton<BotMaster>();

        // Spawn Bots in chains
        for (var i = 0; i < m_GameMaster.NbChains; ++i)
        {
            SpawnChain(0.0f, 0.0f);
        }
        // Spawn omnibots
        for (var i = 0; i < m_GameMaster.NbOmnibots; ++i)
        {
            var botEntity = SpawnBot(0.0f, 0.0f);
            EntityManager.SetComponentData(botEntity, new Role
            {
                Value = BotRole.Omnibot
            });
        }

        // Spawn buckets
        SpawnEntities(m_GameMaster.NbBuckets, m_GameMaster.BucketPrefab);

        // Spawn fires
        SpawnEntities(m_GameMaster.NbFires, m_GameMaster.FirePrefab, true);

        var spawnPrefabEntity = GetSingletonEntity<SpawnPrefabsTag>();
        EntityManager.DestroyEntity(spawnPrefabEntity);

        return inputDeps;
    }

    void SpawnChain(float minX, float minY)
    {
        var chainEntity = EntityManager.CreateEntity();
        var chainSharedComponent = new ChainParentComponent { Chain = chainEntity };

        var randomSeed = new Random((uint)System.DateTime.Now.Ticks);

        var previous = Entity.Null;
        var currentInline = Entity.Null;
        var relativeTo = Entity.Null;
        int i;
        for (i = 0; i < m_GameMaster.NbBotsPerChain; ++i )
        {
            var botEntity = SpawnChainBot(chainSharedComponent, minX, minY);
            if (currentInline != Entity.Null)
            {
                EntityManager.AddComponentData(currentInline, new InLine
                {
                    Previous = previous,
                    Next = botEntity,
                    Progress = ((float) i - 1) / m_GameMaster.NbBotsPerChain
                });
            }

            previous = currentInline;
            currentInline = botEntity;

            // Assign roles
            if (i == 0)
            {
                relativeTo = botEntity;
                EntityManager.SetComponentData(botEntity, new Role
                {
                    Value = BotRole.Fill
                });
            }
            else if (i == m_GameMaster.NbBotsPerChain / 2)
            {
                EntityManager.SetComponentData(botEntity, new Role
                {
                    Value = BotRole.Throw
                });
            }
            else if (i < m_GameMaster.NbBotsPerChain / 2)
            {
                EntityManager.SetComponentData(botEntity, new Role
                {
                    Value = BotRole.PassFull
                });
            }
            else if (i > m_GameMaster.NbBotsPerChain / 2)
            {
                EntityManager.SetComponentData(botEntity, new Role
                {
                    Value = BotRole.PassEmpty
                });
            }
        }
        
        if (currentInline != Entity.Null)
        {
            EntityManager.AddComponentData(currentInline, new InLine
            {
                Previous = previous,
                Next = Entity.Null,
                Progress = ((float) i) / m_GameMaster.NbBotsPerChain
            });
        }
        
        EntityManager.AddComponentData(chainEntity, new FromTo
        {
            Source = randomSeed.NextInt2(m_GameMaster.NbRows),
            Target = randomSeed.NextInt2(m_GameMaster.NbRows),
            RelativeTo = relativeTo
        });
    }
    
    Entity SpawnChainBot(ChainParentComponent chain, float minX, float minY)
    {
        var botEntity = SpawnBot(minX, minY);

        EntityManager.AddSharedComponentData(botEntity, chain);

        return botEntity;
    }

    Entity SpawnBot(float minX, float minY)
    {
        var botEntity = EntityManager.Instantiate(m_GameMaster.BotPrefab);

        EntityManager.AddComponentData(botEntity, new MovementSpeed { Value = m_BotMaster.Speed });

        var randomSeed = new Random((uint)System.DateTime.Now.Ticks);
        EntityManager.SetComponentData(botEntity, new Position2D
        {
            Value = new float2(randomSeed.NextFloat(minX, m_GameMaster.NbCols), randomSeed.NextFloat(minY, m_GameMaster.NbRows))
        });

        return botEntity;
    }

    void SpawnEntities(int count, Entity prefab, bool isInGrid = false)
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
                        Value = new float2(randomSeed.NextFloat(5, m_GameMaster.NbCols - 5), randomSeed.NextFloat(5, m_GameMaster.NbRows))
                    });
                }
                else
                {
                    var gridPos = new int2(randomSeed.NextInt(5, m_GameMaster.NbCols - 5), randomSeed.NextInt(5, m_GameMaster.NbRows - 5));

                    EntityManager.SetComponentData(spawnedEntities[i], new PositionInGrid
                    {
                        Value = gridPos
                    });

                    var pos2D = m_Grid.ToPos2D(gridPos);
                    EntityManager.SetComponentData(spawnedEntities[i], new Translation
                    {
                        Value = new float3(pos2D.x, 0.5f, pos2D.y)
                    });
                }
            }
        }
    }
}