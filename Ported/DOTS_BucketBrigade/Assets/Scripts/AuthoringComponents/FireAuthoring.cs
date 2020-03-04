using Unity.Entities;
using UnityEngine;

public class FireAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<FireTag>(entity);
        dstManager.AddComponent<PositionInGrid>(entity);
        dstManager.AddComponent<NewFireTag>(entity);
        
        dstManager.AddComponentData<GradientState>(entity, new GradientState { Value = 1.0f });
    }
}
