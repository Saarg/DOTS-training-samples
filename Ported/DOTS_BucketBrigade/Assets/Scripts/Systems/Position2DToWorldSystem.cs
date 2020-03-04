using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(MoveToDestinationSystem))]
public class Position2DToWorldSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var positioningHandle = Entities.WithoutBurst().WithChangeFilter<Position2D>().ForEach(
            (Entity entity,
             ref Position2D position2D, 
             ref Translation translation) =>
        {
            translation.Value.x = position2D.Value.x;
            translation.Value.z = position2D.Value.y;
        }).Schedule(inputDeps);

        return positioningHandle;
    }
}
