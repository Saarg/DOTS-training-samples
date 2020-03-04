using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class GridUpdate : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var grid = GetSingleton<Grid>();

        grid.Physical.Capacity = grid.Physical.Length + grid.Simulation.Length;
        grid.Simulation.Capacity = grid.Simulation.Length * 2;

        Entities.WithStructuralChanges().ForEach((Entity entity, ToDeleteFromGridTag tag, PositionInGrid pos) =>
        {
            grid.Physical.Remove(pos.Value);
            EntityManager.DestroyEntity(entity);
        }).Run();
        
        Entities.WithStructuralChanges().ForEach((Entity entity, NewFireTag tag, PositionInGrid pos) =>
        {
            grid.Simulation.Remove(pos.Value);
        }).Run();

        return inputDeps;
    }
}