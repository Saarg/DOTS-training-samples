using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class GameMasterAuthoring : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    [Header("Game")]
    [SerializeField, Tooltip("The number of chains spawned with each the number of firefighters below.")]
    public int NbChains = 2;

    [SerializeField, Tooltip("The number of firefighters in each chain, half will fill buckets, half will fight the fire.")]
    public int NbBotsPerChain = 30;

    [SerializeField, Range(0, 10), Tooltip("The number of omnibots, they serve multiple purposes at once.")]
    public int NbOmnibots = 5;

    [SerializeField, Range(0, 100), Tooltip("The number of buckets available to firefighters")]
    public int NbBuckets = 3;

    [SerializeField, Range(1, 10), Tooltip("The number of starting fires.")]
    public int NbFires = 1;

    [Header("Prefabs")]
    [SerializeField, Tooltip("The prefab used to spawn the bucket.")]
    public GameObject Bucket_Prefab;

    [SerializeField, Tooltip("The prefab for the flame.")]
    public GameObject Fire_Prefab;

    [SerializeField, Tooltip("The prefab for the bot.")]
    public GameObject Bot_Prefab;

    [Header("Colors")]
    [SerializeField, Tooltip("Color of a cell in a cool state.")]
    public Color CellColor_Cool;
    [SerializeField, Tooltip("Color of a cell in hot state.")]
    public Color CellColor_Hot;

    [SerializeField, Tooltip("Color of a bot with the task to fill the buck with water.")]
    public Color BotColor_Fill;
    [SerializeField, Tooltip("Color of a bot with the task to pass a full bucket of water.")]
    public Color BotColor_PassFull;
    [SerializeField, Tooltip("Color of a bot with the task to pass an empty bucket of water.")]
    public Color BotColor_PassEmpty;
    [SerializeField, Tooltip("Color of a bot with the task to throw a full bucket on the fire.")]
    public Color BotColor_Throw;
    [SerializeField, Tooltip("Color of a bot with the task to do everything.")]
    public Color BotColor_Omnibot;

    [SerializeField, Tooltip("Color of an empty bucket.")]
    public Color BucketColor_Empty;
    [SerializeField, Tooltip("Color of a full bucket.")]
    public Color BucketColor_Full;

    [Header("Grid")]
    [SerializeField, Tooltip("The number of rows in the fire grid.")]
    public int NbRows = 50;

    [SerializeField, Tooltip("The number of columns in the fire grid.")]
    public int NbCols = 50;

    [SerializeField, Range(0.5f, 1.5f), Tooltip("The size of a cell in the grid")]
    public float CellSize = 1.0f;

    [Header("Fire")]
    [SerializeField, Range(0.05f, 0.15f), Tooltip("How high the flames reach at max temperature.")]
    public float Fire_MaxHeight = 0.1f;

    [SerializeField, Range(0.25f, 0.75f), Tooltip("When the temperature reaches this value, the cell is on fire.")]
    public float Fire_Flashpoint = 0.25f;

    [SerializeField, Range(1, 5), Tooltip("How far (cells) does heat travel?")]
    public int Fire_HeatRadius = 1;

    [SerializeField, Range(0.01f, 1.0f), Tooltip("How fast will adjacent cells heat up?")]
    public float Fire_HeatTransferRate = 0.75f;

    [Header("Water")]
    [SerializeField, Range(0.5f, 1.0f), Tooltip("Water bucket reduces fire gradient by this amount")]
    public float Water_CoolingStrength = 1.0f;

    [SerializeField, Range(0.0f, 1.0f), Tooltip("Splash damage of water bucket. (1 = no loss of power over distance)")]
    public float Water_CoolingStrengthFallOff = 0.7f;

    [SerializeField, Range(0.0f, 0.5f), Tooltip("Water sources will refill by this amount per second")]
    public float Water_RefillRate = 0.1f;

    [SerializeField, Range(1, 5), Tooltip("Number of cells affected by a bucket of water")]
    public int Water_SplashRadius = 3;

    [SerializeField, Range(0.1f, 1.0f), Tooltip("The multiplier for the speed of a bot when he has a full bucket.")]
    public float Water_CarryMultiplier = 0.5f;

    [Header("Bucket")]
    [SerializeField, Range(1.0f, 5.0f), Tooltip("How much water does a bucket hold?")]
    public float Bucket_Capacity = 1.0f;

    [SerializeField, Range(0.001f, 1.0f), Tooltip("Buckets fill up by this much per second")]
    public float Bucket_FillRate = 0.1f;

    [SerializeField, Range(0.1f, 0.3f), Tooltip("Visual scale of bucket when EMPTY (no effect on water capacity)")]
    public float Bucket_Size_Empty = 0.2f;

    [SerializeField, Range(0.3f, 0.5f), Tooltip("Visual scale of bucket when FULL (no effect on water capacity)")]
    public float Bucket_Size_Full = 0.4f;

    [Header("Bots")]
    [SerializeField, Range(0.1f, 2.0f), Tooltip("The speed at which the bot moves.")]
    public float Bot_Speed = 0.5f;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GameMaster
        {
            NbChains = NbChains,
            NbBotsPerChain = NbBotsPerChain,
            NbBuckets = NbBuckets,
            NbOmnibots = NbOmnibots,
            NbFires = NbFires,

            NbCols = NbCols,
            NbRows = NbRows,

            BucketPrefab = conversionSystem.GetPrimaryEntity(Bucket_Prefab),
            BotPrefab = conversionSystem.GetPrimaryEntity(Bot_Prefab),
            FirePrefab = conversionSystem.GetPrimaryEntity(Fire_Prefab)
        });

        dstManager.AddComponentData(entity, new ColorMaster
        {
            CellColor_Cool = CellColor_Cool,
            CellColor_Hot = CellColor_Hot,

            BotColor_Fill= BotColor_Fill,
            BotColor_PassFull = BotColor_PassFull,
            BotColor_PassEmpty = BotColor_PassEmpty,
            BotColor_Throw = BotColor_Throw,
            BotColor_Omnibot = BotColor_Omnibot,

            BucketColor_Empty = BucketColor_Empty,
            BucketColor_Full = BucketColor_Full
        });

        dstManager.AddComponentData(entity, new Grid
        {
            Physical = new UnsafeHashMap<int2, Grid.Cell>(NbRows * NbCols, Allocator.Persistent),
            Simulation = new UnsafeHashMap<int2, int>(NbRows * NbCols, Allocator.Persistent),
            CellSize = CellSize
        });

        dstManager.AddComponentData(entity, new WaterMaster
        {
            CoolingStrength = Water_CoolingStrength,
            CoolingStrengthFallOff = Water_CoolingStrengthFallOff,
            RefillRate = Water_RefillRate,
            SplashRadius = Water_SplashRadius,
            CarryMultiplier = Water_CarryMultiplier
        });

        dstManager.AddComponentData(entity, new FireMaster
        {
            MaxHeight = Fire_MaxHeight,
            Flashpoint = Fire_Flashpoint,
            HeatRadius = Fire_HeatRadius,
            HeatTransferRate = Fire_HeatTransferRate
        });

        dstManager.AddComponentData(entity, new BucketMaster
        {
            Capacity = Bucket_Capacity,
            FillRate = Bucket_FillRate,
            Size_Empty = Bucket_Size_Empty,
            Size_Full = Bucket_Size_Full
        });

        dstManager.AddComponentData(entity, new BotMaster
        {
            Speed = Bot_Speed
        });

        dstManager.RemoveComponent<Translation>(entity);
        dstManager.RemoveComponent<Rotation>(entity);
        dstManager.RemoveComponent<LocalToWorld>(entity);

        // Create the spawn prefab entity, so the SpawnSystem knows this entity is ready.
        var spawnEntity = conversionSystem.CreateAdditionalEntity(this);
        dstManager.AddComponent<SpawnPrefabsTag>(spawnEntity);
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(Bucket_Prefab);
        referencedPrefabs.Add(Fire_Prefab);
        referencedPrefabs.Add(Bot_Prefab);
    }
}
