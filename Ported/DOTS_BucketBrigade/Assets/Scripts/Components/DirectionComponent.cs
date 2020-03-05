using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;

public struct Direction2D : IComponentData
{
    public float2 Value;
    
    float3x3 ToRotationMatrix()
    {
        var d = Value;
        return new float3x3
            (
                // t
                new float3(d.y, 0, -d.x),
                // b
                new float3(0, 1, 0),
                // n
                new float3(d.x, 0, d.y)
            );
    }
}