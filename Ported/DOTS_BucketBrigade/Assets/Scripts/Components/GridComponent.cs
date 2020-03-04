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

    public int2 ToGridPos(Position2D position2D) => ToGridPos(position2D.Value);
    public int2 ToGridPos(float3 position) => ToGridPos(position.xz);
    public int2 ToGridPos(float2 position2D)
    {
        return (int2) math.floor(position2D / CellSize);
    }

    public float2 ToPos2D(PositionInGrid gridPos) => ToPos2D(gridPos.Value);
    public float2 ToPos2D(int2 gridPos)
    {
        return (float2) (gridPos) * CellSize + CellSize / 2.0f;
    }
}
