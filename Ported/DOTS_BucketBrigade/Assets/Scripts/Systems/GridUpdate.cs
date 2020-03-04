using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class GridUpdate : JobComponentSystem
{
    private EntityCommandBufferSystem m_CommandBufferSystem;

    [BurstCompile, RequireComponentTag(typeof(ToDeleteFromGridTag))]
    struct ClearFiresGrid : IJobForEachWithEntity<PositionInGrid>
    {
        [NativeSetThreadIndex]
        public int ThreadIndex;
        
        public UnsafeHashMap<int2, Grid.Cell> Grid;
        public EntityCommandBuffer.Concurrent CommandBuffer;
        
        public void Execute(Entity entity, int index, ref PositionInGrid positionInGrid)
        {
            Grid.Remove(positionInGrid.Value);
            CommandBuffer.DestroyEntity(ThreadIndex, entity);
        }
    }

    [BurstCompile, RequireComponentTag(typeof(NewFireTag))]
    struct NewFireJob : IJobForEachWithEntity<PositionInGrid>
    {
        public UnsafeHashMap<int2, int> Grid;
        
        public void Execute(Entity entity, int index, ref PositionInGrid positionInGrid)
        {
            Grid.Remove(positionInGrid.Value);
        }
    }
    
    protected override void OnCreate()
    {
        m_CommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var grid = GetSingleton<Grid>();

        if (grid.Physical.Capacity < grid.Physical.Length + grid.Simulation.Length)
        {
            grid.Physical.Capacity = (grid.Physical.Length + grid.Simulation.Length) * 2;
        }

        if (grid.Simulation.Capacity < grid.Simulation.Length * 2)
        {
            grid.Simulation.Capacity = grid.Simulation.Length * 4;
        }

        var clearGridJob = new ClearFiresGrid
        {
            Grid = grid.Physical,
            CommandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };
        var clearGridJobHandle = clearGridJob.ScheduleSingle(this, inputDeps);
        m_CommandBufferSystem.AddJobHandleForProducer(clearGridJobHandle);
        
        var newFireJob = new NewFireJob
        {
            Grid = grid.Simulation
        };
        var newFireJobHandle = newFireJob.ScheduleSingle(this, clearGridJobHandle);

        return newFireJobHandle;
    }
}