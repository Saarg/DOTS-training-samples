using Unity.Entities;

public struct FromTo : IComponentData
{
    public Entity Source;
    public Entity Target;
}