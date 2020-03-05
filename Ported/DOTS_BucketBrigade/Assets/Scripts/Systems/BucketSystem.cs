using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems
{
    [UpdateAfter(typeof(MoveToDestinationSystem))]
    public class BucketSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireSingletonForUpdate<Grid>();
        }

        protected override void OnUpdate()
        {
            var grid = GetSingleton<Grid>();
            var deltaTime = Time.DeltaTime;

            var bucketSingleton = GetSingleton<BucketMaster>();
            Dependency = Entities.WithNone<Carried>().WithAll<BucketTag>()
                .ForEach((ref GradientState gradientState, ref NonUniformScale scale,
                in Position2D position) =>
            {
                switch (grid.Physical[grid.ToGridPos(position)].Flags)
                {
                    case Grid.Cell.ContentFlags.Fire:
                        gradientState.Value = 0;
                        break;
                    case Grid.Cell.ContentFlags.Water:
                        gradientState.Value = math.min(1.0f, gradientState.Value + bucketSingleton.FillRate * deltaTime / bucketSingleton.Capacity);
                        break;
                    case Grid.Cell.ContentFlags.Nothing:
                        break;
                    default:
                        throw new NotImplementedException();
                }
                
                scale.Value = new float3(math.lerp(bucketSingleton.Size_Empty, bucketSingleton.Size_Full, gradientState.Value));
            }).Schedule(Dependency);

            var position2DFromEntity = GetComponentDataFromEntity<Position2D>();
            var scaleFromEntity = GetComponentDataFromEntity<NonUniformScale>(true);
            Dependency = Entities.WithAll<BucketTag>().WithReadOnly(scaleFromEntity)
                .ForEach((Entity entity, ref Translation translation,
                in Carried carried) =>
            {
                var bucketPos = position2DFromEntity[entity];
                var botPos = position2DFromEntity[carried.Value];

                bucketPos.Value = botPos.Value;
                translation.Value.y = scaleFromEntity[carried.Value].Value.y + scaleFromEntity[entity].Value.y * 0.5f;

                position2DFromEntity[entity] = bucketPos;
            }).Schedule(Dependency);
            
            Dependency = Entities.WithAll<BucketTag>().WithNone<Carried>().ForEach((Entity entity, ref Translation translation, 
                in Position2D position2D, in NonUniformScale scale) =>
            {
                var pos = position2D;
                var gridPos = grid.ToGridPos(pos);
                pos.Value = gridPos;
                translation.Value.y = scale.Value.y * 0.5f;
            }).Schedule(Dependency);
        }
    }
}