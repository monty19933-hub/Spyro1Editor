using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Read-only first-frame source-model preview. Never changes an import package or substitutes an icon.</summary>
internal static class UniversalMobyModelPreview
{
    internal sealed record Frame(byte[]? Pixels, string Description, string? SourceHash = null, int VertexCount = 0, int FaceCount = 0);
    private sealed record Vertex(double X, double Y, double Z);
    private sealed record Face(int[] Vertices, uint[] Colors, byte[]? Texture);
    private static readonly ConcurrentDictionary<string, Lazy<Task<Frame>>> Cache = new();
    private static readonly SemaphoreSlim Workers = new(2);
    internal const int Size = 224;

    internal static Task<Frame> LoadAsync(UniversalSourceLibrary library, UniversalMobyVariant variant, UniversalJapaneseSource? japanese,
        Spyro.Editor.Core.Crash.CrashWarpedDisc? crash = null, Spyro.Editor.Core.Music.YearOfTheDragonDisc? yotd = null)
    {
        string key = $"{library.ImagePath}|{japanese?.ImagePath}|{crash?.ImagePath}|{yotd?.ImagePath}|{variant.SourceLevelKey}|{variant.ClassId}";
        return Cache.GetOrAdd(key, _ => new Lazy<Task<Frame>>(() => Task.Run(async () =>
        {
            await Workers.WaitAsync();
            try
            {
                byte[] package, vram;
                if (UniversalSpecialCatalog.IsSpecial(variant))
                {
                    var asset = UniversalMobyExport.SpecialProvider(japanese, library, crash, yotd)?.Invoke(UniversalSpecialCatalog.SpecialIdForClass(variant.ClassId)!);
                    if (asset == null)
                        return new(null, variant.SourceLevelKey == UniversalCrashBoxes.SourceKey ? "Choose your Crash disc to preview this model."
                            : variant.SourceLevelKey == UniversalCrashEnemies.YotdSourceKey ? "Choose your Year of the Dragon disc to preview this creature."
                            : "Choose your Japanese disc to preview this model.");
                    package = asset.Package; vram = asset.SourceVram;
                }
                else if (variant.HasLocalModel)
                {
                    lock (library) { var data = library.Get(variant.SourceLevelKey).Data; package = data.Package(variant.ClassId); vram = data.Vram().ToArray(); }
                }
                else if (variant.SharedModel)
                {
                    byte[] shared = library.Disc.EntryBytes(8);
                    int root = -1;
                    for (int field = 4; field + 8 <= 0x800; field += 8)
                        if (Word(shared, field + 4) == variant.ClassId) { root = (int)Word(shared, field); break; }
                    if (root <= 0 || root >= shared.Length) return new(null, "Shared model source is not available for preview.");
                    package = shared[root..]; vram = library.Get(variant.SourceLevelKey).Data.Vram().ToArray();
                }
                else return new(null, "No visible model — this object is a controller or trigger.");
                return Decode(package, vram, UniversalDragonflyEggs.IsEggClass(variant.ClassId));
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException)
            { return new(null, "Model preview unavailable: " + ex.Message); }
            finally { Workers.Release(); }
        }))).Value;
    }

    internal static Control Build(Func<Task<Frame>> load, bool detailed = false)
    {
        Image image = new() { Stretch = Stretch.Uniform, IsVisible = false, Name = detailed ? "UniversalMobyModelImage" : null };
        TextBlock note = new() { Text = detailed ? "Loading source model…" : "…", TextWrapping = TextWrapping.Wrap,
            FontSize = detailed ? 11 : 9, Foreground = Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(6), Name = detailed ? "UniversalMobyModelPreviewStatus" : null };
        Grid body = new(); body.Children.Add(image); body.Children.Add(note);
        Border host = new() { Name = detailed ? "UniversalMobyModelPreview" : null, Child = body, Background = new SolidColorBrush(Color.Parse("#EAF0F5")),
            CornerRadius = new CornerRadius(8), ClipToBounds = true, Height = detailed ? 224 : 62, Width = detailed ? double.NaN : 62 };
        bool started = false;
        int generation = 0;
        host.AttachedToVisualTree += async (_, _) =>
        {
            if (started) return; started = true;
            int request = ++generation;
            Frame frame = await load();
            if (request != generation) return;
            if (frame.Pixels != null)
            {
                WriteableBitmap bitmap = new(new PixelSize(Size, Size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                using (var buffer = bitmap.Lock())
                    for (int y = 0; y < Size; y++) Marshal.Copy(frame.Pixels, y * Size * 4, buffer.Address + y * buffer.RowBytes, Size * 4);
                image.Source = bitmap; image.IsVisible = true; note.IsVisible = false;
            }
            else note.Text = detailed ? frame.Description : "No preview";
            ToolTip.SetTip(host, frame.Description);
            host.Tag = frame;
        };
        host.DetachedFromVisualTree += (_, _) =>
        {
            generation++; started = false;
            if (image.Source is IDisposable bitmap) bitmap.Dispose();
            image.Source = null; image.IsVisible = false;
        };
        return host;
    }

    internal static Frame Decode(byte[] package, byte[] vram, bool dragonflyEgg = false)
    {
        List<Vertex> vertices = []; List<Face> faces = [];
        if (unchecked((int)Word(package, 0)) < 0)
        {
            var geometry = NativeSimpleGouraudModelGeometry.Decode(package);
            vertices.AddRange(geometry.Vertices.Select(v => new Vertex(v.X, v.Y, v.Z)));
            faces.AddRange(geometry.Faces.Select(f => new Face(f.VertexIndices.Take(f.IsTriangle ? 3 : 4).ToArray(),
                f.ColorIndices.Select(i => geometry.PackedRgbWords[i]).ToArray(), null)));
        }
        else
        {
            int animations = (int)Word(package, 0);
            Require(animations is > 0 and <= 1024, "Unknown animated model header.");
            int animation = Enumerable.Range(0, animations).Select(i => (int)Word(package, 0x38 + i * 4)).FirstOrDefault(i => i > 0);
            Require(animation > 0, "No animation frame is available.");
            int md = (int)Word(package, 0x34), count = Byte(package, animation + 8), shadingCount = Half(package, animation + 2);
            Require(count > 0 && Half(package, animation) > 0 && Byte(package, animation + 4) == 0, "This animation encoding has no admitted preview decoder.");
            uint packed = Word(package, animation + 0x24), aux = Word(package, animation + 0x28);
            // Frame-specific face modifications need a separate decoder; never silently draw the wrong topology.
            Require((aux & 0xFFFF) == 0, "Frame-specific face patches are not supported yet.");
            int start = checked(md + (int)(packed & 0x1FFFFF)), absoluteCount = (int)(aux >> 24);
            Require(absoluteCount is > 0 && absoluteCount <= count, "Invalid vertex partition.");
            int absolute = start, deltaStart = checked(start + absoluteCount * 4), delta = deltaStart;
            int x = 0, y = 0, z = 0, flag = 0, shift = Byte(package, animation + 6) + 1;
            Require(shift <= 10, "Invalid vertex scale.");
            for (int i = 0; i < count; i++)
            {
                int word;
                if (flag == 0)
                {
                    Require(absolute < deltaStart, "Vertex stream crossed its absolute partition.");
                    word = unchecked((int)Word(package, absolute)); absolute += 4;
                    x = word >> 21; y = unchecked(word << 10) >> 21; z = unchecked(word << 20) >> 19;
                }
                else
                {
                    word = unchecked((short)Half(package, delta)); delta += 2;
                    x += (word >> 11) << shift; y -= (unchecked(word << 21) >> 27) << shift; z -= (unchecked(word << 26) >> 27) << shift;
                }
                vertices.Add(new(x, y, z)); flag = word & 1;
            }
            Require(absolute == deltaStart, "Vertex partition was not fully consumed.");
            int colors = checked(md + (int)Word(package, animation + 0x18));
            int table = checked(md + (int)Word(package, animation + 0x14));
            int end = checked(table + 4 + (int)Word(package, table));
            Require(end <= package.Length && end > table + 4 && end - table <= 0x20004, "Invalid model face table.");
            for (int p = table + 4; p < end;)
            {
                uint word = Word(package, p), color = Word(package, p + 4);
                bool quad = (word & 0x80000000) != 0, alternate = quad && (word & 4) != 0, textured = (word & 2) != 0 || alternate;
                Require(!alternate, "Alternate face commands need a separate preview decoder.");
                int bytes = quad ? textured ? 24 : 12 : textured ? 20 : 8;
                Require(p + bytes <= end, "Truncated face command.");
                List<int> vi = [(int)(word >> 22 & 511), (int)(word >> 13 & 511), (int)(word >> 4 & 511)];
                List<int> ci = [(int)(color >> 19 & 511), (int)(color >> 10 & 511), (int)(color >> 1 & 511)];
                if (quad) { uint fourth = Word(package, p + 8); vi.Add((int)(fourth >> 2 & 511)); ci.Add((int)(fourth >> 11 & 511)); }
                Require(vi.All(i => i < count) && ci.All(i => i < shadingCount), "Face references an unavailable vertex or colour.");
                faces.Add(new(vi.ToArray(), ci.Select(i => Word(package, colors + i * 4)).ToArray(), textured ? package.AsSpan(p + (quad ? 12 : 8), 12).ToArray() : null));
                p += bytes;
            }
        }
        Require(faces.Count > 0, "No drawable model faces.");
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(package));
        return new(Rasterize(vertices, faces, vram, dragonflyEgg), $"Actual source model · first animation, frame 1 · {vertices.Count} vertices · {faces.Count} faces", hash, vertices.Count, faces.Count);
    }

    private static byte[] Rasterize(List<Vertex> vertices, List<Face> faces, byte[] vram, bool dragonflyEgg)
    {
        double yaw = dragonflyEgg ? 1.1 : 0.62, pitch = dragonflyEgg ? 0.45 : 0.18;
        var rotated = vertices.Select(v => { double xx = v.X * Math.Cos(yaw) - v.Y * Math.Sin(yaw), depth = v.X * Math.Sin(yaw) + v.Y * Math.Cos(yaw);
            return new Vertex(xx, v.Z * Math.Cos(pitch) + depth * Math.Sin(pitch), depth * Math.Cos(pitch) - v.Z * Math.Sin(pitch)); }).ToArray();
        double minX = rotated.Min(v => v.X), maxX = rotated.Max(v => v.X), minY = rotated.Min(v => v.Y), maxY = rotated.Max(v => v.Y);
        double scale = (Size - 24) / Math.Max(1, Math.Max(maxX - minX, maxY - minY));
        var projected = rotated.Select(v => new Vertex((v.X - (minX + maxX) / 2) * scale + Size / 2.0, (v.Y - (minY + maxY) / 2) * scale + Size / 2.0, v.Z)).ToArray();
        byte[] pixels = new byte[Size * Size * 4]; double[] zbuffer = Enumerable.Repeat(double.NegativeInfinity, Size * Size).ToArray();
        foreach (var face in faces)
        {
            DrawTriangle(0, 1, 2);
            if (face.Vertices.Length == 4) DrawTriangle(1, 2, 3);
            void DrawTriangle(int a, int b, int c)
            {
                Vertex va = projected[face.Vertices[a]], vb = projected[face.Vertices[b]], vc = projected[face.Vertices[c]];
                double area = Cross(vb.X - va.X, vb.Y - va.Y, vc.X - va.X, vc.Y - va.Y);
                if (Math.Abs(area) < .001) return;
                int x0 = Math.Clamp((int)Math.Floor(Math.Min(va.X, Math.Min(vb.X, vc.X))), 0, Size - 1), x1 = Math.Clamp((int)Math.Ceiling(Math.Max(va.X, Math.Max(vb.X, vc.X))), 0, Size - 1);
                int y0 = Math.Clamp((int)Math.Floor(Math.Min(va.Y, Math.Min(vb.Y, vc.Y))), 0, Size - 1), y1 = Math.Clamp((int)Math.Ceiling(Math.Max(va.Y, Math.Max(vb.Y, vc.Y))), 0, Size - 1);
                for (int yy = y0; yy <= y1; yy++) for (int xx = x0; xx <= x1; xx++)
                {
                    double wb = Cross(xx + .5 - va.X, yy + .5 - va.Y, vc.X - va.X, vc.Y - va.Y) / area,
                        wc = Cross(vb.X - va.X, vb.Y - va.Y, xx + .5 - va.X, yy + .5 - va.Y) / area, wa = 1 - wb - wc;
                    if (wa < 0 || wb < 0 || wc < 0) continue;
                    double depth = va.Z * wa + vb.Z * wb + vc.Z * wc; int index = yy * Size + xx;
                    if (depth < zbuffer[index]) continue;
                    int r = Channel(0), g = Channel(8), bl = Channel(16);
                    if (face.Texture is { } t)
                    {
                        // PS1's 4-bpp/8-bpp/direct texture pages and CLUT are read from this donor's VRAM.
                        double u = t[UvOffset(a)] * wa + t[UvOffset(b)] * wb + t[UvOffset(c)] * wc, v = t[UvOffset(a) + 1] * wa + t[UvOffset(b) + 1] * wb + t[UvOffset(c) + 1] * wc;
                        ushort texel = TexturePixel(vram, t, (int)u, (int)v);
                        if ((texel & 0x7FFF) == 0) continue;
                        r = Math.Min(255, ((texel & 31) * 255 / 31) * r / 128); g = Math.Min(255, ((texel >> 5 & 31) * 255 / 31) * g / 128); bl = Math.Min(255, ((texel >> 10 & 31) * 255 / 31) * bl / 128);
                    }
                    int o = index * 4; pixels[o] = (byte)r; pixels[o + 1] = (byte)g; pixels[o + 2] = (byte)bl; pixels[o + 3] = 255; zbuffer[index] = depth;
                    int UvOffset(int corner) => corner == 3 ? 10 : corner * 4;
                    int Channel(int shift) => Math.Clamp((int)((face.Colors[a] >> shift & 255) * wa + (face.Colors[b] >> shift & 255) * wb + (face.Colors[c] >> shift & 255) * wc), 0, 255);
                }
            }
        }
        Require(pixels.Where((_, i) => i % 4 == 3).Count(a => a != 0) >= 4, "The decoded model has no visible surface in this view.");
        return pixels;
    }
    private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;
    private static ushort TexturePixel(byte[] vram, byte[] texture, int u, int v)
    {
        int page = Half(texture, 6), clut = Half(texture, 2), mode = page >> 7 & 3;
        int pageX = (page & 15) * 64, pageY = (page & 16) * 16;
        int x = pageX + (mode == 0 ? u / 4 : mode == 1 ? u / 2 : u), y = pageY + v;
        ushort value = VramWord(x, y);
        if (mode == 0) return VramWord((clut & 63) * 16 + (value >> ((u & 3) * 4) & 15), clut >> 6);
        if (mode == 1) return VramWord((clut & 63) * 16 + (value >> ((u & 1) * 8) & 255), clut >> 6);
        Require(mode == 2, "Unknown texture page mode."); return value;
        ushort VramWord(int px, int py) { int offset = checked((py * 512 + px - 512) * 2); Require(px is >= 512 and < 1024 && py is >= 0 and < 512 && offset <= vram.Length - 2, "Texture page leaves source VRAM."); return Half(vram, offset); }
    }
    private static uint Word(byte[] bytes, int offset) { Require(offset >= 0 && offset <= bytes.Length - 4, "Model word leaves its source package."); return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)); }
    private static ushort Half(byte[] bytes, int offset) { Require(offset >= 0 && offset <= bytes.Length - 2, "Model halfword leaves its source package."); return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset)); }
    private static byte Byte(byte[] bytes, int offset) { Require(offset >= 0 && offset < bytes.Length, "Model byte leaves its source package."); return bytes[offset]; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
