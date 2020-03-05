using Unity.Entities;
using UnityEngine;

public class BucketAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<BucketTag>(entity);
        dstManager.AddComponent<Position2D>(entity);
        dstManager.AddComponent<GradientState>(entity);
        dstManager.AddComponentData(entity, new Capacity{ Value = 1.0f });
    }
}
