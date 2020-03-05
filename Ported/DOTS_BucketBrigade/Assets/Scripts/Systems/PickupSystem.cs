using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateBefore(typeof(MoveToDestinationSystem))]
public class PickupSystem : JobComponentSystem
{
    private EntityCommandBufferSystem m_CommandBufferSystem;

    EntityQuery m_BucketsAvailable;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        m_BucketsAvailable = GetEntityQuery(ComponentType.Exclude<Carried>(), ComponentType.ReadOnly<BucketTag>());
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // Get the buckets that can be picked up
        var bucketEntities = m_BucketsAvailable.ToEntityArrayAsync(Allocator.TempJob, out var bucketEntitiesHandle);
        var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var pos2DFromEntity = GetComponentDataFromEntity<Position2D>(true);
        var inlineFromEntity = GetComponentDataFromEntity<InLine>(true);
        var carryingFromEntity = GetComponentDataFromEntity<Carrying>(true);

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
                    if (carryingFromEntity.Exists(previous))
                    {
                        commandBuffer.AddComponent(nativeThreadIndex, entity, new Destination2D
                        {
                            Value = pos2DFromEntity[previous].Value
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

            if (bucketEntity != Entity.Null)
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
        }).WithReadOnly(pos2DFromEntity)
          .Schedule(findBucketJobHandle);

        m_CommandBufferSystem.AddJobHandleForProducer(findBucketJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(pickupBucketJobHandle);

        var disposeHandle = bucketEntities.Dispose(pickupBucketJobHandle);

        return disposeHandle;
    }
}
