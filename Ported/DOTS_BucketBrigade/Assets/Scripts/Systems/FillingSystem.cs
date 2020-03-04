using Unity.Entities;
using Unity.Mathematics;

public class FillingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;

        var waterSingleton = GetSingleton<WaterMaster>();
        Entities.WithNone<BucketTag>().ForEach((ref GradientState gradientState, 
            in Capacity capacity) =>
        {
            gradientState.Value = math.min(capacity.Value, gradientState.Value + waterSingleton.RefillRate * deltaTime);
        }).Schedule();
    }
}