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

    bool firstFrame = true;
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (firstFrame)
        {
            firstFrame = false;
            return default;
        }

        var grid = GetSingleton<Grid>();
        var gameMaster = GetSingleton<GameMaster>();

        // Get the buckets that can be picked up
        var bucketEntities = m_BucketsAvailable.ToEntityArrayAsync(Allocator.TempJob, out var bucketEntitiesHandle);
        var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var pos2DFromEntity = GetComponentDataFromEntity<Position2D>(true);
        var inlineFromEntity = GetComponentDataFromEntity<InLine>(true);
        var carryingFromEntity = GetComponentDataFromEntity<Carrying>(true);
        var gradientStateFromEntity = GetComponentDataFromEntity<GradientState>(true);
        var roleFromEntity = GetComponentDataFromEntity<Role>(true);

        // This job gives a Destination2D to bots that do not have one yet and are able to go find a bucket
        var findBucketJobHandle = Entities
            .WithNone<Destination2D>()
            .WithNone<Carrying>()
            .WithAll<BotTag>()
            .WithNone<MovingTowards>()
            .ForEach((Entity entity, int nativeThreadIndex) =>
            {
                var role = roleFromEntity[entity].Value;
                if (role == BotRole.Fill)
                {
                    GoToNearestBucket(
                        bucketEntities, 
                        pos2DFromEntity, 
                        entity,
                        nativeThreadIndex, 
                        commandBuffer);
                }
                else if (role == BotRole.Omnibot)
                {
                    GoToNearestEmptyBucket(
                        bucketEntities, 
                        pos2DFromEntity, 
                        gradientStateFromEntity, 
                        entity,
                        nativeThreadIndex, 
                        commandBuffer);
                }
                else if (role == BotRole.PassEmpty || role == BotRole.PassFull || role == BotRole.Throw)
                {
                    // Need to check the chain previous members to see if they have a bucket to pass further
                    if (inlineFromEntity.Exists(entity))
                    {
                        var previous = inlineFromEntity[entity].Previous;
                        // Special case where previous is the Thrower so this guy needs to go scoop up empty buckets
                        var previousRole = roleFromEntity[previous].Value;
                        if (previousRole == BotRole.Throw)
                        {
                            GoToNearestEmptyBucket(
                                bucketEntities, 
                                pos2DFromEntity, 
                                gradientStateFromEntity, 
                                entity,
                                nativeThreadIndex, 
                                commandBuffer);
                        }
                        // If previous member is carrying a bucket, go towards him
                        else if (previous != Entity.Null && carryingFromEntity.Exists(previous))
                        {
                            // Pass_Empty must only get a destination once the carried bucket is empty
                            // Pass_Full must only get a destination once the carried bucket is full
                            var bucket = carryingFromEntity[previous].Value;
                            if (gradientStateFromEntity.Exists(bucket))
                            {
                                var gradient = gradientStateFromEntity[bucket].Value;
                                if (role == BotRole.PassEmpty && gradient <= 0.0f || role == BotRole.PassFull && gradient >= 1.0f || role == BotRole.Throw)
                                {
                                    // current ----- TARGET ----- previous
                                    var currentPos = pos2DFromEntity[entity].Value;
                                    var previousPos = pos2DFromEntity[previous].Value;
                                    var target = (currentPos + previousPos) / 2.0f;

                                    commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                                    {
                                        Value = target
                                    });

                                    commandBuffer.AddComponent(nativeThreadIndex, entity, new MovingTowards
                                    {
                                        Entity = bucket,
                                        Position = target
                                    });
                                }
                            }
                        }
                    }
                }
            }).WithReadOnly(pos2DFromEntity)
              .WithReadOnly(inlineFromEntity)
              .WithReadOnly(carryingFromEntity)
              .WithReadOnly(gradientStateFromEntity)
              .WithReadOnly(roleFromEntity)
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
            var closeEnough = false;
            if (pos2DFromEntity.Exists(entity))
            {
                var distance = math.distancesq(movingTowards.Position, pos2DFromEntity[entity].Value);
                if (distance < 0.25f)
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
            .ForEach((Entity entity, int nativeThreadIndex) =>
            {
                var role = roleFromEntity[entity].Value;
                // If you are carrying a bucket, you need a destination to drop off your bucket
                if (role == BotRole.Omnibot || role == BotRole.Fill || role == BotRole.Throw)
                {
                    // If your bucket is full, go to nearest fire, if empty, go to nearest water
                    var bucketEntity = carryingFromEntity[entity].Value;
                    if (gradientStateFromEntity.Exists(bucketEntity) && pos2DFromEntity.Exists(entity))
                    {
                        var gradient = gradientStateFromEntity[bucketEntity].Value;
                        // pass down the chain
                        if (   role == BotRole.Fill && gradient >= 1.0f 
                            || role == BotRole.Throw && gradient <= 0.0f)
                        {
                            GoToNextInLine(
                                inlineFromEntity, 
                                carryingFromEntity, 
                                pos2DFromEntity, 
                                nativeThreadIndex, 
                                entity, 
                                commandBuffer);
                        }
                        else
                        {
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
                }
                else if (role == BotRole.PassEmpty || role == BotRole.PassFull)
                {
                    // Find the next in line and go to the half point
                    GoToNextInLine(
                        inlineFromEntity, 
                        carryingFromEntity, 
                        pos2DFromEntity, 
                        nativeThreadIndex, 
                        entity, 
                        commandBuffer);
                }
            }).WithReadOnly(gradientStateFromEntity)
              .WithReadOnly(pos2DFromEntity)
              .WithReadOnly(inlineFromEntity)
              .WithReadOnly(carryingFromEntity)
              .WithReadOnly(roleFromEntity)
              .Schedule(pickupBucketJobHandle);

        var dropBucketJobHandle = Entities
            .WithAll<BotTag>()
            .WithNone<Destination2D>()
            .WithAll<Carrying>()
            .ForEach((Entity entity, int nativeThreadIndex, ref MovingTowards movingTowards) =>
        {
            var closeEnough = false;
            if (pos2DFromEntity.Exists(entity))
            {
                var distance = math.distancesq(movingTowards.Position, pos2DFromEntity[entity].Value);
                if (distance < 0.25f)
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

    static void GoToNearestBucket(
        NativeArray<Entity> bucketEntities,
        ComponentDataFromEntity<Position2D> pos2DFromEntity,
        Entity entity,
        int nativeThreadIndex,
        EntityCommandBuffer.Concurrent commandBuffer)
    {
        var distance = 999f;
        var nearestBucket = Entity.Null;
        for (int i = 0, length = bucketEntities.Length; i < length; ++i)
        {
            if (   pos2DFromEntity.Exists(bucketEntities[i]) 
                && pos2DFromEntity.Exists(entity))
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

        GoTo(entity, nearestBucket, commandBuffer, nativeThreadIndex, pos2DFromEntity);
    }
    
    static void GoToNearestEmptyBucket(
        NativeArray<Entity> bucketEntities,
        ComponentDataFromEntity<Position2D> pos2DFromEntity,
        ComponentDataFromEntity<GradientState> gradientStateFromEntity,
        Entity entity,
        int nativeThreadIndex,
        EntityCommandBuffer.Concurrent commandBuffer)
    {
        var distance = 999f;
        var nearestBucket = Entity.Null;
        for (int i = 0, length = bucketEntities.Length; i < length; ++i)
        {
            if (   pos2DFromEntity.Exists(bucketEntities[i]) 
                && pos2DFromEntity.Exists(entity) 
                && gradientStateFromEntity.Exists(bucketEntities[i])
                && gradientStateFromEntity[bucketEntities[i]].Value <= 0.0f)
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
        
        GoTo(entity, nearestBucket, commandBuffer, nativeThreadIndex, pos2DFromEntity);
    }

    static bool GoToNextInLine(ComponentDataFromEntity<InLine> inlineFromEntity,
        ComponentDataFromEntity<Carrying> carryingFromEntity,
        ComponentDataFromEntity<Position2D> pos2DFromEntity,
        int nativeThreadIndex,
        Entity entity,
        EntityCommandBuffer.Concurrent commandBuffer)
    {
        var success = false;
        
        if (inlineFromEntity.Exists(entity))
        {
            var next = inlineFromEntity[entity].Next;
            if (next != Entity.Null && !carryingFromEntity.Exists(next) && carryingFromEntity.Exists(entity))
            {
                var currentPos = pos2DFromEntity[entity].Value;
                var nextPos = pos2DFromEntity[next].Value;
                var targetPos = (currentPos + nextPos) / 2.0f;

                GoTo(entity, carryingFromEntity[entity].Value, targetPos, commandBuffer, nativeThreadIndex);

                success = true;
            }
        }

        return success;
    }
    
    static void GoTo(Entity entity, Entity targetEntity, EntityCommandBuffer.Concurrent commandBuffer, int nativeThreadIndex, ComponentDataFromEntity<Position2D> pos2DFromEntity)
    {
        // Tell the bot to go to that destination
        if (entity != Entity.Null)
        {
            GoTo(entity, targetEntity, pos2DFromEntity[targetEntity].Value, commandBuffer, nativeThreadIndex);
        }
    }

    static void GoTo(Entity entity, Entity targetEntity, float2 targetPos2D, EntityCommandBuffer.Concurrent commandBuffer, int nativeThreadIndex)
    {
        commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
        {
            Value = targetPos2D
        });

        commandBuffer.AddComponent(nativeThreadIndex, entity, new MovingTowards
        {
            Entity = targetEntity,
            Position = targetPos2D
        });
    }
}
    
    
