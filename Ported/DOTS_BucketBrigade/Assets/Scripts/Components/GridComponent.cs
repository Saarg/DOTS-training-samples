using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

public struct Grid : IComponentData
{
    public UnsafeMultiHashMap<int2, Entity> Value;
}