using Unity.Entities;
using Unity.Mathematics;

public class FillingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithChangeFilter<FillRate>().WithNone<BucketTag>().ForEach((ref GradientState gradientState, 
            in FillRate fillState, in Capacity capacity) =>
        {
            gradientState.Value = math.min(capacity.Value, gradientState.Value + fillState.Value * Time.DeltaTime);
        }).Schedule();
    }
}