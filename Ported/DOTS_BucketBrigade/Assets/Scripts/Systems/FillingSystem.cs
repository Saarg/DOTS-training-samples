using Unity.Entities;
using Unity.Mathematics;

public class FillingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;
        
        Entities.WithChangeFilter<FillRate>().WithNone<BucketTag>().ForEach((ref GradientState gradientState, 
            in FillRate fillState, in Capacity capacity) =>
        {
            gradientState.Value = math.min(capacity.Value, gradientState.Value + fillState.Value * deltaTime);
        }).Schedule();
    }
}