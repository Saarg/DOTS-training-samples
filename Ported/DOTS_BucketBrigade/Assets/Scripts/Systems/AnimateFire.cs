using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(GridUpdate))]
public class AnimateFire : JobComponentSystem
{
    private static uint InvwkRnd(ref uint seed)
    {
        seed += ((seed * seed) | 5u);
        return seed;
    }

    private static float InvwkRndf(ref uint seed)
    {
        return math.asfloat((InvwkRnd(ref seed) >> 9) | 0x3f800000) - 1.0f;
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        const float speeed = 2.0f;
        float et = (float)Time.ElapsedTime;
        float dt = (float)Time.DeltaTime;

        return Entities
            .WithAll<MaxOutFireTag>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Translation t) =>
            {
                float rspeed = speeed * (((uint) entity.Index) % 4 + 1);
                uint seed = (uint)(et * rspeed);
                InvwkRnd(ref seed);
                seed *= (1 + (uint)entity.Index);

                t.Value.y = math.lerp(t.Value.y, 1.0f - InvwkRndf(ref seed) * 0.5f, dt * rspeed);
            })
            .Schedule(inputDeps);
    }
}
