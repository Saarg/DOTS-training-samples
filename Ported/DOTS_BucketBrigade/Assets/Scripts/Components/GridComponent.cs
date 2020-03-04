using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

public struct Grid : IComponentData
{
    [Serializable]
    public struct Cell
    {
        public enum ContentFlags
        {
            Nothing,
            Fire,
            Water,
        }
        public Entity Entity;
        public ContentFlags Flags;
    }
    
    public UnsafeHashMap<int2, Cell> Physical;
    public UnsafeHashMap<int2, int /*<unused>*/> Simulation;
	public float CellSize;

    int2 ToGridPos(Position2D position2D) => ToGridPos(position2D.Value);
    int2 ToGridPos(float3 position) => ToGridPos(position.xz);
    int2 ToGridPos(float2 position2D)
    {
        return (int2) math.floor(position2D / CellSize);
    }

    float2 ToPos2D(PositionInGrid gridPos) => ToPos2D(gridPos.Value);
    float2 ToPos2D(int2 gridPos)
    {
        return (float2) (gridPos) * CellSize;
    }
}
