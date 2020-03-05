using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class WaterFillingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;

        var waterSingleton = GetSingleton<WaterMaster>();
        Entities.WithAll<WaterTag>().ForEach((ref GradientState gradientState, ref NonUniformScale scale, 
            in Capacity capacity) =>
        {
            gradientState.Value = math.min(1.0f, 
                gradientState.Value + waterSingleton.RefillRate * deltaTime / capacity.Value);

            scale.Value.xz = gradientState.Value * capacity.Value * 0.1f;
        }).Schedule();
    }
}