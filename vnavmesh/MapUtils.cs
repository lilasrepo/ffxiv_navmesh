using System.Numerics;

namespace Navmesh;

public static class MapUtils
{
    public static Vector3? FlagToPoint(NavmeshQuery q)
    {
        var flag = GetFlagPosition();
        if (flag == null)
            return null;
        return q.FindPointOnFloor(new(flag.Value.X, 1024, flag.Value.Y));
    }

    private unsafe static Vector2? GetFlagPosition()
    {
        // porting-note: AgentMap.FlagMarkerCount / FlagMapMarkers are game-7.5-only fields.
        // FlagToPoint feature degrades to "no flag set"; navmesh follow-flag command no-ops on TC 7.1.
        return null;
    }
}
