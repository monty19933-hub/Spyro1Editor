using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Sky;

/// <summary>Reads an FBX model (binary 7.x, or ASCII 7.x) for a sky.</summary>
/// <remarks>Every mesh is placed by its model's transform and its parents' (translation, rotation with its order, scaling,
/// pivots and offsets, pre/post rotation, the geometric transform) and fan-triangulated. Texture coordinates come out with
/// V = 0 at the TOP row of the picture: FBX stores v = 0 at the bottom, so V = 1 - v. Only the first UV set and the first
/// vertex colour set are read. A face's picture is its material's diffuse texture (else its base colour or emissive one):
/// embedded in the file, else found by its relative or full name, else by its bare name next to the file or in the
/// <c>&lt;name&gt;.fbm</c> folder. The material colour is <c>DiffuseColor</c>, else grey. Skinned meshes are read in their
/// rest pose. Y-up files (and 3ds Max's Z-up axes) keep their axes; any other axis system is turned to Y-up, with a note.</remarks>
public static class FbxSkyReader
{
    private const long MaxArrayBytes = 256L * 1024 * 1024;
    private const int MaxDepth = 64;
    private static readonly byte[] BinaryMagic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0");

    /// <summary>Reads <paramref name="fbxPath"/>; pictures are decoded with <paramref name="decoder"/> (each distinct one once,
    /// at most 4096 wide). Throws <see cref="InvalidDataException"/> with a plain-English message for a broken or too-large file.</summary>
    public static SkyModel Read(string fbxPath, SkyImageDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        string path = Path.GetFullPath(fbxPath);
        SkyModelFiles.CheckFileSize(path, "FBX");
        byte[] data = File.ReadAllBytes(path);
        List<Node> roots;
        if (data.Length >= BinaryMagic.Length && data.AsSpan(0, BinaryMagic.Length).SequenceEqual(BinaryMagic))
            roots = new BinaryParser(data).Parse();
        else if (LooksAscii(data))
            roots = new AsciiParser(data).Parse();
        else if (data.Length > 0 && data.Length < BinaryMagic.Length && BinaryMagic.AsSpan(0, data.Length).SequenceEqual(data))
            throw Truncated();
        else
            throw new InvalidDataException("This is not an FBX file.");
        return new Scene(roots, path, decoder).Build();
    }

    private static bool LooksAscii(byte[] data)
    {
        string head = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 4096)).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return head.StartsWith("; FBX", StringComparison.Ordinal) || head.Contains("FBXHeaderExtension", StringComparison.Ordinal);
    }

    private static InvalidDataException Truncated() => new("The FBX file is cut short or damaged (truncated data).");

    private static InvalidDataException Broken(string problem) => new($"The FBX file is damaged: {problem}.");

    // ---------------- the node tree shared by both formats ----------------

    private sealed class Node(string name)
    {
        public string Name { get; } = name;
        public List<object> Props { get; set; } = [];
        public List<Node> Children { get; set; } = [];
        public Node? Child(string name) => Children.Find(c => c.Name == name);
        public object? Prop(int index) => index < Props.Count ? Props[index] : null;
    }

    /// <summary>A binary array property, decoded only when it is used.</summary>
    private sealed class PackedArray(byte[] file, char type, int count, int encoding, int offset, int length)
    {
        private int ElementSize => type switch { 'd' or 'l' => 8, 'f' or 'i' => 4, _ => 1 };

        private byte[] Bytes()
        {
            int size = checked(count * ElementSize);
            if (encoding == 0) return file.AsSpan(offset, size).ToArray();
            byte[] result = new byte[size];
            try
            {
                using ZLibStream zlib = new(new MemoryStream(file, offset, length, writable: false), CompressionMode.Decompress);
                zlib.ReadExactly(result);
            }
            catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException)
            {
                throw new InvalidDataException("A compressed block in the FBX file is damaged (bad zlib data).", e);
            }
            return result;
        }

        public double[] Doubles()
        {
            byte[] b = Bytes();
            return type switch
            {
                'd' => MemoryMarshal.Cast<byte, double>(b).ToArray(),
                'f' => Array.ConvertAll(MemoryMarshal.Cast<byte, float>(b).ToArray(), v => (double)v),
                'i' => Array.ConvertAll(MemoryMarshal.Cast<byte, int>(b).ToArray(), v => (double)v),
                'l' => Array.ConvertAll(MemoryMarshal.Cast<byte, long>(b).ToArray(), v => (double)v),
                _ => Array.ConvertAll(b, v => (double)v),
            };
        }

        public int[] Ints()
        {
            byte[] b = Bytes();
            return type switch
            {
                'i' => MemoryMarshal.Cast<byte, int>(b).ToArray(),
                'l' => Array.ConvertAll(MemoryMarshal.Cast<byte, long>(b).ToArray(), v => v is < int.MinValue or > int.MaxValue ? throw Broken("an index is too large") : (int)v),
                _ => Array.ConvertAll(Doubles(), ToInt),
            };
        }
    }

    private static int ToInt(double v) =>
        double.IsFinite(v) && v >= int.MinValue && v <= int.MaxValue ? (int)v : throw Broken("an index is not a whole number");

    private static double Num(object? value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        short s => s,
        bool b => b ? 1 : 0,
        string t when double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) => d,
        _ => throw Broken("a number is missing"),
    };

    private static long Id(object? value) => value switch
    {
        long l => l,
        int i => i,
        double d when d == Math.Floor(d) && Math.Abs(d) < 9e15 => (long)d,
        string t when long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) => l,
        _ => throw Broken("an object id is missing"),
    };

    private static string Text(object? value) => value switch
    {
        string s => s,
        ArraySegment<byte> b => Encoding.UTF8.GetString(b),
        null => "",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    /// <summary>The numbers a node holds: its one array, or its separate number values.</summary>
    private static double[] Doubles(Node? node)
    {
        if (node == null) return [];
        if (node.Props.Count == 1 && node.Props[0] is PackedArray packed) return packed.Doubles();
        if (node.Props.Count == 1 && node.Props[0] is double[] values) return values;
        return [.. node.Props.Select(Num)];
    }

    private static int[] Ints(Node? node)
    {
        if (node == null) return [];
        if (node.Props.Count == 1 && node.Props[0] is PackedArray packed) return packed.Ints();
        return Array.ConvertAll(node.Props.Count == 1 && node.Props[0] is double[] values ? values : [.. node.Props.Select(Num)], ToInt);
    }

    /// <summary>An object's name without the class ("Name\0\x01Class" in binary files, "Class::Name" in ASCII ones).</summary>
    private static string ObjectName(Node node)
    {
        string name = Text(node.Prop(1));
        int binary = name.IndexOf("\0\u0001", StringComparison.Ordinal);
        if (binary >= 0) return name[..binary];
        int ascii = name.IndexOf("::", StringComparison.Ordinal);
        return ascii >= 0 ? name[(ascii + 2)..] : name;
    }

    // ---------------- binary ----------------

    private sealed class BinaryParser(byte[] d)
    {
        private bool wide;

        public List<Node> Parse()
        {
            if (d.Length < 27) throw Truncated();
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(23));
            CheckVersion(version);
            wide = version >= 7500;
            long pos = 27;
            List<Node> roots = [];
            while (true)
            {
                if (pos >= d.Length) throw Truncated();
                Node? node = ReadNode(ref pos, d.Length, 0);
                if (node == null) return roots;
                roots.Add(node);
            }
        }

        private Node? ReadNode(ref long pos, long limit, int depth)
        {
            if (depth > MaxDepth) throw Broken("its objects are nested too deeply");
            int head = wide ? 25 : 13;
            if (pos + head > limit) throw Truncated();
            int at = (int)pos;
            ulong end, count, listLength;
            if (wide)
            {
                end = BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(at));
                count = BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(at + 8));
                listLength = BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(at + 16));
            }
            else
            {
                end = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at));
                count = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at + 4));
                listLength = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at + 8));
            }
            int nameLength = d[at + head - 1];
            if (end == 0)
            {
                pos += head;
                return null;
            }
            long nameStart = pos + head, propStart = nameStart + nameLength;
            if (end > (ulong)d.Length || listLength > (ulong)d.Length || propStart + (long)listLength > (long)end) throw Truncated();
            if ((long)end > limit || (long)end <= pos || count > listLength) throw Broken("a record's size is wrong");
            long propEnd = propStart + (long)listLength;
            Node node = new(Encoding.ASCII.GetString(d, (int)nameStart, nameLength));
            long p = propStart;
            for (ulong i = 0; i < count; i++) node.Props.Add(ReadProp(ref p, propEnd));
            pos = propEnd;
            while (pos < (long)end)
            {
                Node? child = ReadNode(ref pos, (long)end, depth + 1);
                if (child == null) break;
                node.Children.Add(child);
            }
            pos = (long)end;
            return node;
        }

        private object ReadProp(ref long p, long end)
        {
            void Need(long from, long n) { if (from + n > end) throw Truncated(); }
            Need(p, 1);
            char type = (char)d[p++];
            int at = (int)p;
            switch (type)
            {
                case 'Y': Need(p, 2); p += 2; return BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(at));
                case 'C': Need(p, 1); p += 1; return d[at] != 0;
                case 'I': Need(p, 4); p += 4; return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at));
                case 'F': Need(p, 4); p += 4; return BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(at));
                case 'D': Need(p, 8); p += 8; return BinaryPrimitives.ReadDoubleLittleEndian(d.AsSpan(at));
                case 'L': Need(p, 8); p += 8; return BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(at));
                case 'S':
                case 'R':
                {
                    Need(p, 4);
                    long length = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at));
                    Need(p + 4, length);
                    p += 4 + length;
                    return type == 'S' ? Encoding.UTF8.GetString(d, at + 4, (int)length) : new ArraySegment<byte>(d, at + 4, (int)length);
                }
                case 'f':
                case 'd':
                case 'l':
                case 'i':
                case 'b':
                {
                    Need(p, 12);
                    uint count = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at));
                    uint encoding = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at + 4));
                    long length = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at + 8));
                    Need(p + 12, length);
                    long raw = (long)count * (type is 'd' or 'l' ? 8 : type is 'f' or 'i' ? 4 : 1);
                    if (raw > MaxArrayBytes) throw new InvalidDataException("The FBX file holds a list larger than 256 MB, which is too large for a sky.");
                    if (encoding > 1) throw Broken("a list uses an unknown packing");
                    if (encoding == 0 && length < raw) throw Truncated();
                    p += 12 + length;
                    return new PackedArray(d, type, (int)count, (int)encoding, at + 12, (int)length);
                }
                default:
                    throw Broken($"it has an unknown value type ({(int)type})");
            }
        }
    }

    private static void CheckVersion(long version)
    {
        if (version < 7000)
            throw new InvalidDataException($"This FBX file is version {version / 1000}.{version % 1000 / 100}, which is too old; export it again as FBX 7 (2011 or newer).");
        if (version >= 8000)
            throw new InvalidDataException($"This FBX file is version {version / 1000}.{version % 1000 / 100}, which is newer than this reader knows (7.x).");
    }

    // ---------------- ASCII ----------------

    private sealed class AsciiParser(byte[] data)
    {
        private readonly string s = Encoding.UTF8.GetString(data);
        private int p;

        public List<Node> Parse()
        {
            if (s.Length > 0 && s[0] == '﻿') p = 1;
            List<Node> roots = ParseList(nested: false, depth: 0);
            Node? header = roots.Find(n => n.Name == "FBXHeaderExtension")?.Child("FBXVersion");
            if (header?.Prop(0) is { } version) CheckVersion((long)Num(version));
            return roots;
        }

        private List<Node> ParseList(bool nested, int depth)
        {
            if (depth > MaxDepth) throw Broken("its objects are nested too deeply");
            List<Node> list = [];
            while (true)
            {
                Skip(newlines: true);
                if (p >= s.Length)
                {
                    if (nested) throw Truncated();
                    return list;
                }
                if (s[p] == '}')
                {
                    if (!nested) throw Broken($"an unexpected '}}' on line {Line()}");
                    p++;
                    return list;
                }
                int start = p;
                while (p < s.Length && s[p] != ':' && !char.IsWhiteSpace(s[p]) && s[p] is not ('{' or '}' or ',' or '"')) p++;
                if (p >= s.Length) throw Truncated();
                if (s[p] != ':' || p == start) throw Broken($"a name was expected on line {Line()}");
                Node node = new(s[start..p]);
                p++;
                if (node.Name == "a") ParseNumbers(node); else ParseValues(node, depth);
                list.Add(node);
            }
        }

        private void ParseValues(Node node, int depth)
        {
            bool afterComma = false, isArray = false;
            while (true)
            {
                Skip(newlines: afterComma);
                if (p >= s.Length) return;
                char c = s[p];
                if (c is '\n' or '\r' or '}') return;
                if (c == '{')
                {
                    p++;
                    node.Children = ParseList(nested: true, depth + 1);
                    if (isArray && node.Child("a") is Node a)
                    {
                        node.Props = a.Props;
                        node.Children = [];
                    }
                    return;
                }
                if (c == ',') { p++; afterComma = true; continue; }
                afterComma = false;
                if (c == '"')
                {
                    int close = s.IndexOf('"', p + 1);
                    if (close < 0) throw Truncated();
                    node.Props.Add(s[(p + 1)..close]);
                    p = close + 1;
                }
                else if (c == '*')
                {
                    p++;
                    Token();
                    isArray = true;
                }
                else
                {
                    string token = Token();
                    if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)) node.Props.Add(whole);
                    else if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)) node.Props.Add(real);
                    else node.Props.Add(token);
                }
            }
        }

        /// <summary>An array's <c>a:</c> list: numbers, separated by commas, across lines.</summary>
        private void ParseNumbers(Node node)
        {
            List<double> values = [];
            while (true)
            {
                Skip(newlines: true);
                if (p >= s.Length) throw Truncated();
                if (s[p] == '}') break;
                if (s[p] == ',') { p++; continue; }
                int line = p;
                string token = Token();
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Broken($"“{token}” is not a number (line {Line(line)})");
                values.Add(value);
                if (values.Count > MaxArrayBytes / 8) throw new InvalidDataException("The FBX file holds a list larger than 256 MB, which is too large for a sky.");
            }
            node.Props = [values.ToArray()];
        }

        private string Token()
        {
            int start = p;
            while (p < s.Length && !char.IsWhiteSpace(s[p]) && s[p] is not (',' or '{' or '}' or ';')) p++;
            if (p == start) throw Broken($"a value was expected on line {Line()}");
            return s[start..p];
        }

        private void Skip(bool newlines)
        {
            while (p < s.Length)
            {
                char c = s[p];
                if (c == ';') { while (p < s.Length && s[p] != '\n') p++; continue; }
                if (c is '\n' or '\r' && !newlines) return;
                if (!char.IsWhiteSpace(c)) return;
                p++;
            }
        }

        private int Line(int at = -1)
        {
            int line = 1, end = Math.Min(at < 0 ? p : at, s.Length);
            for (int i = 0; i < end; i++) if (s[i] == '\n') line++;
            return line;
        }
    }

    // ---------------- the scene ----------------

    private sealed class Scene(List<Node> roots, string path, SkyImageDecoder decoder)
    {
        private readonly List<string> notes = [];
        private readonly Dictionary<long, Node> objects = [];
        private readonly List<Node> order = [];
        private readonly Dictionary<long, List<(long Id, string? Property)>> children = [];
        private readonly Dictionary<long, List<long>> parents = [];
        private readonly Dictionary<Node, Dictionary<string, List<object>>> properties = [];
        private readonly Dictionary<long, M4> worlds = [];
        private readonly Dictionary<long, (int Texture, Rgba32 Colour)> looks = [];
        private SkyTextureSet textures = null!;
        private Dictionary<string, ArraySegment<byte>>? embeddedByName;

        public SkyModel Build()
        {
            Node objectsNode = roots.Find(n => n.Name == "Objects") ?? throw Broken("it has no objects");
            foreach (Node node in objectsNode.Children)
            {
                if (node.Props.Count == 0 || node.Prop(0) is string) continue;
                long id = Id(node.Prop(0));
                if (objects.TryAdd(id, node)) order.Add(node);
            }
            foreach (Node c in roots.Find(n => n.Name == "Connections")?.Children ?? [])
            {
                if (c.Name != "C" || c.Props.Count < 3) continue;
                string kind = Text(c.Prop(0));
                if (kind is not ("OO" or "OP")) continue;
                long child = Id(c.Prop(1)), parent = Id(c.Prop(2));
                string? property = kind == "OP" ? Text(c.Prop(3)) : null;
                (children.TryGetValue(parent, out var list) ? list : children[parent] = []).Add((child, property));
                (parents.TryGetValue(child, out var up) ? up : parents[child] = []).Add(parent);
            }

            textures = new SkyTextureSet(decoder, notes);
            var (upAxis, axes) = Axes();
            List<SkyModelTriangle> triangles = [];
            foreach (Node model in order)
            {
                if (model.Name != "Model") continue;
                long modelId = Id(model.Prop(0));
                List<long> meshes = [.. Linked(modelId, "Geometry").Where(g => Text(objects[g].Prop(2)) is "Mesh" or "")];
                if (meshes.Count == 0) continue;
                M4 place = axes * World(modelId, 0) * Geometric(model);
                List<long> materials = Linked(modelId, "Material");
                foreach (long mesh in meshes) AddMesh(objects[mesh], place, materials, triangles);
            }

            if (order.Any(n => n.Name == "Deformer" && Text(n.Prop(2)) == "Skin"))
                SkyTextureSet.Note(notes, "The file has skinned (rigged) meshes; they are used in their rest pose.");
            if (triangles.Count == 0) throw new InvalidDataException("The FBX file has no faces to show as a sky.");
            return new SkyModel(triangles, textures.Images, upAxis, notes);
        }

        /// <summary>The objects of <paramref name="kind"/> connected under <paramref name="parent"/>, in connection order.</summary>
        private List<long> Linked(long parent, string kind) =>
            children.TryGetValue(parent, out var list)
                ? [.. list.Where(c => objects.TryGetValue(c.Id, out Node? n) && n.Name == kind).Select(c => c.Id).Distinct()]
                : [];

        private Dictionary<string, List<object>> Properties(Node node)
        {
            if (properties.TryGetValue(node, out var known)) return known;
            Dictionary<string, List<object>> result = new(StringComparer.Ordinal);
            foreach (Node p in node.Child("Properties70")?.Children ?? [])
                if (p.Name == "P" && p.Props.Count >= 1 && p.Props[0] is string name)
                    result[name] = p.Props.Count > 4 ? p.Props.GetRange(4, p.Props.Count - 4) : [];
            return properties[node] = result;
        }

        private (double X, double Y, double Z) Vector(Node node, string name, double fallback)
        {
            if (Properties(node).TryGetValue(name, out var v) && v.Count >= 3) return (Num(v[0]), Num(v[1]), Num(v[2]));
            return (fallback, fallback, fallback);
        }

        private double Number(Node node, string name, double fallback) =>
            Properties(node).TryGetValue(name, out var v) && v.Count >= 1 ? Num(v[0]) : fallback;

        private (SkyModelUpAxis Up, M4 Axes) Axes()
        {
            Node? settings = roots.Find(n => n.Name == "GlobalSettings");
            if (settings == null) return (SkyModelUpAxis.Y, M4.Identity);
            int Sign(string name) => Number(settings, name, 1) < 0 ? -1 : 1;
            int up = (int)Number(settings, "UpAxis", 1), upSign = Sign("UpAxisSign");
            int front = (int)Number(settings, "FrontAxis", 2), frontSign = Sign("FrontAxisSign");
            int coord = (int)Number(settings, "CoordAxis", 0), coordSign = Sign("CoordAxisSign");
            if (up == 1 && upSign >= 0 && front == 2 && frontSign >= 0 && coord == 0 && coordSign >= 0) return (SkyModelUpAxis.Y, M4.Identity);
            if (up == 2 && upSign >= 0 && front == 1 && frontSign < 0 && coord == 0 && coordSign >= 0) return (SkyModelUpAxis.Z, M4.Identity);
            string[] names = ["X", "Y", "Z"];
            string sign(int s) => s < 0 ? "-" : "+";
            if (up is >= 0 and <= 2 && front is >= 0 and <= 2 && coord is >= 0 and <= 2 && up != front && up != coord && front != coord)
            {
                // Rows: the file's right (coord), up and front directions become X, Y and Z.
                M4 m = M4.Rows(Axis(coord, coordSign), Axis(up, upSign), Axis(front, frontSign));
                SkyTextureSet.Note(notes, $"The FBX file's axes (up {sign(upSign)}{names[up]}, front {sign(frontSign)}{names[front]}, right {sign(coordSign)}{names[coord]}) were turned so Y is up.");
                return (SkyModelUpAxis.Y, m);
            }
            if (up is < 0 or > 2 || (up == 1 && upSign >= 0)) return (SkyModelUpAxis.Y, M4.Identity);
            if (up == 2 && upSign >= 0) return (SkyModelUpAxis.Z, M4.Identity);
            SkyTextureSet.Note(notes, $"The FBX file's up axis is {sign(upSign)}{names[up]}; the model was turned so {(up == 2 ? "Z" : "Y")} is up.");
            return (up, upSign < 0) switch
            {
                (0, false) => (SkyModelUpAxis.Y, M4.Rows((0, -1, 0), (1, 0, 0), (0, 0, 1))),
                (0, true) => (SkyModelUpAxis.Y, M4.Rows((0, 1, 0), (-1, 0, 0), (0, 0, 1))),
                (1, _) => (SkyModelUpAxis.Y, M4.Rows((1, 0, 0), (0, -1, 0), (0, 0, -1))),
                _ => (SkyModelUpAxis.Z, M4.Rows((1, 0, 0), (0, -1, 0), (0, 0, -1))),
            };
        }

        private static (double, double, double) Axis(int axis, int sign) =>
            axis switch { 0 => (sign, 0, 0), 1 => (0, sign, 0), _ => (0, 0, sign) };

        private M4 World(long modelId, int depth)
        {
            if (worlds.TryGetValue(modelId, out M4 known)) return known;
            if (depth > 256) throw Broken("its objects are parented in a loop");
            Node model = objects[modelId];
            long? parent = null;
            foreach (long up in parents.TryGetValue(modelId, out var ups) ? ups : [])
                if (objects.TryGetValue(up, out Node? node) && node.Name == "Model") { parent = up; break; }
            M4 world = parent is long p ? World(p, depth + 1) * Local(model) : Local(model);
            return worlds[modelId] = world;
        }

        /// <summary>FBX's local transform: T * Roff * Rp * Rpre * R * Rpost⁻¹ * Rp⁻¹ * Soff * Sp * S * Sp⁻¹.</summary>
        private M4 Local(Node model)
        {
            bool rotationActive = Number(model, "RotationActive", 0) != 0;
            int rotationOrder = rotationActive ? (int)Number(model, "RotationOrder", 0) : 0;
            var rp = Vector(model, "RotationPivot", 0);
            var sp = Vector(model, "ScalingPivot", 0);
            M4 m = M4.Translate(Vector(model, "Lcl Translation", 0)) * M4.Translate(Vector(model, "RotationOffset", 0)) * M4.Translate(rp);
            if (rotationActive) m *= M4.Euler(Vector(model, "PreRotation", 0), 0);
            m *= M4.Euler(Vector(model, "Lcl Rotation", 0), rotationOrder);
            if (rotationActive) m *= M4.Euler(Vector(model, "PostRotation", 0), 0).Transposed();
            return m * M4.Translate((-rp.X, -rp.Y, -rp.Z)) * M4.Translate(Vector(model, "ScalingOffset", 0)) * M4.Translate(sp) *
                   M4.Scale(Vector(model, "Lcl Scaling", 1)) * M4.Translate((-sp.X, -sp.Y, -sp.Z));
        }

        private M4 Geometric(Node model) =>
            M4.Translate(Vector(model, "GeometricTranslation", 0)) * M4.Euler(Vector(model, "GeometricRotation", 0), 0) *
            M4.Scale(Vector(model, "GeometricScaling", 1));

        private sealed record Layer(string Mapping, int[]? Index, double[] Values, int Stride, string What)
        {
            /// <summary>The entry of <see cref="Values"/> a corner uses, or -1 for none.</summary>
            public int Find(int polygonVertex, int vertex, int polygon)
            {
                int i = Mapping switch
                {
                    "ByPolygonVertex" => polygonVertex,
                    "ByVertice" or "ByVertex" or "ByControlPoint" => vertex,
                    "ByPolygon" => polygon,
                    _ => 0,
                };
                if (Index != null)
                {
                    if (i >= Index.Length) throw OutOfRange(What);
                    i = Index[i];
                    if (i < 0) return -1;
                }
                if ((long)(i + 1) * Stride > Values.Length) throw OutOfRange(What);
                return i;
            }
        }

        private static InvalidDataException OutOfRange(string what) => new($"The FBX file's mesh refers to {what} that does not exist (index out of range).");

        private Layer? ReadLayer(Node geometry, string element, string values, string index, int stride, string what)
        {
            Node? node = geometry.Children.Where(c => c.Name == element).OrderBy(c => c.Props.Count > 0 ? Num(c.Prop(0)) : 0).FirstOrDefault();
            if (node == null) return null;
            string mapping = Text(node.Child("MappingInformationType")?.Prop(0));
            if (mapping is not ("ByPolygonVertex" or "ByVertice" or "ByVertex" or "ByControlPoint" or "ByPolygon" or "AllSame"))
            {
                SkyTextureSet.Note(notes, $"A mesh's {what} use an unsupported layout (“{mapping}”) and were ignored.");
                return null;
            }
            string reference = Text(node.Child("ReferenceInformationType")?.Prop(0));
            Node? indexNode = node.Child(index);
            int[]? indices = reference is "IndexToDirect" or "Index" && indexNode != null ? Ints(indexNode) : null;
            double[] data = Doubles(node.Child(values));
            return data.Length == 0 ? null : new Layer(mapping, indices, data, stride, what);
        }

        private void AddMesh(Node geometry, M4 place, List<long> materials, List<SkyModelTriangle> triangles)
        {
            double[] vertices = Doubles(geometry.Child("Vertices"));
            if (vertices.Length % 3 != 0) throw Broken("a mesh's corner list is incomplete");
            int[] polygonVertices = Ints(geometry.Child("PolygonVertexIndex"));
            int vertexCount = vertices.Length / 3;

            long newTriangles = 0;
            int corners = 0;
            foreach (int raw in polygonVertices)
            {
                int vertex = raw < 0 ? ~raw : raw;
                if (vertex >= vertexCount) throw OutOfRange("a corner");
                corners++;
                if (raw < 0) { if (corners >= 3) newTriangles += corners - 2; corners = 0; }
            }
            if (corners >= 3) newTriangles += corners - 2;
            SkyModelFiles.CheckTriangles(triangles.Count + newTriangles);
            if (newTriangles == 0) return;

            Layer? uv = ReadLayer(geometry, "LayerElementUV", "UV", "UVIndex", 2, "a texture coordinate");
            Layer? colour = ReadLayer(geometry, "LayerElementColor", "Colors", "ColorIndex", 4, "a vertex colour");
            Node? materialNode = geometry.Children.Where(c => c.Name == "LayerElementMaterial").OrderBy(c => c.Props.Count > 0 ? Num(c.Prop(0)) : 0).FirstOrDefault();
            string materialMapping = Text(materialNode?.Child("MappingInformationType")?.Prop(0));
            int[] materialIndices = materialNode == null ? [] : Ints(materialNode.Child("Materials"));

            var placed = new (double X, double Y, double Z)[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                placed[i] = place.Apply(vertices[i * 3], vertices[i * 3 + 1], vertices[i * 3 + 2]);
                if (!double.IsFinite(placed[i].X) || !double.IsFinite(placed[i].Y) || !double.IsFinite(placed[i].Z))
                    throw Broken("a corner's position is not a number");
            }

            List<SkyModelCorner> polygon = [];
            int polygonIndex = 0;
            for (int pv = 0; pv < polygonVertices.Length; pv++)
            {
                int raw = polygonVertices[pv];
                int vertex = raw < 0 ? ~raw : raw;
                double u = 0, v = 0;
                if (uv != null && uv.Find(pv, vertex, polygonIndex) is int t and >= 0) { u = uv.Values[t * 2]; v = uv.Values[t * 2 + 1]; }
                Rgba32? tint = null;
                if (colour != null && colour.Find(pv, vertex, polygonIndex) is int c and >= 0)
                    tint = SkyModelFiles.Color(colour.Values[c * 4], colour.Values[c * 4 + 1], colour.Values[c * 4 + 2], colour.Values[c * 4 + 3]);
                var (x, y, z) = placed[vertex];
                polygon.Add(new SkyModelCorner(x, y, z, u, 1 - v, tint));
                if (raw >= 0 && pv + 1 < polygonVertices.Length) continue;

                if (polygon.Count >= 3)
                {
                    int slot = 0;
                    if (materialMapping == "ByPolygon")
                    {
                        if (polygonIndex >= materialIndices.Length) throw OutOfRange("a material");
                        slot = materialIndices[polygonIndex];
                    }
                    else if (materialIndices.Length > 0) slot = materialIndices[0];
                    var (texture, tone) = slot >= 0 && slot < materials.Count ? Look(materials[slot]) : (-1, SkyModelFiles.Grey);
                    for (int k = 1; k + 1 < polygon.Count; k++) triangles.Add(new(polygon[0], polygon[k], polygon[k + 1], texture, tone));
                }
                polygon.Clear();
                polygonIndex++;
            }
        }

        private (int Texture, Rgba32 Colour) Look(long materialId)
        {
            if (looks.TryGetValue(materialId, out var known)) return known;
            Node material = objects[materialId];
            Rgba32 colour = SkyModelFiles.Grey;
            foreach (string name in (string[])["DiffuseColor", "Diffuse"])
                if (Properties(material).TryGetValue(name, out var v) && v.Count >= 3) { colour = SkyModelFiles.Color(Num(v[0]), Num(v[1]), Num(v[2])); break; }

            long? texture = null;
            int best = int.MaxValue;
            foreach (var (id, property) in children.TryGetValue(materialId, out var list) ? list : [])
            {
                if (!objects.TryGetValue(id, out Node? node) || node.Name is not ("Texture" or "LayeredTexture")) continue;
                int rank = property == null ? 3
                    : property.Contains("Diffuse", StringComparison.OrdinalIgnoreCase) ? 0
                    : property.Contains("base", StringComparison.OrdinalIgnoreCase) && property.Contains("color", StringComparison.OrdinalIgnoreCase) ? 1
                    : property.Contains("Emissive", StringComparison.OrdinalIgnoreCase) ? 2 : int.MaxValue;
                long candidate = id;
                if (node.Name == "LayeredTexture")
                {
                    List<long> layers = Linked(id, "Texture");
                    if (layers.Count == 0) continue;
                    candidate = layers[0];
                }
                if (rank < best) { best = rank; texture = candidate; }
            }
            int index = texture is long t ? Picture(objects[t], ObjectName(material)) : -1;
            return looks[materialId] = (index, colour);
        }

        private int Picture(Node texture, string materialName)
        {
            long textureId = Id(texture.Prop(0));
            List<Node> videos = [.. Linked(textureId, "Video").Select(v => objects[v])];
            List<string?> references = [Text(texture.Child("RelativeFilename")?.Prop(0))];
            references.AddRange(videos.Select(v => Text(v.Child("RelativeFilename")?.Prop(0))));
            references.Add(Text(texture.Child("FileName")?.Prop(0)));
            references.Add(Text(texture.Child("Filename")?.Prop(0)));
            references.AddRange(videos.Select(v => Text(v.Child("Filename")?.Prop(0))));
            references.AddRange(videos.Select(v => Properties(v).TryGetValue("Path", out var p) && p.Count > 0 ? Text(p[0]) : null));
            string display = references.Select(r => BareName(r)).FirstOrDefault(r => r.Length > 0) ?? ObjectName(texture);

            foreach (Node video in videos)
                if (Embedded(video) is { Count: > 0 } bytes)
                    return textures.Get(ContentKey(bytes), display, () => bytes.ToArray());
            foreach (string name in references.Select(BareName).Where(n => n.Length > 0))
                if (EmbeddedByName().TryGetValue(name, out var shared))
                    return textures.Get(ContentKey(shared), display, () => shared.ToArray());

            string folder = Path.GetDirectoryName(path) ?? ".";
            string fbm = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + ".fbm");
            string? file = SkyModelFiles.Find(references, [folder, fbm]);
            if (file == null)
            {
                SkyTextureSet.Note(notes, $"The picture “{display}” (material “{materialName}”) is not inside the FBX file and was not found next to it; those faces use their colours instead.");
                return -1;
            }
            return textures.Get(file, display, () => SkyTextureSet.ReadFile(file));
        }

        private static string ContentKey(ArraySegment<byte> bytes) => "embedded:" + Convert.ToHexString(SHA256.HashData(bytes));

        private static string BareName(string? reference)
        {
            string r = (reference ?? "").Replace('\\', '/').Trim();
            return r[(r.LastIndexOf('/') + 1)..];
        }

        /// <summary>A video's embedded file: raw bytes in binary files, base64 text in ASCII ones.</summary>
        private static ArraySegment<byte>? Embedded(Node video)
        {
            Node? content = video.Child("Content");
            if (content == null) return null;
            if (content.Props.Count == 1 && content.Props[0] is ArraySegment<byte> raw) return raw;
            string base64 = string.Concat(content.Props.OfType<string>());
            if (base64.Length == 0) return null;
            try { return new ArraySegment<byte>(Convert.FromBase64String(base64)); }
            catch (FormatException) { return null; }
        }

        /// <summary>Embedded files by bare name: the FBX SDK embeds a file once even when several videos use it.</summary>
        private Dictionary<string, ArraySegment<byte>> EmbeddedByName()
        {
            if (embeddedByName != null) return embeddedByName;
            embeddedByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (Node video in order.Where(n => n.Name == "Video"))
                if (Embedded(video) is { Count: > 0 } bytes)
                    foreach (string name in new[] { "RelativeFilename", "Filename" }.Select(k => BareName(Text(video.Child(k)?.Prop(0)))).Where(n => n.Length > 0))
                        embeddedByName.TryAdd(name, bytes);
            return embeddedByName;
        }
    }

    /// <summary>A 4x4 transform for column vectors (row-major).</summary>
    private readonly struct M4
    {
        private readonly double[] m;
        private M4(double[] values) => m = values;

        public static M4 Identity => new([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

        public static M4 Translate((double X, double Y, double Z) t) => new([1, 0, 0, t.X, 0, 1, 0, t.Y, 0, 0, 1, t.Z, 0, 0, 0, 1]);

        public static M4 Scale((double X, double Y, double Z) s) => new([s.X, 0, 0, 0, 0, s.Y, 0, 0, 0, 0, s.Z, 0, 0, 0, 0, 1]);

        public static M4 Rows((double, double, double) x, (double, double, double) y, (double, double, double) z) =>
            new([x.Item1, x.Item2, x.Item3, 0, y.Item1, y.Item2, y.Item3, 0, z.Item1, z.Item2, z.Item3, 0, 0, 0, 0, 1]);

        /// <summary>Euler angles in degrees. Order 0 = XYZ (X turned first: Rz * Ry * Rx), 1 XZY, 2 YZX, 3 YXZ, 4 ZXY, 5 ZYX.</summary>
        public static M4 Euler((double X, double Y, double Z) degrees, int order)
        {
            M4 x = Rotate(0, degrees.X), y = Rotate(1, degrees.Y), z = Rotate(2, degrees.Z);
            return order switch
            {
                1 => y * z * x,
                2 => x * z * y,
                3 => z * x * y,
                4 => y * x * z,
                5 => x * y * z,
                _ => z * y * x,
            };
        }

        private static M4 Rotate(int axis, double degrees)
        {
            if (degrees == 0) return Identity;
            double r = degrees * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            return axis switch
            {
                0 => new([1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0, 0, 0, 0, 1]),
                1 => new([c, 0, s, 0, 0, 1, 0, 0, -s, 0, c, 0, 0, 0, 0, 1]),
                _ => new([c, -s, 0, 0, s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]),
            };
        }

        /// <summary>The transpose: the inverse of a pure rotation.</summary>
        public M4 Transposed()
        {
            double[] t = new double[16];
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++) t[c * 4 + r] = m[r * 4 + c];
            return new(t);
        }

        public static M4 operator *(M4 a, M4 b)
        {
            double[] r = new double[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    r[i * 4 + j] = a.m[i * 4] * b.m[j] + a.m[i * 4 + 1] * b.m[4 + j] + a.m[i * 4 + 2] * b.m[8 + j] + a.m[i * 4 + 3] * b.m[12 + j];
            return new(r);
        }

        public (double X, double Y, double Z) Apply(double x, double y, double z) =>
            (m[0] * x + m[1] * y + m[2] * z + m[3], m[4] * x + m[5] * y + m[6] * z + m[7], m[8] * x + m[9] * y + m[10] * z + m[11]);
    }
}
