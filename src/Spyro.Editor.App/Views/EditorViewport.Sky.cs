using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Spyro.Editor.Core.Sky;

namespace Spyro.Editor.App.Views;

/// <summary>The level's real sky behind the 3D view: the panorama of the sky the game draws (see
/// <see cref="LevelSkyResolver"/>), looked up for each direction the camera sees. The game draws its sky around the camera
/// without moving it, so only the camera's turn and tilt matter.</summary>
public sealed partial class EditorViewport
{
    private SkyImage? _skyPanorama;
    private WriteableBitmap? _skyBackground;
    private (double Yaw, double Pitch, double Width, double Height, bool Flip) _skyBackgroundKey;

    /// <summary>The sky to draw behind the 3D view (a panorama, middle facing +X), or null for the plain backdrop.</summary>
    public void SetSkyPanorama(SkyImage? panorama)
    {
        _skyPanorama = panorama;
        _skyBackgroundKey = default;
        InvalidateVisual();
    }

    internal bool HasSkyPanoramaForTesting => _skyPanorama != null;

    /// <summary>The drawn sky's colour at a point of the view (fractions of its width and height), after a render.</summary>
    internal (byte R, byte G, byte B)? SkyBackgroundPixelForTesting(double fx, double fy)
    {
        if (_skyBackground == null) return null;
        using var locked = _skyBackground.Lock();
        int x = Math.Clamp((int)(fx * _skyBackground.PixelSize.Width), 0, _skyBackground.PixelSize.Width - 1);
        int y = Math.Clamp((int)(fy * _skyBackground.PixelSize.Height), 0, _skyBackground.PixelSize.Height - 1);
        byte[] pixel = new byte[4];
        Marshal.Copy(locked.Address + y * locked.RowBytes + x * 4, pixel, 0, 4);
        return (pixel[0], pixel[1], pixel[2]);
    }

    /// <summary>Draws the sky behind the scene; false when there is none to draw.</summary>
    private bool DrawSkyBackground(DrawingContext context, Rect bounds)
    {
        if (_skyPanorama is not { } panorama || _nativeGteProjectionExperimentEnabled || bounds.Width < 2 || bounds.Height < 2)
            return false;
        // A third of the view's resolution, drawn smoothly scaled: the sky has no sharp detail.
        int width = Math.Clamp((int)Math.Ceiling(bounds.Width / 3), 32, 640), height = Math.Clamp((int)Math.Ceiling(bounds.Height / 3), 18, 360);
        var key = (_flyCamera.Yaw, _flyCamera.Pitch, bounds.Width, bounds.Height, _flipMapY);
        if (_skyBackground == null || _skyBackground.PixelSize.Width != width || _skyBackground.PixelSize.Height != height || key != _skyBackgroundKey)
        {
            if (_skyBackground == null || _skyBackground.PixelSize.Width != width || _skyBackground.PixelSize.Height != height)
            {
                _skyBackground?.Dispose();
                _skyBackground = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Unpremul);
            }
            RenderSky(panorama, _skyBackground, bounds);
            _skyBackgroundKey = key;
        }
        context.DrawImage(_skyBackground, new Rect(0, 0, width, height), bounds);
        return true;
    }

    /// <summary>Each pixel looks up the sky in its direction, with the same projection the 3D view draws the level with.</summary>
    private void RenderSky(SkyImage panorama, WriteableBitmap target, Rect bounds)
    {
        int width = target.PixelSize.Width, height = target.PixelSize.Height;
        double focal = Math.Min(bounds.Width, bounds.Height) * 0.9;
        double cosYaw = Math.Cos(_flyCamera.Yaw), sinYaw = Math.Sin(_flyCamera.Yaw), cosPitch = Math.Cos(_flyCamera.Pitch), sinPitch = Math.Sin(_flyCamera.Pitch);
        // The view's axes (in its own X/Y, which the map orientation may flip relative to the world's).
        SkyVector forward = new(cosYaw * cosPitch, sinYaw * cosPitch, sinPitch), right = new(-sinYaw, cosYaw, 0), up = new(-cosYaw * sinPitch, -sinYaw * sinPitch, cosPitch);
        double signX = Core.EditorUiDefaults.MapXAxisScreenSign(_flipMapY), signY = Core.EditorUiDefaults.MapYAxisScreenSign(_flipMapY);
        byte[] row = new byte[width * 4];
        using var locked = target.Lock();
        for (int y = 0; y < height; y++)
        {
            double vertical = -((y + 0.5) * bounds.Height / height - bounds.Height / 2) / focal;
            for (int x = 0; x < width; x++)
            {
                double across = ((x + 0.5) * bounds.Width / width - bounds.Width / 2) / focal;
                SkyVector d = forward + right * across + up * vertical;
                var (u, v) = SkyDirections.ToUv(new(d.X * signX, d.Y * signY, d.Z));
                var (r, g, b, _) = panorama.Sample(u, v);
                row[x * 4] = (byte)r; row[x * 4 + 1] = (byte)g; row[x * 4 + 2] = (byte)b; row[x * 4 + 3] = 255;
            }
            Marshal.Copy(row, 0, locked.Address + y * locked.RowBytes, row.Length);
        }
    }
}
