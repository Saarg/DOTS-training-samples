using Unity.Entities;

[GenerateAuthoringComponent]
public struct ChainParentComponent : ISharedComponentData
{
    public Entity Chain;
}
