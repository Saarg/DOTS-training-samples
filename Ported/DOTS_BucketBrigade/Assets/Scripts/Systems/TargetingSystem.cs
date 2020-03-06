using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class TargetingSystem : JobComponentSystem
{
    [BurstCompile]
    struct FromToJob : IJobForEach<FromTo>
    {
        [ReadOnly]
        public Grid Grid;
        
        [ReadOnly]
        public GameMaster GameMaster;
        
        [ReadOnly]
        public ComponentDataFromEntity<Position2D> Position2DFromEntity;

        public void Execute(ref FromTo fromTo)
        {
            if (fromTo.RelativeTo == Entity.Null)
                return;
            var position = Position2DFromEntity[fromTo.RelativeTo];

            var nearestFire = FindNearestOfTag(position.Value, Grid.Cell.ContentFlags.Fire, Grid, GameMaster);
            var nearestWater = FindNearestOfTag(nearestFire, Grid.Cell.ContentFlags.Water, Grid, GameMaster);
            
            if (math.distancesq(fromTo.Source, nearestWater) > 100.0f)
                fromTo.Source = nearestWater;
            if (math.distancesq(fromTo.Target, nearestFire) > 4.0f)
                fromTo.Target = nearestFire;
        }
    }

    private const float k_TimeBetweenUpdates = 2.0f;
    private float m_TimeSinceLastUpdate = k_TimeBetweenUpdates;
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_TimeSinceLastUpdate += Time.DeltaTime;
        if (m_TimeSinceLastUpdate <= k_TimeBetweenUpdates)
        {
            return default;
        }
        m_TimeSinceLastUpdate = 0.0f;
        
        var fromToJob = new FromToJob
        {
            Grid = GetSingleton<Grid>(),
            GameMaster = GetSingleton<GameMaster>(),
            Position2DFromEntity = GetComponentDataFromEntity<Position2D>(true)
        };
        
        var fromToJobHandle = fromToJob.Schedule(this, inputDeps);
        
        return fromToJobHandle;
    }

    public static float2 FindNearestOfTag(float2 position, Grid.Cell.ContentFlags flag, Grid grid, GameMaster gameMaster)
    {
        var biggerLength = gameMaster.NbRows > gameMaster.NbCols ? gameMaster.NbRows : gameMaster.NbCols;

        for (var i = 0; i < biggerLength; ++i)
        {
            for (var j = 0; j <= i; ++j)
            {
                for (var k = 0; k <= i; ++k)
                {
                    if (j < i && k < i)
                        continue;
                    var testJPosKPos = new float2(position.x + j, position.y + k);
                    if (grid.Physical[grid.ToGridPos(testJPosKPos)].Flags == flag)
                    {
                        return testJPosKPos;
                    }

                    var testJPosKNeg = new float2(position.x + j, position.y - k);
                    if (grid.Physical[grid.ToGridPos(testJPosKNeg)].Flags == flag)
                    {
                        return testJPosKNeg;
                    }

                    var testJNegKPos = new float2(position.x - j, position.y + k);
                    if (grid.Physical[grid.ToGridPos(testJNegKPos)].Flags == flag)
                    {
                        return testJNegKPos;
                    }

                    var testJNegKNeg = new float2(position.x - j, position.y - k);
                    if (grid.Physical[grid.ToGridPos(testJNegKNeg)].Flags == flag)
                    {
                        return testJNegKNeg;
                    }
                }
            }
        }

        return position;
    }
}
