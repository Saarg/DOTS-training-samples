using Unity.Entities;
using UnityEngine;

public class BucketAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<BucketTag>(entity);
    }
}
