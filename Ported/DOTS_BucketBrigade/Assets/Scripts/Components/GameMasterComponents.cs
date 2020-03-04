using Unity.Entities;
using UnityEngine;

public struct GameMaster : IComponentData
{
    // Game setup
    public int NbChains;
    public int NbBotsPerChain;
    public int NbBuckets;
    public int NbOmnibots;
    public int NbFires;

    public int NbCols;
    public int NbRows;

    // Prefabs
    public Entity BucketPrefab;
    public Entity BotPrefab;
    public Entity FirePrefab;
}

public struct FireMaster : IComponentData
{
    public float MaxHeight;
    public float Flashpoint;
    public int HeatRadius;
    public float HeatTransferRate;
}

public struct WaterMaster : IComponentData
{
    public float CoolingStrength;
    public float CoolingStrengthFallOff;
    public float RefillRate;
    public int SplashRadius;
    public float CarryMultiplier;
}

public struct BucketMaster : IComponentData
{
    public float Capacity;
    public float FillRate;
    public float Size_Empty;
    public float Size_Full;
}

public struct BotMaster : IComponentData
{
    public float Speed;
}

public struct ColorMaster : IComponentData
{
    public Color CellColor_Cool;
    public Color CellColor_Hot;

    public Color BotColor_Fill;
    public Color BotColor_PassFull;
    public Color BotColor_PassEmpty;
    public Color BotColor_Throw;
    public Color BotColor_Omnibot;

    public Color BucketColor_Empty;
    public Color BucketColor_Full;
}