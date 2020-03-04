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

        Entities.ForEach((Entity entity, ToDeleteFromGridTag tag, PositionInGrid pos) =>
        {
            grid.Physical.Remove(pos.Value);
            EntityManager.DestroyEntity(entity);
        }).Run();
        
        Entities.ForEach((Entity entity, NewFireTag tag, PositionInGrid pos) =>
        {
            grid.Simulation.Remove(pos.Value);
        }).Run();

        return inputDeps;
    }
}