using Unity.Entities;
using Unity.Mathematics;

public struct FromTo : IComponentData
{
    public float2 Source;
    public float2 Target;
    public Entity RelativeTo;
}