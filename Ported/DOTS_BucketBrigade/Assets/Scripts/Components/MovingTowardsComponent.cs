using Unity.Entities;
using Unity.Mathematics;

public struct MovingTowards : IComponentData
{
    public float2 Position;
    public Entity Entity;
}