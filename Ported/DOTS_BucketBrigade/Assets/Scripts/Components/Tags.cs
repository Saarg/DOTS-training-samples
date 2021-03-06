﻿using Unity.Entities;

public struct FireTag : IComponentData { }

public struct SpawnAroundSimFireTag : IComponentData { }

public struct WaterTag : IComponentData { }
public struct BucketTag : IComponentData { }
public struct DestroyBucketWhenEmptyTag : IComponentData { }
public struct BotTag : IComponentData { }
public struct FireFrontTag : IComponentData { }
public struct PreFireTag : IComponentData { }
public struct NewFireTag : IComponentData { }
public struct MaxOutFireTag : IComponentData { }
public struct ToDeleteFromGridTag : IComponentData { }
public struct DelayedDeleteTag : IComponentData { }
public struct SpawnPrefabsTag : IComponentData { }
