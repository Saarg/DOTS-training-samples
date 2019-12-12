﻿using System;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
[Serializable]
public struct LookForThrowTargetState : IComponentData
{
    public Entity GrabbedEntity;
    public float3 TargetSize;
}