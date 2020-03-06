using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ClickableFloorToGridSystem : SystemBase
{
    private Camera m_Camera;

    protected override void OnCreate()
    {
        m_Camera = Camera.main;
    }

    protected override void OnUpdate()
    {
        if (!Input.GetMouseButton(1) && !Input.GetMouseButtonDown(0))
            return;

        GameMaster gameMaster = GetSingleton<GameMaster>();
        var ray = m_Camera.ScreenPointToRay(Input.mousePosition);
        var endSim = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        var commandBuffer = endSim.CreateCommandBuffer();
        Entities.ForEach((MeshCollider collider) =>
        {
            if (collider.Raycast(ray, out var hit, 1000.0f))
            {
                var fireEntity = commandBuffer.Instantiate(gameMaster.FirePrefab);

                var pos = new int2((int) hit.point.x, (int) hit.point.z);
                commandBuffer.SetComponent(fireEntity, new PositionInGrid {Value = pos});

                commandBuffer.SetComponent(fireEntity, new Translation
                {
                    Value = new float3(hit.point.x, 0.5f, hit.point.z)
                });
            }
        }).WithoutBurst().Run();
    }
}
