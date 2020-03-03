using Unity.Entities;

public struct InLine : IComponentData
{
    public Entity Previous;
    public Entity Next;
    public Entity Chain;
}