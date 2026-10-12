using System.Globalization;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Sky;

/// <summary>Reads a Wavefront OBJ model and its MTL material files for a sky.</summary>
/// <remarks>Faces of any size are fan-triangulated. Texture coordinates come out with V = 0 at the TOP row of the picture:
/// OBJ stores v = 0 at the bottom, so V = 1 - v. A face's picture is its material's <c>map_Kd</c> (else <c>map_Ke</c>); its
/// material colour is <c>Kd</c>, else grey; vertex colours (<c>v x y z r g b</c>, 0-1) are kept on the corners. A picture or
/// material file that cannot be found or read becomes a note. The up axis is Y.</remarks>
public static class ObjSkyReader
{
    private sealed class Material(string name, string folder)
    {
        public string Name { get; } = name;
        public string Folder { get; } = folder;
        public Rgba32? Kd { get; set; }
        public string? MapKd { get; set; }
        public string? MapKe { get; set; }
    }

    private readonly record struct Triangle(int V0, int T0, int V1, int T1, int V2, int T2, int Material);

    /// <summary>Reads <paramref name="objPath"/>; pictures are decoded with <paramref name="decoder"/> (each distinct file once,
    /// at most 4096 wide). Throws <see cref="InvalidDataException"/> with a plain-English message for a broken or too-large file.</summary>
    public static SkyModel Read(string objPath, SkyImageDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        string path = Path.GetFullPath(objPath);
        SkyModelFiles.CheckFileSize(path, "model");
        string objFolder = Path.GetDirectoryName(path) ?? ".";

        List<string> notes = [];
        List<(double X, double Y, double Z)> positions = [];
        List<Rgba32?> colours = [];
        List<(double U, double V)> uvs = [];
        List<Triangle> triangles = [];
        List<string> materialNames = [];
        Dictionary<string, int> materialSlots = new(StringComparer.Ordinal);
        Dictionary<string, Material> materials = new(StringComparer.Ordinal);
        HashSet<string> readMtlFiles = new(StringComparer.OrdinalIgnoreCase);
        bool missingMtl = false, shortFaces = false;
        int currentMaterial = -1;
        List<(int V, int T)> corners = [];

        using StreamReader reader = new(path);
        int lineNumber = 0;
        while (NextLine(reader, ref lineNumber) is string raw)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int space = IndexOfSpace(line);
            string keyword = space < 0 ? line : line[..space];
            string rest = space < 0 ? "" : line[(space + 1)..].Trim();
            switch (keyword)
            {
                case "v":
                {
                    double[] n = Numbers(rest, lineNumber, "vertex");
                    if (n.Length < 3) throw Broken(lineNumber, "a vertex needs three numbers");
                    positions.Add((n[0], n[1], n[2]));
                    colours.Add(n.Length >= 6 ? SkyModelFiles.Color(n[3], n[4], n[5]) : null);
                    break;
                }
                case "vt":
                {
                    double[] n = Numbers(rest, lineNumber, "texture coordinate");
                    if (n.Length < 1) throw Broken(lineNumber, "a texture coordinate needs a number");
                    uvs.Add((n[0], n.Length > 1 ? n[1] : 0));
                    break;
                }
                case "f":
                {
                    corners.Clear();
                    foreach (string token in StripComment(rest).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                        corners.Add(Corner(token, positions.Count, uvs.Count, lineNumber));
                    if (corners.Count < 3) { shortFaces = true; break; }
                    SkyModelFiles.CheckTriangles((long)triangles.Count + corners.Count - 2);
                    for (int i = 1; i + 1 < corners.Count; i++)
                        triangles.Add(new(corners[0].V, corners[0].T, corners[i].V, corners[i].T, corners[i + 1].V, corners[i + 1].T, currentMaterial));
                    break;
                }
                case "usemtl":
                    if (rest.Length == 0) { currentMaterial = -1; break; }
                    if (!materialSlots.TryGetValue(rest, out currentMaterial))
                    {
                        currentMaterial = materialNames.Count;
                        materialSlots[rest] = currentMaterial;
                        materialNames.Add(rest);
                    }
                    break;
                case "mtllib":
                    foreach (string file in MtlFiles(rest, objFolder))
                    {
                        string? found = SkyModelFiles.Find([file], [objFolder]);
                        if (found == null)
                        {
                            missingMtl = true;
                            SkyTextureSet.Note(notes, $"The material file “{Path.GetFileName(file.Replace('\\', '/'))}” was not found next to the model; its materials use vertex colours or grey.");
                        }
                        else if (readMtlFiles.Add(found))
                            ReadMtl(found, materials);
                    }
                    break;
            }
        }

        if (shortFaces) SkyTextureSet.Note(notes, "Some faces had fewer than three corners and were skipped.");
        if (triangles.Count == 0) throw new InvalidDataException("The OBJ file has no faces to show as a sky.");

        SkyTextureSet textures = new(decoder, notes);
        List<string> unknown = [];
        var looks = new (int Texture, Rgba32 Colour)[materialNames.Count];
        for (int i = 0; i < materialNames.Count; i++)
        {
            string name = materialNames[i];
            if (!materials.TryGetValue(name, out Material? material))
                material = materials.Values.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            if (material == null) { unknown.Add(name); looks[i] = (-1, SkyModelFiles.Grey); continue; }
            looks[i] = (Picture(material, objFolder, textures, notes), material.Kd ?? SkyModelFiles.Grey);
        }
        if (unknown.Count > 0 && !missingMtl)
            notes.Add($"These materials are not in the material files, so they use vertex colours or grey: {string.Join(", ", unknown.Take(5).Select(n => $"“{n}”"))}{(unknown.Count > 5 ? $" and {unknown.Count - 5} more" : "")}.");

        SkyModelCorner Make(int v, int t)
        {
            if (v >= positions.Count) throw new InvalidDataException($"A face uses vertex {v + 1}, but the file has only {positions.Count} vertices (index out of range).");
            if (t >= uvs.Count) throw new InvalidDataException($"A face uses texture coordinate {t + 1}, but the file has only {uvs.Count} (index out of range).");
            var (x, y, z) = positions[v];
            var (u, vv) = t >= 0 ? uvs[t] : (0, 0);
            return new(x, y, z, u, 1 - vv, colours[v]);
        }

        List<SkyModelTriangle> result = new(triangles.Count);
        foreach (Triangle t in triangles)
        {
            var (texture, colour) = t.Material >= 0 ? looks[t.Material] : (-1, SkyModelFiles.Grey);
            result.Add(new(Make(t.V0, t.T0), Make(t.V1, t.T1), Make(t.V2, t.T2), texture, colour));
        }
        return new SkyModel(result, textures.Images, SkyModelUpAxis.Y, notes);
    }

    private static int Picture(Material material, string objFolder, SkyTextureSet textures, List<string> notes)
    {
        string? reference = material.MapKd ?? material.MapKe;
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        string? file = SkyModelFiles.Find([reference], [material.Folder, objFolder]);
        string display = Path.GetFileName(reference.Replace('\\', '/').Trim('"'));
        if (file == null)
        {
            SkyTextureSet.Note(notes, $"The picture “{display}” (material “{material.Name}”) was not found next to the material file or the model; those faces use their material or vertex colours instead.");
            return -1;
        }
        return textures.Get(file, display, () => SkyTextureSet.ReadFile(file));
    }

    private static void ReadMtl(string path, Dictionary<string, Material> into)
    {
        SkyModelFiles.CheckFileSize(path, "material");
        string folder = Path.GetDirectoryName(path) ?? ".";
        Material? current = null;
        using StreamReader reader = new(path);
        int lineNumber = 0;
        while (NextLine(reader, ref lineNumber) is string raw)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int space = IndexOfSpace(line);
            string keyword = space < 0 ? line : line[..space];
            string rest = space < 0 ? "" : line[(space + 1)..].Trim();
            if (keyword.Equals("newmtl", StringComparison.OrdinalIgnoreCase))
            {
                current = new Material(rest, folder);
                into.TryAdd(rest, current);
            }
            else if (current == null) continue;
            else if (keyword.Equals("Kd", StringComparison.OrdinalIgnoreCase))
            {
                double[] n = TryNumbers(StripComment(rest));
                if (n.Length >= 3) current.Kd = SkyModelFiles.Color(n[0], n[1], n[2]);
                else if (n.Length == 1) current.Kd = SkyModelFiles.Color(n[0], n[0], n[0]);
            }
            else if (keyword.Equals("map_Kd", StringComparison.OrdinalIgnoreCase)) current.MapKd = MapFile(rest);
            else if (keyword.Equals("map_Ke", StringComparison.OrdinalIgnoreCase)) current.MapKe = MapFile(rest);
        }
    }

    /// <summary>The file name of a <c>map_</c> statement: the rest of the line after its options (<c>-s 1 1 1</c> and the
    /// like), spaces included.</summary>
    private static string MapFile(string rest)
    {
        int p = 0;
        while (true)
        {
            int start = SkipSpaces(rest, p);
            if (start >= rest.Length || rest[start] != '-') return Unquote(rest[start..].Trim());
            int end = TokenEnd(rest, start);
            int arguments = rest[start..end].ToLowerInvariant() switch
            {
                "-blendu" or "-blendv" or "-cc" or "-clamp" or "-bm" or "-boost" or "-texres" or "-imfchan" or "-type" => 1,
                "-mm" => 2,
                "-o" or "-s" or "-t" => -3,
                _ => 0,
            };
            if (arguments == 0) return Unquote(rest[start..].Trim());
            p = end;
            for (int i = 0; i < Math.Abs(arguments); i++)
            {
                int a = SkipSpaces(rest, p), b = TokenEnd(rest, a);
                if (a >= rest.Length) break;
                if (arguments < 0 && !double.TryParse(rest.AsSpan(a, b - a), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) break;
                p = b;
            }
        }
    }

    /// <summary>The files an <c>mtllib</c> line names: the whole line when it is one existing file (names may contain spaces),
    /// else each space-separated name.</summary>
    private static IEnumerable<string> MtlFiles(string rest, string objFolder)
    {
        if (rest.Length == 0) return [];
        if (SkyModelFiles.Find([rest], [objFolder]) != null) return [rest];
        return rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    private static (int V, int T) Corner(string token, int vertexCount, int uvCount, int lineNumber)
    {
        int slash = token.IndexOf('/');
        string vPart = slash < 0 ? token : token[..slash];
        string tPart = "";
        if (slash >= 0)
        {
            int second = token.IndexOf('/', slash + 1);
            tPart = second < 0 ? token[(slash + 1)..] : token[(slash + 1)..second];
        }
        int v = Index(vPart, vertexCount, lineNumber, token) ?? throw Broken(lineNumber, $"the face corner “{token}” has no vertex");
        int t = Index(tPart, uvCount, lineNumber, token) ?? -1;
        return (v, t);
    }

    /// <summary>A 0-based index from a 1-based (or negative, counted back from the latest) OBJ index; null for an empty part.</summary>
    private static int? Index(string part, int count, int lineNumber, string token)
    {
        if (part.Length == 0) return null;
        if (!int.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) || value == 0)
            throw Broken(lineNumber, $"“{token}” is not a valid face corner");
        if (value > 0) return value - 1;
        int resolved = count + value;
        if (resolved < 0) throw Broken(lineNumber, $"the face corner “{token}” points before the first vertex (index out of range)");
        return resolved;
    }

    private static string? NextLine(StreamReader reader, ref int lineNumber)
    {
        string? line = reader.ReadLine();
        if (line == null) return null;
        lineNumber++;
        while (line.EndsWith('\\') && reader.ReadLine() is string next)
        {
            lineNumber++;
            line = line[..^1] + " " + next;
        }
        return line;
    }

    private static double[] Numbers(string text, int lineNumber, string what)
    {
        string[] tokens = StripComment(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        double[] values = new double[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]))
                throw Broken(lineNumber, $"the {what} has “{tokens[i]}” where a number belongs");
        return values;
    }

    private static double[] TryNumbers(string text)
    {
        List<double> values = [];
        foreach (string token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) break;
            values.Add(value);
        }
        return [.. values];
    }

    private static string StripComment(string text)
    {
        int hash = text.IndexOf('#');
        return hash < 0 ? text : text[..hash];
    }

    private static string Unquote(string text) => text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;

    private static int IndexOfSpace(string line)
    {
        for (int i = 0; i < line.Length; i++)
            if (char.IsWhiteSpace(line[i])) return i;
        return -1;
    }

    private static int SkipSpaces(string text, int p)
    {
        while (p < text.Length && char.IsWhiteSpace(text[p])) p++;
        return p;
    }

    private static int TokenEnd(string text, int p)
    {
        while (p < text.Length && !char.IsWhiteSpace(text[p])) p++;
        return p;
    }

    private static InvalidDataException Broken(int lineNumber, string problem) => new($"Line {lineNumber} of the OBJ file is broken: {problem}.");
}
