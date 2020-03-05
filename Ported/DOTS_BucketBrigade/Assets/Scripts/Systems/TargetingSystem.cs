using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

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

        float2 FindNearestOfTag(float2 position, Grid.Cell.ContentFlags flag, Grid grid, GameMaster gameMaster)
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
        
        public void Execute(ref FromTo fromTo)
        {
            if (fromTo.RelativeTo == Entity.Null)
                return;
            var position = Position2DFromEntity[fromTo.RelativeTo];

            var nearestFire = FindNearestOfTag(position.Value, Grid.Cell.ContentFlags.Fire, Grid, GameMaster);
            var nearestWater = FindNearestOfTag(nearestFire, Grid.Cell.ContentFlags.Water, Grid, GameMaster);

            if (math.any(fromTo.Source != nearestWater))
                fromTo.Source = nearestWater;
            if (math.any(fromTo.Target != nearestFire))
                fromTo.Target = nearestFire;
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var fromToJob = new FromToJob
        {
            Grid = GetSingleton<Grid>(),
            GameMaster = GetSingleton<GameMaster>(),
            Position2DFromEntity = GetComponentDataFromEntity<Position2D>(true)
        };
        
        var fromToJobHandle = fromToJob.Schedule(this, inputDeps);

        return fromToJobHandle;
    }
}
