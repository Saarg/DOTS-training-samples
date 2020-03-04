using Unity.Entities;

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