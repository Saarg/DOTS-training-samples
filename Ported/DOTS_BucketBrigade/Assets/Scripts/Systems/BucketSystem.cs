using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems
{
    [UpdateAfter(typeof(MoveToDestinationSystem))]
    [UpdateAfter(typeof(SpreadFire))]
    public class BucketSystem : SystemBase
    {
        private EntityCommandBufferSystem m_CommandBufferSystem;
        
        protected override void OnCreate()
        {
            m_CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            RequireSingletonForUpdate<Grid>();
        }

        protected override void OnUpdate()
        {
            var grid = GetSingleton<Grid>();
            var deltaTime = Time.DeltaTime;

            var bucketSingleton = GetSingleton<BucketMaster>();
            var gradientFromEntity = GetComponentDataFromEntity<GradientState>();
            var ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
            
            Dependency = Entities.WithNone<Carried>().WithAll<BucketTag>()
            .ForEach((Entity entity, int nativeThreadIndex, ref NonUniformScale scale,
            in Position2D position) =>
            {
                var gradientState = gradientFromEntity[entity];
                var gridPos = grid.ToGridPos(position);
                switch (grid.Physical[gridPos].Flags)
                {
                    case Grid.Cell.ContentFlags.Fire:
                        if (gradientState.Value > 0.0f)
                        {
                            var offset = int2.zero;
                            for (offset.y = -bucketSingleton.SplashRadius;
                                offset.y < bucketSingleton.SplashRadius;
                                offset.y++)
                            {
                                for (offset.x = -bucketSingleton.SplashRadius;
                                    offset.x < bucketSingleton.SplashRadius;
                                    offset.x++)
                                {
                                    if (grid.Physical.ContainsKey(gridPos + offset))
                                    {
                                        var cell = grid.Physical[gridPos + offset];

                                        if (cell.Flags == Grid.Cell.ContentFlags.Fire)
                                        {
                                            var fireGradient = gradientFromEntity[cell.Entity];

                                            fireGradient.Value =
                                                                 -gradientState.Value *
                                                                 bucketSingleton.CoolingStrength *
                                                                 bucketSingleton.CoolingStrengthFallOff *
                                                                 math.length(offset);

                                            gradientFromEntity[cell.Entity] = fireGradient;
                                        }
                                    }
                                }
                            }
                        }

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
                gradientFromEntity[entity] = gradientState;
            }).Schedule(Dependency);
            m_CommandBufferSystem.AddJobHandleForProducer(Dependency);

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
            
            Dependency = Entities.WithAll<DestroyBucketWhenEmptyTag>()
                .ForEach((Entity entity, int entityInQueryIndex, in GradientState state) =>
                {
                    if (state.Value <= 0)
                        ecb.DestroyEntity(entityInQueryIndex, entity);
                }).Schedule(Dependency);
            m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}