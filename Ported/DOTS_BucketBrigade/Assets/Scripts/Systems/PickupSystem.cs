using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

[UpdateBefore(typeof(MoveToDestinationSystem))]
public class PickupSystem : JobComponentSystem
{
    EntityCommandBufferSystem m_CommandBufferSystem;

    EntityQuery m_BucketsAvailable;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        m_BucketsAvailable = GetEntityQuery(ComponentType.Exclude<Carried>(), ComponentType.ReadOnly<BucketTag>());
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var grid = GetSingleton<Grid>();
        var gameMaster = GetSingleton<GameMaster>();

        // Get the buckets that can be picked up
        var bucketEntities = m_BucketsAvailable.ToEntityArrayAsync(Allocator.TempJob, out var bucketEntitiesHandle);
        var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var pos2DFromEntity = GetComponentDataFromEntity<Position2D>(true);
        var gridPosFromEntity = GetComponentDataFromEntity<PositionInGrid>(true);
        var inlineFromEntity = GetComponentDataFromEntity<InLine>(true);
        var carryingFromEntity = GetComponentDataFromEntity<Carrying>(true);
        var gradientStateFromEntity = GetComponentDataFromEntity<GradientState>(true);

        // This job gives a Destination2D to bots that do not have one yet and are able to go find a bucket
        var findBucketJobHandle = Entities
            .WithNone<Destination2D>()
            .WithNone<Carrying>()
            .WithAll<BotTag>()
            .WithNone<MovingTowards>()
            .ForEach((Entity entity, int nativeThreadIndex, ref Role role) =>
        {
            if (role.Value == BotRole.Fill || role.Value == BotRole.Omnibot || role.Value == BotRole.Throw)
            {
                // Get nearest bucket
                var distance = 999f;
                Entity nearestBucket = Entity.Null;

                for (int i = 0, length = bucketEntities.Length; i < length; ++i)
                {
                    if (pos2DFromEntity.Exists(bucketEntities[i]) && pos2DFromEntity.Exists(entity))
                    {
                        var bucketPos = pos2DFromEntity[bucketEntities[i]];
                        var newDistance = math.distance(bucketPos.Value, pos2DFromEntity[entity].Value);
                        // If the distance is smaller than the previous smallest distance, switch it out
                        if (newDistance < distance)
                        {
                            distance = newDistance;
                            nearestBucket = bucketEntities[i];
                        }
                    }
                }

                // Tell the bot to go to that destination
                if (nearestBucket != Entity.Null)
                {
                    commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                    {
                        Value = pos2DFromEntity[nearestBucket].Value
                    });

                    commandBuffer.AddComponent(nativeThreadIndex, entity, new MovingTowards
                    {
                        Entity = nearestBucket,
                        Position = pos2DFromEntity[nearestBucket].Value
                    });
                }
            }
            else if (role.Value == BotRole.PassEmpty || role.Value == BotRole.PassFull)
            {
                // Need to check the chain previous members to see if they have a bucket to pass further
                if (inlineFromEntity.Exists(entity))
                {
                    // If previous member is carrying a bucket, go towards him
                    var previous = inlineFromEntity[entity].Previous;
                    if (previous != Entity.Null && carryingFromEntity.Exists(previous))
                    {
                        // current ----- TARGET ----- previous
                        var currentPos = pos2DFromEntity[entity].Value;
                        var previousPos = pos2DFromEntity[entity].Value;
                        var target = (currentPos + previousPos) / 2.0f;

                        commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                        {
                            Value = target
                        });

                        commandBuffer.AddComponent(nativeThreadIndex, entity, new MovingTowards
                        {
                            Entity = Entity.Null,
                            Position = target
                        });
                    }
                }
            }
        }).WithReadOnly(pos2DFromEntity)
          .WithReadOnly(inlineFromEntity)
          .WithReadOnly(carryingFromEntity)
          .Schedule(bucketEntitiesHandle);

        var carriedFromEntity = GetComponentDataFromEntity<Carried>(true);
        // This job asks bots that are on a bucket to pick it up.
        // It looks through all bots that will be on top of the bucket,
        // so they arrived there (no more destination) and that are not carrying any bucket
        var pickupBucketJobHandle = Entities
            .WithNone<Carrying>()
            .WithNone<Destination2D>()
            .WithAll<BotTag>()
            .WithReadOnly(carriedFromEntity)
            .ForEach((Entity entity, int nativeThreadIndex, ref MovingTowards movingTowards) =>
        {
            bool closeEnough = false;
            if (pos2DFromEntity.Exists(entity))
            {
                var distance = math.distance(movingTowards.Position, pos2DFromEntity[entity].Value);
                if (distance < 0.5f)
                {
                    closeEnough = true;
                }
            }
            
            if (closeEnough && gradientStateFromEntity.Exists(movingTowards.Entity))
            {
                var gradient = gradientStateFromEntity[movingTowards.Entity].Value;
                if ((gradient <= 0.0f || gradient >= 1.0f) && !carriedFromEntity.HasComponent(movingTowards.Entity))
                {
                    commandBuffer.AddComponent(nativeThreadIndex, movingTowards.Entity, new Carried
                    {
                        Value = entity
                    });
                    commandBuffer.AddComponent(nativeThreadIndex, entity, new Carrying
                    {
                        Value = movingTowards.Entity
                    });
                    commandBuffer.RemoveComponent<MovingTowards>(nativeThreadIndex, entity);
                }
            }
        }).WithReadOnly(gradientStateFromEntity)
          .WithReadOnly(pos2DFromEntity)
          .Schedule(findBucketJobHandle);

        var moveBucketJobHandle = Entities
            .WithAll<BotTag>()
            .WithAll<Carrying>()
            .WithNone<Destination2D>()
            .WithNone<MovingTowards>()
            .ForEach((Entity entity, int nativeThreadIndex, ref Role role) =>
        {
            // If you are carrying a bucket, you need a destination to drop off your bucket
            if (role.Value == BotRole.Omnibot || role.Value == BotRole.Fill || role.Value == BotRole.Throw)
            {
                // If your bucket is full, go to nearest fire, if empty, go to nearest water
                var bucketEntity = carryingFromEntity[entity].Value;
                if (gradientStateFromEntity.Exists(bucketEntity) && pos2DFromEntity.Exists(entity))
                {
                    var gradient = gradientStateFromEntity[bucketEntity].Value;
                    bool inSearchOfFire = gradient >= 1.0f;

                    var nearestDestination = TargetingSystem.FindNearestOfTag(
                        pos2DFromEntity[entity].Value,
                        inSearchOfFire ? Grid.Cell.ContentFlags.Fire : Grid.Cell.ContentFlags.Water,
                        grid,
                        gameMaster);

                    commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                    {
                        Value = nearestDestination
                    });
                    commandBuffer.AddComponent(nativeThreadIndex, entity, new MovingTowards
                    {
                        Entity = Entity.Null,
                        Position = nearestDestination
                    });
                }
            }
            else if (role.Value == BotRole.PassEmpty || role.Value == BotRole.PassFull)
            {
                // Find the next in line and go to the half point
                if (inlineFromEntity.Exists(entity))
                {
                    var next = inlineFromEntity[entity].Next;
                    if (next != Entity.Null && !carryingFromEntity.Exists(next))
                    {
                        var currentPos = pos2DFromEntity[entity].Value;
                        var nextPos = pos2DFromEntity[next].Value;
                        var targetPos = (currentPos + nextPos) / 2.0f;

                        commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                        {
                            Value = targetPos
                        });
                        commandBuffer.AddComponent(nativeThreadIndex, entity, new MovingTowards
                        {
                            Entity = Entity.Null,
                            Position = targetPos
                        });
                    }
                }
            }
        }).WithReadOnly(gradientStateFromEntity)
          .WithReadOnly(pos2DFromEntity)
          .WithReadOnly(inlineFromEntity)
          .WithReadOnly(carryingFromEntity)
          .Schedule(pickupBucketJobHandle);

        var dropBucketJobHandle = Entities
            .WithAll<BotTag>()
            .WithNone<Destination2D>()
            .WithAll<Carrying>()
            .ForEach((Entity entity, int nativeThreadIndex, ref Role role, ref MovingTowards movingTowards) =>
        {
            bool closeEnough = false;
            if (pos2DFromEntity.Exists(entity))
            {
                var distance = math.distance(movingTowards.Position, pos2DFromEntity[entity].Value);
                if (distance < 0.5f)
                {
                    closeEnough = true;
                }
            }

            if (closeEnough && carryingFromEntity.Exists(entity))
            {
                var bucketEntity = carryingFromEntity[entity].Value;
                commandBuffer.RemoveComponent<Carried>(nativeThreadIndex, bucketEntity);
                commandBuffer.RemoveComponent<Carrying>(nativeThreadIndex, entity);
                commandBuffer.RemoveComponent<MovingTowards>(nativeThreadIndex, entity);
            }

        }).WithReadOnly(pos2DFromEntity)
          .WithReadOnly(carryingFromEntity)
          .Schedule(moveBucketJobHandle);

        m_CommandBufferSystem.AddJobHandleForProducer(findBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(pickupBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(moveBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(dropBucketJobHandle);

        var disposeHandle = bucketEntities.Dispose(dropBucketJobHandle);

        return disposeHandle;
    }
}
