using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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
                    }
                }
            }
        }).WithReadOnly(pos2DFromEntity)
          .WithReadOnly(inlineFromEntity)
          .WithReadOnly(carryingFromEntity)
          .Schedule(bucketEntitiesHandle);

        // This job asks bots that are on a bucket to pick it up.
        // It looks through all bots that will be on top of the bucket,
        // so they arrived there (no more destination) and that are not carrying any bucket
        var pickupBucketJobHandle = Entities
            .WithNone<Carrying>()
            .WithNone<Destination2D>()
            .WithAll<BotTag>()
            .ForEach((Entity entity, int nativeThreadIndex) =>
        {
            var bucketEntity = Entity.Null;
            // Get the bucket that is nearest the bot, should be right next
            for (int i = 0, length = bucketEntities.Length; i < length; ++i)
            {
                if (pos2DFromEntity.Exists(bucketEntities[i]) && pos2DFromEntity.Exists(entity))
                {
                    var bucketPos = pos2DFromEntity[bucketEntities[i]];
                    var distance = math.distance(bucketPos.Value, pos2DFromEntity[entity].Value);
                    if (distance < 0.5f)
                    {
                        bucketEntity = bucketEntities[i];
                    }
                }
            }

            if (bucketEntity != Entity.Null && gradientStateFromEntity.Exists(bucketEntity))
            {
                var gradient = gradientStateFromEntity[bucketEntity].Value;
                if (gradient <= 0.0f || gradient >= 1.0f)
                {
                    commandBuffer.AddComponent(nativeThreadIndex, bucketEntity, new Carried
                    {
                        Value = entity
                    });
                    commandBuffer.AddComponent(nativeThreadIndex, entity, new Carrying
                    {
                        Value = bucketEntity
                    });
                }
            }
        }).WithReadOnly(gradientStateFromEntity)
          .WithReadOnly(pos2DFromEntity)
          .Schedule(findBucketJobHandle);

        var moveBucketJobHandle = Entities
            .WithAll<BotTag>()
            .WithAll<Carrying>()
            .WithNone<Destination2D>()
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

                    var destinationFound = FindNearestCell(
                        grid,
                        inSearchOfFire ? Grid.Cell.ContentFlags.Fire : Grid.Cell.ContentFlags.Water,
                        pos2DFromEntity,
                        gridPosFromEntity,
                        entity,
                        out var nearestDestination);

                    if (destinationFound)
                    {
                        commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                        {
                            Value = nearestDestination
                        });
                    }
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
                    }
                }
            }
        }).WithReadOnly(gradientStateFromEntity)
          .WithReadOnly(inlineFromEntity)
          .WithReadOnly(gridPosFromEntity)
          .WithReadOnly(carryingFromEntity)
          .Schedule(pickupBucketJobHandle);

        var dropBucketJobHandle = Entities
            .WithAll<BotTag>()
            .WithNone<Destination2D>()
            .ForEach((Entity entity, int nativeThreadIndex, ref Role role) =>
        {
            // If you arrived at your destination and you are carrying a bucket, drop it
        }).WithReadOnly(pos2DFromEntity)
          .Schedule(moveBucketJobHandle);

        m_CommandBufferSystem.AddJobHandleForProducer(findBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(pickupBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(moveBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(dropBucketJobHandle);

        var disposeHandle = bucketEntities.Dispose(pickupBucketJobHandle);

        return disposeHandle;
    }

    bool FindNearestCell(Grid grid,
                          Grid.Cell.ContentFlags flag,
                          ComponentDataFromEntity<Position2D> pos2DFromEntity,
                          ComponentDataFromEntity<PositionInGrid> gridPosFromEntity,
                          Entity currentBot,
                          out float2 nearestDestination)
    {
        var botPos = pos2DFromEntity[currentBot].Value;

        nearestDestination = float2.zero;
        var distance = 999.0f;
        bool destinationFound = false;

        for (int i = 0; i < grid.Physical.Length; ++i)
        {
            var keyPair = grid.Physical[i];
            if (keyPair.Flags == flag)
            {
                var gridPos2D = float2.zero; 
                if (gridPosFromEntity.Exists(keyPair.Entity))
                {
                    gridPos2D = grid.ToPos2D(gridPosFromEntity[keyPair.Entity].Value);
                }

                var newDistance = math.distance(botPos, gridPos2D);
                if (newDistance < distance)
                {
                    distance = newDistance;
                    nearestDestination = gridPos2D;
                    destinationFound = true;
                }
            }
        }

        return destinationFound;
    }
}
