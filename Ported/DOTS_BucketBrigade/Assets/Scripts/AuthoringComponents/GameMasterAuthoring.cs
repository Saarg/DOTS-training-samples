using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class GameMasterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField, Tooltip("The number of chains spawned with each the number of firefighters below.")]
    public int NbChains = 2;

    [SerializeField, Tooltip("The number of firefighters in each chain, half will fill buckets, half will fight the fire.")]
    public int NbFirefightersPerChain = 30;

    [SerializeField, Tooltip("The number of rows in the fire grid.")]
    public int NbRows = 50;

    [SerializeField, Tooltip("The number of columns in the fire grid.")]
    public int NbCols = 50;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GameMaster
        {
            NbChains = NbChains,
            NbFirefightersPerChain = NbFirefightersPerChain
        });

        dstManager.AddComponentData(entity, new Grid
        {
            Value = new UnsafeMultiHashMap<int2, Entity>(NbRows * NbCols, Allocator.Persistent)
        });

        dstManager.RemoveComponent<Translation>(entity);
        dstManager.RemoveComponent<Rotation>(entity);
        dstManager.RemoveComponent<LocalToWorld>(entity);
    }
}
