﻿using Unity.Entities;
using UnityEngine;

public class WaterAuthoringComponent : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<WaterTag>(entity);
    }
}
