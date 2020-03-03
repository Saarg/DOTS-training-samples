using Unity.Entities;
using Unity.Mathematics;

public struct Position2D : IComponentData
{
    public float2 Value;
}

public struct PositionInGrid : IComponentData
{
    public int2 Value;
}
