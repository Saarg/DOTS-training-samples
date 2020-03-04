using Unity.Entities;
using UnityEngine;

public class WaterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<WaterTag>(entity);
        dstManager.AddComponentData(entity, new GradientState { Value = 0.9f });
        dstManager.AddComponentData(entity, new Capacity { Value = transform.localScale.x * 10 });
    }
}
