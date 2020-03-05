using Unity.Entities;
using Unity.Mathematics;

public class TargetingSystem : SystemBase
{

    protected override void OnUpdate()
    {
        var position2Ds = GetComponentDataFromEntity<Position2D>(true);
        Entities.ForEach((Entity entity, ref FromTo fromTo) =>
        {
            if (fromTo.RelativeTo == Entity.Null) 
                return;
            var position = position2Ds[fromTo.RelativeTo];

            var nearestFire = FindNearestOfTag(position.Value, Grid.Cell.ContentFlags.Fire);
            var nearestWater = FindNearestOfTag(nearestFire, Grid.Cell.ContentFlags.Water);
            
            if (math.any(fromTo.Source != nearestWater))
                fromTo.Source = nearestWater;
            if (math.any(fromTo.Target != nearestFire))
                fromTo.Target = nearestFire;
        }).WithoutBurst().Run();
    }

    float2 FindNearestOfTag(float2 position, Grid.Cell.ContentFlags flag)
    {
        var grid = GetSingleton<Grid>();
        var gameMaster = GetSingleton<GameMaster>();

        var biggerLength = gameMaster.NbRows > gameMaster.NbCols ? gameMaster.NbRows : gameMaster.NbCols;

        for (var i = 0; i < biggerLength; ++i)
        {
            for (var j = 0; j <= i; ++j)
            {
                for (var k = 0; k <= i; ++k)
                {
                    var testJPosKPos = new float2(position.x + j, position.y + k);
                    if (grid.Physical[grid.ToGridPos(testJPosKPos)].Flags.HasFlag(flag))
                    {
                        return testJPosKPos;
                    }
                    
                    var testJPosKNeg = new float2(position.x + j, position.y - k);
                    if (grid.Physical[grid.ToGridPos(testJPosKNeg)].Flags.HasFlag(flag))
                    {
                        return testJPosKNeg;
                    }
                    
                    var testJNegKPos = new float2(position.x - j, position.y + k);
                    if (grid.Physical[grid.ToGridPos(testJNegKPos)].Flags.HasFlag(flag))
                    {
                        return testJNegKPos;
                    }
                    
                    var testJNegKNeg = new float2(position.x - j, position.y - k);
                    if (grid.Physical[grid.ToGridPos(testJNegKNeg)].Flags.HasFlag(flag))
                    {
                        return testJNegKNeg;
                    }
                    
                }
            }
        }

        return position;
    }
    
    
}
