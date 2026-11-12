using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Spyro.Editor.App.Views;

/// <summary>Keeps the editor's windows inside the part of the screen they can use (below the menu bar, above the
/// Dock or the taskbar). A window that opens over another and would be taller than that is made as tall as the free
/// part and its contents scroll, so the buttons at its foot can always be reached; it is moved so none of it is off
/// the screen.</summary>
internal static class ScreenFit
{
    /// <summary>Room left round a window that fills the free part of the screen.</summary>
    private const double Margin = 16;
    private static bool _installed;

    /// <summary>Applies <see cref="Fit"/> to every window that opens over another.</summary>
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            // Sizing is a nicety: a screen that cannot be read (a test run without one) leaves the window as it is.
            try { Fit(window); } catch (Exception e) when (e is InvalidOperationException or NullReferenceException or NotSupportedException) { }
        }, RoutingStrategies.Direct);
    }

    /// <summary>The free part of the screen a window is on (or its owner's), in the window's own units; null when the
    /// screen is not known.</summary>
    public static Rect? FreeArea(Window window)
    {
        var screens = window.Screens;
        var screen = screens.ScreenFromWindow(window) ?? (window.Owner is Window owner ? owner.Screens.ScreenFromWindow(owner) : null) ?? screens.Primary;
        if (screen == null || screen.WorkingArea.Width <= 0 || screen.WorkingArea.Height <= 0) return null;
        double scale = screen.Scaling > 0 ? screen.Scaling : 1;
        var area = screen.WorkingArea;
        return new Rect(area.X / scale, area.Y / scale, area.Width / scale, area.Height / scale);
    }

    /// <summary>The tallest a window over <paramref name="owner"/> may be (null: not known).</summary>
    public static double? MaxHeightOver(Window owner)
    {
        try { return FreeArea(owner) is { } area ? Math.Max(320, area.Height - 2 * Margin) : null; }
        catch (Exception e) when (e is InvalidOperationException or NullReferenceException or NotSupportedException) { return null; }
    }

    public static void Fit(Window window)
    {
        // The main window is the owner's to size.
        if (window.Owner == null) return;
        var screens = window.Screens;
        var screen = screens.ScreenFromWindow(window) ?? (window.Owner is Window owner ? owner.Screens.ScreenFromWindow(owner) : null) ?? screens.Primary;
        if (screen == null || screen.WorkingArea.Height <= 0) return;
        double scale = screen.Scaling > 0 ? screen.Scaling : 1;
        var area = screen.WorkingArea;
        double maxHeight = Math.Max(320, area.Height / scale - 2 * Margin), maxWidth = Math.Max(420, area.Width / scale - 2 * Margin);
        bool sized = window.SizeToContent is SizeToContent.Height or SizeToContent.WidthAndHeight;
        double height = window.Bounds.Height > 0 ? window.Bounds.Height : window.Height;
        if (height > maxHeight + 0.5)
        {
            // Too tall for the screen: its contents scroll (a window that keeps its own scrolling part already does).
            if (sized && window.Content is Control content && content is not ScrollViewer && !HasOwnScroller(content))
            {
                window.Content = null;
                window.Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            }
            if (!sized) window.Height = maxHeight;
        }
        if (window.MaxHeight > maxHeight) window.MaxHeight = maxHeight;
        if (window.MaxWidth > maxWidth) window.MaxWidth = maxWidth;
        // Wholly on the screen: moved up (and in) when its foot or side would be under the Dock or past the edge.
        Dispatcher(() =>
        {
            double h = Math.Min(window.Bounds.Height > 0 ? window.Bounds.Height : maxHeight, maxHeight), w = Math.Min(window.Bounds.Width > 0 ? window.Bounds.Width : maxWidth, maxWidth);
            int top = area.Y + (int)Math.Round(Margin * scale), bottom = area.Bottom - (int)Math.Round(Margin * scale) - (int)Math.Round(h * scale);
            int left = area.X + (int)Math.Round(Margin * scale), right = area.Right - (int)Math.Round(Margin * scale) - (int)Math.Round(w * scale);
            var at = window.Position;
            int y = Math.Max(top, Math.Min(at.Y, bottom)), x = Math.Max(left, Math.Min(at.X, right));
            if (x != at.X || y != at.Y) window.Position = new PixelPoint(x, y);
        });
    }

    /// <summary>Whether a window's contents already scroll at their root (a docked layout round a scrolling part).</summary>
    private static bool HasOwnScroller(Control content) => content is DockPanel dock && dock.Children.OfType<ScrollViewer>().Any();

    /// <summary>Runs after the window has been laid out at its new size.</summary>
    private static void Dispatcher(Action action) => Avalonia.Threading.Dispatcher.UIThread.Post(action, Avalonia.Threading.DispatcherPriority.Background);
}
