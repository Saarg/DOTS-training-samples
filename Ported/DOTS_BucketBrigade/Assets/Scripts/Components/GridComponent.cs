using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

public struct Grid : IComponentData
{
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
}
