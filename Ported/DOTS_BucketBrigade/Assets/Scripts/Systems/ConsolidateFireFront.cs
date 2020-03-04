using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

//[UpdateAfter()]
public class ConsolidateFireFront : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem m_CommandBufferSystem;
    
    protected override void OnCreate()
    {
        m_CommandBufferSystem = World
            .DefaultGameObjectInjectionWorld
            .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        EntityCommandBuffer.Concurrent ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var grid = GetSingleton<Grid>();
        var handle = Entities.ForEach((Entity entity, int entityInQueryIndex, FireFrontTag tag, PositionInGrid posInGrid) =>
            {
                var aroundPos = new int2[]
                {
                    posInGrid.Value + new int2(-1, -1),
                    posInGrid.Value + new int2(-1, 0),
                    posInGrid.Value + new int2(-1, 1),
                    posInGrid.Value + new int2(0, -1),
                    //posInGrid.Value + new int2(0, 0),
                    posInGrid.Value + new int2(0, 1),
                    posInGrid.Value + new int2(1, -1),
                    posInGrid.Value + new int2(1, 0),
                    posInGrid.Value + new int2(1, 1),
                };

                var isInFront = true;
                foreach (var pos in aroundPos)
                {
                    if (grid.Physical.ContainsKey(pos))
                        isInFront = false;
                }
                
                if (!isInFront)
                    ecb.RemoveComponent<FireFrontTag>(entityInQueryIndex, entity);
                
            })
            .Schedule(inputDeps);
        
        m_CommandBufferSystem.AddJobHandleForProducer(handle);
        
        return handle;
    }
}
