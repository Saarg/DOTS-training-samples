using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BucketAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<BucketTag>(entity);
        dstManager.AddComponentData(entity, new Position2D { Value = new float2(transform.position.x, transform.position.z)});
        dstManager.AddComponent<GradientState>(entity);
    }
}
