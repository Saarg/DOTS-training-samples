using Unity.Entities;
using Unity.Jobs;

public class SpawnSystem : JobComponentSystem
{
    protected override void OnCreate()
    {
        base.OnCreate();


    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        return inputDeps;
    }
}