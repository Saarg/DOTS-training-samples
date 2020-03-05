using Unity.Entities;
using Unity.Mathematics;

// Chop chop
public struct Chopper : IComponentData
{
    public float VerticalDirection;

    public float VerticalSpeed;
    
    public float VerticalPos;
    public float MaxVerticalPos;

    public enum ActionState
    {
        FlyingToDestination,
        Dropping,
        PerformingAction,
        MovingUp,
    }

    public ActionState State;
    public bool IsToDropWaterOnFire;

    public float2 Destination;
}