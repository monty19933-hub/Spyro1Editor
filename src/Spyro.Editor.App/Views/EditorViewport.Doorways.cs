namespace Spyro.Editor.App.Views;

/// <summary>Where the 3D view's camera stands and looks (for Terrain › Doorways › Add Doorway… "in front of the camera").</summary>
public sealed partial class EditorViewport
{
    /// <summary>The fly camera's position and horizontal looking direction in level coordinates (editor units, X east, Y
    /// north); false outside the 3D view.</summary>
    internal bool TryGetFlyCameraInLevel(out double x, out double y, out double z, out double forwardX, out double forwardY)
    {
        x = y = z = forwardX = forwardY = 0;
        if (_viewMode != ViewportViewMode.Fly3D)
            return false;
        x = FromFlyViewX(_flyCamera.X);
        y = FromFlyViewY(_flyCamera.Y);
        z = _flyCamera.Z;
        // The camera's forward direction in view space, turned back into level space (each axis keeps its own sign).
        forwardX = FromFlyViewX(Math.Cos(_flyCamera.Yaw));
        forwardY = FromFlyViewY(Math.Sin(_flyCamera.Yaw));
        double length = Math.Sqrt(forwardX * forwardX + forwardY * forwardY);
        if (length < 1e-9)
            return false;
        forwardX /= length;
        forwardY /= length;
        return true;
    }

    /// <summary>Tests and pictures: the 3D view's camera at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>)
    /// looking at the level point (<paramref name="tx"/>, <paramref name="ty"/>, <paramref name="tz"/>).</summary>
    internal void LookFromForTesting(double x, double y, double z, double tx, double ty, double tz)
    {
        double vx = ToFlyViewX(tx) - ToFlyViewX(x), vy = ToFlyViewY(ty) - ToFlyViewY(y);
        SetFlyGameCameraForTesting(x, y, z, Math.Atan2(vy, vx), Math.Atan2(tz - z, Math.Sqrt(vx * vx + vy * vy)));
    }
}
