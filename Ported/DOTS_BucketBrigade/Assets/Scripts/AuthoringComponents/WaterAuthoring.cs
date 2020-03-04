using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WaterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<WaterTag>(entity);
        var position = transform.position;
        dstManager.AddComponentData(entity, new Position2D
        {
            Value = new float2(position.x, position.z)
        });
        dstManager.AddComponentData(entity, new GradientState { Value = 0.9f });
        dstManager.AddComponentData(entity, new Capacity { Value = transform.localScale.x * 10 });
    }
}
