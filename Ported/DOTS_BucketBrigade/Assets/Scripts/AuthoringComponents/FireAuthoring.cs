using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class FireAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<FireTag>(entity);
        dstManager.AddComponentData<PositionInGrid>(entity, new PositionInGrid { Value = new int2((int)transform.position.x, (int)transform.position.z) });
        dstManager.AddComponent<NewFireTag>(entity);
        
        dstManager.AddComponentData<GradientState>(entity, new GradientState { Value = 1.0f });
    }
}
