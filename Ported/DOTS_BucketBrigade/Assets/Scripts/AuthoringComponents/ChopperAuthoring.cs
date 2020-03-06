using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Chop chop
public class ChopperAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float ChopperVerticalSpeed = 10;
    public float ChopperHorizontalSpeed = 0.5f;
    public bool DropFire = false;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData<Chopper>(entity, new Chopper
        {
            VerticalDirection = 0,
            
            VerticalSpeed = ChopperVerticalSpeed,
            
            VerticalPos = transform.position.y,
            MaxVerticalPos = transform.position.y,
            
            State = Chopper.ActionState.MovingUp,
            
            DropFire = DropFire,
        });

        dstManager.AddComponentData<Position2D>(entity, new Position2D { Value = ((float3)transform.position).xz});
        dstManager.AddComponentData<MovementSpeed>(entity, new MovementSpeed { Value = ChopperHorizontalSpeed});
        dstManager.AddComponentData<FromTo>(entity, new FromTo { RelativeTo = entity });
    }
}