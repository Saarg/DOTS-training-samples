using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class GridUpdate : JobComponentSystem
{
    private static NativeArray<int2> m_AroundCells;
    private EntityCommandBufferSystem m_CommandBufferSystem;
    
    // Downgrade a fire to a sim-fire
    [BurstCompile, RequireComponentTag(typeof(ToDeleteFromGridTag)), ExcludeComponent(typeof(PreFireTag))]
    struct ClearFiresGrid : IJobForEachWithEntity<PositionInGrid>
    {
        [NativeSetThreadIndex]
        public int ThreadIndex;
        
        public UnsafeHashMap<int2, Grid.Cell> Grid;
        public UnsafeHashMap<int2, int> SimulationGrid;
        public EntityCommandBuffer.Concurrent CommandBuffer;

        public NativeArray<int2> AroundCells;
        
        public void Execute(Entity entity, int index, ref PositionInGrid positionInGrid)
        {
            // Check the around fires to set the fire-front tag accordingly:
            for (var i = 0; i < AroundCells.Length; ++i)
            {
                var currentPos = positionInGrid.Value + AroundCells[i];
                if (Grid.TryGetValue(currentPos, out global::Grid.Cell cell) && cell.Flags == global::Grid.Cell.ContentFlags.Fire)
                {
                    CommandBuffer.AddComponent<SpawnAroundSimFireTag>(ThreadIndex, cell.Entity);
                }
            }

            Grid.Remove(positionInGrid.Value);
            // Check that we don't already have something in the sim grid:
            if (!SimulationGrid.TryAdd(positionInGrid.Value, 0))
            {
                CommandBuffer.DestroyEntity(ThreadIndex, entity);
                return;
            }
            // Set the correct tags for the fire:
            CommandBuffer.RemoveComponent<ToDeleteFromGridTag>(ThreadIndex, entity);
            CommandBuffer.RemoveComponent<NewFireTag>(ThreadIndex, entity);
            CommandBuffer.RemoveComponent<SpawnAroundSimFireTag>(ThreadIndex, entity);
            CommandBuffer.AddComponent<PreFireTag>(ThreadIndex, entity);
        }
    }
    
    // Downgrade a sim-fire to a destroyed entity
    [BurstCompile, RequireComponentTag(typeof(ToDeleteFromGridTag), typeof(PreFireTag))]
    struct ClearSimFiresGrid : IJobForEachWithEntity<PositionInGrid>
    {
        [NativeSetThreadIndex]
        public int ThreadIndex;
        
        public UnsafeHashMap<int2, int> SimulationGrid;
        public EntityCommandBuffer.Concurrent CommandBuffer;
        
        public void Execute(Entity entity, int index, ref PositionInGrid positionInGrid)
        {
            SimulationGrid.Remove(positionInGrid.Value);
            CommandBuffer.DestroyEntity(ThreadIndex, entity);
        }
    }

    [BurstCompile, RequireComponentTag(typeof(NewFireTag))]
    struct NewFireJob : IJobForEachWithEntity<PositionInGrid>
    {
        public UnsafeHashMap<int2, int> SimulationGrid;
        
        public void Execute(Entity entity, int index, ref PositionInGrid positionInGrid)
        {
            SimulationGrid.Remove(positionInGrid.Value);
        }
    }
    
    public UnsafeHashMap<int2, Grid.Cell> Physical;
    public UnsafeHashMap<int2, int /*<unused>*/> Simulation;
    
    protected override void OnCreate()
    {
        m_AroundCells = new NativeArray<int2>(new int2[]
        {
            new int2(-1, -1),
            new int2(-1, 0),
            new int2(-1, 1),
            new int2(0, -1),
            // new int2(0, 0),
            new int2(0, 1),
            new int2(1, -1),
            new int2(1, 0),
            new int2(1, 1),
        }, Allocator.Persistent);
        
        m_CommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<Grid>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var grid = GetSingleton<Grid>();
        if (!Physical.IsCreated || !Simulation.IsCreated)
        {
            Physical = grid.Physical;
            Simulation = grid.Simulation;
        }

        // Update the capacities if necessary 
        if (Physical.Capacity < Physical.Length + Simulation.Length)
        {
            Physical.Capacity = (Physical.Length + Simulation.Length) * 2;
        }

        if (Simulation.Capacity < Simulation.Length * 2)
        {
            Simulation.Capacity = Simulation.Length * 4;
        }
        
        var ecbMaxOutCleanup = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var maxOutFireCleanupJobHandle = Entities
            .WithAll<MaxOutFireTag>()
            .ForEach((Entity entity, int entityInQueryIndex, in GradientState state) =>
            {
                if (state.Value < 1.0f)
                    ecbMaxOutCleanup.RemoveComponent<MaxOutFireTag>(entityInQueryIndex, entity);
            }).Schedule(inputDeps);
        m_CommandBufferSystem.AddJobHandleForProducer(maxOutFireCleanupJobHandle);
        
        var clearsimGridJob = new ClearSimFiresGrid
        {
            SimulationGrid = Simulation,
            CommandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };
        var clearSimGridJobHandle = clearsimGridJob.ScheduleSingle(this, maxOutFireCleanupJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(clearSimGridJobHandle);
      
        var clearGridJob = new ClearFiresGrid
        {
            Grid = Physical,
            SimulationGrid = Simulation,
            AroundCells = m_AroundCells,
            CommandBuffer = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };
        var clearGridJobHandle = clearGridJob.ScheduleSingle(this, clearSimGridJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(clearGridJobHandle);
        
        var newFireJob = new NewFireJob
        {
            SimulationGrid = Simulation
        };
        var newFireJobHandle = newFireJob.ScheduleSingle(this, clearGridJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(newFireJobHandle);

        var physical = Physical.AsParallelWriter();
        var waterUpdateJobHandle = Entities.WithChangeFilter<WaterTag>().WithAll<WaterTag>()
            .ForEach((Entity entity, in Position2D position2D, in Capacity capacity) =>
            {
                var gridPos = grid.ToGridPos(position2D);
                var offset = int2.zero;
                int radius = (int)math.round(capacity.Value * 0.05f);
                for (offset.y = -radius; offset.y <= radius; offset.y++)
                {
                    for (offset.x = -radius; offset.x <= radius; offset.x++)
                    {
                        var cell = new Grid.Cell {Entity = entity, Flags = Grid.Cell.ContentFlags.Water};
                        physical.TryAdd(gridPos + offset, cell);
                    }
                }
            }).Schedule(newFireJobHandle);
        m_CommandBufferSystem.AddJobHandleForProducer(waterUpdateJobHandle);

        var ecb = m_CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var fireCleanupJobHandle = Entities
            .WithAll<FireTag>()
            .ForEach((Entity entity, int entityInQueryIndex, in PositionInGrid pos) =>
            {
                if (grid.Physical.TryGetValue(pos.Value, out Grid.Cell cell) && cell.Entity != entity)
                    ecb.DestroyEntity(entityInQueryIndex, entity);
            }).Schedule(waterUpdateJobHandle);
        
        m_CommandBufferSystem.AddJobHandleForProducer(fireCleanupJobHandle);

        var cleanupJobHandle = Entities
            .WithAll<DelayedDeleteTag>()
            .ForEach((Entity entity, int entityInQueryIndex) =>
            {
                ecb.DestroyEntity(entityInQueryIndex, entity);
            }).Schedule(fireCleanupJobHandle);
        
        m_CommandBufferSystem.AddJobHandleForProducer(cleanupJobHandle);
        return JobHandle.CombineDependencies(waterUpdateJobHandle, fireCleanupJobHandle, cleanupJobHandle);
    }

    protected override void OnDestroy()
    {
        if (Physical.IsCreated)
            Physical.Dispose();
        
        if (Simulation.IsCreated)
            Simulation.Dispose();
        m_AroundCells.Dispose();
    }
}
