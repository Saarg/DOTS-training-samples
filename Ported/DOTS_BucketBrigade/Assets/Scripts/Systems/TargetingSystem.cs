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

            var nearestWater = FindNearestOfTag(position.Value, Grid.Cell.ContentFlags.Water);
            var nearestFire = FindNearestOfTag(position.Value, Grid.Cell.ContentFlags.Fire);

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
            for (var j = -i; j <= i; ++j)
            {
                for (var k = -i; k <= i; ++k)
                {
                    var testPosition = new float2(position.x +j, position.y + k);
                    if (grid.Physical[grid.ToGridPos(testPosition)].Flags.HasFlag(flag))
                    {
                        return testPosition;
                    }
                }
            }
        }

        return position;
    }
    
    
}
