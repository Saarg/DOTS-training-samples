using Unity.Entities;

public struct GameMaster : IComponentData
{
    public int NbChains;
    public int NbFirefightersPerChain;
    public int NbRows;
    public int NbCols;
}