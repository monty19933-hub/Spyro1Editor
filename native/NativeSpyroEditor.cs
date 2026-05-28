using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SpyroNativeEditor
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0 && string.Equals(args[0], "--smoke", StringComparison.OrdinalIgnoreCase))
                {
                    string workspace = FindWorkspace();
                    List<LevelDefinition> levels = SpyroLevelCatalog.Load(workspace);
                    if (levels.Count < 30 || SpyroLevelCatalog.FindByKey(levels, "darkhollow") == null || SpyroLevelCatalog.FindByKey(levels, "peacekeepers") == null)
                        throw new InvalidOperationException("Level catalog did not load from workspace; editor would fall back to the two-level starter list.");
                    string stoneHillGeometry = Path.Combine(workspace, "editor-cache", "stonehill-runtime-scene-editor-overlay.json");
                    if (!File.Exists(stoneHillGeometry))
                        stoneHillGeometry = ResolveWorkspaceFile(workspace, "stonehill-runtime-scene-editor-overlay.json", "generated-research");
                    string stoneHillMobyCache = Path.Combine(workspace, "editor-cache", "stonehill-mobys.json");
                    GeometryCandidate geometry = GeometryLoader.LoadFirstCandidate(stoneHillGeometry);
                    List<Moby> mobys = File.Exists(stoneHillMobyCache)
                        ? MobyLoader.LoadCached(stoneHillMobyCache)
                        : MobyLoader.Load(ResolveStoneHillRamPath(workspace));
                    MobyMetadataLoader.Apply(workspace, mobys);
                    bool hasNamedMoby = false;
                    foreach (Moby moby in mobys)
                    {
                        if (!string.IsNullOrEmpty(moby.Label) && moby.Label.IndexOf("0x", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            hasNamedMoby = true;
                            break;
                        }
                    }
                    Environment.Exit((geometry.Polygons.Count > 0 && geometry.Edges.Count > 0 && mobys.Count > 0 && hasNamedMoby) ? 0 : 2);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    ShowStartupError(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    ShowStartupError(e.ExceptionObject as Exception);
                };
                Application.Run(new EditorForm(FindWorkspace()));
            }
            catch (Exception ex)
            {
                if (args != null && args.Length > 0 && string.Equals(args[0], "--smoke", StringComparison.OrdinalIgnoreCase))
                    Environment.Exit(1);
                ShowStartupError(ex);
            }
        }

        private static void ShowStartupError(Exception ex)
        {
            string message = ex == null ? "Unknown startup error." : ex.ToString();
            try
            {
                File.WriteAllText(Path.Combine(FindWorkspace(), "native-startup-error.txt"), message, Encoding.UTF8);
            }
            catch
            {
            }

            try
            {
                MessageBox.Show(message, "Native Spyro Editor startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        internal static string FindWorkspace()
        {
            string current = Directory.GetCurrentDirectory();
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string parent = Path.GetFullPath(Path.Combine(exeDir, ".."));
            string currentParent = Path.GetFullPath(Path.Combine(current, ".."));
            string grandParent = Path.GetFullPath(Path.Combine(parent, ".."));

            string[] candidates = new string[] { current, exeDir, parent, currentParent, grandParent };
            foreach (string candidate in candidates)
            {
                if (IsWorkspaceCandidate(candidate))
                    return Path.GetFullPath(candidate);
            }

            return current;
        }

        private static bool IsWorkspaceCandidate(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (File.Exists(Path.Combine(path, "spyro-level-catalog.json"))) return true;
            return Directory.Exists(Path.Combine(path, "native")) && Directory.Exists(Path.Combine(path, "tools"));
        }

        internal static string ResolveWorkspaceFile(string workspace, string fileName, params string[] cleanupBuckets)
        {
            string direct = Path.Combine(workspace, fileName);
            if (File.Exists(direct))
                return direct;

            string localRoot = Path.Combine(workspace, "_local");
            if (Directory.Exists(localRoot))
            {
                string[] cleanupDirs = Directory.GetDirectories(localRoot, "cleanup-*");
                Array.Sort(cleanupDirs, StringComparer.OrdinalIgnoreCase);
                string[] buckets = cleanupBuckets != null && cleanupBuckets.Length > 0
                    ? cleanupBuckets
                    : new string[] { "generated-research", "game-and-capture-artifacts" };

                for (int i = cleanupDirs.Length - 1; i >= 0; i--)
                {
                    foreach (string bucket in buckets)
                    {
                        string candidate = Path.Combine(Path.Combine(cleanupDirs[i], bucket), fileName);
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }

            return direct;
        }

        internal static string ResolveStoneHillRamPath(string workspace)
        {
            string[] candidates = new string[]
            {
                "stonehill-before-clean.bin",
                "stonehill-before-gem-clean.bin",
                "duckstation-mainram-fresh-stonehill.bin"
            };

            foreach (string candidate in candidates)
            {
                string path = ResolveWorkspaceFile(workspace, candidate, "game-and-capture-artifacts");
                if (File.Exists(path))
                    return path;
            }

            return Path.Combine(workspace, candidates[0]);
        }
    }

    internal sealed class LevelDefinition
    {
        public string Key;
        public string ScriptKey;
        public string DisplayName;
        public int LevelId;
        public int SourceWadEntry;
        public string SourceTableWadOffset;
        public int SourceRecordCount;
        public string Confidence;
        public string RuntimeMobyPointer;

        public bool ApplyStoneHillMetadata
        {
            get { return string.Equals(Key, "stonehill", StringComparison.OrdinalIgnoreCase); }
        }

        public bool HasSourceTable
        {
            get { return SourceWadEntry >= 0 && SourceRecordCount > 0 && !string.IsNullOrEmpty(SourceTableWadOffset); }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;
        }
    }

    internal static class SpyroLevelCatalog
    {
        private static List<LevelDefinition> defaultLevels;

        public static List<LevelDefinition> Load(string workspace)
        {
            string path = Path.Combine(workspace, "spyro-level-catalog.json");
            if (File.Exists(path))
            {
                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                    object[] entries = root != null && root.ContainsKey("levels") ? root["levels"] as object[] : null;
                    if (entries != null)
                    {
                        List<LevelDefinition> levels = new List<LevelDefinition>();
                        foreach (object obj in entries)
                        {
                            Dictionary<string, object> entry = obj as Dictionary<string, object>;
                            if (entry == null) continue;
                            LevelDefinition level = new LevelDefinition();
                            level.Key = GetString(entry, "key", "");
                            level.ScriptKey = GetString(entry, "scriptKey", level.Key);
                            level.DisplayName = GetString(entry, "displayName", level.Key);
                            level.LevelId = GetInt(entry, "levelId", -1);
                            level.SourceWadEntry = GetInt(entry, "sourceWadEntry", -1);
                            level.SourceTableWadOffset = GetString(entry, "sourceTableWadOffset", "");
                            level.SourceRecordCount = GetInt(entry, "sourceRecordCount", 0);
                            level.Confidence = GetString(entry, "confidence", "");
                            level.RuntimeMobyPointer = GetString(entry, "runtimeMobyPointer", "");
                            if (!string.IsNullOrEmpty(level.Key) && !string.IsNullOrEmpty(level.DisplayName))
                                levels.Add(level);
                        }
                        if (levels.Count > 0)
                            return levels;
                    }
                }
                catch
                {
                }
            }

            return BuiltInFallback();
        }

        public static List<LevelDefinition> LoadDefault()
        {
            if (defaultLevels == null)
                defaultLevels = Load(Program.FindWorkspace());
            return defaultLevels;
        }

        public static LevelDefinition FindByKey(List<LevelDefinition> levels, string key)
        {
            if (levels == null || string.IsNullOrEmpty(key)) return null;
            string normalized = NormalizeKey(key);
            foreach (LevelDefinition level in levels)
            {
                if (level == null) continue;
                if (NormalizeKey(level.Key) == normalized || NormalizeKey(level.ScriptKey) == normalized || NormalizeKey(level.DisplayName) == normalized)
                    return level;
            }
            return null;
        }

        public static int SourceRecordCountForKey(string key)
        {
            LevelDefinition level = FindByKey(LoadDefault(), key);
            return level == null ? 0 : level.SourceRecordCount;
        }

        internal static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder builder = new StringBuilder();
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
            }
            return builder.ToString();
        }

        private static string GetString(Dictionary<string, object> entry, string name, string fallback)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null) return fallback;
            return Convert.ToString(entry[name]);
        }

        private static int GetInt(Dictionary<string, object> entry, string name, int fallback)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null) return fallback;
            try { return Convert.ToInt32(entry[name]); }
            catch { return fallback; }
        }

        private static List<LevelDefinition> BuiltInFallback()
        {
            List<LevelDefinition> levels = new List<LevelDefinition>();
            levels.Add(new LevelDefinition { Key = "artisans", ScriptKey = "Artisans", DisplayName = "Artisans", LevelId = 0x0A, SourceWadEntry = 10, SourceTableWadOffset = "0x9D42AC", SourceRecordCount = 174, Confidence = "fallback" });
            levels.Add(new LevelDefinition { Key = "stonehill", ScriptKey = "StoneHill", DisplayName = "Stone Hill", LevelId = 0x0B, SourceWadEntry = 12, SourceTableWadOffset = "0xD72B38", SourceRecordCount = 195, Confidence = "fallback", RuntimeMobyPointer = "0x80173658" });
            return levels;
        }
    }

    internal sealed class ObjectTemplate
    {
        public string Id;
        public string Family;
        public string DisplayName;
        public string Category;
        public string SourceLevelKey;
        public string SourceLevelSlug;
        public string SourceLevelName;
        public int SourceTrueIndex = -1;
        public string Label;
        public string Kind;
        public int Type;
        public int State;
        public uint SpecialDataPointer;
        public int SourceByte36;
        public int SourceByte37;
        public int SourceByte4F;
        public int Flag4A;
        public int Flag4B;
        public Color Color;
        public bool ShowInAddList = true;
        public bool LoaderTransformed;
        public string AddSupportStatus;
        public string RequiredExporterFeature;
        public string TestedStatus;
        public string TestedResult;
        public string DonorMapConfidence;
        public string DependencyRisk;
        public string Note;

        public string SourceDescription
        {
            get
            {
                string level = string.IsNullOrEmpty(SourceLevelName) ? SourceLevelKey : SourceLevelName;
                return level + " T" + SourceTrueIndex.ToString();
            }
        }
    }

    internal static class ObjectTemplateLoader
    {
        public static List<ObjectTemplate> Load(string workspace, List<LevelDefinition> levels)
        {
            List<ObjectTemplate> result = new List<ObjectTemplate>();
            string path = Path.Combine(workspace, "spyro-object-templates.json");
            if (!File.Exists(path)) return result;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                object[] entries = root != null && root.ContainsKey("templates") ? root["templates"] as object[] : null;
                if (entries == null) return result;

                foreach (object obj in entries)
                {
                    Dictionary<string, object> entry = obj as Dictionary<string, object>;
                    if (entry == null) continue;
                    bool enabled = GetBool(entry, "enabled", true);
                    if (!enabled) continue;

                    ObjectTemplate template = new ObjectTemplate();
                    template.Id = GetString(entry, "id", "");
                    template.Family = GetString(entry, "family", "");
                    template.DisplayName = GetString(entry, "displayName", GetString(entry, "label", ""));
                    template.Category = GetString(entry, "category", "Other");
                    template.SourceLevelKey = GetString(entry, "sourceLevelKey", "");
                    template.SourceLevelSlug = GetString(entry, "sourceLevelSlug", "");
                    template.SourceLevelName = GetString(entry, "sourceLevelName", template.SourceLevelKey);
                    template.SourceTrueIndex = GetFlexibleInt(entry, "sourceTrueIndex", -1);
                    template.Label = GetString(entry, "label", template.DisplayName);
                    template.Kind = GetString(entry, "kind", template.Family);
                    template.Type = GetFlexibleInt(entry, "typeHex", GetFlexibleInt(entry, "typeId", 0));
                    template.State = GetFlexibleInt(entry, "stateHex", 0);
                    template.SpecialDataPointer = (uint)GetFlexibleInt64(entry, "specialDataPointer", 0);
                    template.SourceByte36 = GetFlexibleInt(entry, "sourceByte36Hex", 0);
                    template.SourceByte37 = GetFlexibleInt(entry, "sourceByte37Hex", 0);
                    template.SourceByte4F = GetFlexibleInt(entry, "sourceByte4FHex", 0);
                    template.Flag4A = GetFlexibleInt(entry, "flag4AHex", 0);
                    template.Flag4B = GetFlexibleInt(entry, "flag4BHex", 0);
                    template.Color = ParseColor(GetString(entry, "color", ""), FallbackColor(template.Type));
                    template.ShowInAddList = GetBool(entry, "showInAddList", true);
                    template.LoaderTransformed = GetBool(entry, "loaderTransformed", false);
                    template.AddSupportStatus = GetString(entry, "addSupportStatus", "");
                    template.RequiredExporterFeature = GetString(entry, "requiredExporterFeature", "");
                    template.TestedStatus = GetString(entry, "testedStatus", "");
                    template.TestedResult = GetString(entry, "testedResult", "");
                    template.DonorMapConfidence = GetString(entry, "donorMapConfidence", "");
                    template.DependencyRisk = GetString(entry, "dependencyRisk", "");
                    template.Note = GetString(entry, "note", "");

                    LevelDefinition sourceLevel = SpyroLevelCatalog.FindByKey(levels, template.SourceLevelKey);
                    if (sourceLevel == null || !sourceLevel.HasSourceTable) continue;
                    if (template.SourceTrueIndex < 0 || template.SourceTrueIndex >= sourceLevel.SourceRecordCount) continue;
                    if (string.IsNullOrEmpty(template.SourceLevelSlug)) template.SourceLevelSlug = sourceLevel.Key;
                    if (string.IsNullOrEmpty(template.SourceLevelName)) template.SourceLevelName = sourceLevel.DisplayName;
                    if (string.IsNullOrEmpty(template.SourceLevelKey)) template.SourceLevelKey = sourceLevel.ScriptKey;
                    if (string.IsNullOrEmpty(template.DisplayName)) template.DisplayName = template.Label;
                    if (string.IsNullOrEmpty(template.Label)) template.Label = template.DisplayName;
                    result.Add(template);
                }
            }
            catch
            {
            }

            return result;
        }

        private static string GetString(Dictionary<string, object> entry, string name, string fallback)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null) return fallback;
            return Convert.ToString(entry[name]);
        }

        private static bool GetBool(Dictionary<string, object> entry, string name, bool fallback)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null) return fallback;
            try { return Convert.ToBoolean(entry[name]); }
            catch { return fallback; }
        }

        private static int GetFlexibleInt(Dictionary<string, object> entry, string name, int fallback)
        {
            long value = GetFlexibleInt64(entry, name, fallback);
            if (value < int.MinValue || value > int.MaxValue) return fallback;
            return (int)value;
        }

        private static long GetFlexibleInt64(Dictionary<string, object> entry, string name, long fallback)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null) return fallback;
            object value = entry[name];
            try
            {
                string text = Convert.ToString(value).Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt64(text.Substring(2), 16);
                if (text.Length == 0) return fallback;
                return Convert.ToInt64(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string text = value.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.Length != 6) return fallback;
            try
            {
                int raw = Convert.ToInt32(text, 16);
                return Color.FromArgb((raw >> 16) & 0xFF, (raw >> 8) & 0xFF, raw & 0xFF);
            }
            catch
            {
                return fallback;
            }
        }

        private static Color FallbackColor(int type)
        {
            if (type == 0x20) return Color.FromArgb(244, 212, 77);
            if (type == 0x30) return Color.FromArgb(183, 140, 255);
            if (type == 0x18) return Color.FromArgb(255, 140, 90);
            if (type == 0x00) return Color.FromArgb(143, 166, 184);
            return Color.FromArgb(93, 173, 226);
        }
    }

    internal sealed class EditorForm : Form
    {
        private readonly string workspace;
        private string editPath;
        private string terrainEditPath;
        private string terrainMaterialOverridesPath;
        private string customTerrainTexturesPath;
        private string liveOriginalsPath;
        private string playerEditPath;
        private string currentRamPath;
        private readonly List<LevelDefinition> levelDefinitions;
        private readonly List<ObjectTemplate> objectTemplates;
        private LevelDefinition loadedLevelDefinition;
        private bool suppressLevelSelectionLoad;
        private string currentLevelName = "No level loaded";
        private string currentLevelKey = "";
        private int currentLevelId = -1;
        private bool currentLevelSupportsSourcePatchers;
        private SplitContainer mainSplit;
        private readonly CanvasView canvas;
        private readonly StatusStrip statusStrip;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly ToolStripButton mobyModeButton;
        private readonly ToolStripButton terrainModeButton;
        private readonly ToolStripButton view3DButton;
        private readonly ToolStripMenuItem facesButton;
        private readonly ToolStripMenuItem linesButton;
        private readonly ToolStripMenuItem labelsButton;
        private readonly ToolStripMenuItem terrainStyleButton;
        private readonly ToolStripMenuItem sourceTextureButton;
        private readonly ToolStripMenuItem surfaceColorAssistButton;
        private readonly ToolStripMenuItem completeTerrainDrawButton;
        private readonly ToolStripMenuItem textureFamilyHighlightButton;
        private readonly ToolStripMenuItem sourceTextureRawButton;
        private readonly ToolStripMenuItem textureCornerMappingButton;
        private readonly ToolStripMenuItem textureDebugLabelsButton;
        private readonly ToolStripMenuItem textureAtlasAutoButton;
        private readonly ToolStripMenuItem textureAtlasCloseButton;
        private readonly ToolStripMenuItem textureAtlasStandardButton;
        private readonly ToolStripMenuItem textureWindowWholeButton;
        private readonly ToolStripMenuItem textureWindowDepthButton;
        private readonly ToolStripMenuItem textureWindowWord3Button;
        private readonly ToolStripMenuItem textureWindowWord4MidButton;
        private readonly ToolStripMenuItem textureWindowWord4HighButton;
        private readonly ToolStripMenuItem heightTintButton;
        private readonly ToolStripMenuItem materialTintButton;
        private readonly ToolStripMenuItem contoursButton;
        private readonly ToolStripMenuItem groundCuesButton;
        private readonly ToolStripMenuItem linkedMoveButton;
        private readonly ToolStripMenuItem groundSnapButton;
        private readonly ToolStripMenuItem snapGroundButton;
        private readonly ToolStripMenuItem clickPlaceButton;
        private readonly ToolStripMenuItem dragLockButton;
        private readonly ToolStripComboBox levelSelectBox;
        private readonly ToolStripComboBox nudgeStepBox;
        private readonly ToolStripMenuItem mirrorXButton;
        private readonly ToolStripMenuItem mirrorYButton;
        private readonly Timer settleTimer;
        private ListView mobyList;
        private ComboBox catalogFilterBox;
        private TextBox catalogSearchBox;
        private Label catalogSummaryLabel;
        private Label treasureSummaryLabel;
        private ComboBox selectionGroupBox;
        private Button previousGroupMemberButton;
        private Button nextGroupMemberButton;
        private Label inspectorHeaderLabel;
        private Label selectedTitleLabel;
        private Label identityLabel;
        private Label patchLabel;
        private NumericUpDown xBox;
        private NumericUpDown yBox;
        private NumericUpDown zBox;
        private TextBox notesBox;
        private Button saveEditsButton;
        private Button loadEditsButton;
        private Button resetSelectedButton;
        private Button clearEditsButton;
        private Button setRedGemButton;
        private Button setGreenGemButton;
        private Button setBlueGemButton;
        private Button setYellowGemButton;
        private Button setPurpleGemButton;
        private ComboBox addTemplateBox;
        private ComboBox addSlotBox;
        private Button addObjectButton;
        private Button changeSelectedButton;
        private Button editContentsButton;
        private Button copyMutationSourceButton;
        private Button pasteMutationButton;
        private Button hideSelectedButton;
        private Button objectAddLabButton;
        private Button testSelectedAppendButton;
        private Button liveApplyButton;
        private Button liveRevertButton;
        private Button patchTopRankedButton;
        private Button patchBroadButton;
        private Button combinedPatchButton;
        private Button teaserDemoButton;
        private Button validateSourceButton;
        private Button behaviorDiffButton;
        private Button springChestHelperButton;
        private CheckBox spyroRecolorBox;
        private ComboBox spyroColorBox;
        private Panel spyroColorSwatch;
        private CheckBox crystalDragonRecolorBox;
        private ComboBox crystalDragonColorBox;
        private Panel crystalDragonColorSwatch;
        private Button savePlayerColorsButton;
        private Button resetPlayerColorsButton;
        private Button createPlayerColorPatchButton;
        private Label playerColorStatusLabel;
        private ComboBox skyboxTargetBox;
        private ComboBox skyboxDonorBox;
        private Button swapSkyboxButton;
        private Button buildSkyboxCatalogButton;
        private Label skyboxStatusLabel;
        private ComboBox skyColorPresetBox;
        private TextBox skyColorPaletteBox;
        private Button createSkyColorPatchButton;
        private ComboBox levelTextTargetBox;
        private TextBox levelTextReplacementBox;
        private Button createLevelTextPatchButton;
        private Label levelTextStatusLabel;
        private ComboBox exeStringBox;
        private TextBox exeStringReplacementBox;
        private Button buildExeStringCatalogButton;
        private Button createExeStringPatchButton;
        private Label exeStringStatusLabel;
        private ComboBox terrainDonorLevelBox;
        private ComboBox terrainTextureList;
        private PictureBox terrainTexturePreview;
        private Label terrainTextureSummaryLabel;
        private Button terrainApplyTextureButton;
        private Button terrainReplaceTextureButton;
        private Button terrainRefreshTextureButton;
        private Button terrainImportTextureButton;
        private Button terrainDarkHollowPaletteButton;
        private Button terrainDarkHollowTexturePackButton;

        private GeometryCandidate geometry;
        private readonly List<Moby> mobys = new List<Moby>();
        private readonly List<SelectionGroup> selectionGroups = new List<SelectionGroup>();
        private readonly Dictionary<int, string> terrainMaterialOverrides = new Dictionary<int, string>();
        private readonly Dictionary<int, CustomTerrainTexture> customTerrainTextures = new Dictionary<int, CustomTerrainTexture>();
        private readonly List<TerrainTextureChoice> terrainTextureChoices = new List<TerrainTextureChoice>();
        private float zoom = 0.08f;
        private PointF pan = new PointF(40, 40);
        private bool showFaces = true;
        private bool showLines = true;
        private bool showLabels = true;
        private bool useGameTerrainStyle = true;
        private bool showSourceTextures;
        private bool useSurfaceColorAssist = true;
        private bool completeTerrainDraw = true;
        private bool showTextureFamilyHighlight = true;
        private bool useRawSourceTexture;
        private bool useTextureCornerMapping = true;
        private bool showTextureDebugLabels;
        private bool showHeightTint = true;
        private bool showMaterialIds;
        private bool showContours = true;
        private bool showGroundCues = true;
        private bool view3D;
        private const float Default3DYaw = -0.42f;
        private const float Default3DPitch = 0.82f;
        private const float View3DHeightScale = 0.46f;
        private float view3DYaw = Default3DYaw;
        private float view3DPitch = Default3DPitch;
        private bool mirrorViewX;
        private bool mirrorViewY = true;
        private DateTime fastUntil = DateTime.MinValue;
        private int dragMobyIndex = -1;
        private int selectedMobyIndex = -1;
        private int selectedTerrainIndex = -1;
        private int hoverTerrainIndex = -1;
        private PointF selectedTerrainPoint;
        private PointF hoverTerrainPoint;
        private float hoverTerrainZ;
        private bool hasHoverTerrainZ;
        private EditorMode editorMode = EditorMode.Mobys;
        private PointF dragOffset;
        private bool dragStartedIn3D;
        private float dragPlaneZ;
        private bool panning;
        private bool rotating3D;
        private Point lastMouse;
        private Point rightMouseDownPoint;
        private bool rightContextPending;
        private bool rightDragActive;
        private bool updatingInspector;
        private bool updatingSelection;
        private bool hasUnsavedEdits;
        private int savedEditCount;
        private int savedTerrainEditCount;
        private int activeSelectionGroupIndex = -1;
        private int mutationClipboardMobyIndex = -1;
        private int copiedTerrainTextureId = -1;
        private bool updatingAddObjectChoices;
        private bool updatingGroupSelection;
        private bool updatingPlayerColorControls;
        private bool spyroRecolorEnabled;
        private bool crystalDragonRecolorEnabled;
        private string spyroColorPreset = "Classic Purple";
        private string crystalDragonColorPreset = "Classic Green";
        private Bitmap terrainTextureAtlas;
        private Bitmap terrainTextureLumaAtlas;
        private string terrainTextureAtlasPath = "";
        private int terrainTextureTileSize = 64;
        private string terrainTextureAtlasTier = "";
        private TerrainTextureAtlasPreference terrainTextureAtlasPreference = TerrainTextureAtlasPreference.Auto;
        private SourceTextureWindowMode sourceTextureWindowMode = SourceTextureWindowMode.WholeTile;
        private const int TerrainTextureAtlasColumns = 8;
        private const string CatalogAllObjects = "All objects";
        private const string CatalogEdited = "Edited";
        private const string CatalogNeedsId = "Needs ID";
        private const string CatalogGems = "Gems";
        private const string CatalogChests = "Chests";
        private const string CatalogEnemies = "Enemies/Fodder";
        private const string CatalogDragons = "Dragons";
        private const string CatalogWhirlwinds = "Whirlwinds";
        private const string CatalogScenery = "Scenery";
        private const string CatalogKeys = "Keys";
        private const string CatalogCameras = "Camera";
        private const string CatalogHelpers = "Helpers";
        private const string CatalogPortals = "Audio/Portal";
        private const string CatalogOther = "Other";
        private const int AppendObjectChoiceIndex = -2000000000;
        private const int ExternalTemplateChoiceIndexBase = -1900000000;

        private enum EditorMode
        {
            Mobys,
            Terrain
        }

        private enum TerrainTextureAtlasPreference
        {
            Auto,
            HqClose,
            HqStandard
        }

        private enum SourceTextureWindowMode
        {
            WholeTile,
            FaceDepthLow4,
            Word3High4,
            Word4Mid4,
            Word4High4
        }

        private sealed class LevelTreasureSummary
        {
            public int BaseTotal;
            public int CurrentTotal;
            public int EditedRecords;
        }

        public EditorForm(string workspace)
        {
            this.workspace = workspace;
            levelDefinitions = SpyroLevelCatalog.Load(workspace);
            objectTemplates = ObjectTemplateLoader.Load(workspace, levelDefinitions);
            editPath = "";
            terrainEditPath = "";
            terrainMaterialOverridesPath = "";
            customTerrainTexturesPath = "";
            liveOriginalsPath = "";
            playerEditPath = Path.Combine(workspace, "spyro-player-edits.json");
            currentRamPath = "";
            Text = "Spyro Native Level Editor";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1420;
            Height = 900;
            MinimumSize = new Size(980, 680);

            ToolStrip tools = new ToolStrip();
            tools.GripStyle = ToolStripGripStyle.Hidden;
            levelSelectBox = new ToolStripComboBox();
            levelSelectBox.DropDownStyle = ComboBoxStyle.DropDownList;
            levelSelectBox.AutoSize = false;
            levelSelectBox.Width = 176;
            foreach (LevelDefinition level in levelDefinitions)
                levelSelectBox.Items.Add(level);
            ToolStripButton captureCurrentLevelButton = new ToolStripButton("Capture Current");
            ToolStripButton fitButton = new ToolStripButton("Fit");
            ToolStripMenuItem resetButton = new ToolStripMenuItem("Reset View");
            ToolStripButton resetEditsToolButton = new ToolStripButton("Reset Edits");
            ToolStripDropDownButton viewMenuButton = new ToolStripDropDownButton("View");
            ToolStripDropDownButton terrainMenuButton = new ToolStripDropDownButton("Terrain");
            ToolStripDropDownButton placementMenuButton = new ToolStripDropDownButton("Placement");
            ToolStripMenuItem textureAtlasMenu = new ToolStripMenuItem("Texture Atlas");
            ToolStripMenuItem textureWindowMenu = new ToolStripMenuItem("Texture Window");
            mobyModeButton = new ToolStripButton("Moby Mode") { CheckOnClick = false, Checked = true };
            terrainModeButton = new ToolStripButton("Terrain Mode") { CheckOnClick = false, Checked = false };
            view3DButton = new ToolStripButton("3D View") { CheckOnClick = true, Checked = false };
            facesButton = new ToolStripMenuItem("Faces") { CheckOnClick = true, Checked = true };
            linesButton = new ToolStripMenuItem("Lines") { CheckOnClick = true, Checked = true };
            labelsButton = new ToolStripMenuItem("Labels") { CheckOnClick = true, Checked = true };
            terrainStyleButton = new ToolStripMenuItem("Game Terrain") { CheckOnClick = true, Checked = true };
            sourceTextureButton = new ToolStripMenuItem("Source Textures") { CheckOnClick = true, Checked = false };
            sourceTextureButton.Enabled = false;
            surfaceColorAssistButton = new ToolStripMenuItem("Surface Assist") { CheckOnClick = true, Checked = true };
            completeTerrainDrawButton = new ToolStripMenuItem("Complete Terrain Draw") { CheckOnClick = true, Checked = true };
            textureFamilyHighlightButton = new ToolStripMenuItem("Same Texture Highlight") { CheckOnClick = true, Checked = true };
            sourceTextureRawButton = new ToolStripMenuItem("Raw Atlas Colors") { CheckOnClick = true, Checked = false };
            textureCornerMappingButton = new ToolStripMenuItem("Runtime Texture Corners") { CheckOnClick = true, Checked = true };
            textureDebugLabelsButton = new ToolStripMenuItem("Texture Debug Labels") { CheckOnClick = true, Checked = false };
            textureAtlasAutoButton = new ToolStripMenuItem("Auto: Prefer HQ-close") { Checked = true };
            textureAtlasCloseButton = new ToolStripMenuItem("HQ-close 16-tile");
            textureAtlasStandardButton = new ToolStripMenuItem("HQ 4-tile");
            textureWindowWholeButton = new ToolStripMenuItem("Whole texture tile") { Checked = true };
            textureWindowDepthButton = new ToolStripMenuItem("Window: face depth low 4");
            textureWindowWord3Button = new ToolStripMenuItem("Window: word3 bits 7-10");
            textureWindowWord4MidButton = new ToolStripMenuItem("Window: word4 bits 8-11");
            textureWindowWord4HighButton = new ToolStripMenuItem("Window: word4 bits 12-15");
            heightTintButton = new ToolStripMenuItem("Height Tint") { CheckOnClick = true, Checked = true };
            materialTintButton = new ToolStripMenuItem("Material IDs") { CheckOnClick = true, Checked = false };
            contoursButton = new ToolStripMenuItem("Contours") { CheckOnClick = true, Checked = true };
            groundCuesButton = new ToolStripMenuItem("Ground Cues") { CheckOnClick = true, Checked = true };
            mirrorXButton = new ToolStripMenuItem("Flip X") { CheckOnClick = true, Checked = false };
            mirrorYButton = new ToolStripMenuItem("Flip Y") { CheckOnClick = true, Checked = true };
            linkedMoveButton = new ToolStripMenuItem("Move Linked") { CheckOnClick = true, Checked = true };
            groundSnapButton = new ToolStripMenuItem("Ground Z") { CheckOnClick = true, Checked = true };
            snapGroundButton = new ToolStripMenuItem("Snap Z Now");
            clickPlaceButton = new ToolStripMenuItem("Click Place") { CheckOnClick = true, Checked = false };
            dragLockButton = new ToolStripMenuItem("Drag Lock") { CheckOnClick = true, Checked = false };
            nudgeStepBox = new ToolStripComboBox();
            nudgeStepBox.DropDownStyle = ComboBoxStyle.DropDownList;
            nudgeStepBox.AutoSize = false;
            nudgeStepBox.Width = 64;
            nudgeStepBox.Items.AddRange(new object[] { "4", "16", "64", "128", "256" });
            nudgeStepBox.SelectedIndex = 2;
            viewMenuButton.ToolTipText = "View controls: reset orientation and mirror the map without changing saved coordinates.";
            terrainMenuButton.ToolTipText = "Terrain display controls for faces, lines, labels, materials, height tint, contours, and ground cues.";
            placementMenuButton.ToolTipText = "Placement controls for linked moves, ground snapping, click-place, and drag lock.";
            levelSelectBox.ToolTipText = "Choose a captured Spyro level workbench to load. Selecting a level loads it immediately.";
            captureCurrentLevelButton.ToolTipText = "Capture the level currently running in DuckStation into the editor cache.";
            resetEditsToolButton.ToolTipText = "Reset all active moby and terrain edits back to the original loaded level state.";
            mobyModeButton.ToolTipText = "Edit moby/object positions and object parameters.";
            terrainModeButton.ToolTipText = "Inspect terrain faces and vertices without moving mobys.";
            view3DButton.ToolTipText = "Show an angled height view. Right-drag pans; Shift+right-drag rotates. Object dragging remains top-down only.";
            terrainStyleButton.ToolTipText = "Use a Stone Hill-like terrain palette instead of pure height debug colors.";
            sourceTextureButton.ToolTipText = "Draw WAD-source 64x64 texture tiles on decoded terrain faces when the Stone Hill atlas is available.";
            surfaceColorAssistButton.ToolTipText = "Use manual terrain material labels and level-aware automatic surface hints while keeping the texture/detail data visible.";
            completeTerrainDrawButton.ToolTipText = "Draw every terrain face/edge at zoomed-out views. Disable only if navigation gets slow.";
            textureFamilyHighlightButton.ToolTipText = "When a terrain face is selected, softly highlight every face with the same texture/material ID.";
            sourceTextureRawButton.ToolTipText = "Draw the atlas colors directly, without the face-color lighting pass. Useful for spotting palette/descriptor issues.";
            textureCornerMappingButton.ToolTipText = "Use the decoded runtime texture-corner triangle. Turn off to map the texture directly to the terrain polygon.";
            textureDebugLabelsButton.ToolTipText = "Draw compact face labels with texture id, depth, flip, and current window candidate.";
            textureAtlasMenu.ToolTipText = "Choose which decoded texture descriptor tier the terrain renderer uses.";
            textureWindowMenu.ToolTipText = "Diagnostic guesses for selecting a 32x32 sub-window inside each texture tile.";
            heightTintButton.ToolTipText = "Blend terrain height color into the game-like terrain palette.";
            materialTintButton.ToolTipText = "Color terrain faces by decoded Spyro texture/material id from the runtime scene overlay.";
            contoursButton.ToolTipText = "Draw contour lines across terrain faces so elevation changes are visible.";
            groundCuesButton.ToolTipText = "Draw moby ground-offset rings and selected-object Z offset labels.";
            mirrorXButton.ToolTipText = "Mirror the map view horizontally only. Saved XYZ coordinates are unchanged.";
            mirrorYButton.ToolTipText = "Mirror the map view vertically only. Saved XYZ coordinates are unchanged.";
            linkedMoveButton.ToolTipText = "Move known linked records together. Dragon links only move confirmed dragon and pedestal records.";
            groundSnapButton.ToolTipText = "When moving in X/Y, keep each moby at its original terrain-ground offset.";
            snapGroundButton.ToolTipText = "Snap the selected moby, and any linked records, to the terrain height at their current X/Y.";
            clickPlaceButton.ToolTipText = "Place the selected moby or linked group on the clicked terrain point.";
            dragLockButton.ToolTipText = "Select mobys without starting a drag.";
            nudgeStepBox.ToolTipText = "Arrow-key placement step in world units.";

            levelSelectBox.SelectedIndexChanged += delegate
            {
                if (suppressLevelSelectionLoad) return;
                LevelDefinition requested = levelSelectBox.SelectedItem as LevelDefinition;
                if (requested == null) return;
                LevelDefinition previous = loadedLevelDefinition;
                if (!LoadLevel(requested))
                    SelectLevelInToolbar(previous == null ? "" : previous.Key);
            };
            captureCurrentLevelButton.Click += delegate { RunCurrentLevelCapture(); };
            fitButton.Click += delegate { FitGeometry(); };
            resetButton.Click += delegate { ResetViewOrientation(); FitGeometry(); };
            resetEditsToolButton.Click += delegate { ResetAllEdits(); };
            mobyModeButton.Click += delegate { SetEditorMode(EditorMode.Mobys); };
            terrainModeButton.Click += delegate { SetEditorMode(EditorMode.Terrain); };
            view3DButton.CheckedChanged += delegate { view3D = view3DButton.Checked; if (view3D) Reset3DOrientation(); FitGeometry(); statusLabel.Text = view3D ? "3D view enabled with a softer oblique map angle." : "Top-down view enabled."; };
            facesButton.CheckedChanged += delegate { showFaces = facesButton.Checked; canvas.Invalidate(); };
            linesButton.CheckedChanged += delegate { showLines = linesButton.Checked; canvas.Invalidate(); };
            labelsButton.CheckedChanged += delegate { showLabels = labelsButton.Checked; canvas.Invalidate(); };
            terrainStyleButton.CheckedChanged += delegate { useGameTerrainStyle = terrainStyleButton.Checked; canvas.Invalidate(); };
            sourceTextureButton.CheckedChanged += delegate { showSourceTextures = sourceTextureButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            surfaceColorAssistButton.CheckedChanged += delegate { useSurfaceColorAssist = surfaceColorAssistButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            completeTerrainDrawButton.CheckedChanged += delegate { completeTerrainDraw = completeTerrainDrawButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            textureFamilyHighlightButton.CheckedChanged += delegate { showTextureFamilyHighlight = textureFamilyHighlightButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            sourceTextureRawButton.CheckedChanged += delegate { useRawSourceTexture = sourceTextureRawButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            textureCornerMappingButton.CheckedChanged += delegate { useTextureCornerMapping = textureCornerMappingButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            textureDebugLabelsButton.CheckedChanged += delegate { showTextureDebugLabels = textureDebugLabelsButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            textureAtlasAutoButton.Click += delegate { SetTerrainTextureAtlasPreference(TerrainTextureAtlasPreference.Auto); };
            textureAtlasCloseButton.Click += delegate { SetTerrainTextureAtlasPreference(TerrainTextureAtlasPreference.HqClose); };
            textureAtlasStandardButton.Click += delegate { SetTerrainTextureAtlasPreference(TerrainTextureAtlasPreference.HqStandard); };
            textureWindowWholeButton.Click += delegate { SetSourceTextureWindowMode(SourceTextureWindowMode.WholeTile); };
            textureWindowDepthButton.Click += delegate { SetSourceTextureWindowMode(SourceTextureWindowMode.FaceDepthLow4); };
            textureWindowWord3Button.Click += delegate { SetSourceTextureWindowMode(SourceTextureWindowMode.Word3High4); };
            textureWindowWord4MidButton.Click += delegate { SetSourceTextureWindowMode(SourceTextureWindowMode.Word4Mid4); };
            textureWindowWord4HighButton.Click += delegate { SetSourceTextureWindowMode(SourceTextureWindowMode.Word4High4); };
            heightTintButton.CheckedChanged += delegate { showHeightTint = heightTintButton.Checked; canvas.Invalidate(); };
            materialTintButton.CheckedChanged += delegate { showMaterialIds = materialTintButton.Checked; canvas.Invalidate(); UpdateInspector(); };
            contoursButton.CheckedChanged += delegate { showContours = contoursButton.Checked; canvas.Invalidate(); };
            groundCuesButton.CheckedChanged += delegate { showGroundCues = groundCuesButton.Checked; canvas.Invalidate(); };
            mirrorXButton.CheckedChanged += delegate { mirrorViewX = mirrorXButton.Checked; FitGeometry(); statusLabel.Text = "View mirror changed. XYZ coordinates and saved edits are unchanged."; };
            mirrorYButton.CheckedChanged += delegate { mirrorViewY = mirrorYButton.Checked; FitGeometry(); statusLabel.Text = "View mirror changed. XYZ coordinates and saved edits are unchanged."; };
            linkedMoveButton.CheckedChanged += delegate { UpdateInspector(); canvas.Invalidate(); };
            groundSnapButton.CheckedChanged += delegate { UpdateInspector(); };
            snapGroundButton.Click += delegate { SnapSelectedToGround(); };
            clickPlaceButton.CheckedChanged += delegate
            {
                if (clickPlaceButton.Checked)
                    SetEditorMode(EditorMode.Mobys);
                hoverTerrainIndex = -1;
                hasHoverTerrainZ = false;
                statusLabel.Text = clickPlaceButton.Checked ? "Click-place enabled for the selected moby or linked group." : "Click-place disabled.";
                canvas.Invalidate();
            };
            dragLockButton.CheckedChanged += delegate { statusLabel.Text = dragLockButton.Checked ? "Drag lock enabled." : "Drag lock disabled."; };

            viewMenuButton.DropDownItems.Add(resetButton);
            viewMenuButton.DropDownItems.Add(new ToolStripSeparator());
            viewMenuButton.DropDownItems.Add(mirrorXButton);
            viewMenuButton.DropDownItems.Add(mirrorYButton);

            terrainMenuButton.DropDownItems.Add(facesButton);
            terrainMenuButton.DropDownItems.Add(linesButton);
            terrainMenuButton.DropDownItems.Add(labelsButton);
            terrainMenuButton.DropDownItems.Add(new ToolStripSeparator());
            terrainMenuButton.DropDownItems.Add(terrainStyleButton);
            terrainMenuButton.DropDownItems.Add(sourceTextureButton);
            terrainMenuButton.DropDownItems.Add(surfaceColorAssistButton);
            terrainMenuButton.DropDownItems.Add(completeTerrainDrawButton);
            terrainMenuButton.DropDownItems.Add(textureFamilyHighlightButton);
            textureAtlasMenu.DropDownItems.Add(textureAtlasAutoButton);
            textureAtlasMenu.DropDownItems.Add(textureAtlasCloseButton);
            textureAtlasMenu.DropDownItems.Add(textureAtlasStandardButton);
            terrainMenuButton.DropDownItems.Add(textureAtlasMenu);
            textureWindowMenu.DropDownItems.Add(textureWindowWholeButton);
            textureWindowMenu.DropDownItems.Add(textureWindowDepthButton);
            textureWindowMenu.DropDownItems.Add(textureWindowWord3Button);
            textureWindowMenu.DropDownItems.Add(textureWindowWord4MidButton);
            textureWindowMenu.DropDownItems.Add(textureWindowWord4HighButton);
            terrainMenuButton.DropDownItems.Add(textureWindowMenu);
            terrainMenuButton.DropDownItems.Add(sourceTextureRawButton);
            terrainMenuButton.DropDownItems.Add(textureCornerMappingButton);
            terrainMenuButton.DropDownItems.Add(textureDebugLabelsButton);
            terrainMenuButton.DropDownItems.Add(heightTintButton);
            terrainMenuButton.DropDownItems.Add(materialTintButton);
            terrainMenuButton.DropDownItems.Add(contoursButton);
            terrainMenuButton.DropDownItems.Add(groundCuesButton);

            placementMenuButton.DropDownItems.Add(linkedMoveButton);
            placementMenuButton.DropDownItems.Add(groundSnapButton);
            placementMenuButton.DropDownItems.Add(snapGroundButton);
            placementMenuButton.DropDownItems.Add(clickPlaceButton);
            placementMenuButton.DropDownItems.Add(dragLockButton);

            tools.Items.Add(new ToolStripLabel("Level"));
            tools.Items.Add(levelSelectBox);
            tools.Items.Add(captureCurrentLevelButton);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(fitButton);
            tools.Items.Add(resetEditsToolButton);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(mobyModeButton);
            tools.Items.Add(terrainModeButton);
            tools.Items.Add(view3DButton);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(viewMenuButton);
            tools.Items.Add(terrainMenuButton);
            tools.Items.Add(placementMenuButton);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(new ToolStripLabel("Step"));
            tools.Items.Add(nudgeStepBox);

            canvas = new CanvasView(this);
            canvas.Dock = DockStyle.Fill;
            canvas.BackColor = Color.FromArgb(24, 26, 30);

            mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.FixedPanel = FixedPanel.Panel2;
            mainSplit.SplitterWidth = 6;
            mainSplit.Panel1.Controls.Add(canvas);
            mainSplit.Panel2.Controls.Add(BuildInspectorPanel());

            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Ready");
            statusStrip.Items.Add(statusLabel);
            LoadPlayerColorOptions(false);

            Controls.Add(mainSplit);
            Controls.Add(statusStrip);
            Controls.Add(tools);
            tools.Dock = DockStyle.Top;
            statusStrip.Dock = DockStyle.Bottom;

            settleTimer = new Timer();
            settleTimer.Interval = 260;
            settleTimer.Tick += delegate
            {
                if (DateTime.UtcNow >= fastUntil)
                {
                    settleTimer.Stop();
                    canvas.Invalidate();
                }
            };

            Shown += delegate
            {
                ApplyInitialPanelLayout();
                BeginInvoke(new MethodInvoker(delegate { InitializeNoLevelLoadedState(); }));
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!ConfirmDiscardEdits("close the editor"))
                    e.Cancel = true;
            };
        }

        private void ApplyInitialPanelLayout()
        {
            if (mainSplit == null) return;
            int width = mainSplit.ClientSize.Width;
            if (width < 500) return;

            int desiredInspectorWidth = Math.Min(380, Math.Max(300, width / 3));
            int distance = Math.Max(260, width - desiredInspectorWidth);
            int maxDistance = Math.Max(260, width - 260);
            if (distance > maxDistance) distance = maxDistance;
            if (distance < 260) distance = 260;

            try
            {
                mainSplit.Panel1MinSize = 260;
                mainSplit.Panel2MinSize = 260;
                mainSplit.SplitterDistance = distance;
            }
            catch
            {
                // Some WinForms layouts briefly report tiny sizes during startup.
            }
        }

        private Control BuildInspectorPanel()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8);
            root.BackColor = Color.FromArgb(236, 239, 243);
            root.ColumnCount = 1;
            root.RowCount = 9;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 252f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));

            inspectorHeaderLabel = new Label();
            inspectorHeaderLabel.Text = "Stone Hill Objects";
            inspectorHeaderLabel.Dock = DockStyle.Fill;
            inspectorHeaderLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            inspectorHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(inspectorHeaderLabel, 0, 0);

            TableLayoutPanel catalogTools = new TableLayoutPanel();
            catalogTools.Dock = DockStyle.Fill;
            catalogTools.ColumnCount = 4;
            catalogTools.RowCount = 1;
            catalogTools.Margin = new Padding(0, 0, 0, 4);
            catalogTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128f));
            catalogTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));
            catalogTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            catalogTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62f));

            catalogFilterBox = new ComboBox();
            catalogFilterBox.Dock = DockStyle.Fill;
            catalogFilterBox.DropDownStyle = ComboBoxStyle.DropDownList;
            catalogFilterBox.Items.AddRange(new object[]
            {
                CatalogAllObjects,
                CatalogEdited,
                CatalogNeedsId,
                CatalogGems,
                CatalogChests,
                CatalogEnemies,
                CatalogDragons,
                CatalogWhirlwinds,
                CatalogScenery,
                CatalogKeys,
                CatalogCameras,
                CatalogHelpers,
                CatalogPortals,
                CatalogOther
            });
            catalogFilterBox.SelectedIndex = 0;
            catalogFilterBox.SelectedIndexChanged += delegate { RefreshMobyList(); };
            catalogTools.Controls.Add(catalogFilterBox, 0, 0);

            Label findLabel = new Label();
            findLabel.Dock = DockStyle.Fill;
            findLabel.TextAlign = ContentAlignment.MiddleRight;
            findLabel.Text = "Find";
            catalogTools.Controls.Add(findLabel, 1, 0);

            catalogSearchBox = new TextBox();
            catalogSearchBox.Dock = DockStyle.Fill;
            catalogSearchBox.Margin = new Padding(5, 3, 5, 3);
            catalogSearchBox.AccessibleName = "Find object";
            catalogSearchBox.TextChanged += delegate { RefreshMobyList(); };
            catalogTools.Controls.Add(catalogSearchBox, 2, 0);

            catalogSummaryLabel = new Label();
            catalogSummaryLabel.Dock = DockStyle.Fill;
            catalogSummaryLabel.TextAlign = ContentAlignment.MiddleRight;
            catalogSummaryLabel.AutoEllipsis = true;
            catalogSummaryLabel.Text = "0/0";
            catalogTools.Controls.Add(catalogSummaryLabel, 3, 0);
            root.Controls.Add(catalogTools, 0, 1);

            treasureSummaryLabel = new Label();
            treasureSummaryLabel.Dock = DockStyle.Fill;
            treasureSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            treasureSummaryLabel.AutoEllipsis = true;
            treasureSummaryLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            treasureSummaryLabel.ForeColor = Color.FromArgb(68, 80, 92);
            treasureSummaryLabel.Text = "Treasure: 0";
            root.Controls.Add(treasureSummaryLabel, 0, 2);

            TableLayoutPanel groupTools = new TableLayoutPanel();
            groupTools.Dock = DockStyle.Fill;
            groupTools.ColumnCount = 4;
            groupTools.RowCount = 1;
            groupTools.Margin = new Padding(0, 0, 0, 4);
            groupTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));
            groupTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            groupTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));
            groupTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));

            Label groupLabel = new Label();
            groupLabel.Dock = DockStyle.Fill;
            groupLabel.TextAlign = ContentAlignment.MiddleLeft;
            groupLabel.Text = "Group";
            groupTools.Controls.Add(groupLabel, 0, 0);

            selectionGroupBox = new ComboBox();
            selectionGroupBox.Dock = DockStyle.Fill;
            selectionGroupBox.DropDownStyle = ComboBoxStyle.DropDownList;
            selectionGroupBox.SelectedIndexChanged += delegate
            {
                if (updatingGroupSelection) return;
                activeSelectionGroupIndex = selectionGroupBox.SelectedIndex <= 0 ? -1 : selectionGroupBox.SelectedIndex - 1;
                if (activeSelectionGroupIndex >= 0 && catalogFilterBox != null && catalogFilterBox.SelectedIndex > 0)
                    catalogFilterBox.SelectedIndex = 0;
                RefreshMobyList();
                FocusActiveSelectionGroup();
                UpdateInspector();
                canvas.Invalidate();
            };
            groupTools.Controls.Add(selectionGroupBox, 1, 0);

            previousGroupMemberButton = NewPanelButton("<");
            previousGroupMemberButton.Click += delegate { SelectAdjacentGroupMember(-1); };
            groupTools.Controls.Add(previousGroupMemberButton, 2, 0);

            nextGroupMemberButton = NewPanelButton(">");
            nextGroupMemberButton.Click += delegate { SelectAdjacentGroupMember(1); };
            groupTools.Controls.Add(nextGroupMemberButton, 3, 0);
            root.Controls.Add(groupTools, 0, 3);

            mobyList = new ListView();
            mobyList.Dock = DockStyle.Fill;
            mobyList.View = View.Details;
            mobyList.FullRowSelect = true;
            mobyList.HideSelection = false;
            mobyList.MultiSelect = false;
            mobyList.Columns.Add("ID", 72);
            mobyList.Columns.Add("Icon", 42);
            mobyList.Columns.Add("Category", 84);
            mobyList.Columns.Add("Name", 150);
            mobyList.Columns.Add("Type", 54);
            mobyList.Columns.Add("Work", 80);
            mobyList.SelectedIndexChanged += delegate
            {
                if (updatingSelection || mobyList.SelectedItems.Count == 0) return;
                object tag = mobyList.SelectedItems[0].Tag;
                if (tag is int) SelectMoby((int)tag);
            };
            mobyList.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right) return;
                ListViewItem item = mobyList.GetItemAt(e.X, e.Y);
                if (item == null || !(item.Tag is int)) return;
                int mobyIndex = (int)item.Tag;
                SelectMoby(mobyIndex);
                ShowMobyContextMenu(mobyIndex, mobyList, e.Location);
            };
            mobyList.Resize += delegate { AdjustMobyListColumns(); };
            root.Controls.Add(mobyList, 0, 4);

            selectedTitleLabel = new Label();
            selectedTitleLabel.Dock = DockStyle.Fill;
            selectedTitleLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            selectedTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            selectedTitleLabel.AutoEllipsis = true;
            selectedTitleLabel.Text = "No moby selected";
            root.Controls.Add(selectedTitleLabel, 0, 5);

            TableLayoutPanel details = new TableLayoutPanel();
            details.Dock = DockStyle.Fill;
            details.ColumnCount = 2;
            details.RowCount = 5;
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < details.RowCount; i++)
                details.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));

            identityLabel = NewDetailLabel();
            patchLabel = NewDetailLabel();
            xBox = NewCoordinateBox();
            yBox = NewCoordinateBox();
            zBox = NewCoordinateBox();
            xBox.ValueChanged += delegate { InspectorCoordinateChanged(false); };
            yBox.ValueChanged += delegate { InspectorCoordinateChanged(false); };
            zBox.ValueChanged += delegate { InspectorCoordinateChanged(true); };

            AddDetailRow(details, 0, "Info", identityLabel);
            AddDetailRow(details, 1, "Patch", patchLabel);
            AddDetailRow(details, 2, "X", xBox);
            AddDetailRow(details, 3, "Y", yBox);
            AddDetailRow(details, 4, "Z", zBox);
            root.Controls.Add(details, 0, 6);

            saveEditsButton = NewPanelButton("Save Edits");
            loadEditsButton = NewPanelButton("Load Edits");
            resetSelectedButton = NewPanelButton("Reset Selected");
            clearEditsButton = NewPanelButton("Reset All Edits");
            setRedGemButton = NewPanelButton("Set Red 1");
            setGreenGemButton = NewPanelButton("Set Green 2");
            setBlueGemButton = NewPanelButton("Set Blue 5");
            setYellowGemButton = NewPanelButton("Set Yellow 10");
            setPurpleGemButton = NewPanelButton("Set Purple 25");
            addTemplateBox = NewPanelComboBox();
            addSlotBox = NewPanelComboBox();
            addObjectButton = NewPanelButton("Add New at Click");
            changeSelectedButton = NewPanelButton("Change Selected");
            editContentsButton = NewPanelButton("Edit Contents");
            copyMutationSourceButton = NewPanelButton("Copy Obj");
            pasteMutationButton = NewPanelButton("Clone/Add");
            hideSelectedButton = NewPanelButton("Remove Slot");
            objectAddLabButton = NewPanelButton("Object Add Lab");
            testSelectedAppendButton = NewPanelButton("Test Selected Add BIN");
            liveApplyButton = NewPanelButton("Live Apply");
            liveRevertButton = NewPanelButton("Live Revert");
            patchTopRankedButton = NewPanelButton("Create Loader BIN");
            patchBroadButton = NewPanelButton("Create Terrain BIN");
            combinedPatchButton = NewPanelButton("Create Combined BIN");
            teaserDemoButton = NewPanelButton("Teaser Demo CUE");
            validateSourceButton = NewPanelButton("Validate Source");
            behaviorDiffButton = NewPanelButton("Behavior Diff");
            springChestHelperButton = NewPanelButton("Spring Helper");
            spyroRecolorBox = new CheckBox();
            spyroRecolorBox.Text = "Change Spyro color";
            spyroRecolorBox.Dock = DockStyle.Fill;
            spyroRecolorBox.TextAlign = ContentAlignment.MiddleLeft;
            spyroColorBox = NewPanelComboBox();
            spyroColorBox.Items.AddRange(PlayerColorPresetNames());
            spyroColorSwatch = NewColorSwatchPanel();
            crystalDragonRecolorBox = new CheckBox();
            crystalDragonRecolorBox.Text = "Change crystal dragons";
            crystalDragonRecolorBox.Dock = DockStyle.Fill;
            crystalDragonRecolorBox.TextAlign = ContentAlignment.MiddleLeft;
            crystalDragonColorBox = NewPanelComboBox();
            crystalDragonColorBox.Items.AddRange(CrystalDragonColorPresetNames());
            crystalDragonColorSwatch = NewColorSwatchPanel();
            savePlayerColorsButton = NewPanelButton("Save Choice");
            resetPlayerColorsButton = NewPanelButton("Reset Colors");
            createPlayerColorPatchButton = NewPanelButton("Color BIN (not solved)");
            createPlayerColorPatchButton.Enabled = false;
            playerColorStatusLabel = NewDetailLabel();
            playerColorStatusLabel.Text = "Research only: color choices save, but export is disabled until the real source is mapped.";
            terrainDonorLevelBox = NewPanelComboBox();
            terrainTextureList = NewPanelComboBox();
            terrainTextureList.Dock = DockStyle.Fill;
            terrainTextureList.SelectedIndexChanged += delegate { UpdateTerrainTexturePreview(); };
            terrainTexturePreview = new PictureBox();
            terrainTexturePreview.Dock = DockStyle.Fill;
            terrainTexturePreview.BorderStyle = BorderStyle.FixedSingle;
            terrainTexturePreview.BackColor = Color.White;
            terrainTexturePreview.SizeMode = PictureBoxSizeMode.CenterImage;
            terrainTextureSummaryLabel = NewDetailLabel();
            terrainTextureSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            terrainTextureSummaryLabel.Text = "Choose a terrain texture source.";
            terrainApplyTextureButton = NewPanelButton("Apply to Face");
            terrainReplaceTextureButton = NewPanelButton("Replace Same");
            terrainRefreshTextureButton = NewPanelButton("Refresh");
            terrainImportTextureButton = NewPanelButton("Import PNG");
            terrainDarkHollowPaletteButton = NewPanelButton("Dark Hollow Match");
            terrainDarkHollowTexturePackButton = NewPanelButton("DH Texture Pack");
            skyboxTargetBox = NewPanelComboBox();
            skyboxDonorBox = NewPanelComboBox();
            PopulateSkyboxChoices(skyboxTargetBox);
            PopulateSkyboxChoices(skyboxDonorBox);
            swapSkyboxButton = NewPanelButton("Plan Only");
            buildSkyboxCatalogButton = NewPanelButton("Build Catalog");
            skyboxStatusLabel = NewDetailLabel();
            skyboxStatusLabel.Text = "Plan-only research. Do not boot old skybox CUEs.";
            skyColorPresetBox = NewPanelComboBox();
            PopulateSkyColorPresetChoices(skyColorPresetBox);
            skyColorPaletteBox = new TextBox();
            skyColorPaletteBox.Dock = DockStyle.Fill;
            skyColorPaletteBox.Font = new Font("Consolas", 8.0f);
            skyColorPaletteBox.Text = "#081132 #111D4D #1F316F #314A8C #667CA8 #9DAED0 #CDD5EA";
            createSkyColorPatchButton = NewPanelButton("Create Color CUE");
            levelTextTargetBox = NewPanelComboBox();
            PopulateLevelTextChoices(levelTextTargetBox);
            levelTextReplacementBox = new TextBox();
            levelTextReplacementBox.Dock = DockStyle.Fill;
            levelTextReplacementBox.Font = new Font("Consolas", 8.5f);
            levelTextReplacementBox.CharacterCasing = CharacterCasing.Upper;
            createLevelTextPatchButton = NewPanelButton("Create Text CUE");
            levelTextStatusLabel = NewDetailLabel();
            levelTextStatusLabel.Text = "Fixed-length level name string patch. Good first test for fly-in and portal labels.";
            exeStringBox = NewPanelComboBox();
            exeStringReplacementBox = new TextBox();
            exeStringReplacementBox.Dock = DockStyle.Fill;
            exeStringReplacementBox.Font = new Font("Consolas", 8.5f);
            buildExeStringCatalogButton = NewPanelButton("Build/Refresh Words");
            createExeStringPatchButton = NewPanelButton("Create Word CUE");
            exeStringStatusLabel = NewDetailLabel();
            exeStringStatusLabel.Text = "Build the word catalog to edit other fixed-length executable strings.";
            UpdateLevelTextStatus();
            LoadExeStringChoices(false);
            UpdateExeStringStatus();

            TabControl actionTabs = new TabControl();
            actionTabs.Dock = DockStyle.Fill;
            actionTabs.Margin = new Padding(0, 2, 0, 6);

            TableLayoutPanel mainActions = NewActionPanel(4);
            mainActions.Controls.Add(saveEditsButton, 0, 0);
            mainActions.Controls.Add(loadEditsButton, 1, 0);
            mainActions.Controls.Add(resetSelectedButton, 0, 1);
            mainActions.Controls.Add(clearEditsButton, 1, 1);
            mainActions.Controls.Add(liveApplyButton, 0, 2);
            mainActions.Controls.Add(liveRevertButton, 1, 2);
            mainActions.Controls.Add(patchTopRankedButton, 0, 3);
            mainActions.SetColumnSpan(patchTopRankedButton, 2);

            TableLayoutPanel gemActions = NewActionPanel(3);
            gemActions.Controls.Add(setRedGemButton, 0, 0);
            gemActions.Controls.Add(setGreenGemButton, 1, 0);
            gemActions.Controls.Add(setBlueGemButton, 0, 1);
            gemActions.Controls.Add(setYellowGemButton, 1, 1);
            gemActions.Controls.Add(setPurpleGemButton, 0, 2);
            gemActions.SetColumnSpan(setPurpleGemButton, 2);

            TableLayoutPanel objectActions = NewActionPanel(9);
            AddActionLabel(objectActions, 0, "Template");
            objectActions.Controls.Add(addTemplateBox, 1, 0);
            AddActionLabel(objectActions, 1, "Add As");
            objectActions.Controls.Add(addSlotBox, 1, 1);
            objectActions.Controls.Add(addObjectButton, 0, 2);
            objectActions.SetColumnSpan(addObjectButton, 2);
            objectActions.Controls.Add(changeSelectedButton, 0, 3);
            objectActions.SetColumnSpan(changeSelectedButton, 2);
            objectActions.Controls.Add(editContentsButton, 0, 4);
            objectActions.SetColumnSpan(editContentsButton, 2);
            objectActions.Controls.Add(copyMutationSourceButton, 0, 5);
            objectActions.Controls.Add(pasteMutationButton, 1, 5);
            objectActions.Controls.Add(hideSelectedButton, 0, 6);
            objectActions.SetColumnSpan(hideSelectedButton, 2);
            objectActions.Controls.Add(objectAddLabButton, 0, 7);
            objectActions.SetColumnSpan(objectAddLabButton, 2);
            objectActions.Controls.Add(testSelectedAppendButton, 0, 8);
            objectActions.SetColumnSpan(testSelectedAppendButton, 2);

            TableLayoutPanel toolActions = NewActionPanel(6);
            toolActions.Controls.Add(combinedPatchButton, 0, 0);
            toolActions.SetColumnSpan(combinedPatchButton, 2);
            toolActions.Controls.Add(patchBroadButton, 0, 1);
            toolActions.SetColumnSpan(patchBroadButton, 2);
            toolActions.Controls.Add(validateSourceButton, 0, 2);
            toolActions.SetColumnSpan(validateSourceButton, 2);
            toolActions.Controls.Add(behaviorDiffButton, 0, 3);
            toolActions.SetColumnSpan(behaviorDiffButton, 2);
            toolActions.Controls.Add(springChestHelperButton, 0, 4);
            toolActions.SetColumnSpan(springChestHelperButton, 2);
            toolActions.Controls.Add(teaserDemoButton, 0, 5);
            toolActions.SetColumnSpan(teaserDemoButton, 2);

            TableLayoutPanel colorActions = NewColorActionPanel();
            colorActions.Controls.Add(spyroRecolorBox, 0, 0);
            colorActions.SetColumnSpan(spyroRecolorBox, 2);
            AddActionLabel(colorActions, 1, "Spyro");
            colorActions.Controls.Add(spyroColorBox, 1, 1);
            colorActions.Controls.Add(spyroColorSwatch, 0, 2);
            colorActions.SetColumnSpan(spyroColorSwatch, 2);
            colorActions.Controls.Add(crystalDragonRecolorBox, 0, 3);
            colorActions.SetColumnSpan(crystalDragonRecolorBox, 2);
            AddActionLabel(colorActions, 4, "Crystal");
            colorActions.Controls.Add(crystalDragonColorBox, 1, 4);
            colorActions.Controls.Add(crystalDragonColorSwatch, 0, 5);
            colorActions.SetColumnSpan(crystalDragonColorSwatch, 2);
            colorActions.Controls.Add(savePlayerColorsButton, 0, 6);
            colorActions.Controls.Add(resetPlayerColorsButton, 1, 6);
            colorActions.Controls.Add(createPlayerColorPatchButton, 0, 7);
            colorActions.SetColumnSpan(createPlayerColorPatchButton, 2);
            colorActions.Controls.Add(playerColorStatusLabel, 0, 8);
            colorActions.SetColumnSpan(playerColorStatusLabel, 2);

            TableLayoutPanel terrainActions = NewTerrainActionPanel();
            AddActionLabel(terrainActions, 0, "Source");
            terrainActions.Controls.Add(terrainDonorLevelBox, 1, 0);
            terrainActions.Controls.Add(terrainTexturePreview, 0, 1);
            terrainActions.SetColumnSpan(terrainTexturePreview, 2);
            terrainActions.Controls.Add(terrainTextureList, 0, 2);
            terrainActions.SetColumnSpan(terrainTextureList, 2);
            terrainActions.Controls.Add(terrainApplyTextureButton, 0, 3);
            terrainActions.Controls.Add(terrainReplaceTextureButton, 1, 3);
            terrainActions.Controls.Add(terrainRefreshTextureButton, 0, 4);
            terrainActions.Controls.Add(terrainImportTextureButton, 1, 4);
            terrainActions.Controls.Add(terrainDarkHollowPaletteButton, 0, 5);
            terrainActions.Controls.Add(terrainDarkHollowTexturePackButton, 1, 5);
            terrainActions.Controls.Add(terrainTextureSummaryLabel, 0, 6);
            terrainActions.SetColumnSpan(terrainTextureSummaryLabel, 2);

            TableLayoutPanel textActions = NewActionPanel(8);
            AddActionLabel(textActions, 0, "Target");
            textActions.Controls.Add(levelTextTargetBox, 1, 0);
            AddActionLabel(textActions, 1, "Name");
            textActions.Controls.Add(levelTextReplacementBox, 1, 1);
            textActions.Controls.Add(createLevelTextPatchButton, 0, 2);
            textActions.SetColumnSpan(createLevelTextPatchButton, 2);
            AddActionLabel(textActions, 3, "Word");
            textActions.Controls.Add(exeStringBox, 1, 3);
            AddActionLabel(textActions, 4, "Text");
            textActions.Controls.Add(exeStringReplacementBox, 1, 4);
            textActions.Controls.Add(buildExeStringCatalogButton, 0, 5);
            textActions.Controls.Add(createExeStringPatchButton, 1, 5);
            textActions.Controls.Add(levelTextStatusLabel, 0, 6);
            textActions.SetColumnSpan(levelTextStatusLabel, 2);
            textActions.Controls.Add(exeStringStatusLabel, 0, 7);
            textActions.SetColumnSpan(exeStringStatusLabel, 2);

            TableLayoutPanel skyboxActions = NewActionPanel(7);
            AddActionLabel(skyboxActions, 0, "Target");
            skyboxActions.Controls.Add(skyboxTargetBox, 1, 0);
            AddActionLabel(skyboxActions, 1, "Skybox");
            skyboxActions.Controls.Add(skyboxDonorBox, 1, 1);
            skyboxActions.Controls.Add(swapSkyboxButton, 0, 2);
            skyboxActions.Controls.Add(buildSkyboxCatalogButton, 1, 2);
            AddActionLabel(skyboxActions, 3, "Colors");
            skyboxActions.Controls.Add(skyColorPresetBox, 1, 3);
            skyboxActions.Controls.Add(skyColorPaletteBox, 0, 4);
            skyboxActions.SetColumnSpan(skyColorPaletteBox, 2);
            skyboxActions.Controls.Add(createSkyColorPatchButton, 0, 5);
            skyboxActions.SetColumnSpan(createSkyColorPatchButton, 2);
            skyboxActions.Controls.Add(skyboxStatusLabel, 0, 6);
            skyboxActions.SetColumnSpan(skyboxStatusLabel, 2);
            actionTabs.TabPages.Add(NewActionTab("Main", mainActions));
            actionTabs.TabPages.Add(NewActionTab("Gems", gemActions));
            actionTabs.TabPages.Add(NewActionTab("Objects", objectActions));
            actionTabs.TabPages.Add(NewActionTab("Terrain", terrainActions));
            actionTabs.TabPages.Add(NewActionTab("Colors", colorActions));
            actionTabs.TabPages.Add(NewActionTab("Text", textActions));
            actionTabs.TabPages.Add(NewActionTab("Skybox", skyboxActions));
            actionTabs.TabPages.Add(NewActionTab("Tools", toolActions));

            saveEditsButton.Click += delegate { SaveEdits(); };
            loadEditsButton.Click += delegate { LoadSavedEdits(true); };
            resetSelectedButton.Click += delegate { ResetSelectedMoby(); };
            clearEditsButton.Click += delegate { ResetAllEdits(); };
            setRedGemButton.Click += delegate { SetSelectedGemColor("red"); };
            setGreenGemButton.Click += delegate { SetSelectedGemColor("green"); };
            setBlueGemButton.Click += delegate { SetSelectedGemColor("blue"); };
            setYellowGemButton.Click += delegate { SetSelectedGemColor("yellow"); };
            setPurpleGemButton.Click += delegate { SetSelectedGemColor("purple"); };
            addTemplateBox.SelectedIndexChanged += delegate { if (!updatingAddObjectChoices) UpdateAddObjectButton(); };
            addSlotBox.SelectedIndexChanged += delegate { if (!updatingAddObjectChoices) UpdateAddObjectButton(); };
            addObjectButton.Click += delegate { AddObjectFromTemplateAtClick(); };
            changeSelectedButton.Click += delegate { ChangeSelectedMobyToTemplate(); };
            editContentsButton.Click += delegate { ShowChestContentsEditor(); };
            copyMutationSourceButton.Click += delegate { CopySelectedMutationSource(); };
            pasteMutationButton.Click += delegate { PasteMutationIntoSelected(); };
            hideSelectedButton.Click += delegate { HideSelectedMobySlot(); };
            objectAddLabButton.Click += delegate { RunObjectAddLab(); };
            testSelectedAppendButton.Click += delegate { RunSingleAppendExporter(); };
            liveApplyButton.Click += delegate { if (editorMode == EditorMode.Terrain) RunLiveTerrainMove(false); else RunLiveMobyMove(false); };
            liveRevertButton.Click += delegate { if (editorMode == EditorMode.Terrain) RunLiveTerrainMove(true); else RunLiveMobyMove(true); };
            patchTopRankedButton.Click += delegate { RunPatchExporter(true); };
            patchBroadButton.Click += delegate { RunTerrainPatchExporter(); };
            combinedPatchButton.Click += delegate { RunCombinedPatchExporter(); };
            validateSourceButton.Click += delegate { RunSourceValidation(); };
            behaviorDiffButton.Click += delegate { RunBehaviorDiff(); };
            springChestHelperButton.Click += delegate { RunSpringChestRuntimeHelper(); };
            spyroRecolorBox.CheckedChanged += delegate { PlayerColorControlsChanged(); };
            spyroColorBox.SelectedIndexChanged += delegate { PlayerColorControlsChanged(); };
            crystalDragonRecolorBox.CheckedChanged += delegate { PlayerColorControlsChanged(); };
            crystalDragonColorBox.SelectedIndexChanged += delegate { PlayerColorControlsChanged(); };
            savePlayerColorsButton.Click += delegate { SavePlayerColorOptions(true); };
            resetPlayerColorsButton.Click += delegate { ResetPlayerColorOptions(); };
            createPlayerColorPatchButton.Click += delegate { RunPlayerColorPatchExporter(); };
            skyboxTargetBox.SelectedIndexChanged += delegate { UpdateSkyboxStatus(); };
            skyboxDonorBox.SelectedIndexChanged += delegate { UpdateSkyboxStatus(); };
            skyColorPresetBox.SelectedIndexChanged += delegate { UpdateSkyboxStatus(); };
            levelTextTargetBox.SelectedIndexChanged += delegate
            {
                LevelTextChoice choice = SelectedLevelTextChoice();
                if (choice != null && levelTextReplacementBox != null)
                    levelTextReplacementBox.Text = choice.OriginalName;
                UpdateLevelTextStatus();
            };
            levelTextReplacementBox.TextChanged += delegate { UpdateLevelTextStatus(); };
            exeStringBox.SelectedIndexChanged += delegate
            {
                ExeStringChoice choice = SelectedExeStringChoice();
                if (choice != null && exeStringReplacementBox != null)
                    exeStringReplacementBox.Text = choice.Text;
                UpdateExeStringStatus();
            };
            exeStringReplacementBox.TextChanged += delegate { UpdateExeStringStatus(); };
            swapSkyboxButton.Click += delegate { RunSkyboxPatchExporter(); };
            buildSkyboxCatalogButton.Click += delegate { RunSkyboxCatalogBuilder(); };
            createSkyColorPatchButton.Click += delegate { RunSkyColorPatchExporter(); };
            createLevelTextPatchButton.Click += delegate { RunLevelTextPatchExporter(); };
            buildExeStringCatalogButton.Click += delegate { RunExeStringCatalogBuilder(); };
            createExeStringPatchButton.Click += delegate { RunExeStringPatchExporter(); };
            terrainDonorLevelBox.SelectedIndexChanged += delegate { RefreshTerrainTextureLibrary(); };
            terrainApplyTextureButton.Click += delegate { ApplySelectedTerrainTextureToFace(); };
            terrainReplaceTextureButton.Click += delegate { ReplaceSelectedTerrainTextureFamily(); };
            terrainRefreshTextureButton.Click += delegate { RefreshTerrainTextureLibrary(); };
            terrainImportTextureButton.Click += delegate { ImportCustomTerrainTexture(); };
            terrainDarkHollowPaletteButton.Click += delegate { ApplyDarkHollowTerrainPaletteMatch(); };
            terrainDarkHollowTexturePackButton.Click += delegate { CreateDarkHollowTerrainTexturePack(); };
            teaserDemoButton.Click += delegate { RunTeaserDemoExporter(); };
            root.Controls.Add(actionTabs, 0, 7);

            notesBox = new TextBox();
            notesBox.Dock = DockStyle.Fill;
            notesBox.Multiline = true;
            notesBox.ReadOnly = true;
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.BackColor = Color.White;
            notesBox.Font = new Font("Consolas", 8.5f);
            notesBox.Text = "Select a moby to inspect its decoded identity and edit status.";
            root.Controls.Add(notesBox, 0, 8);

            return root;
        }

        private static TableLayoutPanel NewActionPanel(int rows)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(4, 5, 4, 4);
            panel.ColumnCount = 2;
            panel.RowCount = rows;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < rows; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            return panel;
        }

        private static TableLayoutPanel NewColorActionPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            panel.Padding = new Padding(4, 8, 4, 4);
            panel.ColumnCount = 2;
            panel.RowCount = 9;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            return panel;
        }

        private static TableLayoutPanel NewTerrainActionPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(4, 5, 4, 4);
            panel.ColumnCount = 2;
            panel.RowCount = 7;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            return panel;
        }

        private static void AddActionLabel(TableLayoutPanel panel, int row, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            panel.Controls.Add(label, 0, row);
        }

        private static TabPage NewActionTab(string title, Control content)
        {
            TabPage page = new TabPage(title);
            page.Padding = new Padding(0);
            page.BackColor = Color.FromArgb(236, 239, 243);
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            return page;
        }

        private static ComboBox NewPanelComboBox()
        {
            ComboBox box = new ComboBox();
            box.Dock = DockStyle.Fill;
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.IntegralHeight = false;
            box.MaxDropDownItems = 14;
            box.Margin = new Padding(3);
            box.DropDown += delegate { UpdateComboBoxDropDownWidth(box, 420); };
            return box;
        }

        private static void UpdateComboBoxDropDownWidth(ComboBox box, int minimumWidth)
        {
            if (box == null) return;
            int width = Math.Max(minimumWidth, box.Width);
            for (int i = 0; i < box.Items.Count; i++)
            {
                string text = Convert.ToString(box.Items[i]);
                if (string.IsNullOrEmpty(text)) continue;
                int measured = TextRenderer.MeasureText(text, box.Font).Width + SystemInformation.VerticalScrollBarWidth + 32;
                if (measured > width) width = measured;
            }
            box.DropDownWidth = Math.Min(width, 1100);
        }

        private static Label NewDetailLabel()
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            return label;
        }

        private static Button NewPanelButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(3);
            return button;
        }

        private static Panel NewColorSwatchPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(5, 3, 5, 3);
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.BackColor = Color.MediumPurple;
            return panel;
        }

        private static object[] PlayerColorPresetNames()
        {
            return new object[]
            {
                "Classic Purple",
                "Red",
                "Blue",
                "Green",
                "Gold",
                "Pink",
                "Teal",
                "White",
                "Black"
            };
        }

        private static object[] CrystalDragonColorPresetNames()
        {
            return new object[]
            {
                "Classic Green",
                "Blue",
                "Purple",
                "Red",
                "Gold",
                "Pink",
                "White",
                "Teal"
            };
        }

        private static Color PlayerPresetColor(string preset)
        {
            switch (preset)
            {
                case "Red": return Color.FromArgb(198, 54, 64);
                case "Blue": return Color.FromArgb(65, 105, 210);
                case "Green": return Color.FromArgb(58, 166, 92);
                case "Gold": return Color.FromArgb(229, 177, 54);
                case "Pink": return Color.FromArgb(222, 92, 182);
                case "Teal": return Color.FromArgb(52, 180, 178);
                case "White": return Color.FromArgb(232, 232, 222);
                case "Black": return Color.FromArgb(46, 43, 57);
                default: return Color.FromArgb(114, 74, 178);
            }
        }

        private static Color CrystalDragonPresetColor(string preset)
        {
            switch (preset)
            {
                case "Blue": return Color.FromArgb(80, 185, 234);
                case "Purple": return Color.FromArgb(160, 100, 225);
                case "Red": return Color.FromArgb(230, 75, 84);
                case "Gold": return Color.FromArgb(236, 202, 80);
                case "Pink": return Color.FromArgb(236, 126, 198);
                case "White": return Color.FromArgb(220, 245, 240);
                case "Teal": return Color.FromArgb(70, 220, 190);
                default: return Color.FromArgb(84, 228, 142);
            }
        }

        private static string ColorToHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        private static NumericUpDown NewCoordinateBox()
        {
            NumericUpDown box = new NumericUpDown();
            box.Dock = DockStyle.Fill;
            box.DecimalPlaces = 2;
            box.Increment = 16m;
            box.Minimum = -1000000m;
            box.Maximum = 1000000m;
            box.ThousandsSeparator = true;
            return box;
        }

        private static void AddDetailRow(TableLayoutPanel panel, int row, string labelText, Control editor)
        {
            Label label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(editor, 1, row);
        }

        private bool FastMode
        {
            get { return DateTime.UtcNow < fastUntil; }
        }

        private void StartFastMode(int milliseconds)
        {
            fastUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
            settleTimer.Stop();
            settleTimer.Interval = Math.Max(80, milliseconds + 45);
            settleTimer.Start();
        }

        private void InitializeNoLevelLoadedState()
        {
            loadedLevelDefinition = null;
            currentLevelName = "No level loaded";
            currentLevelKey = "";
            currentLevelId = -1;
            currentLevelSupportsSourcePatchers = false;
            editPath = "";
            terrainEditPath = "";
            terrainMaterialOverridesPath = "";
            customTerrainTexturesPath = "";
            liveOriginalsPath = "";
            currentRamPath = "";
            geometry = null;
            mobys.Clear();
            selectionGroups.Clear();
            selectedMobyIndex = -1;
            selectedTerrainIndex = -1;
            hoverTerrainIndex = -1;
            hasHoverTerrainZ = false;
            hasUnsavedEdits = false;
            savedEditCount = 0;
            savedTerrainEditCount = 0;
            DisposeTerrainTextureAtlas();
            DisposeCustomTerrainTextures();
            SelectLevelInToolbar("");
            if (inspectorHeaderLabel != null)
                inspectorHeaderLabel.Text = "Choose a Level";
            Text = "Spyro Native Level Editor";
            UpdateLevelActionButtons();
            RefreshMobyList();
            RefreshTerrainTextureLibrary();
            UpdateInspector();
            if (statusLabel != null)
                statusLabel.Text = "Choose a level from the dropdown. Use Capture Current while standing in Dark Hollow to create its editor cache.";
            if (canvas != null)
                canvas.Invalidate();
        }

        private bool HasLoadedLevel()
        {
            return loadedLevelDefinition != null && !string.IsNullOrEmpty(currentLevelKey) && geometry != null;
        }

        private bool LoadLevelByKey(string levelKey)
        {
            LevelDefinition level = FindLevelDefinition(levelKey);
            if (level == null)
            {
                MessageBox.Show(this, "Level is not in spyro-level-catalog.json: " + levelKey, "Level not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return LoadLevel(level);
        }

        private LevelDefinition FindLevelDefinition(string levelKey)
        {
            return SpyroLevelCatalog.FindByKey(levelDefinitions, levelKey);
        }

        private void SelectLevelInToolbar(string levelKey)
        {
            if (levelSelectBox == null) return;
            LevelDefinition level = FindLevelDefinition(levelKey);
            suppressLevelSelectionLoad = true;
            try
            {
                if (level != null)
                {
                    if (!object.ReferenceEquals(levelSelectBox.SelectedItem, level))
                        levelSelectBox.SelectedItem = level;
                }
                else if (levelSelectBox.SelectedIndex >= 0)
                {
                    levelSelectBox.SelectedIndex = -1;
                }
            }
            finally
            {
                suppressLevelSelectionLoad = false;
            }
        }

        private void SelectTerrainDonorLevel(string levelKey)
        {
            if (terrainDonorLevelBox == null || string.IsNullOrEmpty(levelKey)) return;
            if (terrainDonorLevelBox.Items.Count == 0)
            {
                foreach (LevelDefinition level in levelDefinitions)
                {
                    string overlayPath = GetLevelGeometryPath(level.Key);
                    if (File.Exists(overlayPath))
                        terrainDonorLevelBox.Items.Add(level);
                }
            }

            foreach (object item in terrainDonorLevelBox.Items)
            {
                LevelDefinition level = item as LevelDefinition;
                if (level != null && string.Equals(level.Key, levelKey, StringComparison.OrdinalIgnoreCase))
                {
                    terrainDonorLevelBox.SelectedItem = level;
                    return;
                }
            }
        }

        private void RunCurrentLevelCapture()
        {
            string scriptPath = Path.Combine(workspace, "tools", "Capture-SpyroCurrentLevelWorkbench.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing current-level capture script: " + scriptPath, "Capture script missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started current DuckStation level capture. Select the captured level from the dropdown when it finishes.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start current-level capture", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool LoadLevel(LevelDefinition level)
        {
            try
            {
                string levelName = level.DisplayName;
                string levelKey = level.Key;
                if (!ConfirmDiscardEdits("reload " + levelName))
                    return false;

                string geometryPath = GetLevelGeometryPath(levelKey);
                string mobyCachePath = GetLevelMobyCachePath(levelKey);
                string ramPath = GetLevelRamPath(levelKey);
                bool hasMobyCache = File.Exists(mobyCachePath);
                if (!File.Exists(geometryPath))
                    throw new FileNotFoundException(MissingLevelAssetMessage(levelName, "geometry overlay"), geometryPath);
                if (!hasMobyCache && !File.Exists(ramPath))
                    throw new FileNotFoundException(MissingLevelAssetMessage(levelName, "RAM dump"), ramPath);

                currentLevelName = levelName;
                currentLevelKey = levelKey;
                currentLevelId = level.LevelId;
                currentLevelSupportsSourcePatchers = level.HasSourceTable;
                loadedLevelDefinition = level;
                mutationClipboardMobyIndex = -1;
                editPath = Path.Combine(workspace, levelKey + "-native-edits.json");
                terrainEditPath = Path.Combine(workspace, levelKey + "-terrain-edits.json");
                terrainMaterialOverridesPath = Path.Combine(workspace, levelKey + "-terrain-material-overrides.json");
                customTerrainTexturesPath = Path.Combine(workspace, levelKey + "-custom-terrain-textures.json");
                liveOriginalsPath = Path.Combine(workspace, levelKey + "-live-moby-originals.json");
                currentRamPath = File.Exists(ramPath) ? ramPath : "";
                Text = "Spyro Native Level Editor - " + levelName;
                if (inspectorHeaderLabel != null)
                    inspectorHeaderLabel.Text = levelName + " Objects";
                SelectLevelInToolbar(levelKey);
                SelectSkyboxTargetForLevel(levelKey);
                UpdateLevelActionButtons();

                Cursor = Cursors.WaitCursor;
                statusLabel.Text = "Loading " + levelName + " geometry...";
                statusStrip.Refresh();
                geometry = GeometryLoader.LoadFirstCandidate(geometryPath);
                int materialOverrideCount = LoadTerrainMaterialOverrides();
                LoadTerrainTextureAtlas();
                LoadCustomTerrainTextures();
                mobys.Clear();
                if (hasMobyCache)
                    mobys.AddRange(MobyLoader.LoadCached(mobyCachePath));
                else
                    mobys.AddRange(MobyLoader.Load(ramPath));
                int namedMobys = MobyMetadataLoader.Apply(workspace, mobys, levelKey, level.ApplyStoneHillMetadata);
                if (level.HasSourceTable)
                    ApplyLevelSourcePatchStatus(levelKey, levelName);
                else
                    ApplyRuntimeOnlyPatchStatus(levelName);
                InitializeGroundOffsets();
                foreach (Moby moby in mobys)
                    moby.CaptureBaseIdentity();
                selectedTerrainIndex = -1;
                hoverTerrainIndex = -1;
                hasHoverTerrainZ = false;
                savedEditCount = LoadSavedEdits(false);
                SelectTerrainDonorLevel(levelKey);
                RefreshTerrainTextureLibrary();
                BuildSelectionGroups();
                hasUnsavedEdits = false;
                RefreshMobyList();
                SelectMoby(mobys.Count > 0 ? 0 : -1);
                UpdateLevelActionButtons();
                FitGeometry();
                statusLabel.Text = string.Format(
                    "Loaded {0}{10}: {1} faces, {2} lines, {3} mobys, {4} named, {5} moby edit(s), {6} terrain edit(s), {7} color option(s), {8} material labels. {9}.",
                    levelName,
                    geometry.Polygons.Count,
                    geometry.Edges.Count,
                    mobys.Count,
                    namedMobys,
                    savedEditCount,
                    savedTerrainEditCount,
                    CountActivePlayerColorOptions(),
                    materialOverrideCount,
                    TreasureSummaryText(CalculateTreasureSummary()),
                    hasMobyCache ? " from portable cache" : "");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Load level failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Load failed";
                return false;
            }
            finally
            {
                Cursor = Cursors.Default;
                canvas.Invalidate();
            }
        }

        private string GetLevelGeometryPath(string levelKey)
        {
            string cached = Path.Combine(workspace, "editor-cache", levelKey + "-runtime-scene-editor-overlay.json");
            if (File.Exists(cached))
                return cached;
            return Program.ResolveWorkspaceFile(workspace, levelKey + "-runtime-scene-editor-overlay.json", "generated-research");
        }

        private string GetLevelMobyCachePath(string levelKey)
        {
            return Path.Combine(workspace, "editor-cache", levelKey + "-mobys.json");
        }

        private string GetLevelRamPath(string levelKey)
        {
            if (string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
                return Program.ResolveStoneHillRamPath(workspace);
            return Program.ResolveWorkspaceFile(workspace, levelKey + "-before-clean.bin", "game-and-capture-artifacts");
        }

        private static string MissingLevelAssetMessage(string levelName, string assetName)
        {
            if (string.Equals(levelName, "Artisans", StringComparison.OrdinalIgnoreCase))
                return "Missing Artisans " + assetName + ". Stand in Artisans in DuckStation and run Capture Current Level Workbench From DuckStation.bat once to refresh the cached editor files.";
            if (string.Equals(levelName, "Stone Hill", StringComparison.OrdinalIgnoreCase) && string.Equals(assetName, "RAM dump", StringComparison.OrdinalIgnoreCase))
                return "Missing Stone Hill RAM dump. Expected cached editor file stonehill-before-gem-clean.bin or duckstation-mainram-fresh-stonehill.bin.";
            return "Missing " + levelName + " " + assetName + ". Stand in that level in DuckStation and run Capture Current Level Workbench From DuckStation.bat once to create the cached editor files.";
        }

        private void UpdateLevelActionButtons()
        {
            bool loaded = HasLoadedLevel();
            bool mobySourcePatchers = loaded && currentLevelSupportsSourcePatchers;
            bool stoneHillTerrainPatchers = loaded && IsStoneHillLevel();
            if (saveEditsButton != null) saveEditsButton.Enabled = loaded;
            if (loadEditsButton != null) loadEditsButton.Enabled = loaded;
            if (clearEditsButton != null) clearEditsButton.Enabled = loaded;
            if (resetSelectedButton != null) resetSelectedButton.Enabled = loaded;
            if (liveApplyButton != null) liveApplyButton.Enabled = loaded;
            if (liveRevertButton != null) liveRevertButton.Enabled = loaded;
            if (addObjectButton != null) addObjectButton.Enabled = loaded;
            if (changeSelectedButton != null) changeSelectedButton.Enabled = loaded;
            if (editContentsButton != null) editContentsButton.Enabled = loaded;
            if (copyMutationSourceButton != null) copyMutationSourceButton.Enabled = loaded;
            if (pasteMutationButton != null) pasteMutationButton.Enabled = loaded;
            if (hideSelectedButton != null) hideSelectedButton.Enabled = loaded;
            if (objectAddLabButton != null) objectAddLabButton.Enabled = loaded && currentLevelSupportsSourcePatchers;
            if (testSelectedAppendButton != null) testSelectedAppendButton.Enabled = loaded && currentLevelSupportsSourcePatchers;
            if (patchTopRankedButton != null) patchTopRankedButton.Enabled = mobySourcePatchers;
            if (patchBroadButton != null) patchBroadButton.Enabled = stoneHillTerrainPatchers;
            if (combinedPatchButton != null) combinedPatchButton.Enabled = stoneHillTerrainPatchers;
            if (teaserDemoButton != null) teaserDemoButton.Enabled = loaded;
            if (validateSourceButton != null) validateSourceButton.Enabled = stoneHillTerrainPatchers;
            if (behaviorDiffButton != null) behaviorDiffButton.Enabled = stoneHillTerrainPatchers;
            if (terrainApplyTextureButton != null) terrainApplyTextureButton.Enabled = loaded;
            if (terrainReplaceTextureButton != null) terrainReplaceTextureButton.Enabled = loaded;
            if (terrainRefreshTextureButton != null) terrainRefreshTextureButton.Enabled = loaded;
            if (terrainImportTextureButton != null) terrainImportTextureButton.Enabled = stoneHillTerrainPatchers;
            if (terrainDarkHollowPaletteButton != null) terrainDarkHollowPaletteButton.Enabled = stoneHillTerrainPatchers;
            if (terrainDarkHollowTexturePackButton != null) terrainDarkHollowTexturePackButton.Enabled = stoneHillTerrainPatchers;
        }

        private void PopulateSkyboxChoices(ComboBox box)
        {
            if (box == null) return;
            box.Items.Clear();
            foreach (SkyboxChoice choice in SkyboxChoice.All())
                box.Items.Add(choice);
            if (box.Items.Count > 0)
                box.SelectedIndex = 0;
        }

        private void PopulateSkyColorPresetChoices(ComboBox box)
        {
            if (box == null) return;
            box.Items.Clear();
            foreach (SkyColorPresetChoice choice in SkyColorPresetChoice.All())
                box.Items.Add(choice);
            if (box.Items.Count > 0)
                box.SelectedIndex = 0;
        }

        private void SelectSkyboxTargetForLevel(string levelKey)
        {
            if (skyboxTargetBox == null) return;
            string normalized = SpyroLevelCatalog.NormalizeKey(levelKey);
            for (int i = 0; i < skyboxTargetBox.Items.Count; i++)
            {
                SkyboxChoice choice = skyboxTargetBox.Items[i] as SkyboxChoice;
                if (choice != null && SpyroLevelCatalog.NormalizeKey(choice.Key) == normalized)
                {
                    skyboxTargetBox.SelectedIndex = i;
                    UpdateSkyboxStatus();
                    return;
                }
            }
            UpdateSkyboxStatus();
        }

        private void UpdateSkyboxStatus()
        {
            if (skyboxStatusLabel == null) return;
            SkyboxChoice target = SelectedSkyboxChoice(skyboxTargetBox);
            SkyboxChoice donor = SelectedSkyboxChoice(skyboxDonorBox);
            if (target == null || donor == null)
            {
                skyboxStatusLabel.Text = "Choose target level and donor skybox. Plan-only research; no bootable CUE.";
                return;
            }
            SkyColorPresetChoice colorPreset = SelectedSkyColorPresetChoice();
            string colorText = colorPreset == null ? "" : " Color CUE preset: " + colorPreset.Description;
            skyboxStatusLabel.Text = GetSkyboxStatusText(target, donor) + colorText;
        }

        private static SkyboxChoice SelectedSkyboxChoice(ComboBox box)
        {
            return box == null ? null : box.SelectedItem as SkyboxChoice;
        }

        private SkyColorPresetChoice SelectedSkyColorPresetChoice()
        {
            return skyColorPresetBox == null ? null : skyColorPresetBox.SelectedItem as SkyColorPresetChoice;
        }

        private string GetSkyboxStatusText(SkyboxChoice target, SkyboxChoice donor)
        {
            string baseText = target.DisplayName + " skybox <- " + donor.DisplayName + ".";
            Dictionary<string, object> targetRow;
            Dictionary<string, object> donorRow;
            string catalogError;
            if (!TryReadSkyboxCatalogPair(target, donor, out targetRow, out donorRow, out catalogError))
            {
                return baseText + " Build catalog to check archive-size-compatible donors.";
            }

            string targetStatus = GetJsonString(targetRow, "status", "");
            string donorStatus = GetJsonString(donorRow, "status", "");
            if (!string.Equals(targetStatus, "cataloged", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(donorStatus, "cataloged", StringComparison.OrdinalIgnoreCase))
            {
                return baseText + " Catalog needs probe refresh for this pair.";
            }

            int targetSize = GetJsonInt(targetRow, "skySubfileSize", -1);
            int donorSize = GetJsonInt(donorRow, "skySubfileSize", -1);
            if (targetSize > 0 && targetSize == donorSize)
                return baseText + " Same-size only (" + FormatHex(targetSize) + "); whole-subfile BIN export is terrain-unsafe.";

            return baseText + " Catalog blocks size mismatch: target " + FormatHex(targetSize) + ", donor " + FormatHex(donorSize) + ".";
        }

        private bool TryReadSkyboxCatalogPair(SkyboxChoice target, SkyboxChoice donor, out Dictionary<string, object> targetRow, out Dictionary<string, object> donorRow, out string error)
        {
            targetRow = null;
            donorRow = null;
            error = "";
            string path = Path.Combine(workspace, "spyro-skybox-catalog.json");
            if (!File.Exists(path)) return false;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                targetRow = FindSkyboxCatalogRow(root, target);
                donorRow = FindSkyboxCatalogRow(root, donor);
                return targetRow != null && donorRow != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static Dictionary<string, object> FindSkyboxCatalogRow(Dictionary<string, object> root, SkyboxChoice choice)
        {
            if (root == null || choice == null || !root.ContainsKey("levels")) return null;
            object[] levels = root["levels"] as object[];
            if (levels == null) return null;
            string wantedKey = SpyroLevelCatalog.NormalizeKey(choice.Key);
            string wantedScriptKey = SpyroLevelCatalog.NormalizeKey(choice.ScriptKey);
            string wantedName = SpyroLevelCatalog.NormalizeKey(choice.DisplayName);
            foreach (object raw in levels)
            {
                Dictionary<string, object> row = raw as Dictionary<string, object>;
                if (row == null) continue;
                string key = SpyroLevelCatalog.NormalizeKey(GetJsonString(row, "key", ""));
                string scriptKey = SpyroLevelCatalog.NormalizeKey(GetJsonString(row, "scriptKey", ""));
                string displayName = SpyroLevelCatalog.NormalizeKey(GetJsonString(row, "displayName", ""));
                if (key == wantedKey || scriptKey == wantedScriptKey || displayName == wantedName)
                    return row;
            }
            return null;
        }

        private static string FormatHex(int value)
        {
            return value < 0 ? "unknown" : "0x" + value.ToString("X");
        }

        private bool IsStoneHillLevel()
        {
            return string.Equals(currentLevelKey, "stonehill", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPeacekeepersLevel()
        {
            return string.Equals(currentLevelKey, "peacekeepers", StringComparison.OrdinalIgnoreCase);
        }

        private string ScriptLevelKey(string levelKey)
        {
            LevelDefinition level = FindLevelDefinition(levelKey);
            return level == null || string.IsNullOrEmpty(level.ScriptKey) ? levelKey : level.ScriptKey;
        }

        private int SourceRecordCountForLevel(string levelKey)
        {
            LevelDefinition level = FindLevelDefinition(levelKey);
            return level == null ? 0 : level.SourceRecordCount;
        }

        private string SourcePatchLeadForLevel(string levelKey, int trueIndex)
        {
            LevelDefinition level = FindLevelDefinition(levelKey);
            if (level == null || !level.HasSourceTable)
                return "Source table pending for true record " + trueIndex.ToString() + ".";
            string confidence = string.IsNullOrEmpty(level.Confidence) ? "" : " (" + level.Confidence + ")";
            return "WAD entry " + level.SourceWadEntry.ToString() + ", true record " + trueIndex.ToString() + ", XYZ +0x0C/+0x10/+0x14" + confidence;
        }

        private void ApplyLevelSourcePatchStatus(string levelKey, string levelName)
        {
            int sourceRecordCount = SourceRecordCountForLevel(levelKey);
            foreach (Moby moby in mobys)
            {
                if (sourceRecordCount > 0 && moby.TrueIndex >= 0 && moby.TrueIndex < sourceRecordCount)
                {
                    moby.PatchStatus = "loader-table-patchable";
                    moby.PatchLead = SourcePatchLeadForLevel(levelKey, moby.TrueIndex);
                    if (moby.PatchPriority <= 0)
                        moby.PatchPriority = 1;
                }
                else
                {
                    moby.PatchStatus = "runtime-captured; source table pending";
                    moby.PatchLead = levelName + " runtime record T" + moby.TrueIndex.ToString() + " is not covered by the mapped source table yet.";
                    moby.PatchPriority = 0;
                }
            }
        }

        private void ApplyRuntimeOnlyPatchStatus(string levelName)
        {
            foreach (Moby moby in mobys)
            {
                moby.PatchStatus = "runtime-captured; source patcher pending";
                moby.PatchLead = levelName + " loaded from live RAM capture. Permanent BIN/source table mapping is not wired yet.";
                moby.PatchPriority = 0;
            }
        }

        private int LoadTerrainMaterialOverrides()
        {
            terrainMaterialOverrides.Clear();
            if (!File.Exists(terrainMaterialOverridesPath))
                return 0;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(terrainMaterialOverridesPath, Encoding.UTF8)) as Dictionary<string, object>;
                object[] entries = root != null && root.ContainsKey("overrides") ? root["overrides"] as object[] : null;
                if (entries == null) return 0;

                foreach (object entry in entries)
                {
                    Dictionary<string, object> item = entry as Dictionary<string, object>;
                    if (item == null || !item.ContainsKey("textureId") || !item.ContainsKey("surface")) continue;
                    int textureId = Convert.ToInt32(item["textureId"]);
                    string surface = NormalizeTerrainSurface(Convert.ToString(item["surface"]));
                    if (textureId >= 0 && !string.IsNullOrEmpty(surface))
                        terrainMaterialOverrides[textureId] = surface;
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not load terrain material labels: " + ex.Message;
            }
            return terrainMaterialOverrides.Count;
        }

        private void SaveTerrainMaterialOverrides()
        {
            List<object> entries = new List<object>();
            List<int> keys = new List<int>(terrainMaterialOverrides.Keys);
            keys.Sort();
            foreach (int textureId in keys)
            {
                Dictionary<string, object> entry = new Dictionary<string, object>();
                entry["textureId"] = textureId;
                entry["surface"] = terrainMaterialOverrides[textureId];
                entries.Add(entry);
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["generatedBy"] = "NativeSpyroEditor";
            root["levelName"] = currentLevelName;
            root["purpose"] = currentLevelName + " terrain texture/material labels used by the editor surface assist.";
            root["updatedUtc"] = DateTime.UtcNow.ToString("o");
            root["overrides"] = entries.ToArray();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            File.WriteAllText(terrainMaterialOverridesPath, serializer.Serialize(root), Encoding.UTF8);
        }

        private void SetTerrainMaterialOverride(int textureId, string surface)
        {
            if (textureId < 0) return;
            surface = NormalizeTerrainSurface(surface);
            if (string.IsNullOrEmpty(surface))
                terrainMaterialOverrides.Remove(textureId);
            else
                terrainMaterialOverrides[textureId] = surface;

            SaveTerrainMaterialOverrides();
            canvas.Invalidate();
            UpdateInspector();
            statusLabel.Text = string.IsNullOrEmpty(surface)
                ? "Cleared material label for texture " + textureId.ToString() + "."
                : "Marked texture " + textureId.ToString() + " as " + surface + " across " + CountTerrainTextureFaces(textureId).ToString() + " faces.";
        }

        private static string NormalizeTerrainSurface(string surface)
        {
            if (string.IsNullOrEmpty(surface)) return "";
            surface = surface.Trim().ToLowerInvariant();
            if (surface == "grass" || surface == "water" || surface == "stone" || surface == "dirt" || surface == "cliff" || surface == "unknown")
                return surface;
            if (surface == "sand" || surface == "beach")
                return "sand";
            if (surface == "dry" || surface == "dry ground" || surface == "ground" || surface == "desert")
                return "dirt";
            if (surface == "wall" || surface == "rock")
                return "cliff";
            return "";
        }

        private int LoadCustomTerrainTextures()
        {
            DisposeCustomTerrainTextures();
            if (string.IsNullOrEmpty(customTerrainTexturesPath) || !File.Exists(customTerrainTexturesPath))
                return 0;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(customTerrainTexturesPath, Encoding.UTF8)) as Dictionary<string, object>;
                object[] entries = root != null && root.ContainsKey("textures") ? root["textures"] as object[] : null;
                if (entries == null) return 0;

                foreach (object entry in entries)
                {
                    Dictionary<string, object> item = entry as Dictionary<string, object>;
                    if (item == null) continue;
                    int textureId = DictionaryInt(item, "textureId", -1);
                    string sourceImagePath = DictionaryString(item, "sourceImagePath", "");
                    if (textureId < 0 || string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath))
                        continue;

                    Bitmap bitmap = LoadBitmapCopy(sourceImagePath);
                    customTerrainTextures[textureId] = new CustomTerrainTexture(
                        textureId,
                        sourceImagePath,
                        DictionaryString(item, "sourceImageName", Path.GetFileName(sourceImagePath)),
                        DictionaryString(item, "descriptorTier", "hqData"),
                        DictionaryInt(item, "tileSize", 64),
                        bitmap);
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not load custom terrain textures: " + ex.Message;
            }
            return customTerrainTextures.Count;
        }

        private void DisposeCustomTerrainTextures()
        {
            foreach (CustomTerrainTexture texture in customTerrainTextures.Values)
            {
                if (texture != null && texture.PreviewImage != null)
                    texture.PreviewImage.Dispose();
            }
            customTerrainTextures.Clear();
        }

        private void ImportCustomTerrainTexture()
        {
            if (!HasLoadedLevel() || !IsStoneHillLevel())
            {
                MessageBox.Show(this, "Custom terrain texture export is currently wired for Stone Hill.", "Texture importer unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int textureId = TargetTextureIdForCustomImport();
            if (textureId < 0)
            {
                MessageBox.Show(this, "Select a terrain face or a same-level texture ID first.", "No texture slot selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Import terrain texture image";
                dialog.Filter = "Image files|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff|All files|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                Bitmap validation = null;
                try
                {
                    validation = LoadBitmapCopy(dialog.FileName);
                    if (validation.Width <= 0 || validation.Height <= 0)
                        throw new InvalidOperationException("Image has no pixels.");

                    string customDir = Path.Combine(Path.Combine(workspace, "_local"), "custom-textures");
                    Directory.CreateDirectory(customDir);
                    string extension = Path.GetExtension(dialog.FileName);
                    if (string.IsNullOrEmpty(extension)) extension = ".png";
                    string fileName = SafeFilePart(currentLevelKey) + "-texture-" + textureId.ToString("000") + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + extension.ToLowerInvariant();
                    string stagedPath = Path.Combine(customDir, fileName);
                    File.Copy(dialog.FileName, stagedPath, true);

                    string descriptorTier = "hqData";
                    int tileSize = 64;
                    AddOrReplaceCustomTerrainTexture(textureId, stagedPath, Path.GetFileName(dialog.FileName), descriptorTier, tileSize);
                    SaveCustomTerrainTexturesManifest();
                    LoadCustomTerrainTextures();
                    RefreshTerrainTextureLibrary();
                    UpdateInspector();
                    canvas.Invalidate();
                    statusLabel.Text = "Imported custom texture art for " + currentLevelName + " texture ID " + textureId.ToString() + ". Create Terrain BIN will write it into the disposable game image.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Could not import terrain texture", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (validation != null) validation.Dispose();
                }
            }
        }

        private void CreateDarkHollowTerrainTexturePack()
        {
            if (!HasLoadedLevel() || geometry == null || !IsStoneHillLevel())
            {
                MessageBox.Show(this, "Dark Hollow texture-pack export is currently wired for Stone Hill.", "Stone Hill only", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (terrainTextureAtlas == null)
                LoadTerrainTextureAtlas();

            if (terrainTextureAtlas == null)
            {
                MessageBox.Show(this, "Stone Hill source texture atlas is not loaded. Run the WAD texture atlas tool, then try again.", "Missing texture atlas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SortedDictionary<int, bool> textureIds = new SortedDictionary<int, bool>();
            foreach (TerrainPolygon polygon in geometry.Polygons)
            {
                if (polygon == null) continue;
                if (polygon.OriginalTextureId >= 0) textureIds[polygon.OriginalTextureId] = true;
                if (polygon.TextureId >= 0) textureIds[polygon.TextureId] = true;
            }

            if (textureIds.Count == 0)
            {
                MessageBox.Show(this, "The loaded Stone Hill terrain overlay does not expose texture IDs.", "No texture IDs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "Generate Dark Hollow-style PNG texture imports for " + textureIds.Count.ToString() + " Stone Hill texture slot(s)?\n\nThis replaces matching custom imports in the editor manifest. Create Terrain BIN will write the staged PNGs into the disposable game image.",
                "Generate Dark Hollow texture pack",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                string customRoot = Path.Combine(Path.Combine(workspace, "_local"), "custom-textures");
                string packDir = Path.Combine(customRoot, "darkhollow-pack");
                Directory.CreateDirectory(packDir);

                int generated = 0;
                int skipped = 0;
                foreach (int textureId in textureIds.Keys)
                {
                    Rectangle source = TerrainTextureSourceRect(textureId);
                    if (source.IsEmpty)
                    {
                        skipped++;
                        continue;
                    }

                    string fileName = SafeFilePart(currentLevelKey) + "-texture-" + textureId.ToString("000") + "-darkhollow.png";
                    string stagedPath = Path.Combine(packDir, fileName);
                    using (Bitmap tile = CreateDarkHollowTerrainTextureTile(source))
                    {
                        tile.Save(stagedPath, ImageFormat.Png);
                    }

                    AddOrReplaceCustomTerrainTexture(textureId, stagedPath, fileName, "hqData", 64);
                    generated++;
                }

                SaveCustomTerrainTexturesManifest();
                LoadCustomTerrainTextures();
                RefreshTerrainTextureLibrary();
                UpdateInspector();
                canvas.Invalidate();

                statusLabel.Text = "Generated Dark Hollow texture pack for " + generated.ToString() + " Stone Hill texture slot(s)" + (skipped > 0 ? "; skipped " + skipped.ToString() + " atlas-missing slot(s)" : "") + ". Create Terrain BIN will write these custom PNGs.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not generate Dark Hollow texture pack", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Bitmap CreateDarkHollowTerrainTextureTile(Rectangle source)
        {
            Bitmap tile = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(tile))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(terrainTextureAtlas, new Rectangle(0, 0, tile.Width, tile.Height), source, GraphicsUnit.Pixel);
            }

            for (int y = 0; y < tile.Height; y++)
            {
                for (int x = 0; x < tile.Width; x++)
                {
                    tile.SetPixel(x, y, DarkHollowTerrainGradeColor(tile.GetPixel(x, y)));
                }
            }
            return tile;
        }

        private static Color DarkHollowTerrainGradeColor(Color color)
        {
            if (color.A == 0)
                return color;

            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double luma = Math.Max(0.0, Math.Min(1.0, (0.299 * r) + (0.587 * g) + (0.114 * b)));

            bool water = color.B >= color.R + 18 && color.B >= color.G - 10;
            bool grass = color.G >= color.R + 12 && color.G >= color.B - 5;
            bool stone = !water && !grass && color.R >= color.B - 6 && color.G >= color.B - 18;

            int lowR;
            int lowG;
            int lowB;
            int highR;
            int highG;
            int highB;

            if (water)
            {
                lowR = 6; lowG = 22; lowB = 54;
                highR = 36; highG = 68; highB = 116;
            }
            else if (grass)
            {
                lowR = 8; lowG = 48; lowB = 38;
                highR = 50; highG = 112; highB = 70;
            }
            else if (stone)
            {
                lowR = 56; lowG = 54; lowB = 64;
                highR = 138; highG = 130; highB = 146;
            }
            else
            {
                lowR = 10; lowG = 34; lowB = 58;
                highR = 82; highG = 88; highB = 112;
            }

            double t = Math.Max(0.0, Math.Min(1.0, (luma - 0.10) / 0.78));
            double detail = (luma - 0.50) * 18.0;
            return Color.FromArgb(
                color.A,
                DarkHollowClampByte(DarkHollowLerp(lowR, highR, t) + detail),
                DarkHollowClampByte(DarkHollowLerp(lowG, highG, t) + detail),
                DarkHollowClampByte(DarkHollowLerp(lowB, highB, t) + detail));
        }

        private static int DarkHollowLerp(int a, int b, double t)
        {
            return (int)Math.Round(a + ((b - a) * Math.Max(0.0, Math.Min(1.0, t))));
        }

        private static int DarkHollowClampByte(double value)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private void AddOrReplaceCustomTerrainTexture(int textureId, string sourceImagePath, string sourceImageName, string descriptorTier, int tileSize)
        {
            CustomTerrainTexture old;
            if (customTerrainTextures.TryGetValue(textureId, out old) && old != null && old.PreviewImage != null)
                old.PreviewImage.Dispose();

            customTerrainTextures[textureId] = new CustomTerrainTexture(
                textureId,
                sourceImagePath,
                sourceImageName,
                descriptorTier,
                tileSize,
                LoadBitmapCopy(sourceImagePath));
        }

        private void SaveCustomTerrainTexturesManifest()
        {
            List<object> entries = new List<object>();
            List<int> keys = new List<int>(customTerrainTextures.Keys);
            keys.Sort();
            foreach (int textureId in keys)
            {
                CustomTerrainTexture texture = customTerrainTextures[textureId];
                if (texture == null) continue;
                Dictionary<string, object> entry = new Dictionary<string, object>();
                entry["textureId"] = texture.TextureId;
                entry["sourceImagePath"] = texture.SourceImagePath;
                entry["sourceImageName"] = texture.SourceImageName;
                entry["descriptorTier"] = string.IsNullOrEmpty(texture.DescriptorTier) ? "hqData" : texture.DescriptorTier;
                entry["tileSize"] = texture.TileSize > 0 ? texture.TileSize : 64;
                entries.Add(entry);
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["generatedBy"] = "NativeSpyroEditor";
            root["levelName"] = currentLevelName;
            root["levelKey"] = currentLevelKey;
            root["purpose"] = currentLevelName + " custom terrain texture imports for the Stone Hill texture-page patch exporter.";
            root["updatedUtc"] = DateTime.UtcNow.ToString("o");
            root["textureCount"] = entries.Count;
            root["textures"] = entries.ToArray();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            File.WriteAllText(customTerrainTexturesPath, serializer.Serialize(root), Encoding.UTF8);
        }

        private int TargetTextureIdForCustomImport()
        {
            if (selectedTerrainIndex >= 0 && geometry != null && selectedTerrainIndex < geometry.Polygons.Count)
                return geometry.Polygons[selectedTerrainIndex].TextureId;

            TerrainTextureChoice choice = SelectedTerrainTextureChoice();
            if (choice != null && string.Equals(choice.LevelKey, currentLevelKey, StringComparison.OrdinalIgnoreCase))
                return choice.TextureId;

            return copiedTerrainTextureId;
        }

        private int CountCustomTerrainTextureImports()
        {
            return customTerrainTextures.Count;
        }

        private bool TryGetCustomTerrainTexture(int textureId, out CustomTerrainTexture texture)
        {
            return customTerrainTextures.TryGetValue(textureId, out texture) && texture != null && texture.PreviewImage != null;
        }

        private static Bitmap LoadBitmapCopy(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image image = Image.FromStream(stream))
            {
                return new Bitmap(image);
            }
        }

        private static int DictionaryInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        private static string DictionaryString(Dictionary<string, object> dict, string name, string fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            return Convert.ToString(dict[name]);
        }

        private void LoadTerrainTextureAtlas()
        {
            DisposeTerrainTextureAtlas();
            if (!string.Equals(currentLevelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
            {
                UpdateTerrainTextureAtlasPreferenceChecks();
                if (sourceTextureButton != null)
                {
                    sourceTextureButton.Enabled = false;
                    sourceTextureButton.Checked = false;
                    sourceTextureButton.ToolTipText = "Source texture atlas is not decoded for " + currentLevelName + " yet. Runtime face colors still draw the terrain.";
                }
                showSourceTextures = false;
                terrainTextureAtlasPath = "";
                terrainTextureAtlasTier = "";
                return;
            }

            string closePath = Program.ResolveWorkspaceFile(workspace, "stonehill-texture-atlas-close-from-wad-subfile00.png", "game-and-capture-artifacts", "generated-research");
            string standardPath = Program.ResolveWorkspaceFile(workspace, "stonehill-texture-atlas-from-wad-subfile00.png", "game-and-capture-artifacts", "generated-research");
            if (terrainTextureAtlasPreference == TerrainTextureAtlasPreference.HqClose)
            {
                terrainTextureAtlasPath = closePath;
                terrainTextureAtlasTier = "HQ-close 16-tile";
            }
            else if (terrainTextureAtlasPreference == TerrainTextureAtlasPreference.HqStandard)
            {
                terrainTextureAtlasPath = standardPath;
                terrainTextureAtlasTier = "HQ 4-tile";
            }
            else if (File.Exists(closePath))
            {
                terrainTextureAtlasPath = closePath;
                terrainTextureAtlasTier = "HQ-close 16-tile";
            }
            else
            {
                terrainTextureAtlasPath = standardPath;
                terrainTextureAtlasTier = "HQ 4-tile";
            }

            UpdateTerrainTextureAtlasPreferenceChecks();
            if (!File.Exists(terrainTextureAtlasPath))
            {
                if (sourceTextureButton != null)
                {
                    sourceTextureButton.Enabled = false;
                    sourceTextureButton.Checked = false;
                    sourceTextureButton.ToolTipText = "Texture atlas missing for " + TerrainTextureAtlasPreferenceText(terrainTextureAtlasPreference) + ". Run the WAD texture atlas tool before enabling source-texture terrain.";
                }
                showSourceTextures = false;
                terrainTextureAtlasPath = "";
                terrainTextureAtlasTier = "";
                return;
            }

            byte[] bytes = File.ReadAllBytes(terrainTextureAtlasPath);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Bitmap loaded = new Bitmap(stream))
            {
                terrainTextureAtlas = new Bitmap(loaded);
                terrainTextureTileSize = Math.Max(1, terrainTextureAtlas.Width / TerrainTextureAtlasColumns);
                terrainTextureLumaAtlas = CreateLuminanceAtlas(terrainTextureAtlas);
            }
            if (sourceTextureButton != null)
            {
                sourceTextureButton.Enabled = true;
                sourceTextureButton.ToolTipText = "Draw WAD-source " + terrainTextureTileSize.ToString() + "x" + terrainTextureTileSize.ToString() + " " + terrainTextureAtlasTier + " texture tiles mapped to terrain face corners.";
            }
        }

        private static Dictionary<int, TerrainTextureChoice> BuildTerrainTextureChoiceMap(string levelKey, string levelName, GeometryCandidate candidate)
        {
            Dictionary<int, TerrainTextureChoice> byTexture = new Dictionary<int, TerrainTextureChoice>();
            if (candidate == null || candidate.Polygons == null)
                return byTexture;

            foreach (TerrainPolygon polygon in candidate.Polygons)
            {
                if (polygon == null || polygon.TextureId < 0) continue;
                TerrainTextureChoice choice;
                if (!byTexture.TryGetValue(polygon.TextureId, out choice))
                {
                    choice = new TerrainTextureChoice(levelKey, levelName, polygon.TextureId, polygon);
                    byTexture[polygon.TextureId] = choice;
                }
                choice.FaceCount++;
            }
            return byTexture;
        }

        private void RefreshTerrainTextureLibrary()
        {
            if (terrainDonorLevelBox == null || terrainTextureList == null)
                return;

            if (terrainDonorLevelBox.Items.Count == 0)
            {
                foreach (LevelDefinition level in levelDefinitions)
                {
                    string overlayPath = GetLevelGeometryPath(level.Key);
                    if (File.Exists(overlayPath))
                        terrainDonorLevelBox.Items.Add(level);
                }
            }

            if (terrainDonorLevelBox.SelectedIndex < 0 && terrainDonorLevelBox.Items.Count > 0)
            {
                LevelDefinition current = FindLevelDefinition(currentLevelKey);
                if (current != null && terrainDonorLevelBox.Items.Contains(current))
                    terrainDonorLevelBox.SelectedItem = current;
                else
                    terrainDonorLevelBox.SelectedIndex = 0;
                return;
            }

            terrainTextureChoices.Clear();
            terrainTextureList.BeginUpdate();
            try
            {
                terrainTextureList.Items.Clear();
                LevelDefinition donor = terrainDonorLevelBox.SelectedItem as LevelDefinition;
                if (donor == null)
                {
                    terrainTextureSummaryLabel.Text = "No terrain source selected.";
                    UpdateTerrainTexturePreview();
                    return;
                }

                string overlayPath = GetLevelGeometryPath(donor.Key);
                if (!File.Exists(overlayPath))
                {
                    terrainTextureSummaryLabel.Text = donor.DisplayName + " overlay is not captured.";
                    UpdateTerrainTexturePreview();
                    return;
                }

                GeometryCandidate donorGeometry = string.Equals(donor.Key, currentLevelKey, StringComparison.OrdinalIgnoreCase) && geometry != null
                    ? geometry
                    : GeometryLoader.LoadFirstCandidate(overlayPath);

                Dictionary<int, TerrainTextureChoice> byTexture = BuildTerrainTextureChoiceMap(donor.Key, donor.DisplayName, donorGeometry);

                List<int> ids = new List<int>(byTexture.Keys);
                ids.Sort();
                foreach (int id in ids)
                {
                    TerrainTextureChoice choice = byTexture[id];
                    terrainTextureChoices.Add(choice);
                    terrainTextureList.Items.Add(choice);
                }

                string exportNote = string.Equals(donor.Key, currentLevelKey, StringComparison.OrdinalIgnoreCase)
                    ? "Same-level IDs export to BIN for Stone Hill."
                    : "Cross-level IDs are preview/remap only until donor texture import is decoded.";
                if (string.Equals(donor.Key, currentLevelKey, StringComparison.OrdinalIgnoreCase) && CountCustomTerrainTextureImports() > 0)
                    exportNote += " Custom PNG imports: " + CountCustomTerrainTextureImports().ToString() + ".";
                terrainTextureSummaryLabel.Text = donor.DisplayName + ": " + terrainTextureChoices.Count.ToString() + " texture IDs. " + exportNote;
            }
            catch (Exception ex)
            {
                terrainTextureSummaryLabel.Text = "Could not load terrain textures: " + ex.Message;
            }
            finally
            {
                terrainTextureList.EndUpdate();
            }

            UpdateComboBoxDropDownWidth(terrainTextureList, 360);
            UpdateTerrainTexturePreview();
        }

        private TerrainTextureChoice SelectedTerrainTextureChoice()
        {
            if (terrainTextureList == null || terrainTextureList.SelectedItem == null)
                return null;
            return terrainTextureList.SelectedItem as TerrainTextureChoice;
        }

        private void UpdateTerrainTexturePreview()
        {
            if (terrainTexturePreview == null)
                return;

            Image old = terrainTexturePreview.Image;
            terrainTexturePreview.Image = null;
            if (old != null) old.Dispose();

            TerrainTextureChoice choice = SelectedTerrainTextureChoice();
            if (choice == null)
                return;

            terrainTexturePreview.Image = CreateTerrainTexturePreview(choice);
            copiedTerrainTextureId = choice.TextureId;
            if (terrainTextureSummaryLabel != null)
            {
                string scope = string.Equals(choice.LevelKey, currentLevelKey, StringComparison.OrdinalIgnoreCase)
                    ? "same-level/exportable"
                    : "cross-level id preview";
                CustomTerrainTexture custom;
                if (TryGetCustomTerrainTexture(choice.TextureId, out custom))
                    scope += ", custom PNG";
                terrainTextureSummaryLabel.Text = choice.DisplayText + " selected (" + scope + ").";
            }
        }

        private Bitmap CreateTerrainTexturePreview(TerrainTextureChoice choice)
        {
            Bitmap preview = new Bitmap(172, 78);
            using (Graphics g = Graphics.FromImage(preview))
            {
                g.Clear(Color.White);
                Rectangle tileBounds = new Rectangle(8, 8, 62, 62);
                bool drewTile = false;
                CustomTerrainTexture custom;
                if (choice != null && string.Equals(choice.LevelKey, currentLevelKey, StringComparison.OrdinalIgnoreCase) && TryGetCustomTerrainTexture(choice.TextureId, out custom))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(custom.PreviewImage, tileBounds);
                    drewTile = true;
                }
                else if (choice != null && string.Equals(choice.LevelKey, currentLevelKey, StringComparison.OrdinalIgnoreCase) && terrainTextureAtlas != null)
                {
                    Rectangle source = TerrainTextureSourceRect(choice.TextureId);
                    if (!source.IsEmpty)
                    {
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(terrainTextureAtlas, tileBounds, source, GraphicsUnit.Pixel);
                        drewTile = true;
                    }
                }
                if (!drewTile)
                {
                    using (SolidBrush brush = new SolidBrush(choice == null ? Color.LightGray : choice.SampleColor))
                        g.FillRectangle(brush, tileBounds);
                    using (Pen pen = new Pen(Color.FromArgb(80, 80, 80)))
                        g.DrawRectangle(pen, tileBounds);
                }

                using (Font titleFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Font smallFont = new Font("Segoe UI", 8f))
                using (Brush textBrush = new SolidBrush(Color.FromArgb(40, 46, 52)))
                {
                    string title = choice == null ? "No texture" : "Texture ID " + choice.TextureId.ToString();
                    g.DrawString(title, titleFont, textBrush, new RectangleF(78, 9, 88, 18));
                    string line2 = choice == null ? "" : choice.LevelName;
                    g.DrawString(line2, smallFont, textBrush, new RectangleF(78, 30, 88, 18));
                    string line3 = choice == null ? "" : choice.FaceCount.ToString() + " faces";
                    g.DrawString(line3, smallFont, textBrush, new RectangleF(78, 49, 88, 18));
                }
            }
            return preview;
        }

        private void ApplySelectedTerrainTextureToFace()
        {
            TerrainTextureChoice choice = SelectedTerrainTextureChoice();
            if (choice == null)
            {
                MessageBox.Show(this, "Choose a terrain texture first.", "No terrain texture selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedTerrainIndex < 0 || geometry == null || selectedTerrainIndex >= geometry.Polygons.Count)
            {
                MessageBox.Show(this, "Select a target terrain face first.", "No target face selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetTerrainFaceTexture(selectedTerrainIndex, choice.TextureId);
            if (!string.Equals(choice.LevelKey, currentLevelKey, StringComparison.OrdinalIgnoreCase))
                statusLabel.Text += " Cross-level donor art is not imported yet, so this writes the target level's texture ID slot.";
        }

        private void ReplaceSelectedTerrainTextureFamily()
        {
            TerrainTextureChoice choice = SelectedTerrainTextureChoice();
            if (choice == null)
            {
                MessageBox.Show(this, "Choose a terrain texture first.", "No terrain texture selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedTerrainIndex < 0 || geometry == null || selectedTerrainIndex >= geometry.Polygons.Count)
            {
                MessageBox.Show(this, "Select a target terrain face first.", "No target face selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TerrainPolygon target = geometry.Polygons[selectedTerrainIndex];
            ReplaceTerrainTextureFamily(target.OriginalTextureId, choice.TextureId);
            if (!string.Equals(choice.LevelKey, currentLevelKey, StringComparison.OrdinalIgnoreCase))
                statusLabel.Text += " Cross-level donor art is not imported yet, so this writes the target level's texture ID slot.";
        }

        private void SetTerrainTextureAtlasPreference(TerrainTextureAtlasPreference preference)
        {
            terrainTextureAtlasPreference = preference;
            UpdateTerrainTextureAtlasPreferenceChecks();
            LoadTerrainTextureAtlas();
            canvas.Invalidate();
            UpdateInspector();
            statusLabel.Text = "Terrain atlas tier set to " + TerrainTextureAtlasPreferenceText(preference) + ".";
        }

        private void UpdateTerrainTextureAtlasPreferenceChecks()
        {
            if (textureAtlasAutoButton != null) textureAtlasAutoButton.Checked = terrainTextureAtlasPreference == TerrainTextureAtlasPreference.Auto;
            if (textureAtlasCloseButton != null) textureAtlasCloseButton.Checked = terrainTextureAtlasPreference == TerrainTextureAtlasPreference.HqClose;
            if (textureAtlasStandardButton != null) textureAtlasStandardButton.Checked = terrainTextureAtlasPreference == TerrainTextureAtlasPreference.HqStandard;
        }

        private void SetSourceTextureWindowMode(SourceTextureWindowMode mode)
        {
            sourceTextureWindowMode = mode;
            UpdateSourceTextureWindowModeChecks();
            canvas.Invalidate();
            UpdateInspector();
            statusLabel.Text = "Texture window diagnostic set to " + SourceTextureWindowModeText(mode) + ".";
        }

        private void UpdateSourceTextureWindowModeChecks()
        {
            if (textureWindowWholeButton != null) textureWindowWholeButton.Checked = sourceTextureWindowMode == SourceTextureWindowMode.WholeTile;
            if (textureWindowDepthButton != null) textureWindowDepthButton.Checked = sourceTextureWindowMode == SourceTextureWindowMode.FaceDepthLow4;
            if (textureWindowWord3Button != null) textureWindowWord3Button.Checked = sourceTextureWindowMode == SourceTextureWindowMode.Word3High4;
            if (textureWindowWord4MidButton != null) textureWindowWord4MidButton.Checked = sourceTextureWindowMode == SourceTextureWindowMode.Word4Mid4;
            if (textureWindowWord4HighButton != null) textureWindowWord4HighButton.Checked = sourceTextureWindowMode == SourceTextureWindowMode.Word4High4;
        }

        private void DisposeTerrainTextureAtlas()
        {
            if (terrainTextureLumaAtlas != null)
            {
                terrainTextureLumaAtlas.Dispose();
                terrainTextureLumaAtlas = null;
            }
            if (terrainTextureAtlas != null)
            {
                terrainTextureAtlas.Dispose();
                terrainTextureAtlas = null;
            }
            terrainTextureTileSize = 64;
            terrainTextureAtlasTier = "";
        }

        private static Bitmap CreateLuminanceAtlas(Bitmap source)
        {
            Bitmap result = new Bitmap(source.Width, source.Height);
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    int luma = (int)Math.Round((pixel.R * 0.299) + (pixel.G * 0.587) + (pixel.B * 0.114));
                    luma = Math.Max(0, Math.Min(255, 42 + (int)Math.Round(luma * 0.86)));
                    result.SetPixel(x, y, Color.FromArgb(pixel.A, luma, luma, luma));
                }
            }
            return result;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DisposeTerrainTextureAtlas();
            DisposeCustomTerrainTextures();
            base.OnFormClosed(e);
        }

        private bool ConfirmDiscardEdits(string action)
        {
            if (!hasUnsavedEdits) return true;
            DialogResult result = MessageBox.Show(
                this,
                "You have unsaved " + currentLevelName + " edits. Save them before you " + action + "?",
                "Unsaved " + currentLevelName + " edits",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes)
            {
                SaveEdits();
                return !hasUnsavedEdits;
            }
            return true;
        }

        private void RefreshMobyList()
        {
            if (mobyList == null) return;
            updatingSelection = true;
            try
            {
                mobyList.BeginUpdate();
                mobyList.Items.Clear();
                ListViewItem selectedItem = null;
                int visibleCount = 0;
                for (int i = 0; i < mobys.Count; i++)
                {
                    Moby m = mobys[i];
                    if (!ShouldShowMoby(i, m)) continue;
                    string marker = m.IsEdited ? "*" : "";
                    ListViewItem item = new ListViewItem(marker + MobyId(m));
                    item.SubItems.Add(MobyIconText(m));
                    item.SubItems.Add(CatalogCategory(m));
                    item.SubItems.Add(m.DisplayLabel);
                    item.SubItems.Add("0x" + m.Type.ToString("X2"));
                    item.SubItems.Add(GetWorkStatus(m));
                    item.Tag = i;
                    if (m.IsEdited)
                    {
                        item.BackColor = Color.FromArgb(255, 248, 220);
                        item.ForeColor = Color.FromArgb(88, 61, 0);
                    }
                    mobyList.Items.Add(item);
                    visibleCount++;
                    if (i == selectedMobyIndex)
                        selectedItem = item;
                }
                AdjustMobyListColumns();
                if (selectedItem != null)
                {
                    selectedItem.Selected = true;
                    selectedItem.EnsureVisible();
                }
                if (catalogSummaryLabel != null)
                    catalogSummaryLabel.Text = visibleCount.ToString() + "/" + mobys.Count.ToString();
                RefreshTreasureSummary();
            }
            finally
            {
                mobyList.EndUpdate();
                updatingSelection = false;
            }
        }

        private void RefreshTreasureSummary()
        {
            if (treasureSummaryLabel == null) return;
            treasureSummaryLabel.Text = TreasureSummaryText(CalculateTreasureSummary());
        }

        private LevelTreasureSummary CalculateTreasureSummary()
        {
            LevelTreasureSummary summary = new LevelTreasureSummary();
            int sourceRecordCount = SourceRecordCountForLevel(currentLevelKey);
            foreach (Moby moby in mobys)
            {
                int baseValue = TreasureValueForMoby(moby, true, sourceRecordCount);
                int currentValue = TreasureValueForMoby(moby, false, sourceRecordCount);
                summary.BaseTotal += baseValue;
                summary.CurrentTotal += currentValue;
                if (currentValue != baseValue)
                    summary.EditedRecords++;
            }
            return summary;
        }

        private static string TreasureSummaryText(LevelTreasureSummary summary)
        {
            if (summary == null || (summary.BaseTotal == 0 && summary.CurrentTotal == 0))
                return "Treasure: unknown";
            if (summary.BaseTotal == summary.CurrentTotal)
                return "Treasure: " + summary.CurrentTotal.ToString() + " current total";
            int delta = summary.CurrentTotal - summary.BaseTotal;
            string sign = delta >= 0 ? "+" : "";
            return "Treasure: " + summary.CurrentTotal.ToString() + " current total (vanilla " + summary.BaseTotal.ToString() + ", " + sign + delta.ToString() + ")";
        }

        private static int TreasureValueForMoby(Moby moby, bool baseIdentity, int sourceRecordCount)
        {
            if (moby == null) return 0;
            if (!baseIdentity && moby.HasHiddenSlotEdit) return 0;
            if (!baseIdentity && moby.IsAppendedRecord && IsPotentialChestContentMarker(moby) && !moby.HasChestContentLinkEdit) return 0;
            if (baseIdentity && moby.IsAppendedRecord) return 0;
            if (baseIdentity && sourceRecordCount > 0 && moby.TrueIndex >= sourceRecordCount) return 0;

            string label = baseIdentity && moby.HasBaseIdentity ? (moby.BaseLabel ?? "") : moby.DisplayLabel;
            int loose = LooseGemValueFromLabel(label);
            int reward = baseIdentity && moby.HasBaseIdentity ? GemValueFromGemIdByte(moby.BaseFlag4B) : GemValueFromGemIdByte(moby.Flag4B);

            if (!baseIdentity)
            {
                if (moby.HasGemColorEdit)
                    loose = moby.GemValueOverride;
                if (moby.HasRewardColorEdit)
                    reward = moby.RewardValueOverride;
            }

            if (IsChestContentMarker(moby))
                return reward;

            return loose + reward;
        }

        private static int LooseGemValueFromLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return 0;
            string text = label.ToLowerInvariant();
            if (HasAny(text, "purple gem", "25-gem", "gem (25)", "(25)")) return 25;
            if (HasAny(text, "yellow gem", "10-gem", "gem (10)", "(10)")) return 10;
            if (HasAny(text, "blue gem", "5-gem", "gem (5)", "(5)")) return 5;
            if (HasAny(text, "green gem", "2-gem", "gem (2)", "(2)")) return 2;
            if (HasAny(text, "red gem", "1-gem", "gem (1)", "(1)")) return 1;
            return 0;
        }

        private static int GemValueFromGemIdByte(int value)
        {
            switch (value)
            {
                case 0x53: return 1;
                case 0x54: return 2;
                case 0x55: return 5;
                case 0x56: return 10;
                case 0x57: return 25;
                default: return 0;
            }
        }

        private void RefreshMobyListRow(int listIndex)
        {
            RefreshMobyList();
        }

        private void BuildSelectionGroups()
        {
            string preferredKey = ActiveSelectionGroupKey();
            selectionGroups.Clear();

            AddBehaviorLinkSelectionGroups();
            AddGlobalBehaviorInferenceSelectionGroups();
            AddSelectionGroupByPredicate("Special: Dragons and pedestals", "special:dragons", false, delegate(Moby moby) { return string.Equals(CatalogCategory(moby), CatalogDragons, StringComparison.Ordinal); });
            AddSelectionGroupByPredicate("Special: Whirlwinds and exits", "special:whirlwinds-exits", false, delegate(Moby moby)
            {
                string category = CatalogCategory(moby);
                return string.Equals(category, CatalogWhirlwinds, StringComparison.Ordinal) || string.Equals(category, CatalogPortals, StringComparison.Ordinal);
            });
            AddSelectionGroupByPredicate("Special: Cameras", "special:cameras", false, delegate(Moby moby) { return string.Equals(CatalogCategory(moby), CatalogCameras, StringComparison.Ordinal); });
            AddSelectionGroupByPredicate("Special: Nonvisual controls", "special:controls", false, delegate(Moby moby) { return string.Equals(CatalogCategory(moby), CatalogHelpers, StringComparison.Ordinal); });

            AddIdentitySelectionGroups();
            AddAreaSelectionGroups();
            RefreshSelectionGroupBox(preferredKey);
        }

        private int AddBehaviorLinkSelectionGroups()
        {
            string path = Path.Combine(workspace, currentLevelKey + "-behavior-links.json");
            if (!File.Exists(path)) return 0;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null) return 0;

                int added = 0;
                foreach (object obj in GeometryLoader.GetArray(root, "linkGroups"))
                {
                    Dictionary<string, object> entry = obj as Dictionary<string, object>;
                    if (entry == null) continue;

                    string key = GetJsonString(entry, "key", "");
                    if (string.IsNullOrEmpty(key))
                        key = "behavior:" + added.ToString();
                    string name = GetJsonString(entry, "name", key);
                    bool linkedMove = GetJsonBool(entry, "linkedMove", false);

                    SelectionGroup group = new SelectionGroup(key, name, linkedMove);
                    foreach (object value in GeometryLoader.GetArray(entry, "trueIndexes"))
                    {
                        int trueIndex;
                        try { trueIndex = Convert.ToInt32(value); }
                        catch { continue; }
                        int mobyIndex = FindMobyIndexByTrueIndex(trueIndex);
                        if (mobyIndex >= 0)
                            group.Add(mobyIndex);
                    }

                    if (linkedMove && IsDragonBehaviorLinkEntry(entry, key, name))
                        KeepOnlyDragonAndPedestalMembers(group);

                    if (AddSelectionGroup(group, false))
                        added++;
                }
                return added;
            }
            catch
            {
                return 0;
            }
        }

        private int AddGlobalBehaviorInferenceSelectionGroups()
        {
            int added = 0;
            added += AddInferredDragonPedestalSelectionGroups();
            added += AddInferredPortalEntrySelectionGroups();
            added += AddInferredLockedChestContentSelectionGroups();
            added += AddInferredSpringChestPairSelectionGroups();
            return added;
        }

        private int AddInferredDragonPedestalSelectionGroups()
        {
            int added = 0;
            HashSet<int> usedPedestals = new HashSet<int>();
            for (int actorIndex = 0; actorIndex < mobys.Count; actorIndex++)
            {
                Moby actor = mobys[actorIndex];
                if (!IsDragonActorMoby(actor)) continue;
                if (LinkedMoveSelectionGroupForMoby(actorIndex) != null) continue;

                int pedestalIndex = FindNearestMobyIndex(actorIndex, delegate(Moby candidate, int candidateIndex)
                {
                    return !usedPedestals.Contains(candidateIndex)
                        && IsDragonPedestalMoby(candidate)
                        && LinkedMoveSelectionGroupForMoby(candidateIndex) == null;
                }, 256f, 384f);

                if (pedestalIndex < 0) continue;
                usedPedestals.Add(pedestalIndex);

                SelectionGroup group = new SelectionGroup("auto-dragon-pedestal:t" + actor.TrueIndex.ToString() + "-t" + mobys[pedestalIndex].TrueIndex.ToString(),
                    "Behavior: Dragon/pedestal T" + actor.TrueIndex.ToString() + "/T" + mobys[pedestalIndex].TrueIndex.ToString() + " (inferred)", true);
                group.Add(actorIndex);
                group.Add(pedestalIndex);
                if (AddSelectionGroup(group, false))
                    added++;
            }
            return added;
        }

        private int AddInferredPortalEntrySelectionGroups()
        {
            int added = 0;
            for (int anchorIndex = 0; anchorIndex < mobys.Count; anchorIndex++)
            {
                Moby anchor = mobys[anchorIndex];
                if (!IsPortalLinkAnchor(anchor)) continue;
                if (LinkedMoveSelectionGroupForMoby(anchorIndex) != null) continue;

                SelectionGroup group = new SelectionGroup("auto-portal-entry:t" + anchor.TrueIndex.ToString(),
                    "Behavior: Portal entry T" + anchor.TrueIndex.ToString() + " (inferred)", true);
                group.Add(anchorIndex);

                for (int memberIndex = 0; memberIndex < mobys.Count; memberIndex++)
                {
                    if (memberIndex == anchorIndex) continue;
                    Moby member = mobys[memberIndex];
                    if (!IsPortalLinkedNeighbor(anchor, member)) continue;
                    if (LinkedMoveSelectionGroupForMoby(memberIndex) != null) continue;
                    group.Add(memberIndex);
                }

                if (AddSelectionGroup(group, false))
                    added++;
            }
            return added;
        }

        private int AddInferredLockedChestContentSelectionGroups()
        {
            int added = 0;
            for (int chestIndex = 0; chestIndex < mobys.Count; chestIndex++)
            {
                Moby chest = mobys[chestIndex];
                if (!IsLinkedContentsChestLike(chest)) continue;
                if (ChestContentSelectionGroupForMoby(chestIndex) != null) continue;

                SelectionGroup group = new SelectionGroup("auto-chest-contents:t" + chest.TrueIndex.ToString(),
                    "Behavior: " + ChestContentGroupTitle(chest) + " T" + chest.TrueIndex.ToString() + " (inferred)", true);
                group.Add(chestIndex);

                for (int contentIndex = 0; contentIndex < mobys.Count; contentIndex++)
                {
                    if (contentIndex == chestIndex) continue;
                    Moby content = mobys[contentIndex];
                    if (!IsPotentialChestContentMarker(content)) continue;
                    if (IsLikelyChestContentNearChest(chest, content))
                        group.Add(contentIndex);
                }

                if (AddSelectionGroup(group, false))
                    added++;
            }
            return added;
        }

        private int AddInferredSpringChestPairSelectionGroups()
        {
            int added = 0;
            Dictionary<int, int> byTrueIndex = new Dictionary<int, int>();
            for (int i = 0; i < mobys.Count; i++)
            {
                if (mobys[i] != null && mobys[i].TrueIndex >= 0)
                    byTrueIndex[mobys[i].TrueIndex] = i;
            }

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                if (moby == null || moby.SpringChestPartnerTrueIndex < 0) continue;
                int partnerIndex;
                if (!byTrueIndex.TryGetValue(moby.SpringChestPartnerTrueIndex, out partnerIndex)) continue;
                if (partnerIndex < 0 || partnerIndex >= mobys.Count || partnerIndex == i) continue;

                int a = Math.Min(moby.TrueIndex, mobys[partnerIndex].TrueIndex);
                int b = Math.Max(moby.TrueIndex, mobys[partnerIndex].TrueIndex);
                string key = "auto-spring-chest-pair:t" + a.ToString() + "-t" + b.ToString();
                if (seen.Contains(key)) continue;
                seen.Add(key);

                SelectionGroup group = new SelectionGroup(key, "Behavior: Spring chest pair T" + a.ToString() + "/T" + b.ToString(), true);
                group.Add(i);
                group.Add(partnerIndex);
                if (AddSelectionGroup(group, false))
                    added++;
            }
            return added;
        }

        private delegate bool MobyIndexPredicate(Moby moby, int mobyIndex);

        private int FindNearestMobyIndex(int sourceIndex, MobyIndexPredicate predicate, float maxXyDistance, float maxZDistance)
        {
            if (sourceIndex < 0 || sourceIndex >= mobys.Count || predicate == null) return -1;
            Moby source = mobys[sourceIndex];
            int bestIndex = -1;
            float bestDistanceSquared = maxXyDistance * maxXyDistance;
            for (int i = 0; i < mobys.Count; i++)
            {
                if (i == sourceIndex) continue;
                Moby candidate = mobys[i];
                if (!predicate(candidate, i)) continue;
                if (Math.Abs(candidate.Z - source.Z) > maxZDistance) continue;
                float dx = candidate.X - source.X;
                float dy = candidate.Y - source.Y;
                float distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared > bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                bestIndex = i;
            }
            return bestIndex;
        }

        private static bool IsLikelyChestContentNearChest(Moby chest, Moby content)
        {
            if (chest == null || content == null) return false;
            float dx = chest.X - content.X;
            float dy = chest.Y - content.Y;
            float dz = chest.Z - content.Z;
            return (dx * dx) + (dy * dy) <= 96f * 96f && Math.Abs(dz) <= 96f;
        }

        private static string GetJsonString(Dictionary<string, object> dict, string name, string fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            return Convert.ToString(dict[name]);
        }

        private static bool GetJsonBool(Dictionary<string, object> dict, string name, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToBoolean(dict[name]); }
            catch { return fallback; }
        }

        private static int GetJsonInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        private static bool IsDragonBehaviorLinkEntry(Dictionary<string, object> entry, string key, string name)
        {
            string text = ((key ?? "") + " "
                + (name ?? "") + " "
                + GetJsonString(entry, "basis", "") + " "
                + GetJsonString(entry, "reason", "")).ToLowerInvariant();
            return text.IndexOf("dragon", StringComparison.Ordinal) >= 0;
        }

        private void KeepOnlyDragonAndPedestalMembers(SelectionGroup group)
        {
            if (group == null) return;
            for (int i = group.Members.Count - 1; i >= 0; i--)
            {
                int mobyIndex = group.Members[i];
                if (mobyIndex < 0 || mobyIndex >= mobys.Count || !IsDragonOrPedestalMoby(mobys[mobyIndex]))
                    group.Members.RemoveAt(i);
            }
        }

        private string ActiveSelectionGroupKey()
        {
            SelectionGroup group = ActiveSelectionGroup();
            return group == null ? "" : group.Key;
        }

        private void RefreshSelectionGroupBox(string preferredKey)
        {
            if (selectionGroupBox == null) return;

            updatingGroupSelection = true;
            try
            {
                selectionGroupBox.BeginUpdate();
                selectionGroupBox.Items.Clear();
                selectionGroupBox.Items.Add("All smart groups");
                int selected = 0;
                for (int i = 0; i < selectionGroups.Count; i++)
                {
                    SelectionGroup group = selectionGroups[i];
                    selectionGroupBox.Items.Add(group.DisplayName);
                    if (!string.IsNullOrEmpty(preferredKey) && string.Equals(group.Key, preferredKey, StringComparison.Ordinal))
                        selected = i + 1;
                }
                selectionGroupBox.SelectedIndex = selected;
                activeSelectionGroupIndex = selected <= 0 ? -1 : selected - 1;
            }
            finally
            {
                selectionGroupBox.EndUpdate();
                updatingGroupSelection = false;
            }
            UpdateGroupNavigationButtons();
        }

        private SelectionGroup ActiveSelectionGroup()
        {
            if (activeSelectionGroupIndex < 0 || activeSelectionGroupIndex >= selectionGroups.Count)
                return null;
            return selectionGroups[activeSelectionGroupIndex];
        }

        private void FocusActiveSelectionGroup()
        {
            SelectionGroup group = ActiveSelectionGroup();
            if (group == null || group.Members.Count == 0) return;
            if (selectedMobyIndex >= 0 && group.Contains(selectedMobyIndex)) return;
            SelectMoby(group.Members[0]);
            statusLabel.Text = "Selected " + group.DisplayName + ".";
        }

        private void SelectAdjacentGroupMember(int direction)
        {
            SelectionGroup group = ActiveSelectionGroup();
            if (group == null && selectedMobyIndex >= 0)
            {
                group = BestSelectionGroupForMoby(selectedMobyIndex);
                if (group != null)
                    ActivateSelectionGroup(group.Key);
            }
            if (group == null || group.Members.Count == 0) return;

            int current = group.Members.IndexOf(selectedMobyIndex);
            if (current < 0)
                current = direction >= 0 ? -1 : 0;
            int next = current + (direction >= 0 ? 1 : -1);
            if (next < 0) next = group.Members.Count - 1;
            if (next >= group.Members.Count) next = 0;

            int mobyIndex = group.Members[next];
            SelectMoby(mobyIndex);
            statusLabel.Text = "Selected " + MobyId(mobys[mobyIndex]) + " in " + group.DisplayName + ".";
        }

        private void ActivateSelectionGroup(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 0; i < selectionGroups.Count; i++)
            {
                if (!string.Equals(selectionGroups[i].Key, key, StringComparison.Ordinal)) continue;
                activeSelectionGroupIndex = i;
                if (selectionGroupBox != null)
                {
                    updatingGroupSelection = true;
                    try { selectionGroupBox.SelectedIndex = i + 1; }
                    finally { updatingGroupSelection = false; }
                }
                RefreshMobyList();
                UpdateGroupNavigationButtons();
                return;
            }
        }

        private void UpdateGroupNavigationButtons()
        {
            bool enabled = false;
            SelectionGroup active = ActiveSelectionGroup();
            if (active != null && active.Members.Count > 1)
                enabled = true;
            else if (selectedMobyIndex >= 0)
            {
                SelectionGroup group = BestSelectionGroupForMoby(selectedMobyIndex);
                enabled = group != null && group.Members.Count > 1;
            }
            if (previousGroupMemberButton != null) previousGroupMemberButton.Enabled = enabled;
            if (nextGroupMemberButton != null) nextGroupMemberButton.Enabled = enabled;
        }

        private bool IsInActiveSelectionGroup(int mobyIndex)
        {
            SelectionGroup group = ActiveSelectionGroup();
            return group != null && group.Contains(mobyIndex);
        }

        private SelectionGroup BestSelectionGroupForMoby(int mobyIndex)
        {
            SelectionGroup best = null;
            for (int i = 0; i < selectionGroups.Count; i++)
            {
                SelectionGroup group = selectionGroups[i];
                if (!group.Contains(mobyIndex)) continue;
                if (group.LinkedMove) return group;
                if (best == null || group.Members.Count < best.Members.Count)
                    best = group;
            }
            return best;
        }

        private SelectionGroup LinkedMoveSelectionGroupForMoby(int mobyIndex)
        {
            for (int i = 0; i < selectionGroups.Count; i++)
            {
                SelectionGroup group = selectionGroups[i];
                if (group.LinkedMove && group.Contains(mobyIndex))
                    return group;
            }
            return null;
        }

        private void AddIdentitySelectionGroups()
        {
            Dictionary<string, SelectionGroup> byKey = new Dictionary<string, SelectionGroup>();
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                string identity = SmartIdentityName(moby);
                if (string.IsNullOrEmpty(identity)) continue;
                string category = CatalogCategory(moby);
                string key = "identity:" + category.ToLowerInvariant() + ":" + identity.ToLowerInvariant();
                SelectionGroup group;
                if (!byKey.TryGetValue(key, out group))
                {
                    group = new SelectionGroup(key, category + ": " + identity, false);
                    byKey[key] = group;
                }
                group.Add(i);
            }

            List<SelectionGroup> groups = new List<SelectionGroup>(byKey.Values);
            groups.Sort(CompareSelectionGroupNames);
            for (int i = 0; i < groups.Count; i++)
                AddSelectionGroup(groups[i], false);
        }

        private void AddAreaSelectionGroups()
        {
            Dictionary<string, SelectionGroup> byKey = new Dictionary<string, SelectionGroup>();
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                string zone = moby.Zone ?? "";
                if (zone.IndexOf("runtime cluster", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string key = "area:" + zone.ToLowerInvariant();
                SelectionGroup group;
                if (!byKey.TryGetValue(key, out group))
                {
                    group = new SelectionGroup(key, "Area: " + zone, false);
                    byKey[key] = group;
                }
                group.Add(i);
            }

            List<SelectionGroup> groups = new List<SelectionGroup>(byKey.Values);
            groups.Sort(CompareSelectionGroupNames);
            for (int i = 0; i < groups.Count; i++)
                AddSelectionGroup(groups[i], false);
        }

        private void AddSelectionGroupByTrueIndexes(string name, string key, bool linkedMove, int[] trueIndexes)
        {
            SelectionGroup group = new SelectionGroup(key, name, linkedMove);
            for (int i = 0; i < trueIndexes.Length; i++)
            {
                int mobyIndex = FindMobyIndexByTrueIndex(trueIndexes[i]);
                if (mobyIndex >= 0)
                    group.Add(mobyIndex);
            }
            AddSelectionGroup(group, linkedMove);
        }

        private void AddSelectionGroupByPredicate(string name, string key, bool linkedMove, Predicate<Moby> predicate)
        {
            SelectionGroup group = new SelectionGroup(key, name, linkedMove);
            for (int i = 0; i < mobys.Count; i++)
            {
                if (predicate(mobys[i]))
                    group.Add(i);
            }
            AddSelectionGroup(group, false);
        }

        private bool AddSelectionGroup(SelectionGroup group, bool allowSingle)
        {
            if (group == null) return false;
            group.Sort();
            if (!allowSingle && group.Members.Count < 2) return false;
            for (int i = 0; i < selectionGroups.Count; i++)
            {
                if (string.Equals(selectionGroups[i].Key, group.Key, StringComparison.Ordinal))
                    return false;
            }
            selectionGroups.Add(group);
            return true;
        }

        private int FindMobyIndexByTrueIndex(int trueIndex)
        {
            for (int i = 0; i < mobys.Count; i++)
            {
                if (mobys[i].TrueIndex == trueIndex)
                    return i;
            }
            return -1;
        }

        private static int CompareSelectionGroupNames(SelectionGroup a, SelectionGroup b)
        {
            return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static string SmartIdentityName(Moby moby)
        {
            if (moby == null) return "";
            string text = MobySearchText(moby);
            if (HasAny(text, "purple gem", "25-gem", "gem (25)", "(25)")) return "Purple gems (25)";
            if (HasAny(text, "yellow gem", "10-gem", "gem (10)", "(10)")) return "Yellow gems (10)";
            if (HasAny(text, "blue gem", "5-gem", "gem (5)", "(5)")) return "Blue gems (5)";
            if (HasAny(text, "green gem", "2-gem", "gem (2)", "(2)")) return "Green gems (2)";
            if (HasAny(text, "red gem", "1-gem", "gem (1)", "(1)")) return "Red gems (1)";
            if (text.IndexOf("chest content", StringComparison.Ordinal) >= 0 || text.IndexOf("linked reward marker", StringComparison.Ordinal) >= 0) return "Locked chest contents";
            if (text.IndexOf("locked treasure chest", StringComparison.Ordinal) >= 0) return "Locked treasure chest";
            if (text.IndexOf("life chest", StringComparison.Ordinal) >= 0) return "Life chest";
            if (text.IndexOf("flame/charge chest", StringComparison.Ordinal) >= 0 && text.IndexOf("green gem", StringComparison.Ordinal) >= 0) return "Flame/charge chests with green reward";
            if (text.IndexOf("charge chest", StringComparison.Ordinal) >= 0 && text.IndexOf("green gem", StringComparison.Ordinal) >= 0) return "Charge chests with green reward";
            if (text.IndexOf("flame/charge chest", StringComparison.Ordinal) >= 0 || text.IndexOf("flame-or-charge", StringComparison.Ordinal) >= 0) return "Flame/charge chests";
            if (text.IndexOf("charge chest", StringComparison.Ordinal) >= 0 || text.IndexOf("charge-only chest", StringComparison.Ordinal) >= 0) return "Charge chests";
            if (text.IndexOf("shepherd", StringComparison.Ordinal) >= 0 || text.IndexOf("shepard", StringComparison.Ordinal) >= 0) return "Shepherds";
            if (text.IndexOf("ram", StringComparison.Ordinal) >= 0) return "Rams";
            if (text.IndexOf("sheep", StringComparison.Ordinal) >= 0 || text.IndexOf("fodder", StringComparison.Ordinal) >= 0) return "Sheep/fodder";
            if (text.IndexOf("dragon pedestal", StringComparison.Ordinal) >= 0) return "Dragon pedestals";
            if (text.IndexOf("dragon actor", StringComparison.Ordinal) >= 0 || text.IndexOf("dragon model", StringComparison.Ordinal) >= 0) return "Dragon statues";
            if (text.IndexOf("whirlwind", StringComparison.Ordinal) >= 0) return "Whirlwinds";
            if (text.IndexOf("return home", StringComparison.Ordinal) >= 0) return "Return Home platform";
            if (text.IndexOf("portal sound trigger", StringComparison.Ordinal) >= 0) return "Portal sound triggers";
            if (text.IndexOf("camera", StringComparison.Ordinal) >= 0) return "Camera points";
            if (text.IndexOf("taller 2 ball tree", StringComparison.Ordinal) >= 0 || text.IndexOf("tall 2 ball tree", StringComparison.Ordinal) >= 0) return "Tall 2-ball trees";
            if (text.IndexOf("wider tree", StringComparison.Ordinal) >= 0 || text.IndexOf("wide tree", StringComparison.Ordinal) >= 0) return "Wider trees";
            if (text.IndexOf("skinny tree", StringComparison.Ordinal) >= 0) return "Skinny trees";
            if (text.IndexOf("lamp", StringComparison.Ordinal) >= 0) return "Lamps";
            if (text.IndexOf("flag", StringComparison.Ordinal) >= 0) return "Flags";
            if (text.IndexOf("nonvisual", StringComparison.Ordinal) >= 0 || text.IndexOf("placeholder", StringComparison.Ordinal) >= 0) return "Nonvisual controls";
            if (text.IndexOf("invisible", StringComparison.Ordinal) >= 0) return "Invisible helpers";
            return moby.DisplayLabel;
        }

        private bool ShouldShowMoby(int mobyIndex, Moby moby)
        {
            if (moby == null) return false;
            SelectionGroup activeGroup = ActiveSelectionGroup();
            if (activeGroup != null && !activeGroup.Contains(mobyIndex))
                return false;

            string filter = CatalogAllObjects;
            if (catalogFilterBox != null && catalogFilterBox.SelectedItem != null)
                filter = catalogFilterBox.SelectedItem.ToString();

            if (string.Equals(filter, CatalogEdited, StringComparison.Ordinal))
            {
                if (!moby.IsEdited) return false;
            }
            else if (string.Equals(filter, CatalogNeedsId, StringComparison.Ordinal))
            {
                if (!NeedsIdentityWork(moby)) return false;
            }
            else if (!string.Equals(filter, CatalogAllObjects, StringComparison.Ordinal))
            {
                if (!string.Equals(CatalogCategory(moby), filter, StringComparison.Ordinal))
                    return false;
            }

            string query = catalogSearchBox == null ? "" : (catalogSearchBox.Text ?? "").Trim();
            if (query.Length == 0) return true;

            string search = (MobyId(moby) + " "
                + MobyIconText(moby) + " "
                + CatalogCategory(moby) + " "
                + moby.DisplayLabel + " "
                + "0x" + moby.Type.ToString("X2") + " "
                + (moby.Kind ?? "") + " "
                + (moby.Zone ?? "") + " "
                + (moby.Confidence ?? "")).ToLowerInvariant();
            string[] terms = query.ToLowerInvariant().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < terms.Length; i++)
            {
                if (search.IndexOf(terms[i], StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }

        private static bool NeedsIdentityWork(Moby moby)
        {
            if (moby == null) return false;
            string confidence = (moby.Confidence ?? "").ToLowerInvariant();
            string text = MobySearchText(moby);
            if (confidence.Length == 0 || confidence == "unknown" || confidence == "weak" || confidence.IndexOf("inferred", StringComparison.Ordinal) >= 0)
                return true;
            return HasAny(text, "candidate", "name pending", "unidentified", "unknown", "nonvisual control", "linked helper");
        }

        private static string CatalogCategory(Moby moby)
        {
            switch (GetMobyIconKind(moby))
            {
                case MobyIconKind.Gem: return CatalogGems;
                case MobyIconKind.Chest: return CatalogChests;
                case MobyIconKind.SpringChest: return CatalogChests;
                case MobyIconKind.LockedChest: return CatalogChests;
                case MobyIconKind.BlastChest: return CatalogChests;
                case MobyIconKind.Dragon:
                case MobyIconKind.Pedestal:
                case MobyIconKind.Fairy:
                    return CatalogDragons;
                case MobyIconKind.Whirlwind: return CatalogWhirlwinds;
                case MobyIconKind.Key: return CatalogKeys;
                case MobyIconKind.Camera: return CatalogCameras;
                case MobyIconKind.Enemy: return CatalogEnemies;
                case MobyIconKind.Scenery: return CatalogScenery;
                case MobyIconKind.Portal: return CatalogPortals;
                case MobyIconKind.Helper: return CatalogHelpers;
                default: return CatalogOther;
            }
        }

        private static string GetWorkStatus(Moby moby)
        {
            if (moby == null) return "";
            if (moby.Patchable && HasFunctionalRisk(moby)) return "Func test";
            if (!string.IsNullOrEmpty(moby.BehaviorNote)) return "Linked";
            if (moby.Patchable) return "Source";
            return "RAM only";
        }

        private static string MobyIconText(Moby moby)
        {
            switch (GetMobyIconKind(moby))
            {
                case MobyIconKind.Gem: return GemIconText(moby);
                case MobyIconKind.Chest: return "Box";
                case MobyIconKind.SpringChest: return "Spr";
                case MobyIconKind.LockedChest: return "Lck";
                case MobyIconKind.BlastChest: return "Bst";
                case MobyIconKind.Dragon: return "Drg";
                case MobyIconKind.Pedestal: return "Ped";
                case MobyIconKind.Fairy: return "Lgt";
                case MobyIconKind.Whirlwind: return "Whl";
                case MobyIconKind.Key: return "Key";
                case MobyIconKind.Camera: return "Cam";
                case MobyIconKind.Enemy: return "Act";
                case MobyIconKind.Scenery: return "Scn";
                case MobyIconKind.Portal: return "Prt";
                case MobyIconKind.Helper: return "Ctl";
                default: return "Mby";
            }
        }

        private static string GemIconText(Moby moby)
        {
            if (moby != null && moby.HasRewardColorEdit)
                return GemIconTextForColor(moby.RewardColorName);
            string text = MobySearchText(moby);
            if (HasAny(text, "purple gem", "25-gem", "25 value", "gem (25)", "(25)")) return "P25";
            if (HasAny(text, "yellow gem", "10-gem", "10 value", "gem (10)", "(10)")) return "Y10";
            if (HasAny(text, "blue gem", "5-gem", "5 value", "gem (5)", "(5)")) return "B5";
            if (HasAny(text, "green gem", "2-gem", "2 value", "gem (2)", "(2)")) return "G2";
            if (HasAny(text, "red gem", "1-gem", "1 value", "gem (1)", "(1)")) return "R1";
            return "Gem";
        }

        private static string GemIconTextForColor(string color)
        {
            if (string.Equals(color, "purple", StringComparison.OrdinalIgnoreCase)) return "P25";
            if (string.Equals(color, "yellow", StringComparison.OrdinalIgnoreCase)) return "Y10";
            if (string.Equals(color, "blue", StringComparison.OrdinalIgnoreCase)) return "B5";
            if (string.Equals(color, "green", StringComparison.OrdinalIgnoreCase)) return "G2";
            return "R1";
        }

        private static bool IsChestContentMarker(Moby moby)
        {
            if (moby == null) return false;
            if (moby.Type != 0x00 || moby.Flag4A != 0xFF || GemValueFromGemIdByte(moby.Flag4B) == 0)
                return false;
            string text = MobySearchText(moby);
            return HasAny(text, "chest content", "contained gem", "linked reward marker", "reward marker", "gem explosion");
        }

        private enum MobyIconKind
        {
            Generic,
            Gem,
            Chest,
            SpringChest,
            LockedChest,
            BlastChest,
            Dragon,
            Pedestal,
            Fairy,
            Whirlwind,
            Key,
            Camera,
            Enemy,
            Scenery,
            Portal,
            Helper
        }

        private static MobyIconKind GetMobyIconKind(Moby moby)
        {
            if (moby == null) return MobyIconKind.Generic;
            string text = MobySearchText(moby);
            if (IsChestContentMarker(moby)) return MobyIconKind.Gem;
            if (text.IndexOf("camera", StringComparison.Ordinal) >= 0 || text.IndexOf("view point", StringComparison.Ordinal) >= 0 || text.IndexOf("viewpoint", StringComparison.Ordinal) >= 0) return MobyIconKind.Camera;
            if (HasAny(text, "nonvisual", "invisible", "control", "helper") && !IsDragonOrPedestalIdentityText(text)) return MobyIconKind.Helper;
            if (text.IndexOf("whirlwind", StringComparison.Ordinal) >= 0) return MobyIconKind.Whirlwind;
            if (text.IndexOf("fairy", StringComparison.Ordinal) >= 0 || text.IndexOf("pedestal light", StringComparison.Ordinal) >= 0 || text.IndexOf("pedestal-light", StringComparison.Ordinal) >= 0) return MobyIconKind.Fairy;
            if (IsDragonPedestalIdentityText(text)) return MobyIconKind.Pedestal;
            if (IsDragonActorIdentityText(text)) return MobyIconKind.Dragon;
            if (text.IndexOf("key", StringComparison.Ordinal) >= 0) return MobyIconKind.Key;
            if (IsSpringChestLike(moby)) return MobyIconKind.SpringChest;
            if (IsLockedChestLike(moby)) return MobyIconKind.LockedChest;
            if (IsBlastChestLike(moby)) return MobyIconKind.BlastChest;
            if (text.IndexOf("chest", StringComparison.Ordinal) >= 0 || text.IndexOf("container", StringComparison.Ordinal) >= 0 || text.IndexOf("box", StringComparison.Ordinal) >= 0) return MobyIconKind.Chest;
            if (text.IndexOf("gem", StringComparison.Ordinal) >= 0 || text.IndexOf("treasure", StringComparison.Ordinal) >= 0 || text.IndexOf("collectible", StringComparison.Ordinal) >= 0) return MobyIconKind.Gem;
            if (text.IndexOf("enemy", StringComparison.Ordinal) >= 0 || text.IndexOf("fodder", StringComparison.Ordinal) >= 0 || text.IndexOf("sheep", StringComparison.Ordinal) >= 0 || text.IndexOf("shepherd", StringComparison.Ordinal) >= 0 || text.IndexOf("shepard", StringComparison.Ordinal) >= 0 || text.IndexOf("ram", StringComparison.Ordinal) >= 0 || text.IndexOf("thief", StringComparison.Ordinal) >= 0) return MobyIconKind.Enemy;
            if (text.IndexOf("balloonist", StringComparison.Ordinal) >= 0 || text.IndexOf("transport npc", StringComparison.Ordinal) >= 0) return MobyIconKind.Portal;
            if (text.IndexOf("scenery", StringComparison.Ordinal) >= 0 || text.IndexOf("tree", StringComparison.Ordinal) >= 0 || text.IndexOf("lamp", StringComparison.Ordinal) >= 0 || text.IndexOf("flag", StringComparison.Ordinal) >= 0) return MobyIconKind.Scenery;
            if (text.IndexOf("portal", StringComparison.Ordinal) >= 0 || text.IndexOf("return home", StringComparison.Ordinal) >= 0 || text.IndexOf("sound trigger", StringComparison.Ordinal) >= 0) return MobyIconKind.Portal;
            if (moby.Type == 0x00 || text.IndexOf("nonvisual", StringComparison.Ordinal) >= 0 || text.IndexOf("invisible", StringComparison.Ordinal) >= 0 || text.IndexOf("control", StringComparison.Ordinal) >= 0 || text.IndexOf("helper", StringComparison.Ordinal) >= 0) return MobyIconKind.Helper;
            return MobyIconKind.Generic;
        }

        private static string MobySearchText(Moby moby)
        {
            if (moby == null) return "";
            return ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.Zone ?? "") + " " + (moby.Evidence ?? "")).ToLowerInvariant();
        }

        private void UpdateGemButtons(Moby moby)
        {
            bool canEdit = CanEditGemColor(moby) || CanEditRewardColor(moby);
            if (setRedGemButton != null)
                setRedGemButton.Enabled = canEdit;
            if (setGreenGemButton != null)
                setGreenGemButton.Enabled = canEdit;
            if (setBlueGemButton != null)
                setBlueGemButton.Enabled = canEdit;
            if (setYellowGemButton != null)
                setYellowGemButton.Enabled = canEdit;
            if (setPurpleGemButton != null)
                setPurpleGemButton.Enabled = canEdit;
        }

        private void UpdateMutationButtons(Moby moby)
        {
            bool canUseSelectedSlot = moby != null && moby.Patchable && moby.TrueIndex >= 0 && moby.TrueIndex < SourceRecordCountForLevel(currentLevelKey);
            if (copyMutationSourceButton != null)
                copyMutationSourceButton.Enabled = canUseSelectedSlot;
            if (hideSelectedButton != null)
                hideSelectedButton.Enabled = canUseSelectedSlot;
            if (pasteMutationButton != null)
                pasteMutationButton.Enabled = canUseSelectedSlot && mutationClipboardMobyIndex >= 0 && mutationClipboardMobyIndex < mobys.Count && mutationClipboardMobyIndex != selectedMobyIndex;
            if (testSelectedAppendButton != null)
                testSelectedAppendButton.Enabled = currentLevelSupportsSourcePatchers && moby != null && moby.IsAppendedRecord && moby.TrueIndex >= SourceRecordCountForLevel(currentLevelKey);
            if (editContentsButton != null)
                editContentsButton.Enabled = CanOpenChestContentEditor(selectedMobyIndex);
            UpdateAddObjectButton();
        }

        private void RefreshObjectAddChoices()
        {
            if (addTemplateBox == null || addSlotBox == null) return;
            int previousTemplate = SelectedChoiceIndex(addTemplateBox);
            int previousSlot = SelectedChoiceIndex(addSlotBox);
            updatingAddObjectChoices = true;
            try
            {
                addTemplateBox.BeginUpdate();
                addTemplateBox.Items.Clear();
                foreach (MobyChoice choice in BuildAddTemplateChoices())
                    addTemplateBox.Items.Add(choice);
                SelectChoice(addTemplateBox, previousTemplate);
                addTemplateBox.EndUpdate();
                UpdateComboBoxDropDownWidth(addTemplateBox, 720);

                addSlotBox.BeginUpdate();
                addSlotBox.Items.Clear();
                foreach (MobyChoice choice in BuildAddSlotChoices())
                    addSlotBox.Items.Add(choice);
                int preferredSlot = previousSlot != -1 ? previousSlot : AppendObjectChoiceIndex;
                SelectChoice(addSlotBox, preferredSlot);
                addSlotBox.EndUpdate();
                UpdateComboBoxDropDownWidth(addSlotBox, 560);
            }
            finally
            {
                updatingAddObjectChoices = false;
            }
            UpdateAddObjectButton();
        }

        private List<MobyChoice> BuildAddTemplateChoices()
        {
            List<MobyChoice> choices = new List<MobyChoice>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                if (ShouldSkipLocalAddTemplate(moby)) continue;
                if (!CanUseAsAddTemplate(moby)) continue;
                string key = CatalogCategory(moby) + "|" + moby.DisplayLabel + "|" + moby.Type.ToString("X2") + "|" + moby.State.ToString("X2") + "|" + moby.Flag4A.ToString("X2") + "|" + moby.Flag4B.ToString("X2");
                if (seen.Contains(key)) continue;
                seen.Add(key);
                choices.Add(new MobyChoice(i, TrueAddSafetyPrefix(moby) + " - " + CatalogCategory(moby).Replace("/", "+") + ": " + moby.DisplayLabel + " (" + MobyId(moby) + ")"));
            }
            int templateOffset = 0;
            foreach (ObjectTemplate template in objectTemplates)
            {
                if (template == null || !template.ShowInAddList) continue;
                if (IsTemplateFromCurrentLevel(template)) continue;
                if (!CanOfferExternalTemplate(template)) continue;
                string key = "external|" + template.Id;
                if (seen.Contains(key)) continue;
                seen.Add(key);
                choices.Add(new MobyChoice(ExternalTemplateChoiceIndexBase - templateOffset, "Object Library - " + template.Category.Replace("/", "+") + ": " + template.DisplayName + " (" + template.SourceDescription + ")", template));
                templateOffset++;
            }
            choices.Sort(CompareMobyChoices);
            return choices;
        }

        private bool ShouldSkipLocalAddTemplate(Moby moby)
        {
            if (moby == null) return false;
            if (string.Equals(currentLevelKey, "artisans", StringComparison.OrdinalIgnoreCase)
                && moby.SpecialDataPointer == 0x80148370
                && MobySearchText(moby).IndexOf("spring chest", StringComparison.Ordinal) >= 0)
                return true;
            return false;
        }

        private bool IsTemplateFromCurrentLevel(ObjectTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(currentLevelKey)) return false;
            string current = SpyroLevelCatalog.NormalizeKey(currentLevelKey);
            return SpyroLevelCatalog.NormalizeKey(template.SourceLevelKey) == current
                || SpyroLevelCatalog.NormalizeKey(template.SourceLevelSlug) == current
                || SpyroLevelCatalog.NormalizeKey(template.SourceLevelName) == current;
        }

        private bool CanOfferExternalTemplate(ObjectTemplate template)
        {
            if (template == null) return false;
            if (template.SourceTrueIndex < 0) return false;
            if (IsBlockedExternalTemplate(template)) return false;
            string family = (template.Family ?? "").Trim().ToLowerInvariant();
            if (family == "chestcontent") return false;
            return family == "key" || family == "lockedchest" || family == "springchest";
        }

        private static bool IsBlockedExternalTemplate(ObjectTemplate template)
        {
            if (template == null) return false;
            string status = ((template.AddSupportStatus ?? "") + " " + (template.TestedStatus ?? "")).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(status)) return false;
            return status.StartsWith("blocked", StringComparison.Ordinal)
                || status.IndexOf("missing-target-actor-package", StringComparison.Ordinal) >= 0
                || status.IndexOf("missing actor package", StringComparison.Ordinal) >= 0;
        }

        private List<MobyChoice> BuildAddSlotChoices()
        {
            List<MobyChoice> choices = new List<MobyChoice>();
            HashSet<int> added = new HashSet<int>();
            int nextAppend = NextAppendTrueIndex();
            if (nextAppend >= SourceRecordCountForLevel(currentLevelKey))
                choices.Add(new MobyChoice(AppendObjectChoiceIndex, "New source record: T" + nextAppend.ToString() + " (true add)"));

            if (selectedMobyIndex >= 0 && selectedMobyIndex < mobys.Count && CanUseAsAddSlot(mobys[selectedMobyIndex]) && IsLikelyReusableSlot(mobys[selectedMobyIndex]))
            {
                choices.Add(new MobyChoice(selectedMobyIndex, "Selected: " + MobyId(mobys[selectedMobyIndex]) + " " + mobys[selectedMobyIndex].DisplayLabel));
                added.Add(selectedMobyIndex);
            }

            for (int i = 0; i < mobys.Count; i++)
            {
                if (added.Contains(i) || !CanUseAsAddSlot(mobys[i]) || !IsLikelyReusableSlot(mobys[i])) continue;
                choices.Add(new MobyChoice(i, "Reusable: " + MobyId(mobys[i]) + " " + mobys[i].DisplayLabel));
                added.Add(i);
            }
            return choices;
        }

        private int NextAppendTrueIndex()
        {
            int next = SourceRecordCountForLevel(currentLevelKey);
            foreach (Moby moby in mobys)
            {
                if (moby != null && moby.IsAppendedRecord && moby.TrueIndex >= next)
                    next = moby.TrueIndex + 1;
            }
            return next;
        }

        private static int CompareMobyChoices(MobyChoice a, MobyChoice b)
        {
            return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanUseAsAddTemplate(Moby moby)
        {
            if (moby == null || !moby.Patchable || moby.TrueIndex < 0) return false;
            if (moby.IsAppendedRecord) return false;
            if (IsChestContentMarker(moby)) return false;
            string category = CatalogCategory(moby);
            if (string.Equals(category, CatalogHelpers, StringComparison.Ordinal) || string.Equals(category, CatalogPortals, StringComparison.Ordinal) || string.Equals(category, CatalogCameras, StringComparison.Ordinal))
                return false;
            string text = MobySearchText(moby);
            if (text.IndexOf("invisible", StringComparison.Ordinal) >= 0 || text.IndexOf("nonvisual", StringComparison.Ordinal) >= 0 || text.IndexOf("control", StringComparison.Ordinal) >= 0)
                return false;
            return true;
        }

        private static string TrueAddSafetyPrefix(Moby moby)
        {
            if (IsLevelLocalRewardChestTemplate(moby)) return "Supported";
            return IsExperimentalTrueAddTemplate(moby) ? "Experimental" : "Simple";
        }

        private static bool IsLevelLocalRewardChestTemplate(Moby moby)
        {
            if (moby == null) return false;
            if (moby.Type != 0x20 || moby.Flag4A != 0x10) return false;
            if (GemValueFromGemIdByte(moby.Flag4B) <= 0 && !moby.HasRewardColorEdit) return false;

            string text = MobySearchText(moby);
            if (HasAny(text, "locked chest", "unlock chest", "spring chest", "life chest", "extra life"))
                return false;
            return HasAny(text, "chest", "box", "container", "charge", "flame");
        }

        private static bool IsExperimentalTrueAddTemplate(Moby moby)
        {
            if (moby == null) return true;
            string text = MobySearchText(moby);
            if (moby.Type != 0x18) return true;
            if (moby.Type == 0x00) return true;
            if (HasAny(text,
                "dragon", "pedestal", "fairy", "whirlwind", "return home", "portal", "sound trigger",
                "enemy", "ram", "shepherd", "gnorc", "thief", "sheep", "fodder",
                "chest", "treasure", "life chest", "locked", "charge", "flame", "key", "balloon"))
                return true;
            if (moby.Type == 0x20 && moby.SpecialDataPointer != 0)
                return true;
            return false;
        }

        private static bool IsExternalChestTemplate(Moby moby)
        {
            if (moby == null) return false;
            string family = (moby.AppendSourceFamily ?? "").Trim();
            if (string.Equals(family, "lockedChest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase))
                return true;

            string text = MobySearchText(moby);
            return HasAny(text, "locked chest", "unlock chest", "spring chest");
        }

        private static bool IsExternalLockedChestTemplate(Moby moby)
        {
            if (moby == null) return false;
            string family = (moby.AppendSourceFamily ?? "").Trim();
            if (string.Equals(family, "lockedChest", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase)) return false;

            string text = MobySearchText(moby);
            return HasAny(text, "locked chest", "unlock chest") && !HasAny(text, "spring chest");
        }

        private static bool IsExternalSpringChestTemplate(Moby moby)
        {
            if (moby == null) return false;
            string family = (moby.AppendSourceFamily ?? "").Trim();
            if (string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase)) return true;

            string text = MobySearchText(moby);
            return HasAny(text, "spring chest");
        }

        private static bool IsSpringChestObjectTemplate(ObjectTemplate template)
        {
            if (template == null) return false;
            string family = (template.Family ?? "").Trim();
            if (string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase)) return true;
            string text = ((template.DisplayName ?? "") + " " + (template.Label ?? "") + " " + (template.Kind ?? "") + " " + (template.Note ?? "")).ToLowerInvariant();
            return text.IndexOf("spring chest", StringComparison.Ordinal) >= 0;
        }

        private static string TrueAddSafetyWarning(Moby moby)
        {
            if (IsLevelLocalRewardChestTemplate(moby))
                return "Supported: this is a level-local reward chest/box. Create Loader BIN can export it as a true-add, and the gem buttons patch its confirmed +0x53 reward color byte.";
            if (!IsExperimentalTrueAddTemplate(moby))
                return "This looks like a standalone gem/simple loose collectible. It is the safest true-add class we currently export by default.";

            string text = MobySearchText(moby);
            if (HasAny(text, "dragon", "pedestal", "fairy"))
                return "Warning: dragons are linked clusters. Adding one dragon record does not yet clone the pedestal, fairy/control, rescue camera, and save-state linkage, so it can crash or behave incorrectly. Normal BIN export skips this true-add for now.";
            if (text.IndexOf("whirlwind", StringComparison.Ordinal) >= 0)
                return "Warning: whirlwinds are controller-style records. Adding one standalone record can reference missing trigger/activation data and may crash. Normal BIN export skips this true-add for now.";
            if (HasAny(text, "enemy", "ram", "shepherd", "shepard", "gnorc", "norc", "torro", "bull", "thief", "theif", "sheep", "fodder"))
                return "Warning: enemies can share AI/path/reward state with the donor. Create Loader BIN now exports named enemy/fodder true-adds and copies their source special data, but test one new enemy at a time before stacking a lot of them.";
            if (IsExternalLockedChestTemplate(moby))
                return "Paused: cross-level locked chest actor-package/root imports currently soft-lock Artisans even without an appended chest row. Normal exports and Test Selected Add skip this until the package import is fixed.";
            if (IsExternalSpringChestTemplate(moby))
                return "Supported for Artisans and Stone Hill through Test Selected Add BIN: the exporter adds the paired source records, applies the level-specific spring chest package profile, and injects the in-game pop/collect patch. Dark Hollow is deferred because its reward gem currently renders only sparkle with no visible gem mesh.";
            if (HasAny(text, "chest", "treasure", "life chest", "locked", "charge", "flame"))
                return "Warning: level-local reward chests can now export through Create Loader BIN, including reward color edits. Cross-level locked/spring chest templates are still blocked because they need asset/package remap support first.";
            if (moby.Type == 0x00)
                return "Warning: type 0x00 records are usually helpers/controllers, not standalone objects. True-adding these is experimental. Normal BIN export skips this true-add unless you use the isolated Test Selected Add BIN path.";
            return "Warning: this template is not a proven standalone gem or named enemy/fodder. It may have behavior, collision, rendering, or special-data linkage. Normal BIN export skips this true-add unless you use the isolated Test Selected Add BIN path.";
        }

        private static string ObjectTemplateSafetyWarning(ObjectTemplate template)
        {
            if (template == null)
                return "Warning: this Object Library template is experimental until it is tested in-game.";
            string family = (template.Family ?? "").Trim().ToLowerInvariant();
            string suffix = "";
            if (template.LoaderTransformed)
                suffix += " The saved edit will preserve loader-transformed identity bytes from the donor map.";
            if (!string.IsNullOrEmpty(template.DependencyRisk))
                suffix += " Risk note: " + template.DependencyRisk;
            if (IsBlockedExternalTemplate(template))
            {
                string reason = !string.IsNullOrEmpty(template.TestedResult)
                    ? template.TestedResult
                    : "The dependency trace says the target level is missing this object's actor package.";
                string required = !string.IsNullOrEmpty(template.RequiredExporterFeature)
                    ? " Required editor support: " + template.RequiredExporterFeature + "."
                    : "";
                return "Object Library template is blocked for adding right now. " + reason + required + suffix;
            }
            if (family == "key")
                return "Object Library key template. Export copies the donor key record from the user's legal ROM and forces the loader-transformed key identity bytes. Normal Create Loader BIN skips cross-level Object Library objects; use Test Selected Add BIN first." + suffix;
            if (family == "springchest")
                return "Object Library spring chest template. Artisans and Stone Hill exports create a controller/shell pair and Test Selected Add BIN injects the working in-game pop/collect patch automatically. Dark Hollow is deferred for now because the reward mesh is invisible there." + suffix;
            if (family == "lockedchest")
                return "Paused Object Library locked chest template. The actor-package/root import currently soft-locks Artisans even in package-only tests, so normal Create Loader BIN and Test Selected Add skip this until the importer is fixed. Add a Key template separately in levels that do not already have a key." + suffix;
            return "Object Library template. Export copies the donor source record from the user's legal ROM; test one new object at a time when using it in a level that never had this object family." + suffix;
        }

        private void PlaceExternalTemplateAtUsefulDefault(Moby moby)
        {
            if (moby == null) return;
            if (selectedMobyIndex >= 0 && selectedMobyIndex < mobys.Count)
            {
                Moby selected = mobys[selectedMobyIndex];
                moby.X = selected.X;
                moby.Y = selected.Y;
                moby.Z = selected.Z;
                return;
            }
            if (geometry != null && !geometry.Bounds.IsEmpty)
            {
                moby.X = geometry.Bounds.Left + (geometry.Bounds.Width / 2f);
                moby.Y = geometry.Bounds.Top + (geometry.Bounds.Height / 2f);
                moby.Z = 0;
            }
        }

        private bool CanUseAsAddSlot(Moby moby)
        {
            return moby != null && moby.Patchable && moby.TrueIndex >= 0 && moby.TrueIndex < SourceRecordCountForLevel(currentLevelKey);
        }

        private static bool IsLikelyReusableSlot(Moby moby)
        {
            if (moby == null) return false;
            if (moby.HasHiddenSlotEdit) return true;
            string text = MobySearchText(moby);
            return text.IndexOf("invisible", StringComparison.Ordinal) >= 0
                || text.IndexOf("nonvisual", StringComparison.Ordinal) >= 0
                || text.IndexOf("placeholder", StringComparison.Ordinal) >= 0
                || text.IndexOf("helper", StringComparison.Ordinal) >= 0
                || text.IndexOf("control", StringComparison.Ordinal) >= 0
                || text.IndexOf("sound trigger", StringComparison.Ordinal) >= 0;
        }

        private static int SelectedChoiceIndex(ComboBox box)
        {
            MobyChoice choice = box == null ? null : box.SelectedItem as MobyChoice;
            return choice == null ? -1 : choice.Index;
        }

        private static void SelectChoice(ComboBox box, int mobyIndex)
        {
            if (box == null || box.Items.Count == 0)
                return;
            for (int i = 0; i < box.Items.Count; i++)
            {
                MobyChoice choice = box.Items[i] as MobyChoice;
                if (choice != null && choice.Index == mobyIndex)
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
            box.SelectedIndex = 0;
        }

        private static MobyChoice SelectedChoice(ComboBox box)
        {
            return box == null ? null : box.SelectedItem as MobyChoice;
        }

        private void UpdateAddObjectButton()
        {
            if (addObjectButton == null) return;
            MobyChoice source = SelectedChoice(addTemplateBox);
            MobyChoice target = SelectedChoice(addSlotBox);
            addObjectButton.Enabled = source != null
                && target != null
                && (source.IsExternalTemplate || (source.Index >= 0 && source.Index < mobys.Count))
                && (target.Index == AppendObjectChoiceIndex || (target.Index >= 0 && target.Index < mobys.Count))
                && (source.IsExternalTemplate ? target.Index == AppendObjectChoiceIndex : source.Index != target.Index);

            if (changeSelectedButton != null)
            {
                bool canChangeSelected = source != null
                    && !source.IsExternalTemplate
                    && selectedMobyIndex >= 0
                    && selectedMobyIndex < mobys.Count
                    && source.Index >= 0
                    && source.Index < mobys.Count
                    && source.Index != selectedMobyIndex
                    && CanUseAsAddTemplate(mobys[source.Index])
                    && CanUseAsAddSlot(mobys[selectedMobyIndex]);
                changeSelectedButton.Enabled = canChangeSelected;
            }
        }

        private void ChangeSelectedMobyToTemplate()
        {
            MobyChoice sourceChoice = SelectedChoice(addTemplateBox);
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
            {
                statusLabel.Text = "Select the moby slot to change first.";
                return;
            }
            if (sourceChoice == null || sourceChoice.Index < 0 || sourceChoice.Index >= mobys.Count)
            {
                statusLabel.Text = "Choose an object template first.";
                return;
            }
            if (sourceChoice.Index == selectedMobyIndex)
            {
                statusLabel.Text = "Choose a different template than the selected moby.";
                return;
            }

            Moby source = mobys[sourceChoice.Index];
            Moby target = mobys[selectedMobyIndex];
            if (!CanUseAsAddTemplate(source) || !CanUseAsAddSlot(target))
            {
                statusLabel.Text = "The template and selected target must both be source-table mobys.";
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "Change " + MobyId(target) + " " + target.DisplayLabel + " into " + MobyId(source) + " " + source.DisplayLabel + "?\n\nThe selected moby keeps its current XYZ, but object behavior, collision, reward, and visuals come from the template record.",
                "Change selected object type",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            target.SetRecordCloneOverride(source);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(selectedMobyIndex);
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Changed " + MobyId(target) + " into " + source.DisplayLabel + ". Save Edits, then Create Loader BIN.";
        }

        private void AddObjectFromTemplateAtClick()
        {
            MobyChoice sourceChoice = SelectedChoice(addTemplateBox);
            MobyChoice targetChoice = SelectedChoice(addSlotBox);
            if (sourceChoice == null || targetChoice == null || (!sourceChoice.IsExternalTemplate && (sourceChoice.Index < 0 || sourceChoice.Index >= mobys.Count)))
            {
                statusLabel.Text = "Choose an object template first.";
                return;
            }
            if (targetChoice.Index != AppendObjectChoiceIndex && (targetChoice.Index < 0 || targetChoice.Index >= mobys.Count))
            {
                statusLabel.Text = "Choose a new append record or a reusable source-table slot.";
                return;
            }
            if (sourceChoice.IsExternalTemplate && targetChoice.Index != AppendObjectChoiceIndex)
            {
                statusLabel.Text = "Object Library templates must be added as new source records.";
                return;
            }
            if (!sourceChoice.IsExternalTemplate && targetChoice.Index != AppendObjectChoiceIndex && sourceChoice.Index == targetChoice.Index)
            {
                statusLabel.Text = "Choose a different slot than the source template.";
                return;
            }

            if (targetChoice.Index == AppendObjectChoiceIndex)
            {
                int appendTrueIndex = NextAppendTrueIndex();
                Moby source = sourceChoice.IsExternalTemplate ? null : mobys[sourceChoice.Index];
                bool experimentalAdd = sourceChoice.IsExternalTemplate || IsExperimentalTrueAddTemplate(source);
                string sourceLabel = sourceChoice.IsExternalTemplate ? sourceChoice.Template.DisplayName : source.DisplayLabel;
                string sourceWarning = sourceChoice.IsExternalTemplate ? ObjectTemplateSafetyWarning(sourceChoice.Template) : TrueAddSafetyWarning(source);
                string donorText = sourceChoice.IsExternalTemplate
                    ? sourceChoice.Template.SourceDescription
                    : MobyId(source);
                if (sourceChoice.IsExternalTemplate && IsSpringChestObjectTemplate(sourceChoice.Template))
                {
                    AddSpringChestPairFromTemplate(sourceChoice.Template, appendTrueIndex, sourceWarning, donorText);
                    return;
                }

                DialogResult appendResult = MessageBox.Show(
                    this,
                    "Add a new " + sourceLabel + " by appending source record T" + appendTrueIndex.ToString() + " from donor " + donorText + "?\n\n" + sourceWarning + "\n\nThis expands the level source moby table. After confirming, click the terrain map to place it, then Save Edits and Create Loader BIN.",
                    experimentalAdd ? "Add experimental true object" : "Add true new object",
                    MessageBoxButtons.YesNo,
                    experimentalAdd ? MessageBoxIcon.Warning : MessageBoxIcon.Question);
                if (appendResult != DialogResult.Yes) return;

                Moby appended = sourceChoice.IsExternalTemplate
                    ? Moby.CreateAppendedFromTemplate(sourceChoice.Template, mobys.Count, appendTrueIndex)
                    : Moby.CreateAppendedFromSource(source, mobys.Count, appendTrueIndex);
                if (sourceChoice.IsExternalTemplate)
                    PlaceExternalTemplateAtUsefulDefault(appended);
                mobys.Add(appended);
                SelectMoby(mobys.Count - 1);
                if (clickPlaceButton != null)
                    clickPlaceButton.Checked = true;
                hasUnsavedEdits = true;
                BuildSelectionGroups();
                RefreshMobyList();
                RefreshObjectAddChoices();
                UpdateInspector();
                canvas.Invalidate();
                statusLabel.Text = "Added " + (experimentalAdd ? "experimental " : "new ") + sourceLabel + " as " + MobyId(appended) + ". Click the terrain map to place it, then Save Edits and Create Loader BIN.";
                return;
            }

            Moby slotSource = mobys[sourceChoice.Index];
            Moby target = mobys[targetChoice.Index];
            DialogResult result = MessageBox.Show(
                this,
                "Add " + slotSource.DisplayLabel + " by cloning " + MobyId(slotSource) + " into reusable slot " + MobyId(target) + " " + target.DisplayLabel + "?\n\nThis is the older slot-reuse path. It does not expand the level's moby table.",
                "Add object using reusable slot",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            target.SetRecordCloneOverride(slotSource);
            SelectMoby(targetChoice.Index);
            if (clickPlaceButton != null)
                clickPlaceButton.Checked = true;
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Added " + slotSource.DisplayLabel + " into " + MobyId(target) + ". Click the terrain map to place it, then Save Edits and Create Loader BIN.";
        }

        private void AddSpringChestPairFromTemplate(ObjectTemplate template, int controllerTrueIndex, string sourceWarning, string donorText)
        {
            if (template == null) return;
            int shellTrueIndex = controllerTrueIndex + 1;
            DialogResult appendResult = MessageBox.Show(
                this,
                "Add a new " + template.DisplayName + " as a paired spring chest?\n\nThis creates controller T" + controllerTrueIndex.ToString() + " and visible shell T" + shellTrueIndex.ToString() + " from donor " + donorText + ".\n\n" + sourceWarning + "\n\nAfter confirming, click the terrain map to place the linked pair, then Save Edits and use Test Selected Add BIN.",
                "Add spring chest pair",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (appendResult != DialogResult.Yes) return;

            Moby controller = Moby.CreateSpringChestControllerFromTemplate(template, mobys.Count, controllerTrueIndex);
            Moby shell = Moby.CreateAppendedFromTemplate(template, mobys.Count + 1, shellTrueIndex);
            PrepareSpringChestPair(controller, shell);
            ApplySpringChestPairExportProfile(controller, shell);

            PlaceExternalTemplateAtUsefulDefault(shell);
            AlignSpringChestControllerToShell(controller, shell);

            mobys.Add(controller);
            mobys.Add(shell);
            SelectMoby(mobys.Count - 1);
            if (clickPlaceButton != null)
                clickPlaceButton.Checked = true;
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Added spring chest pair " + MobyId(controller) + "/" + MobyId(shell) + ". Click the terrain map to place both, then Save Edits and Test Selected Add BIN.";
        }

        private void AddDarkHollowNativeSpringChestFromTemplate(ObjectTemplate template, int appendTrueIndex, string sourceWarning)
        {
            Moby donor = FindDarkHollowNativeSpringChestDonor();
            if (donor == null)
            {
                MessageBox.Show(this, "Dark Hollow native spring chest donor T61 was not found in the loaded level cache. Reload Dark Hollow in the editor, then try adding the Spring Chest again.", "Dark Hollow spring chest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult appendResult = MessageBox.Show(
                this,
                "Add a new " + template.DisplayName + " as a native Dark Hollow spring chest?\n\nThis creates one local 0x00C2 chest record T" + appendTrueIndex.ToString() + " from Dark Hollow donor " + MobyId(donor) + ".\n\n" + sourceWarning + "\n\nAfter confirming, click the terrain map to place it, then Save Edits and use Test Selected Add BIN.",
                "Add Dark Hollow spring chest",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (appendResult != DialogResult.Yes) return;

            Moby appended = Moby.CreateAppendedFromSource(donor, mobys.Count, appendTrueIndex);
            ApplyDarkHollowNativeSpringChestProfile(appended, donor);
            PlaceExternalTemplateAtUsefulDefault(appended);

            mobys.Add(appended);
            SelectMoby(mobys.Count - 1);
            if (clickPlaceButton != null)
                clickPlaceButton.Checked = true;
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Added Dark Hollow native spring chest " + MobyId(appended) + ". Click the terrain map to place it, then Save Edits and Test Selected Add BIN.";
        }

        private bool IsDarkHollowLevel()
        {
            return string.Equals(SpyroLevelCatalog.NormalizeKey(currentLevelKey), "darkhollow", StringComparison.OrdinalIgnoreCase);
        }

        private Moby FindDarkHollowNativeSpringChestDonor()
        {
            if (!IsDarkHollowLevel()) return null;
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                if (moby == null) continue;
                if (moby.TrueIndex == 61 && IsDarkHollowNativeSpringChestRecord(moby))
                    return moby;
            }
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                if (IsDarkHollowNativeSpringChestRecord(moby))
                    return moby;
            }
            return null;
        }

        private static bool IsDarkHollowNativeSpringChestRecord(Moby moby)
        {
            return moby != null &&
                moby.Type == 0x20 &&
                moby.State == 0x00 &&
                moby.SourceByte36 == 0xC2 &&
                moby.SourceByte37 == 0x00 &&
                moby.SourceByte4F == 0x00 &&
                moby.Flag4A == 0x10 &&
                moby.SpecialDataPointer != 0;
        }

        private void ApplyDarkHollowNativeSpringChestProfile(Moby moby, Moby donor)
        {
            if (moby == null) return;
            moby.Type = 0x20;
            moby.State = 0x00;
            moby.SourceByte36 = 0xC2;
            moby.SourceByte37 = 0x00;
            moby.SourceByte4F = 0x00;
            moby.Flag4A = 0x10;
            if (moby.Flag4B < 0x53 || moby.Flag4B > 0x57)
                moby.Flag4B = 0x54;
            moby.Label = "Spring Chest (new)";
            moby.Kind = "Spring Chest";
            moby.Confidence = "editor Dark Hollow native spring chest";
            moby.Evidence = "new native Dark Hollow 0x00C2 spring chest cloned from donor " + (donor == null ? "T61" : MobyId(donor)) + " by Test Selected Add BIN";
            moby.BehaviorNote = "Native Dark Hollow spring/flame-charge chest clone. Export copies a local 0x00C2 donor row and does not use the Artisans/Stone Hill helper pair.";
            moby.SpecialDataNote = "Copies local Dark Hollow spring chest special data during export.";
            moby.AppendSourceTrueIndex = donor == null ? 61 : donor.TrueIndex;
            moby.AppendSourceIndex = donor == null ? -1 : donor.Index;
            moby.AppendSourceLabel = "Dark Hollow native spring chest donor";
            moby.AppendSourceLevelKey = "DarkHollow";
            moby.AppendSourceLevelName = "Dark Hollow";
            moby.AppendSourceFamily = "springChest";
            moby.AppendPackageImportProfile = "";
            moby.AppendRuntimeIdentityPolicy = "darkhollow-native-00c2-spring-chest";
            moby.CaptureBaseIdentity();
        }

        private static void PrepareSpringChestPair(Moby controller, Moby shell)
        {
            if (controller == null || shell == null) return;
            controller.SpringChestPairRole = "controller";
            controller.SpringChestPartnerTrueIndex = shell.TrueIndex;
            shell.SpringChestPairRole = "shell";
            shell.SpringChestPartnerTrueIndex = controller.TrueIndex;

            string policy = "private-town-controller-alias-with-editor-helper-pair";
            controller.AppendPackageImportProfile = "Alias00C2Root14";
            controller.AppendRuntimeIdentityPolicy = policy;
            shell.AppendPackageImportProfile = "Alias00C2Root14";
            shell.AppendRuntimeIdentityPolicy = policy;
            shell.BehaviorNote = (string.IsNullOrEmpty(shell.BehaviorNote) ? "" : shell.BehaviorNote + " ") + "Paired with the spring chest controller; move/export both records together. The Artisans Test Selected Add BIN path injects the pop/collect patch automatically.";
        }

        private static void AlignSpringChestControllerToShell(Moby controller, Moby shell)
        {
            if (controller == null || shell == null) return;
            controller.X = shell.X;
            controller.Y = shell.Y;
            controller.Z = shell.Z;
            controller.OriginalX = shell.OriginalX;
            controller.OriginalY = shell.OriginalY;
            controller.OriginalZ = shell.OriginalZ;
        }

        private void ApplySpringChestPairExportProfile(Moby controller, Moby shell)
        {
            if (controller == null || shell == null) return;
            string levelKey = SpyroLevelCatalog.NormalizeKey(currentLevelKey);
            if (string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
            {
                string policy = "stonehill-local-00c2-controller-with-imported-0149-shell";
                controller.Type = 0x20;
                controller.State = 0x00;
                controller.SourceByte36 = 0xC2;
                controller.SourceByte37 = 0x00;
                controller.SourceByte4F = 0x00;
                controller.Flag4A = 0x10;
                controller.Flag4B = 0x53;
                controller.AppendPackageImportProfile = "Local00C2Shell0149Over000E";
                controller.AppendRuntimeIdentityPolicy = policy;
                controller.BehaviorNote = "Paired with the visible spring chest shell. The Stone Hill in-game patch uses the local 0x00C2 controller row for hit/pop state.";
                shell.Type = 0x20;
                shell.State = 0x00;
                shell.Flag4A = 0x10;
                shell.AppendPackageImportProfile = "Local00C2Shell0149Over000E";
                shell.AppendRuntimeIdentityPolicy = policy;
                shell.BehaviorNote = (string.IsNullOrEmpty(shell.BehaviorNote) ? "" : shell.BehaviorNote + " ") + "Paired with the Stone Hill local 0x00C2 spring controller; move/export both records together. The Stone Hill Test Selected Add BIN path injects the pop/collect patch automatically.";
            }
            else if (string.Equals(levelKey, "darkhollow", StringComparison.OrdinalIgnoreCase))
            {
                controller.BehaviorNote = "Dark Hollow spring chest export is deferred: the helper can create collectible sparkle, but the visible reward gem mesh is not solved yet.";
                shell.BehaviorNote = (string.IsNullOrEmpty(shell.BehaviorNote) ? "" : shell.BehaviorNote + " ") + "Dark Hollow spring chest export is deferred until the invisible reward mesh issue is solved.";
            }
        }

        private void SetSelectedGemColor(string color)
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            Moby moby = mobys[selectedMobyIndex];
            bool standaloneGem = CanEditGemColor(moby);
            bool rewardDrop = CanEditRewardColor(moby);
            if (!standaloneGem && !rewardDrop)
            {
                statusLabel.Text = "Select a confirmed standalone gem or reward-bearing chest/enemy before changing color/value bytes.";
                return;
            }

            if (standaloneGem)
                moby.SetGemColorOverride(color);
            else if (IsPotentialChestContentMarker(moby))
                moby.SetContainedGemColorOverride(color);
            else
                moby.SetRewardColorOverride(color);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(selectedMobyIndex);
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = standaloneGem
                ? "Set " + MobyId(moby) + " to " + moby.GemColorName + " gem (" + moby.GemValueOverride.ToString() + "). Save Edits, then Create Loader BIN."
                : (IsPotentialChestContentMarker(moby)
                    ? "Set " + MobyId(moby) + " contained gem to " + moby.RewardColorName + " (" + moby.RewardValueOverride.ToString() + "). Save Edits, then Create Loader BIN."
                    : "Set " + MobyId(moby) + " reward drop to " + moby.RewardColorName + " gem (" + moby.RewardValueOverride.ToString() + "). Save Edits, then Create Loader BIN.");
        }

        private void CopySelectedMutationSource()
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            Moby source = mobys[selectedMobyIndex];
            if (!source.Patchable || source.TrueIndex < 0 || source.TrueIndex >= SourceRecordCountForLevel(currentLevelKey))
            {
                statusLabel.Text = "Select a source-table moby before copying an object type.";
                return;
            }

            mutationClipboardMobyIndex = selectedMobyIndex;
            UpdateMutationButtons(source);
            statusLabel.Text = "Copied " + MobyId(source) + " " + source.DisplayLabel + " as the type donor.";
            UpdateInspector();
        }

        private void PasteMutationIntoSelected()
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            if (mutationClipboardMobyIndex < 0 || mutationClipboardMobyIndex >= mobys.Count)
            {
                statusLabel.Text = "Copy a source object type first.";
                return;
            }
            if (mutationClipboardMobyIndex == selectedMobyIndex)
            {
                statusLabel.Text = "Pick a different target slot before pasting the copied type.";
                return;
            }

            Moby source = mobys[mutationClipboardMobyIndex];
            Moby target = mobys[selectedMobyIndex];
            if (!source.Patchable || !target.Patchable)
            {
                statusLabel.Text = "Both source and target must be source-table mobys.";
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "Replace " + MobyId(target) + " " + target.DisplayLabel + " with cloned type " + MobyId(source) + " " + source.DisplayLabel + "?\n\nThe target keeps its current XYZ, but behavior/path data may still come from the donor record.",
                "Paste object type",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            target.SetRecordCloneOverride(source);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(selectedMobyIndex);
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Pasted " + MobyId(source) + " type into " + MobyId(target) + ". Save Edits, then Create Loader BIN.";
        }

        private void HideSelectedMobySlot()
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            HideMobySlot(selectedMobyIndex);
        }

        private void HideMobySlot(int mobyIndex)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            Moby moby = mobys[mobyIndex];
            if (moby.IsAppendedRecord)
            {
                RemoveAppendedMoby(mobyIndex, "Removed new object " + MobyId(moby) + ". Save Edits to keep it removed.");
                return;
            }
            if (!moby.Patchable || moby.TrueIndex < 0 || moby.TrueIndex >= SourceRecordCountForLevel(currentLevelKey))
            {
                statusLabel.Text = "Select a source-table moby before hiding a slot.";
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "Hide " + MobyId(moby) + " " + moby.DisplayLabel + " by moving its source record out of the level?\n\nUse Reset Selected to undo before exporting.",
                "Hide object slot",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            moby.SetHiddenSlotOverride();
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(mobyIndex);
            SelectMoby(mobyIndex);
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Hid " + MobyId(moby) + ". Save Edits, then Create Loader BIN.";
        }

        private static bool CanEditGemColor(Moby moby)
        {
            if (moby == null) return false;
            if (moby.Type != 0x18 || moby.SpecialDataPointer != 0 || moby.Flag4A != 0x40 || moby.Flag4B != 0xFF)
                return false;
            string text = MobySearchText(moby);
            if (text.IndexOf("key", StringComparison.Ordinal) >= 0)
                return false;
            return text.IndexOf("gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("1-gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("2-gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("5-gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("10-gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("25-gem", StringComparison.Ordinal) >= 0
                || moby.HasGemColorEdit;
        }

        private static bool CanEditRewardColor(Moby moby)
        {
            if (moby == null) return false;
            if (IsPotentialChestContentMarker(moby))
                return true;
            if (IsLockedChestRewardMarker(moby))
                return true;
            if (moby.Type != 0x20 || moby.Flag4A != 0x10)
                return false;
            if (moby.HasRewardColorEdit)
                return true;
            if (IsSpringChestLike(moby))
                return true;

            string text = MobySearchText(moby);
            if (HasAny(text, "life chest", "extra life", "sheep", "fodder", "dragon", "pedestal", "whirlwind", "tree", "flag", "lamp", "flower", "grass", "balloon", "nonvisual", "control"))
                return false;
            if (text.IndexOf("chest", StringComparison.Ordinal) >= 0)
                return true;
            return HasAny(text, "gnorc", "enemy", "ram", "shepherd", "shepard");
        }

        private static string DescribeEditStatus(Moby moby)
        {
            if (moby == null || !moby.IsEdited) return "unchanged";
            if (moby.IsAppendedRecord && moby.HasGemColorEdit) return "true-added source record and gem value edited";
            if (moby.IsAppendedRecord && moby.HasRewardColorEdit) return "true-added source record and reward edited";
            if (moby.IsAppendedRecord) return "true-added source record";
            if (moby.HasHiddenSlotEdit) return "removed by hiding source-table slot";
            if (moby.HasRecordCloneEdit && moby.HasGemColorEdit) return "cloned object type and gem value edited";
            if (moby.HasRecordCloneEdit && moby.HasRewardColorEdit) return "cloned object type and reward edited";
            if (moby.HasRecordCloneEdit && moby.HasPositionEdit) return "cloned object type and moved";
            if (moby.HasRecordCloneEdit) return "cloned object type into this slot";
            if (moby.HasPositionEdit && moby.HasGemColorEdit && moby.HasRewardColorEdit) return "moved, gem value edited, and reward edited";
            if (moby.HasPositionEdit && moby.HasRewardColorEdit && IsChestContentMarker(moby)) return "moved and contained gem value edited";
            if (moby.HasPositionEdit && moby.HasRewardColorEdit) return "moved and reward edited";
            if (moby.HasPositionEdit && moby.HasGemColorEdit) return "moved and gem value edited";
            if (moby.HasRewardColorEdit && IsChestContentMarker(moby)) return "contained gem value edited";
            if (moby.HasRewardColorEdit) return "reward edited";
            if (moby.HasGemColorEdit) return "gem value edited";
            return "moved in editor";
        }

        private static bool HasAny(string text, params string[] needles)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string needle in needles)
            {
                if (text.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static bool HasFunctionalRisk(Moby moby)
        {
            if (moby == null) return false;
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.BehaviorNote ?? "")).ToLowerInvariant();
            return text.IndexOf("chest", StringComparison.Ordinal) >= 0
                || text.IndexOf("container", StringComparison.Ordinal) >= 0
                || text.IndexOf("collision", StringComparison.Ordinal) >= 0
                || text.IndexOf("contents", StringComparison.Ordinal) >= 0
                || text.IndexOf("interact", StringComparison.Ordinal) >= 0
                || text.IndexOf("ai/home", StringComparison.Ordinal) >= 0
                || text.IndexOf("home anchor", StringComparison.Ordinal) >= 0;
        }

        private void SetEditorMode(EditorMode mode)
        {
            editorMode = mode;
            dragMobyIndex = -1;
            if (mode == EditorMode.Terrain)
                ApplyTerrainModeVisualPreset();
            if (mobyModeButton != null)
                mobyModeButton.Checked = mode == EditorMode.Mobys;
            if (terrainModeButton != null)
                terrainModeButton.Checked = mode == EditorMode.Terrain;
            if (liveApplyButton != null)
                liveApplyButton.Text = mode == EditorMode.Terrain ? "Live Terrain" : "Live Apply";
            if (liveRevertButton != null)
                liveRevertButton.Text = mode == EditorMode.Terrain ? "Revert Terrain" : "Live Revert";
            UpdateInspector();
            canvas.Invalidate();
            if (statusLabel != null)
                statusLabel.Text = mode == EditorMode.Terrain
                    ? "Terrain Mode: filled terrain view enabled; click a face to inspect vertices and height."
                    : "Moby Mode: click or drag objects to edit placement.";
        }

        private void ApplyTerrainModeVisualPreset()
        {
            showFaces = true;
            showLines = true;
            useGameTerrainStyle = true;
            useSurfaceColorAssist = true;
            completeTerrainDraw = true;
            showHeightTint = true;

            if (facesButton != null) facesButton.Checked = true;
            if (linesButton != null) linesButton.Checked = true;
            if (terrainStyleButton != null) terrainStyleButton.Checked = true;
            if (surfaceColorAssistButton != null) surfaceColorAssistButton.Checked = true;
            if (completeTerrainDrawButton != null) completeTerrainDrawButton.Checked = true;
            if (heightTintButton != null) heightTintButton.Checked = true;
        }

        private string EditorModeText()
        {
            return editorMode == EditorMode.Terrain ? "Terrain" : "Moby";
        }

        private static string TerrainTextureAtlasPreferenceText(TerrainTextureAtlasPreference preference)
        {
            switch (preference)
            {
                case TerrainTextureAtlasPreference.HqClose:
                    return "HQ-close 16-tile";
                case TerrainTextureAtlasPreference.HqStandard:
                    return "HQ 4-tile";
                default:
                    return "Auto";
            }
        }

        private static string SourceTextureWindowModeText(SourceTextureWindowMode mode)
        {
            switch (mode)
            {
                case SourceTextureWindowMode.FaceDepthLow4:
                    return "face depth low 4";
                case SourceTextureWindowMode.Word3High4:
                    return "word3 bits 7-10";
                case SourceTextureWindowMode.Word4Mid4:
                    return "word4 bits 8-11";
                case SourceTextureWindowMode.Word4High4:
                    return "word4 bits 12-15";
                default:
                    return "whole texture tile";
            }
        }

        private string SourceTextureStatusText()
        {
            if (!showSourceTextures || terrainTextureAtlas == null)
                return "off";

            string text = terrainTextureAtlasTier;
            if (sourceTextureWindowMode != SourceTextureWindowMode.WholeTile)
                text += "/" + SourceTextureWindowModeText(sourceTextureWindowMode);
            if (!useTextureCornerMapping)
                text += "/poly-map";
            if (useSurfaceColorAssist)
                text += "/surface";
            if (completeTerrainDraw)
                text += "/complete";
            if (useRawSourceTexture)
                text += "/raw";
            return text;
        }

        private static string FunctionalTestNote(Moby moby)
        {
            if (moby == null) return "";
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.BehaviorNote ?? "")).ToLowerInvariant();
            if (text.IndexOf("chest", StringComparison.Ordinal) >= 0 || text.IndexOf("container", StringComparison.Ordinal) >= 0)
                return "fresh-load a Source BIN and confirm the chest has collision, breaks normally, and grants or drops its gem reward.";
            if (text.IndexOf("sheep", StringComparison.Ordinal) >= 0 || text.IndexOf("fodder", StringComparison.Ordinal) >= 0 || text.IndexOf("ai/home", StringComparison.Ordinal) >= 0 || text.IndexOf("home anchor", StringComparison.Ordinal) >= 0)
                return "fresh-load a Source BIN and confirm the actor's AI/home behavior follows the moved source position.";
            if (text.IndexOf("gem", StringComparison.Ordinal) >= 0 || text.IndexOf("treasure", StringComparison.Ordinal) >= 0 || text.IndexOf("collectible", StringComparison.Ordinal) >= 0)
                return "fresh-load a Source BIN and confirm pickup still changes the gem counter or collectable flag.";
            return moby.Patchable
                ? "fresh-load a Source BIN and test normal behavior; Live Apply is only a runtime XYZ probe."
                : "find a source-window lead before expecting permanent placement or rebuilt behavior.";
        }

        private void AdjustMobyListColumns()
        {
            if (mobyList == null || mobyList.Columns.Count < 6) return;
            int width = Math.Max(220, mobyList.ClientSize.Width - 24);
            mobyList.Columns[0].Width = 74;
            mobyList.Columns[1].Width = 42;
            mobyList.Columns[2].Width = 88;
            mobyList.Columns[4].Width = 54;
            mobyList.Columns[5].Width = 80;
            mobyList.Columns[3].Width = Math.Max(90, width - 338);
        }

        private void SelectMoby(int listIndex)
        {
            selectedMobyIndex = (listIndex >= 0 && listIndex < mobys.Count) ? listIndex : -1;
            if (mobyList != null && !updatingSelection)
            {
                updatingSelection = true;
                try
                {
                    foreach (ListViewItem item in mobyList.Items)
                        item.Selected = false;
                    ListViewItem selectedItem = FindMobyListItem(selectedMobyIndex);
                    if (selectedItem != null)
                    {
                        selectedItem.Selected = true;
                        selectedItem.EnsureVisible();
                    }
                }
                finally
                {
                    updatingSelection = false;
                }
            }
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
        }

        private ListViewItem FindMobyListItem(int mobyIndex)
        {
            if (mobyList == null || mobyIndex < 0) return null;
            foreach (ListViewItem item in mobyList.Items)
            {
                object tag = item.Tag;
                if (tag is int && (int)tag == mobyIndex)
                    return item;
            }
            return null;
        }

        private void UpdateInspector()
        {
            if (selectedTitleLabel == null) return;
            updatingInspector = true;
            try
            {
                if (editorMode == EditorMode.Terrain)
                {
                    UpdateTerrainInspector();
                    return;
                }

                SetCoordinateControlsEnabled(true);
                if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
                {
                    selectedTitleLabel.Text = "No moby selected";
                    identityLabel.Text = "";
                    patchLabel.Text = "";
                    SetCoordinateBox(xBox, 0);
                    SetCoordinateBox(yBox, 0);
                    SetCoordinateBox(zBox, 0);
                    notesBox.Text = "Select a moby to inspect its decoded identity and edit status.";
                    UpdateGemButtons(null);
                    UpdateMutationButtons(null);
                    UpdateGroupNavigationButtons();
                    return;
                }

                Moby m = mobys[selectedMobyIndex];
                UpdateGemButtons(m);
                UpdateMutationButtons(m);
                UpdateGroupNavigationButtons();
                selectedTitleLabel.Text = MobyId(m) + " " + m.DisplayLabel + (m.IsEdited ? " *" : "");
                identityLabel.Text = CatalogCategory(m) + " | Type 0x" + m.Type.ToString("X2") + "  State 0x" + m.State.ToString("X2") + DetailSuffix(m.Confidence);
                patchLabel.Text = m.Patchable ? "Patch-testable lead" : "Not disc-patchable yet";
                if (!string.IsNullOrEmpty(m.PatchStatus))
                    patchLabel.Text = m.PatchStatus;
                if (m.Patchable && HasFunctionalRisk(m))
                    patchLabel.Text = "Source lead; behavior test needed";

                SetCoordinateBox(xBox, m.X);
                SetCoordinateBox(yBox, m.Y);
                SetCoordinateBox(zBox, m.Z);

                StringBuilder notes = new StringBuilder();
                notes.AppendLine(MobyId(m) + " " + m.DisplayLabel);
                if (m.LegacyIndex >= 0)
                    notes.AppendLine("Legacy sparse alias: L" + m.LegacyIndex.ToString());
                notes.AppendLine("True loader record: " + m.TrueIndex.ToString());
                notes.AppendLine("Runtime type/state: 0x" + m.Type.ToString("X2") + " / 0x" + m.State.ToString("X2"));
                notes.AppendLine("Runtime address: " + FormatAddress(m.RuntimeAddress));
                notes.AppendLine("Special data pointer: " + FormatAddress(m.SpecialDataPointer));
                notes.AppendLine("Flags 4A/4B: 0x" + m.Flag4A.ToString("X2") + " / 0x" + m.Flag4B.ToString("X2"));
                notes.AppendLine("Catalog: " + CatalogCategory(m) + " / " + MobyIconText(m));
                SelectionGroup smartGroup = BestSelectionGroupForMoby(selectedMobyIndex);
                if (smartGroup != null) notes.AppendLine("Smart group: " + smartGroup.DisplayName);
                if (!string.IsNullOrEmpty(m.Kind)) notes.AppendLine("Kind: " + m.Kind);
                if (!string.IsNullOrEmpty(m.Zone)) notes.AppendLine("Route zone: " + m.Zone);
                if (!string.IsNullOrEmpty(m.Confidence)) notes.AppendLine("Confidence: " + m.Confidence);
                if (!string.IsNullOrEmpty(m.Evidence)) notes.AppendLine("Evidence: " + m.Evidence);
                if (!string.IsNullOrEmpty(m.BehaviorNote)) notes.AppendLine("Behavior note: " + m.BehaviorNote);
                if (!string.IsNullOrEmpty(m.SpecialDataNote)) notes.AppendLine("Special-data chain: " + m.SpecialDataNote);
                notes.AppendLine();
                notes.AppendLine("Original XYZ: " + FormatVector(m.OriginalX, m.OriginalY, m.OriginalZ));
                notes.AppendLine("Current  XYZ: " + FormatVector(m.X, m.Y, m.Z));
                notes.AppendLine("Raw current int32 XYZ: " + FormatRawVector(m.X, m.Y, m.Z));
                string linkedGroup = LinkedMoveGroupText(selectedMobyIndex);
                if (!string.IsNullOrEmpty(linkedGroup))
                    notes.AppendLine("Linked move group: " + linkedGroup + (LinkedMoveEnabled ? " (on)" : " (off)"));
                SelectionGroup chestContents = ChestContentSelectionGroupForMoby(selectedMobyIndex);
                if (chestContents != null)
                    notes.AppendLine("Chest contents editor: Objects tab > Edit Contents opens the contained gem records without moving the whole linked group.");
                else if (IsSingleRewardChestEditorTarget(m))
                    notes.AppendLine("Chest reward editor: Objects tab > Edit Contents changes this chest's own +0x53 spawned gem value.");
                if (m.HasGroundOffset)
                    notes.AppendLine("Ground Z offset: " + m.GroundOffset.ToString("0.00") + " from terrain" + (GroundSnapEnabled ? " (auto)" : " (off)"));
                if (m.HasGemColorEdit)
                    notes.AppendLine("Gem color edit: " + m.GemColorName + " gem (" + m.GemValueOverride.ToString() + "), source +0x36=0x" + m.GemSourceByte36Override.ToString("X2") + " and +0x4F=0x" + m.GemSourceByte4FOverride.ToString("X2"));
                else if (CanEditGemColor(m))
                {
                    string sourceGemColor = m.SourceGemColorName;
                    if (!string.IsNullOrEmpty(sourceGemColor))
                        notes.AppendLine("Gem color source bytes: +0x36=0x" + m.SourceByte36.ToString("X2") + ", +0x4F=0x" + m.SourceByte4F.ToString("X2") + " (" + sourceGemColor + ").");
                    else
                        notes.AppendLine("Gem color source bytes: red 0x53/01, green 0x54/02, blue 0x55/03, yellow 0x56/04, purple 0x57/05.");
                }
                if (m.HasRewardColorEdit)
                    notes.AppendLine("Reward color edit: drops " + m.RewardColorName + " gem (" + m.RewardValueOverride.ToString() + "), source +0x53=0x" + m.RewardByte53Override.ToString("X2"));
                else if (CanEditRewardColor(m))
                    notes.AppendLine(IsChestContentMarker(m)
                        ? "Contained-gem byte: +0x53 appears to control this locked chest content value. Red 0x53, green 0x54, blue 0x55, yellow 0x56, purple 0x57."
                        : (IsLockedChestRewardMarker(m)
                            ? "Locked chest reward byte: +0x53 appears to control one spawned chest gem. Red 0x53, green 0x54, blue 0x55, yellow 0x56, purple 0x57."
                            : "Reward byte: type 0x20 +0x53 controls gem drop color/value. Red 0x53, green 0x54, blue 0x55, yellow 0x56, purple 0x57."));
                if (m.HasRecordCloneEdit)
                    notes.AppendLine("Object type edit: clone source record T" + m.RecordCloneSourceTrueIndex.ToString() + " " + m.RecordCloneSourceLabel + " into this slot, keeping current XYZ.");
                if (m.HasHiddenSlotEdit)
                    notes.AppendLine("Object remove edit: hide this slot by moving it out of the level.");
                if (m.IsAppendedRecord)
                    notes.AppendLine("True-add edit: append source record T" + m.TrueIndex.ToString() + " from donor T" + m.AppendSourceTrueIndex.ToString() + " " + m.AppendSourceLabel + ".");
                if (mutationClipboardMobyIndex >= 0 && mutationClipboardMobyIndex < mobys.Count)
                    notes.AppendLine("Copied type donor: " + MobyId(mobys[mutationClipboardMobyIndex]) + " " + mobys[mutationClipboardMobyIndex].DisplayLabel + ".");
                notes.AppendLine();
                notes.AppendLine("Edit status: " + DescribeEditStatus(m));
                notes.AppendLine("Disc patch status: " + (m.Patchable ? "loader-table patchable" : "waiting for WAD source link"));
                if (!string.IsNullOrEmpty(m.PatchLead)) notes.AppendLine("Patch lead: " + m.PatchLead);
                string functionalNote = FunctionalTestNote(m);
                if (!string.IsNullOrEmpty(functionalNote)) notes.AppendLine("Functional test: " + functionalNote);
                notes.AppendLine();
                notes.AppendLine("Live Apply writes only runtime XYZ into DuckStation. Use it to identify visible mobys, not to prove collision or rewards.");
                notes.AppendLine("Create Loader BIN writes saved moby edits for every mapped level into one disposable disc image. Fresh-load that CUE for permanent placement, collision, and rewards.");
                notes.AppendLine("Add New at Click appends a true new source moby record. Copy Obj/Clone-Add remains available for slot reuse experiments.");
                notes.AppendLine("Test Selected Add BIN writes only the selected true-added object into a disposable CUE, which is the safe path for cross-level Object Library and scenery experiments.");
                if (IsStoneHillLevel())
                {
                    notes.AppendLine("Create Terrain BIN writes saved terrain Z edits, same-level texture-ID swaps, and staged custom PNG texture imports through the exact runtime scene-sector/source texture data found in the WAD.");
                    notes.AppendLine("Create Combined BIN applies both saved moby edits and saved terrain edits into one fresh-loadable CUE.");
                    notes.AppendLine("Validate Source captures current DuckStation RAM and checks whether a fresh-loaded source BIN rebuilt the moved coordinates.");
                    notes.AppendLine("Behavior Diff captures current DuckStation RAM and compares chest, gem, and fodder records plus linked special-data blocks for collision/reward/AI proof.");
                }
                else
                {
                    notes.AppendLine(currentLevelName + " currently exports permanent moby edits only. Terrain source export and combined BINs are still Stone Hill-only.");
                }
                notesBox.Text = notes.ToString();
            }
            finally
            {
                updatingInspector = false;
            }
        }

        private void UpdateTerrainInspector()
        {
            SetCoordinateControlsEnabled(false);
            UpdateGemButtons(null);
            UpdateMutationButtons(null);
            if (selectedTerrainIndex < 0 || geometry == null || selectedTerrainIndex >= geometry.Polygons.Count)
            {
                selectedTitleLabel.Text = "Terrain Mode";
                identityLabel.Text = "Click a terrain face";
                patchLabel.Text = "Terrain height editing ready";
                SetCoordinateBox(xBox, selectedTerrainPoint.X);
                SetCoordinateBox(yBox, selectedTerrainPoint.Y);
                SetCoordinateBox(zBox, 0);
                notesBox.Text =
                    "Terrain Mode\n\n" +
                    "Click a face in the map to inspect its decoded runtime vertices and height. This mode does not move mobys.\n\n" +
                    "Use Height Tint, Contours, and Ground Cues from the toolbar to read elevation while placing mobys.\n\n" +
                    "Source Textures uses the decoded Stone Hill WAD texture-page atlas when available.\n\n" +
                    "Current terrain source is the runtime scene-sector geometry overlay. Saved Stone Hill terrain height edits, same-level texture-ID swaps, and staged custom PNG texture imports can be exported with Create Terrain BIN.";
                return;
            }

            TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
            if (zBox != null) zBox.Enabled = true;
            float terrainZ;
            bool hasZ = polygon.TryGetZ(selectedTerrainPoint.X, selectedTerrainPoint.Y, out terrainZ);
            selectedTitleLabel.Text = "Terrain Face " + selectedTerrainIndex.ToString();
            identityLabel.Text = polygon.Points.Length.ToString() + " vertices  Avg Z " + polygon.AvgZ.ToString("0.0");
            patchLabel.Text = polygon.IsTerrainEdited
                ? (polygon.HasTextureEdit ? "Terrain texture edit ready for Stone Hill BIN" : "Terrain edit ready for live/source BIN")
                : "Runtime geometry view";
            SetCoordinateBox(xBox, selectedTerrainPoint.X);
            SetCoordinateBox(yBox, selectedTerrainPoint.Y);
            SetCoordinateBox(zBox, hasZ ? terrainZ : polygon.AvgZ);

            StringBuilder notes = new StringBuilder();
            notes.AppendLine("Terrain Face " + selectedTerrainIndex.ToString());
            notes.AppendLine("Clicked XY: " + selectedTerrainPoint.X.ToString("0.0") + ", " + selectedTerrainPoint.Y.ToString("0.0"));
            notes.AppendLine("Interpolated Z: " + (hasZ ? terrainZ.ToString("0.0") : "unavailable"));
            notes.AppendLine("Average Z: " + polygon.AvgZ.ToString("0.0"));
            notes.AppendLine("Vertex Z range: " + polygon.MinZ.ToString("0.0") + " .. " + polygon.MaxZ.ToString("0.0"));
            notes.AppendLine("Terrain edit delta Z: " + (polygon.TerrainEditDeltaZ >= 0f ? "+" : "") + polygon.TerrainEditDeltaZ.ToString("0.0"));
            notes.AppendLine("Bounds: X " + polygon.Bounds.Left.ToString("0.0") + ".." + polygon.Bounds.Right.ToString("0.0") + ", Y " + polygon.Bounds.Top.ToString("0.0") + ".." + polygon.Bounds.Bottom.ToString("0.0"));
            if (polygon.SectorIndex >= 0 || polygon.FaceIndex >= 0 || !string.IsNullOrEmpty(polygon.FaceOffset))
            {
                notes.AppendLine("Runtime source handle: sector " + polygon.SectorIndex.ToString() + ", face " + polygon.FaceIndex.ToString() + ", detail " + (string.IsNullOrEmpty(polygon.Detail) ? "?" : polygon.Detail));
                notes.AppendLine("Runtime offsets: sector " + (string.IsNullOrEmpty(polygon.SectorOffset) ? "?" : polygon.SectorOffset) + ", face " + (string.IsNullOrEmpty(polygon.FaceOffset) ? "?" : polygon.FaceOffset));
            }
            if (polygon.HasTextureId)
            {
                notes.AppendLine("Texture/material id: " + polygon.TextureId.ToString() + (polygon.HasTextureEdit ? " (was " + polygon.OriginalTextureId.ToString() + ")" : ""));
                notes.AppendLine("Texture id face count: " + CountTerrainTextureFaces(polygon.TextureId).ToString());
                CustomTerrainTexture custom;
                if (TryGetCustomTerrainTexture(polygon.TextureId, out custom))
                    notes.AppendLine("Custom texture import: " + custom.SourceImageName + " -> " + custom.DescriptorTier + " " + custom.TileSize.ToString() + "x" + custom.TileSize.ToString());
                if (copiedTerrainTextureId >= 0)
                    notes.AppendLine("Copied texture id: " + copiedTerrainTextureId.ToString());
                notes.AppendLine("Face depth: " + polygon.FaceDepth.ToString() + "  Flip: " + (polygon.FaceFlip ? "yes" : "no"));
                notes.AppendLine("Source texture tile: " + (HasSourceTextureTile(polygon.TextureId) ? "atlas " + terrainTextureTileSize.ToString() + "x" + terrainTextureTileSize.ToString() + " tile available" : "not found in loaded atlas"));
                if (!string.IsNullOrEmpty(terrainTextureAtlasTier))
                    notes.AppendLine("Texture descriptor tier: " + terrainTextureAtlasTier);
                notes.AppendLine("Atlas preference: " + TerrainTextureAtlasPreferenceText(terrainTextureAtlasPreference));
                notes.AppendLine("Texture mapping: " + (useTextureCornerMapping ? "runtime texture-corner triangle" : "terrain polygon corners"));
                notes.AppendLine("Texture window diagnostic: " + SourceTextureWindowModeText(sourceTextureWindowMode));
                if (sourceTextureWindowMode != SourceTextureWindowMode.WholeTile)
                    notes.AppendLine("Current window candidate: " + TextureWindowIndex(polygon).ToString());
                notes.AppendLine("Atlas color mode: " + (useRawSourceTexture ? "raw atlas colors" : "luma atlas tinted by face color"));
                notes.AppendLine("Surface assist: " + (useSurfaceColorAssist ? StoneHillSurfaceKindText(polygon) : "off"));
                if (polygon.HasFaceColor)
                    notes.AppendLine("Face color: RGB " + polygon.FaceColor.R.ToString() + ", " + polygon.FaceColor.G.ToString() + ", " + polygon.FaceColor.B.ToString());
                if (!string.IsNullOrEmpty(polygon.Word3) || !string.IsNullOrEmpty(polygon.Word4))
                    notes.AppendLine("Packed face words: " + polygon.Word3 + " / " + polygon.Word4);
            }
            notes.AppendLine("Source texture display: " + (showSourceTextures ? "on" : "off") + (terrainTextureAtlas == null ? " (atlas not loaded)" : " (" + terrainTextureAtlasTier + ")") + (CountCustomTerrainTextureImports() > 0 ? ", custom imports " + CountCustomTerrainTextureImports().ToString() : ""));
            notes.AppendLine("Terrain draw coverage: " + (completeTerrainDraw ? "complete" : "sampled at distant zoom"));
            notes.AppendLine("Height assist: " + HeightAssistText());
            notes.AppendLine();
            notes.AppendLine("Vertices:");
            for (int i = 0; i < polygon.Points.Length; i++)
                notes.AppendLine("  V" + i.ToString() + ": " + polygon.Points[i].X.ToString("0.0") + ", " + polygon.Points[i].Y.ToString("0.0") + ", " + polygon.ZValues[i].ToString("0.0"));
            notes.AppendLine();
            notes.AppendLine("Terrain editing: PageUp/PageDown nudges the selected face Z by the toolbar step. The Z box sets the selected face average height. Right-click a face or use the Terrain tab to copy/paste texture ids, replace all matching original texture ids, and import PNG art into the chosen texture slot. Create Terrain BIN writes Stone Hill Z edits, same-level texture-ID swaps, and staged custom texture-page imports.");
            notesBox.Text = notes.ToString();
        }

        private void SetCoordinateControlsEnabled(bool enabled)
        {
            if (xBox != null) xBox.Enabled = enabled;
            if (yBox != null) yBox.Enabled = enabled;
            if (zBox != null) zBox.Enabled = enabled;
        }

        private static string DetailSuffix(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : "  " + value;
        }

        private static string MobyId(Moby moby)
        {
            if (moby == null) return "";
            string id = "T" + moby.TrueIndex.ToString() + (moby.IsAppendedRecord ? "+" : "");
            if (moby.LegacyIndex >= 0)
                id += "/L" + moby.LegacyIndex.ToString();
            return id;
        }

        private static string FormatVector(float x, float y, float z)
        {
            return x.ToString("0.00") + ", " + y.ToString("0.00") + ", " + z.ToString("0.00");
        }

        private static string FormatRawVector(float x, float y, float z)
        {
            return ToRawCoordinate(x).ToString() + ", " + ToRawCoordinate(y).ToString() + ", " + ToRawCoordinate(z).ToString();
        }

        private static string FormatAddress(uint value)
        {
            return "0x" + value.ToString("X8");
        }

        private static int ToRawCoordinate(float value)
        {
            return (int)Math.Round(value * 16f);
        }

        private static void SetCoordinateBox(NumericUpDown box, float value)
        {
            if (box == null) return;
            decimal d = (decimal)value;
            if (d < box.Minimum) d = box.Minimum;
            if (d > box.Maximum) d = box.Maximum;
            box.Value = d;
        }

        private void InspectorCoordinateChanged(bool manualZ)
        {
            if (updatingInspector)
                return;
            if (editorMode == EditorMode.Terrain)
            {
                if (manualZ && selectedTerrainIndex >= 0 && geometry != null && selectedTerrainIndex < geometry.Polygons.Count)
                    SetSelectedTerrainAverageZ((float)zBox.Value);
                return;
            }
            if (editorMode != EditorMode.Mobys || selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
                return;
            SetMobyPosition(selectedMobyIndex, (float)xBox.Value, (float)yBox.Value, (float)zBox.Value, true, true, !manualZ);
        }

        private bool LinkedMoveEnabled
        {
            get { return linkedMoveButton == null || linkedMoveButton.Checked; }
        }

        private bool GroundSnapEnabled
        {
            get { return groundSnapButton != null && groundSnapButton.Checked; }
        }

        private bool ClickPlaceEnabled
        {
            get { return clickPlaceButton != null && clickPlaceButton.Checked; }
        }

        private bool DragLockEnabled
        {
            get { return dragLockButton != null && dragLockButton.Checked; }
        }

        private float NudgeStep
        {
            get
            {
                if (nudgeStepBox == null || nudgeStepBox.SelectedItem == null) return 64f;
                float value;
                return float.TryParse(nudgeStepBox.SelectedItem.ToString(), out value) && value > 0f ? value : 64f;
            }
        }

        private void SetMobyPosition(int listIndex, float x, float y, float z, bool refreshList)
        {
            SetMobyPosition(listIndex, x, y, z, refreshList, true, true);
        }

        private void MoveSelectedToWorldPoint(float x, float y)
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            if (view3D)
            {
                statusLabel.Text = "Click-place uses the top-down map view for precise terrain placement.";
                return;
            }

            Moby moby = mobys[selectedMobyIndex];
            SetMobyPosition(selectedMobyIndex, x, y, moby.Z, true, true, true);
            string targetText = LinkedMoveGroupText(selectedMobyIndex);
            if (string.IsNullOrEmpty(targetText))
                targetText = MobyId(moby);
            statusLabel.Text = "Placed " + targetText + " at X " + x.ToString("0.0") + ", Y " + y.ToString("0.0") + ".";
        }

        private void NudgeSelectedMoby(float dx, float dy, float dz, bool allowGroundSnap)
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            Moby moby = mobys[selectedMobyIndex];
            SetMobyPosition(selectedMobyIndex, moby.X + dx, moby.Y + dy, moby.Z + dz, true, true, allowGroundSnap);
            string axis = dz != 0f ? "Z" : "XY";
            statusLabel.Text = "Nudged " + MobyId(moby) + " " + axis + " by " + Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dz)).ToString("0.##") + ".";
        }

        private void NudgeSelectedTerrainFace(float dz)
        {
            if (geometry == null || selectedTerrainIndex < 0 || selectedTerrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
            polygon.ApplyTerrainDeltaZ(polygon.TerrainEditDeltaZ + dz);
            hasUnsavedEdits = true;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Nudged terrain face " + selectedTerrainIndex.ToString() + " Z by " + dz.ToString("0.##") + ". Save Edits to keep this terrain edit.";
        }

        private void SetSelectedTerrainAverageZ(float targetAverageZ)
        {
            if (geometry == null || selectedTerrainIndex < 0 || selectedTerrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
            float delta = targetAverageZ - polygon.AvgZ;
            if (Math.Abs(delta) < 0.001f) return;
            polygon.ApplyTerrainDeltaZ(polygon.TerrainEditDeltaZ + delta);
            hasUnsavedEdits = true;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Set terrain face " + selectedTerrainIndex.ToString() + " average Z to " + targetAverageZ.ToString("0.0") + ".";
        }

        internal void CanvasKeyDown(KeyEventArgs e)
        {
            float step = NudgeStep;
            if (e.Shift) step *= 4f;
            if (e.Control) step *= 0.25f;

            if (editorMode == EditorMode.Terrain)
            {
                if (selectedTerrainIndex < 0 || geometry == null || selectedTerrainIndex >= geometry.Polygons.Count)
                    return;
                if (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
                {
                    NudgeSelectedTerrainFace(e.KeyCode == Keys.PageUp ? step : -step);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                return;
            }

            if (editorMode != EditorMode.Mobys || selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
                return;

            float dx = 0f;
            float dy = 0f;
            float dz = 0f;
            bool handled = true;
            if (e.KeyCode == Keys.Left)
                dx = mirrorViewX ? step : -step;
            else if (e.KeyCode == Keys.Right)
                dx = mirrorViewX ? -step : step;
            else if (e.KeyCode == Keys.Up)
                dy = mirrorViewY ? step : -step;
            else if (e.KeyCode == Keys.Down)
                dy = mirrorViewY ? -step : step;
            else if (e.KeyCode == Keys.PageUp)
                dz = step;
            else if (e.KeyCode == Keys.PageDown)
                dz = -step;
            else
                handled = false;

            if (!handled) return;
            NudgeSelectedMoby(dx, dy, dz, dz == 0f);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void SetMobyPosition(int listIndex, float x, float y, float z, bool refreshList, bool allowLinked, bool allowGroundSnap)
        {
            if (listIndex < 0 || listIndex >= mobys.Count) return;
            Moby m = mobys[listIndex];
            if (allowGroundSnap && GroundSnapEnabled)
                z = SnapZForMoby(m, x, y, false);

            float dx = x - m.X;
            float dy = y - m.Y;
            float dz = z - m.Z;
            List<int> targets = allowLinked ? GetLinkedMoveIndexes(listIndex) : new List<int>(new int[] { listIndex });
            foreach (int targetIndex in targets)
            {
                if (targetIndex < 0 || targetIndex >= mobys.Count) continue;
                Moby target = mobys[targetIndex];
                float targetX = targetIndex == listIndex ? x : target.X + dx;
                float targetY = targetIndex == listIndex ? y : target.Y + dy;
                float targetZ = targetIndex == listIndex ? z : target.Z + dz;
                if (allowGroundSnap && GroundSnapEnabled)
                    targetZ = SnapZForMoby(target, targetX, targetY, false);
                ApplyMobyPosition(targetIndex, targetX, targetY, targetZ, refreshList, !(allowGroundSnap && GroundSnapEnabled));
            }
            hasUnsavedEdits = true;
            UpdateInspector();
            string targetText = targets.Count > 1
                ? "linked group " + LinkedMoveGroupText(listIndex)
                : MobyId(m) + " " + m.DisplayLabel;
            statusLabel.Text = "Moved " + targetText + ". Save Edits to keep this move.";
            canvas.Invalidate();
        }

        private void ApplyMobyPosition(int listIndex, float x, float y, float z, bool refreshList, bool updateGroundOffset)
        {
            if (listIndex < 0 || listIndex >= mobys.Count) return;
            Moby m = mobys[listIndex];
            m.X = x;
            m.Y = y;
            m.Z = z;
            if (updateGroundOffset)
                RefreshGroundOffset(m);
            if (refreshList)
                RefreshMobyListRow(listIndex);
        }

        private void ResetSelectedMoby()
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            Moby m = mobys[selectedMobyIndex];
            if (m.IsAppendedRecord)
            {
                if (m.SpringChestPartnerTrueIndex >= 0)
                {
                    RemoveAppendedSpringChestPair(selectedMobyIndex);
                    return;
                }
                RemoveAppendedMoby(selectedMobyIndex, "Removed new object " + MobyId(m) + ". Save Edits to keep it removed.");
                return;
            }
            SetMobyPosition(selectedMobyIndex, m.OriginalX, m.OriginalY, m.OriginalZ, true, true, false);
            m.ClearGemColorOverride();
            BuildSelectionGroups();
            RefreshMobyListRow(selectedMobyIndex);
            UpdateInspector();
            canvas.Invalidate();
        }

        private void RemoveAppendedMoby(int mobyIndex, string message)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            mobys.RemoveAt(mobyIndex);
            NormalizeAppendedTrueIndexes();
            selectedMobyIndex = Math.Min(mobyIndex, mobys.Count - 1);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = message;
        }

        private void RemoveAppendedSpringChestPair(int mobyIndex)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            Moby selected = mobys[mobyIndex];
            int partnerIndex = IndexOfTrueIndex(selected.SpringChestPartnerTrueIndex);
            List<int> remove = new List<int>();
            remove.Add(mobyIndex);
            if (partnerIndex >= 0 && partnerIndex < mobys.Count && mobys[partnerIndex].IsAppendedRecord)
                remove.Add(partnerIndex);
            remove.Sort();
            for (int i = remove.Count - 1; i >= 0; i--)
                mobys.RemoveAt(remove[i]);
            NormalizeAppendedTrueIndexes();
            selectedMobyIndex = Math.Min(mobyIndex, mobys.Count - 1);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Removed spring chest pair. Save Edits to keep it removed.";
        }

        private void NormalizeAppendedTrueIndexes()
        {
            int nextTrueIndex = SourceRecordCountForLevel(currentLevelKey);
            Dictionary<int, int> trueIndexMap = new Dictionary<int, int>();
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                if (!moby.IsAppendedRecord) continue;
                int oldTrueIndex = moby.TrueIndex;
                moby.Index = i;
                moby.TrueIndex = nextTrueIndex;
                moby.PatchLead = "append source record T" + nextTrueIndex.ToString() + " from donor T" + moby.AppendSourceTrueIndex.ToString();
                if (oldTrueIndex >= 0)
                    trueIndexMap[oldTrueIndex] = nextTrueIndex;
                nextTrueIndex++;
            }
            foreach (Moby moby in mobys)
            {
                if (moby == null || moby.SpringChestPartnerTrueIndex < 0) continue;
                int remapped;
                if (trueIndexMap.TryGetValue(moby.SpringChestPartnerTrueIndex, out remapped))
                    moby.SpringChestPartnerTrueIndex = remapped;
            }
        }

        private void SnapSelectedToGround()
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return;
            List<int> targets = GetLinkedMoveIndexes(selectedMobyIndex);
            foreach (int targetIndex in targets)
            {
                if (targetIndex < 0 || targetIndex >= mobys.Count) continue;
                Moby target = mobys[targetIndex];
                float z = SnapZForMoby(target, target.X, target.Y, true);
                ApplyMobyPosition(targetIndex, target.X, target.Y, z, true, false);
            }
            hasUnsavedEdits = true;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Snapped " + (targets.Count > 1 ? "linked group " + LinkedMoveGroupText(selectedMobyIndex) : MobyId(mobys[selectedMobyIndex])) + " to terrain Z.";
        }

        private List<int> GetLinkedMoveIndexes(int listIndex)
        {
            List<int> result = new List<int>();
            if (listIndex < 0 || listIndex >= mobys.Count) return result;
            result.Add(listIndex);
            if (!LinkedMoveEnabled) return result;

            SelectionGroup linkedGroup = LinkedMoveSelectionGroupForMoby(listIndex);
            if (linkedGroup != null)
            {
                for (int i = 0; i < linkedGroup.Members.Count; i++)
                {
                    if (!result.Contains(linkedGroup.Members[i]))
                        result.Add(linkedGroup.Members[i]);
                }
            }

            result.Sort();
            return result;
        }

        private bool IsLinkedToSelected(int listIndex)
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return false;
            if (!LinkedMoveEnabled) return false;
            List<int> group = GetLinkedMoveIndexes(selectedMobyIndex);
            return group.Contains(listIndex);
        }

        private static bool IsDragonOrPedestalMoby(Moby moby)
        {
            return moby != null && IsDragonOrPedestalIdentityText(MobySearchText(moby));
        }

        private static bool IsDragonActorMoby(Moby moby)
        {
            return moby != null && IsDragonActorIdentityText(MobySearchText(moby));
        }

        private static bool IsDragonPedestalMoby(Moby moby)
        {
            return moby != null && IsDragonPedestalIdentityText(MobySearchText(moby));
        }

        private static bool IsDragonOrPedestalIdentityText(string text)
        {
            return IsDragonPedestalIdentityText(text) || IsDragonActorIdentityText(text);
        }

        private static bool IsDragonActorIdentityText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (HasAny(text, "not a dragon", "not dragon", "nonvisual", "invisible", "control", "helper", "camera", "fairy", "pedestal light", "pedestal-light"))
                return false;
            if (HasAny(text, "tree", "lamp", "flower", "grass", "flag") && !HasAny(text, "dragon actor", "dragon model", "visible dragon", "actual visible dragon", "actor/model"))
                return false;
            return text.IndexOf("dragon actor", StringComparison.Ordinal) >= 0
                || text.IndexOf("dragon model", StringComparison.Ordinal) >= 0
                || text.IndexOf("actual visible dragon", StringComparison.Ordinal) >= 0
                || text.IndexOf("visible dragon actor", StringComparison.Ordinal) >= 0
                || text.IndexOf("actor/model", StringComparison.Ordinal) >= 0
                || string.Equals(text.Trim(), "dragon", StringComparison.Ordinal);
        }

        private static bool IsDragonPedestalIdentityText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (HasAny(text, "nonvisual", "invisible", "control", "helper", "camera", "fairy", "pedestal light", "pedestal-light"))
                return false;
            return text.IndexOf("dragon pedestal", StringComparison.Ordinal) >= 0
                || text.IndexOf("rescue platform", StringComparison.Ordinal) >= 0
                || text.IndexOf(" dragon platform", StringComparison.Ordinal) >= 0;
        }

        private static bool IsPortalLinkAnchor(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            if (HasAny(text, "balloonist", "transport npc"))
                return false;
            return IsPortalLevelNameMoby(moby) || IsReturnHomeMoby(moby) || IsPortalStructureMoby(moby);
        }

        private static bool IsPortalLinkedNeighbor(Moby anchor, Moby candidate)
        {
            if (anchor == null || candidate == null) return false;
            if (IsDragonOrPedestalMoby(candidate) || IsLockedChestLike(candidate) || IsSpringChestLike(candidate) || IsChestEditableContentValue(candidate))
                return false;
            if (GetMobyIconKind(candidate) == MobyIconKind.Enemy || GetMobyIconKind(candidate) == MobyIconKind.Gem || GetMobyIconKind(candidate) == MobyIconKind.Key || GetMobyIconKind(candidate) == MobyIconKind.Camera)
                return false;

            bool closeStructure = IsNearMoby(anchor, candidate, 512f, 512f);
            if (!closeStructure) return false;

            if (IsPortalLevelNameMoby(candidate) || IsReturnHomeMoby(candidate) || IsPortalStructureMoby(candidate) || IsPortalTriggerMoby(candidate))
                return true;

            return IsNearMoby(anchor, candidate, 160f, 384f) && IsGenericPortalHelperCandidate(candidate);
        }

        private static bool IsPortalLevelNameMoby(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            return HasAny(text, "level name", "level-name", "portal name", "portal lettering", "gold lettering");
        }

        private static bool IsReturnHomeMoby(Moby moby)
        {
            if (moby == null) return false;
            return MobySearchText(moby).IndexOf("return home", StringComparison.Ordinal) >= 0;
        }

        private static bool IsPortalStructureMoby(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            if (HasAny(text, "portal pad", "portal arch", "portal frame", "portal entry", "portal platform", "return-home platform", "return home platform"))
                return true;
            return text.IndexOf("portal", StringComparison.Ordinal) >= 0 && HasAny(text, "pad", "arch", "frame", "platform", "scenery");
        }

        private static bool IsPortalTriggerMoby(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            return HasAny(text, "portal sound trigger", "sound trigger", "portal trigger", "entry trigger", "warp trigger", "level entry", "portal control");
        }

        private static bool IsGenericPortalHelperCandidate(Moby moby)
        {
            if (moby == null || moby.Type != 0x00) return false;
            string text = MobySearchText(moby);
            if (HasAny(text, "dragon", "chest", "gem", "treasure", "enemy", "fodder", "sheep", "gnorc", "key", "camera", "view point", "viewpoint"))
                return false;
            return string.IsNullOrEmpty(text.Trim()) || HasAny(text, "nonvisual", "invisible", "control", "helper", "object?");
        }

        private static bool IsNearMoby(Moby a, Moby b, float maxXyDistance, float maxZDistance)
        {
            if (a == null || b == null) return false;
            if (Math.Abs(a.Z - b.Z) > maxZDistance) return false;
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy) <= maxXyDistance * maxXyDistance;
        }

        private string LinkedMoveGroupText(int listIndex)
        {
            List<int> group = GetLinkedMoveIndexes(listIndex);
            if (group.Count <= 1) return "";
            List<string> labels = new List<string>();
            foreach (int index in group)
                labels.Add(MobyId(mobys[index]));
            return string.Join(", ", labels.ToArray());
        }

        private SelectionGroup ChestContentSelectionGroupForMoby(int mobyIndex)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return null;
            for (int i = 0; i < selectionGroups.Count; i++)
            {
                SelectionGroup group = selectionGroups[i];
                if (IsChestContentGroup(group) && group.Contains(mobyIndex))
                    return group;
            }
            return null;
        }

        private static bool IsChestContentGroup(SelectionGroup group)
        {
            if (group == null) return false;
            string text = ((group.Key ?? "") + " " + (group.Name ?? "")).ToLowerInvariant();
            return text.IndexOf("chest", StringComparison.Ordinal) >= 0
                && (text.IndexOf("content", StringComparison.Ordinal) >= 0 || text.IndexOf("reward", StringComparison.Ordinal) >= 0);
        }

        private bool CanOpenChestContentEditor(int mobyIndex)
        {
            int chestIndex;
            List<int> contentIndexes;
            return TryGetChestContentMembers(mobyIndex, out chestIndex, out contentIndexes);
        }

        private bool TryGetChestContentMembers(int mobyIndex, out int chestIndex, out List<int> contentIndexes)
        {
            chestIndex = -1;
            contentIndexes = new List<int>();
            SelectionGroup group = ChestContentSelectionGroupForMoby(mobyIndex);
            if (group == null)
            {
                if (mobyIndex >= 0 && mobyIndex < mobys.Count && IsSingleRewardChestEditorTarget(mobys[mobyIndex]))
                {
                    chestIndex = mobyIndex;
                    contentIndexes.Add(mobyIndex);
                    return true;
                }
                return false;
            }

            for (int i = 0; i < group.Members.Count; i++)
            {
                int memberIndex = group.Members[i];
                if (memberIndex < 0 || memberIndex >= mobys.Count) continue;
                Moby member = mobys[memberIndex];
                if (chestIndex < 0 && (IsLinkedContentsChestLike(member) || IsSingleRewardChestEditorTarget(member)))
                    chestIndex = memberIndex;
                if (IsChestEditableContentValue(member))
                    contentIndexes.Add(memberIndex);
            }

            if (chestIndex < 0)
            {
                for (int i = 0; i < group.Members.Count; i++)
                {
                    int memberIndex = group.Members[i];
                    if (memberIndex >= 0 && memberIndex < mobys.Count && !contentIndexes.Contains(memberIndex))
                    {
                        chestIndex = memberIndex;
                        break;
                    }
                }
            }

            contentIndexes.Sort();
            return chestIndex >= 0 && contentIndexes.Count > 0;
        }

        private static bool IsLockedChestLike(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            return HasAny(text, "locked chest", "unlock chest", "locked container");
        }

        private static bool IsBlastChestLike(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            if (HasAny(text, "super flame chest", "superflame chest", "super flame gem explosion", "cannon chest", "firework chest", "blast chest", "metal chest", "armored chest", "armoured chest"))
                return true;
            return moby.Type == 0x20 && moby.Flag4A == 0x10 && moby.SpecialDataPointer == 0x8016AB40;
        }

        private static bool IsLinkedContentsChestLike(Moby moby)
        {
            if (IsPotentialChestContentMarker(moby)) return false;
            return IsLockedChestLike(moby) || IsBlastChestLike(moby);
        }

        private static string ChestContentGroupTitle(Moby moby)
        {
            if (IsBlastChestLike(moby)) return "Blast chest contents";
            return "Locked chest contents";
        }

        private static bool IsSpringChestLike(Moby moby)
        {
            if (moby == null) return false;
            string text = MobySearchText(moby);
            if (text.IndexOf("spring chest", StringComparison.Ordinal) >= 0)
                return true;
            return moby.Type == 0x20 && moby.Flag4A == 0x10 && moby.SpecialDataPointer == 0x8016B9F8;
        }

        private static bool IsSingleRewardChestEditorTarget(Moby moby)
        {
            if (moby == null) return false;
            if (IsLockedChestRewardMarker(moby)) return true;
            if (moby.Type != 0x20 || moby.Flag4A != 0x10)
                return false;
            if (GemValueFromGemIdByte(moby.Flag4B) <= 0 && !moby.HasRewardColorEdit)
                return false;
            string text = MobySearchText(moby);
            if (IsSpringChestLike(moby)) return true;
            if (IsBlastChestLike(moby)) return true;
            return HasAny(text, "chest", "box", "container") && !HasAny(text, "life chest", "extra life");
        }

        private static bool IsLockedChestRewardMarker(Moby moby)
        {
            if (moby == null) return false;
            return IsLockedChestLike(moby) && GemValueFromGemIdByte(moby.Flag4B) > 0;
        }

        private static bool IsPotentialChestContentMarker(Moby moby)
        {
            if (IsChestContentMarker(moby)) return true;
            return moby != null && moby.Type == 0x00 && moby.Flag4A == 0xFF && GemValueFromGemIdByte(moby.Flag4B) > 0;
        }

        private static bool IsChestEditableContentValue(Moby moby)
        {
            return IsPotentialChestContentMarker(moby) || IsLockedChestRewardMarker(moby) || IsSingleRewardChestEditorTarget(moby);
        }

        private void ShowChestContentsEditor()
        {
            int chestIndex;
            List<int> contentIndexes;
            if (!TryGetChestContentMembers(selectedMobyIndex, out chestIndex, out contentIndexes))
            {
                MessageBox.Show(this, "Select a locked chest, spring chest, reward-bearing chest, or one of its linked content records first.", "No editable chest reward", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (ChestContentsEditorDialog dialog = new ChestContentsEditorDialog(this, chestIndex, contentIndexes))
                dialog.ShowDialog(this);
        }

        private void SetChestContentGemColor(int contentIndex, string color)
        {
            if (contentIndex < 0 || contentIndex >= mobys.Count) return;
            Moby moby = mobys[contentIndex];
            if (!IsChestEditableContentValue(moby)) return;
            if (IsPotentialChestContentMarker(moby))
                moby.SetContainedGemColorOverride(color);
            else
                moby.SetRewardColorOverride(color);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(contentIndex);
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Set " + MobyId(moby) + " chest content to " + moby.RewardColorName + " gem (" + moby.RewardValueOverride.ToString() + "). Save Edits, then Create Loader BIN.";
        }

        private void SetChestContentPosition(int contentIndex, float x, float y, float z)
        {
            if (contentIndex < 0 || contentIndex >= mobys.Count) return;
            Moby moby = mobys[contentIndex];
            if (!IsPotentialChestContentMarker(moby)) return;
            ApplyMobyPosition(contentIndex, x, y, z, true, true);
            int chestIndex;
            List<int> contentIndexes;
            if (TryGetChestContentMembers(contentIndex, out chestIndex, out contentIndexes) && chestIndex >= 0 && chestIndex < mobys.Count)
                moby.SetChestContentLinkOverride(mobys[chestIndex]);
            hasUnsavedEdits = true;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Moved " + MobyId(moby) + " chest content and updated its chest-link offset. Save Edits, then Create Loader BIN.";
        }

        private void StackChestContentsAtChest(int chestIndex, List<int> contentIndexes)
        {
            if (chestIndex < 0 || chestIndex >= mobys.Count || contentIndexes == null) return;
            Moby chest = mobys[chestIndex];
            int moved = 0;
            for (int i = 0; i < contentIndexes.Count; i++)
            {
                int contentIndex = contentIndexes[i];
                if (contentIndex < 0 || contentIndex >= mobys.Count || !IsPotentialChestContentMarker(mobys[contentIndex])) continue;
                ApplyMobyPosition(contentIndex, chest.X, chest.Y, chest.Z, true, true);
                mobys[contentIndex].SetChestContentLinkOverride(chest);
                moved++;
            }
            if (moved == 0) return;
            hasUnsavedEdits = true;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Stacked " + moved.ToString() + " chest content record(s) at " + MobyId(chest) + ". Save Edits, then Create Loader BIN.";
        }

        private int AddChestContentGem(int chestIndex, string color)
        {
            if (chestIndex < 0 || chestIndex >= mobys.Count) return -1;
            int contentIndex = FindReusableChestContentSlotIndex();

            Moby chest = mobys[chestIndex];
            bool appended = false;
            if (contentIndex < 0)
            {
                int templateIndex = FindChestContentTemplateIndex(chestIndex);
                ObjectTemplate externalTemplate = null;
                if (templateIndex < 0)
                    externalTemplate = FindObjectTemplateByFamily("chestContent");
                if (templateIndex < 0 && externalTemplate == null)
                {
                    MessageBox.Show(this,
                        "I could not find a source chest-content marker to clone for this level yet.\n\nTry editing an existing linked chest/explosion gem first so the editor has a known contained-gem template.",
                        "No chest-content template",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return -1;
                }

                int appendTrueIndex = NextAppendTrueIndex();
                Moby added = externalTemplate != null
                    ? Moby.CreateAppendedFromTemplate(externalTemplate, mobys.Count, appendTrueIndex)
                    : Moby.CreateAppendedFromSource(mobys[templateIndex], mobys.Count, appendTrueIndex);
                mobys.Add(added);
                contentIndex = mobys.Count - 1;
                appended = true;
            }

            Moby content = mobys[contentIndex];
            content.ClearGemColorOverride();
            content.ClearRewardColorOverride();
            content.ClearRecordMutationOverride();
            content.SetContainedGemColorOverride(color);
            ApplyMobyPosition(contentIndex, chest.X, chest.Y, chest.Z, true, true);
            content.SetChestContentLinkOverride(chest);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            if (appended)
                RefreshMobyList();
            else
                RefreshMobyListRow(contentIndex);
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = (appended ? "Added linked " : "Reused hidden source slot ")
                + MobyId(content) + " as " + content.RewardColorName + " gem content for " + MobyId(chest) + ". Save Edits, then Create Loader BIN.";
            return contentIndex;
        }

        private ObjectTemplate FindObjectTemplateByFamily(string family)
        {
            if (string.IsNullOrEmpty(family)) return null;
            foreach (ObjectTemplate template in objectTemplates)
            {
                if (template == null) continue;
                if (string.Equals(template.Family, family, StringComparison.OrdinalIgnoreCase))
                    return template;
            }
            return null;
        }

        private bool RemoveChestContentGem(int contentIndex)
        {
            if (contentIndex < 0 || contentIndex >= mobys.Count) return false;
            Moby moby = mobys[contentIndex];
            if (!CanRemoveChestContentGem(moby))
            {
                statusLabel.Text = "Only linked hidden/appended content gems can be removed; the chest's own reward byte can be recolored but not removed safely yet.";
                return false;
            }

            string label = MobyId(moby);
            if (moby.IsAppendedRecord)
            {
                RemoveAppendedMoby(contentIndex, "Removed added chest content " + label + ". Save Edits to keep it removed.");
                return true;
            }

            moby.SetHiddenSlotOverride();
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(contentIndex);
            RefreshObjectAddChoices();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Removed chest content " + label + " by hiding its source slot. Save Edits, then Create Loader BIN.";
            return true;
        }

        private int FindReusableChestContentSlotIndex()
        {
            int sourceRecordCount = SourceRecordCountForLevel(currentLevelKey);
            if (sourceRecordCount <= 0) return -1;

            for (int i = 0; i < mobys.Count; i++)
            {
                if (IsReusableChestContentSlot(i, sourceRecordCount))
                    return i;
            }
            return -1;
        }

        private bool IsReusableChestContentSlot(int mobyIndex, int sourceRecordCount)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return false;
            Moby moby = mobys[mobyIndex];
            if (moby == null || moby.IsAppendedRecord) return false;
            if (moby.TrueIndex < 0 || moby.TrueIndex >= sourceRecordCount) return false;
            if (!moby.HasHiddenSlotEdit) return false;
            return IsPotentialChestContentMarker(moby);
        }

        private int FindChestContentTemplateIndex(int chestIndex)
        {
            int sourceRecordCount = SourceRecordCountForLevel(currentLevelKey);
            if (sourceRecordCount <= 0) return -1;

            SelectionGroup group = ChestContentSelectionGroupForMoby(chestIndex);
            if (group != null)
            {
                for (int i = 0; i < group.Members.Count; i++)
                {
                    int memberIndex = group.Members[i];
                    if (IsChestContentTemplateCandidate(memberIndex, sourceRecordCount, false))
                        return memberIndex;
                }
                for (int i = 0; i < group.Members.Count; i++)
                {
                    int memberIndex = group.Members[i];
                    if (IsChestContentTemplateCandidate(memberIndex, sourceRecordCount, true))
                        return memberIndex;
                }
            }

            int nearest = FindNearestMobyIndex(chestIndex,
                delegate(Moby moby, int mobyIndex) { return IsChestContentTemplateCandidate(mobyIndex, sourceRecordCount, false); },
                320f,
                192f);
            if (nearest >= 0) return nearest;

            nearest = FindNearestMobyIndex(chestIndex,
                delegate(Moby moby, int mobyIndex) { return IsChestContentTemplateCandidate(mobyIndex, sourceRecordCount, true); },
                320f,
                192f);
            if (nearest >= 0) return nearest;

            for (int i = 0; i < mobys.Count; i++)
            {
                if (IsChestContentTemplateCandidate(i, sourceRecordCount, false))
                    return i;
            }
            for (int i = 0; i < mobys.Count; i++)
            {
                if (IsChestContentTemplateCandidate(i, sourceRecordCount, true))
                    return i;
            }
            return -1;
        }

        private bool IsChestContentTemplateCandidate(int mobyIndex, int sourceRecordCount, bool allowHidden)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return false;
            Moby moby = mobys[mobyIndex];
            if (moby == null || moby.IsAppendedRecord) return false;
            if (moby.TrueIndex < 0 || moby.TrueIndex >= sourceRecordCount) return false;
            if (!allowHidden && moby.HasHiddenSlotEdit) return false;
            return IsPotentialChestContentMarker(moby);
        }

        private static bool CanRemoveChestContentGem(Moby moby)
        {
            return moby != null && (moby.IsAppendedRecord || IsPotentialChestContentMarker(moby));
        }

        private static string ChestContentValueText(Moby moby)
        {
            if (moby == null) return "";
            int value = moby.HasRewardColorEdit ? moby.RewardValueOverride : GemValueFromGemIdByte(moby.Flag4B);
            string color = moby.HasRewardColorEdit ? moby.RewardColorName : GemColorNameFromGemIdByte(moby.Flag4B);
            if (string.IsNullOrEmpty(color) || value <= 0)
                return "unknown";
            return color + " (" + value.ToString() + ")";
        }

        private static string GemColorNameFromGemIdByte(int value)
        {
            switch (value)
            {
                case 0x54: return "green";
                case 0x55: return "blue";
                case 0x56: return "yellow";
                case 0x57: return "purple";
                case 0x53: return "red";
                default: return "";
            }
        }

        private string ChestRewardEditorTitle(int chestIndex)
        {
            if (chestIndex >= 0 && chestIndex < mobys.Count)
            {
                Moby chest = mobys[chestIndex];
                if (IsSpringChestLike(chest)) return "Spring Chest Gem";
                if (IsLockedChestLike(chest)) return "Locked Chest Contents";
                if (IsBlastChestLike(chest)) return "Blast Chest Contents";
            }
            return "Chest Gem Editor";
        }

        private string ChestRewardEditorHeader(int chestIndex, int contentCount)
        {
            if (chestIndex < 0 || chestIndex >= mobys.Count) return "Chest reward records";
            Moby chest = mobys[chestIndex];
            string prefix = contentCount == 1 && IsSingleRewardChestEditorTarget(chest) ? "Reward on " : "Linked to ";
            return prefix + MobyId(chest) + " " + chest.DisplayLabel;
        }

        private sealed class ChestContentsEditorDialog : Form
        {
            private readonly EditorForm editor;
            private readonly int chestIndex;
            private readonly List<int> contentIndexes;
            private readonly ListView contentList;
            private readonly NumericUpDown xBox;
            private readonly NumericUpDown yBox;
            private readonly NumericUpDown zBox;
            private readonly Button redButton;
            private readonly Button greenButton;
            private readonly Button blueButton;
            private readonly Button yellowButton;
            private readonly Button purpleButton;
            private readonly Button addRedButton;
            private readonly Button addGreenButton;
            private readonly Button addBlueButton;
            private readonly Button addYellowButton;
            private readonly Button addPurpleButton;
            private readonly Button removeButton;
            private readonly Button applyPositionButton;
            private readonly Button selectButton;
            private bool updating;

            public ChestContentsEditorDialog(EditorForm editor, int chestIndex, List<int> contentIndexes)
            {
                this.editor = editor;
                this.chestIndex = chestIndex;
                this.contentIndexes = new List<int>(contentIndexes ?? new List<int>());

                Text = editor.ChestRewardEditorTitle(chestIndex);
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(680, 468);

                TableLayoutPanel root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(10);
                root.ColumnCount = 1;
                root.RowCount = 6;
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
                Controls.Add(root);

                Label header = new Label();
                header.Dock = DockStyle.Fill;
                header.TextAlign = ContentAlignment.MiddleLeft;
                header.Text = editor.ChestRewardEditorHeader(chestIndex, this.contentIndexes.Count);
                root.Controls.Add(header, 0, 0);

                contentList = new ListView();
                contentList.Dock = DockStyle.Fill;
                contentList.View = View.Details;
                contentList.FullRowSelect = true;
                contentList.HideSelection = false;
                contentList.Columns.Add("Record", 78);
                contentList.Columns.Add("Value", 92);
                contentList.Columns.Add("Label", 200);
                contentList.Columns.Add("XYZ", 220);
                contentList.SelectedIndexChanged += delegate { UpdateSelectedFields(); };
                root.Controls.Add(contentList, 0, 1);

                FlowLayoutPanel colorTools = new FlowLayoutPanel();
                colorTools.Dock = DockStyle.Fill;
                colorTools.FlowDirection = FlowDirection.LeftToRight;
                redButton = NewDialogButton("Red 1");
                greenButton = NewDialogButton("Green 2");
                blueButton = NewDialogButton("Blue 5");
                yellowButton = NewDialogButton("Yellow 10");
                purpleButton = NewDialogButton("Purple 25");
                redButton.Click += delegate { SetSelectedColor("red"); };
                greenButton.Click += delegate { SetSelectedColor("green"); };
                blueButton.Click += delegate { SetSelectedColor("blue"); };
                yellowButton.Click += delegate { SetSelectedColor("yellow"); };
                purpleButton.Click += delegate { SetSelectedColor("purple"); };
                colorTools.Controls.Add(redButton);
                colorTools.Controls.Add(greenButton);
                colorTools.Controls.Add(blueButton);
                colorTools.Controls.Add(yellowButton);
                colorTools.Controls.Add(purpleButton);
                root.Controls.Add(colorTools, 0, 2);

                FlowLayoutPanel addRemoveTools = new FlowLayoutPanel();
                addRemoveTools.Dock = DockStyle.Fill;
                addRemoveTools.FlowDirection = FlowDirection.LeftToRight;
                addRedButton = NewDialogButton("Add Red");
                addGreenButton = NewDialogButton("Add Green");
                addBlueButton = NewDialogButton("Add Blue");
                addYellowButton = NewDialogButton("Add Yellow");
                addPurpleButton = NewDialogButton("Add Purple");
                removeButton = NewDialogButton("Remove Gem");
                addRedButton.Click += delegate { AddContentGem("red"); };
                addGreenButton.Click += delegate { AddContentGem("green"); };
                addBlueButton.Click += delegate { AddContentGem("blue"); };
                addYellowButton.Click += delegate { AddContentGem("yellow"); };
                addPurpleButton.Click += delegate { AddContentGem("purple"); };
                removeButton.Click += delegate { RemoveSelectedContentGem(); };
                addRemoveTools.Controls.Add(addRedButton);
                addRemoveTools.Controls.Add(addGreenButton);
                addRemoveTools.Controls.Add(addBlueButton);
                addRemoveTools.Controls.Add(addYellowButton);
                addRemoveTools.Controls.Add(addPurpleButton);
                addRemoveTools.Controls.Add(removeButton);
                root.Controls.Add(addRemoveTools, 0, 3);

                TableLayoutPanel positionTools = new TableLayoutPanel();
                positionTools.Dock = DockStyle.Fill;
                positionTools.ColumnCount = 6;
                positionTools.RowCount = 2;
                positionTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26f));
                positionTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
                positionTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26f));
                positionTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
                positionTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26f));
                positionTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
                positionTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                positionTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                xBox = NewCoordinateEditor();
                yBox = NewCoordinateEditor();
                zBox = NewCoordinateEditor();
                AddPositionCell(positionTools, "X", xBox, 0, 0);
                AddPositionCell(positionTools, "Y", yBox, 2, 0);
                AddPositionCell(positionTools, "Z", zBox, 4, 0);
                applyPositionButton = NewDialogButton("Apply Position");
                applyPositionButton.Click += delegate { ApplySelectedPosition(); };
                selectButton = NewDialogButton("Select On Map");
                selectButton.Click += delegate { SelectCurrentOnMap(); };
                Button stackButton = NewDialogButton("Stack at Chest");
                stackButton.Click += delegate { editor.StackChestContentsAtChest(this.chestIndex, this.contentIndexes); RefreshRows(); };
                positionTools.Controls.Add(applyPositionButton, 1, 1);
                positionTools.Controls.Add(selectButton, 3, 1);
                positionTools.Controls.Add(stackButton, 5, 1);
                root.Controls.Add(positionTools, 0, 4);

                FlowLayoutPanel bottom = new FlowLayoutPanel();
                bottom.Dock = DockStyle.Fill;
                bottom.FlowDirection = FlowDirection.RightToLeft;
                Button closeButton = NewDialogButton("Close");
                closeButton.DialogResult = DialogResult.OK;
                bottom.Controls.Add(closeButton);
                root.Controls.Add(bottom, 0, 5);

                AcceptButton = closeButton;
                RefreshRows();
                if (contentList.Items.Count > 0)
                    contentList.Items[0].Selected = true;
                UpdateSelectedFields();
            }

            private static Button NewDialogButton(string text)
            {
                Button button = new Button();
                button.Text = text;
                button.Width = 104;
                button.Height = 26;
                button.Margin = new Padding(3);
                return button;
            }

            private static NumericUpDown NewCoordinateEditor()
            {
                NumericUpDown box = new NumericUpDown();
                box.DecimalPlaces = 2;
                box.Minimum = -100000;
                box.Maximum = 100000;
                box.Increment = 16;
                box.Dock = DockStyle.Fill;
                return box;
            }

            private static void AddPositionCell(TableLayoutPanel panel, string label, Control editor, int labelColumn, int row)
            {
                Label text = new Label();
                text.Text = label;
                text.TextAlign = ContentAlignment.MiddleLeft;
                text.Dock = DockStyle.Fill;
                panel.Controls.Add(text, labelColumn, row);
                panel.Controls.Add(editor, labelColumn + 1, row);
            }

            private int SelectedContentIndex()
            {
                if (contentList.SelectedItems.Count == 0) return -1;
                object tag = contentList.SelectedItems[0].Tag;
                return tag is int ? (int)tag : -1;
            }

            private void RefreshRows()
            {
                int selectedTrueIndex = -1;
                int selectedIndex = SelectedContentIndex();
                if (selectedIndex >= 0 && selectedIndex < editor.mobys.Count)
                    selectedTrueIndex = editor.mobys[selectedIndex].TrueIndex;

                updating = true;
                try
                {
                    contentList.BeginUpdate();
                    contentList.Items.Clear();
                    for (int i = 0; i < contentIndexes.Count; i++)
                    {
                        int contentIndex = contentIndexes[i];
                        if (contentIndex < 0 || contentIndex >= editor.mobys.Count) continue;
                        Moby moby = editor.mobys[contentIndex];
                        ListViewItem item = new ListViewItem(MobyId(moby));
                        item.SubItems.Add(ChestContentValueText(moby));
                        item.SubItems.Add(moby.DisplayLabel);
                        item.SubItems.Add(FormatVector(moby.X, moby.Y, moby.Z));
                        item.Tag = contentIndex;
                        contentList.Items.Add(item);
                        if (moby.TrueIndex == selectedTrueIndex)
                            item.Selected = true;
                    }
                    if (contentList.SelectedItems.Count == 0 && contentList.Items.Count > 0)
                        contentList.Items[0].Selected = true;
                }
                finally
                {
                    contentList.EndUpdate();
                    updating = false;
                }
            }

            private void UpdateSelectedFields()
            {
                if (updating) return;
                int contentIndex = SelectedContentIndex();
                bool enabled = contentIndex >= 0 && contentIndex < editor.mobys.Count;
                redButton.Enabled = enabled;
                greenButton.Enabled = enabled;
                blueButton.Enabled = enabled;
                yellowButton.Enabled = enabled;
                purpleButton.Enabled = enabled;
                applyPositionButton.Enabled = enabled;
                selectButton.Enabled = enabled;
                addRedButton.Enabled = chestIndex >= 0 && chestIndex < editor.mobys.Count;
                addGreenButton.Enabled = addRedButton.Enabled;
                addBlueButton.Enabled = addRedButton.Enabled;
                addYellowButton.Enabled = addRedButton.Enabled;
                addPurpleButton.Enabled = addRedButton.Enabled;
                removeButton.Enabled = enabled && CanRemoveChestContentGem(editor.mobys[contentIndex]);
                xBox.Enabled = enabled;
                yBox.Enabled = enabled;
                zBox.Enabled = enabled;
                if (!enabled) return;

                Moby moby = editor.mobys[contentIndex];
                bool canMoveContent = IsPotentialChestContentMarker(moby);
                removeButton.Enabled = CanRemoveChestContentGem(moby);
                applyPositionButton.Enabled = canMoveContent;
                xBox.Enabled = canMoveContent;
                yBox.Enabled = canMoveContent;
                zBox.Enabled = canMoveContent;
                xBox.Value = ClampDecimal(moby.X, xBox.Minimum, xBox.Maximum);
                yBox.Value = ClampDecimal(moby.Y, yBox.Minimum, yBox.Maximum);
                zBox.Value = ClampDecimal(moby.Z, zBox.Minimum, zBox.Maximum);
            }

            private static decimal ClampDecimal(float value, decimal min, decimal max)
            {
                decimal result = (decimal)value;
                if (result < min) return min;
                if (result > max) return max;
                return result;
            }

            private void SetSelectedColor(string color)
            {
                int contentIndex = SelectedContentIndex();
                if (contentIndex < 0) return;
                editor.SetChestContentGemColor(contentIndex, color);
                RefreshRows();
                SelectContentIndex(contentIndex);
                UpdateSelectedFields();
            }

            private void AddContentGem(string color)
            {
                int addedIndex = editor.AddChestContentGem(chestIndex, color);
                if (addedIndex < 0) return;
                if (!contentIndexes.Contains(addedIndex))
                    contentIndexes.Add(addedIndex);
                contentIndexes.Sort();
                RefreshRows();
                SelectContentIndex(addedIndex);
                UpdateSelectedFields();
            }

            private void RemoveSelectedContentGem()
            {
                int contentIndex = SelectedContentIndex();
                if (contentIndex < 0) return;
                bool removedAppendedRecord = contentIndex < editor.mobys.Count && editor.mobys[contentIndex].IsAppendedRecord;
                bool removed = editor.RemoveChestContentGem(contentIndex);
                if (!removed) return;
                contentIndexes.Remove(contentIndex);
                if (removedAppendedRecord)
                {
                    for (int i = 0; i < contentIndexes.Count; i++)
                    {
                        if (contentIndexes[i] > contentIndex)
                            contentIndexes[i] = contentIndexes[i] - 1;
                    }
                }
                RefreshRows();
                UpdateSelectedFields();
            }

            private void ApplySelectedPosition()
            {
                int contentIndex = SelectedContentIndex();
                if (contentIndex < 0) return;
                editor.SetChestContentPosition(contentIndex, (float)xBox.Value, (float)yBox.Value, (float)zBox.Value);
                RefreshRows();
                SelectContentIndex(contentIndex);
                UpdateSelectedFields();
            }

            private void SelectCurrentOnMap()
            {
                int contentIndex = SelectedContentIndex();
                if (contentIndex < 0) return;
                editor.SelectMoby(contentIndex);
                editor.statusLabel.Text = "Selected " + MobyId(editor.mobys[contentIndex]) + " from the chest contents editor.";
            }

            private void SelectContentIndex(int contentIndex)
            {
                foreach (ListViewItem item in contentList.Items)
                    item.Selected = item.Tag is int && (int)item.Tag == contentIndex;
            }
        }

        private void InitializeGroundOffsets()
        {
            foreach (Moby moby in mobys)
            {
                float groundZ;
                if (TryFindGroundZ(moby.OriginalX, moby.OriginalY, moby.OriginalZ, out groundZ))
                {
                    moby.OriginalGroundOffset = moby.OriginalZ - groundZ;
                    moby.GroundOffset = moby.OriginalGroundOffset;
                    moby.HasGroundOffset = true;
                }
                else
                {
                    moby.OriginalGroundOffset = 0f;
                    moby.GroundOffset = 0f;
                    moby.HasGroundOffset = false;
                }
            }
        }

        private void RefreshGroundOffset(Moby moby)
        {
            if (moby == null) return;
            float groundZ;
            if (TryFindGroundZ(moby.X, moby.Y, moby.Z, out groundZ))
            {
                moby.GroundOffset = moby.Z - groundZ;
                moby.HasGroundOffset = true;
            }
        }

        private float SnapZForMoby(Moby moby, float x, float y, bool resetToOriginalOffset)
        {
            if (moby == null) return 0f;
            float offset = resetToOriginalOffset ? moby.OriginalGroundOffset : moby.GroundOffset;
            float preferredGroundZ = moby.HasGroundOffset ? moby.Z - moby.GroundOffset : moby.Z;
            float groundZ;
            if (TryFindGroundZ(x, y, preferredGroundZ, out groundZ))
            {
                if (resetToOriginalOffset)
                    moby.GroundOffset = moby.OriginalGroundOffset;
                return groundZ + offset;
            }
            return moby.Z;
        }

        private bool TryFindGroundZ(float x, float y, float preferredZ, out float z)
        {
            z = 0f;
            if (geometry == null || geometry.Polygons.Count == 0) return false;

            bool found = false;
            float bestScore = float.MaxValue;
            for (int i = 0; i < geometry.Polygons.Count; i++)
            {
                TerrainPolygon polygon = geometry.Polygons[i];
                if (!polygon.Bounds.Contains(x, y)) continue;
                float candidateZ;
                if (!polygon.TryGetZ(x, y, out candidateZ)) continue;
                float score = Math.Abs(candidateZ - preferredZ);
                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    z = candidateZ;
                }
            }
            return found;
        }

        private void SaveEdits()
        {
            if (!HasLoadedLevel())
            {
                MessageBox.Show(this, "Choose a level from the dropdown before saving edits.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                statusLabel.Text = "Choose a level before saving edits.";
                return;
            }

            try
            {
                NormalizeAppendedTrueIndexes();
                savedEditCount = MobyEditStore.Save(editPath, mobys, currentLevelName);
                savedTerrainEditCount = TerrainEditStore.Save(terrainEditPath, geometry == null ? null : geometry.Polygons, currentLevelName);
                SavePlayerColorOptions(false);
                hasUnsavedEdits = false;
                RefreshMobyList();
                UpdateInspector();
                statusLabel.Text = "Saved " + savedEditCount.ToString() + " moby edit(s), " + savedTerrainEditCount.ToString() + " terrain edit(s), and " + CountActivePlayerColorOptions().ToString() + " color option(s). " + TreasureSummaryText(CalculateTreasureSummary()) + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save edits failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PlayerColorControlsChanged()
        {
            if (updatingPlayerColorControls) return;

            spyroRecolorEnabled = spyroRecolorBox != null && spyroRecolorBox.Checked;
            crystalDragonRecolorEnabled = crystalDragonRecolorBox != null && crystalDragonRecolorBox.Checked;
            spyroColorPreset = SelectedComboText(spyroColorBox, "Classic Purple");
            crystalDragonColorPreset = SelectedComboText(crystalDragonColorBox, "Classic Green");
            UpdatePlayerColorControls();
            if (statusLabel != null)
                statusLabel.Text = PlayerColorSummary() + " Save Colors or Save Edits to keep this choice.";
        }

        private void UpdatePlayerColorControls()
        {
            if (spyroRecolorBox == null || spyroColorBox == null || crystalDragonRecolorBox == null || crystalDragonColorBox == null)
                return;

            updatingPlayerColorControls = true;
            try
            {
                spyroRecolorBox.Checked = spyroRecolorEnabled;
                crystalDragonRecolorBox.Checked = crystalDragonRecolorEnabled;
                SelectComboValue(spyroColorBox, spyroColorPreset, "Classic Purple");
                SelectComboValue(crystalDragonColorBox, crystalDragonColorPreset, "Classic Green");
                spyroColorBox.Enabled = spyroRecolorEnabled;
                crystalDragonColorBox.Enabled = crystalDragonRecolorEnabled;
                if (spyroColorSwatch != null)
                    spyroColorSwatch.BackColor = spyroRecolorEnabled ? PlayerPresetColor(spyroColorPreset) : Color.FromArgb(198, 201, 207);
                if (crystalDragonColorSwatch != null)
                    crystalDragonColorSwatch.BackColor = crystalDragonRecolorEnabled ? CrystalDragonPresetColor(crystalDragonColorPreset) : Color.FromArgb(198, 201, 207);
                if (createPlayerColorPatchButton != null)
                {
                    createPlayerColorPatchButton.Enabled = false;
                    createPlayerColorPatchButton.Text = "Color BIN (not solved)";
                }
                if (playerColorStatusLabel != null)
                    playerColorStatusLabel.Text = PlayerColorSummary();
            }
            finally
            {
                updatingPlayerColorControls = false;
            }
        }

        private void SavePlayerColorOptions(bool showMessage)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;

            Dictionary<string, object> spyro = new Dictionary<string, object>();
            spyro["enabled"] = spyroRecolorEnabled;
            spyro["preset"] = spyroColorPreset;
            spyro["previewHex"] = ColorToHex(PlayerPresetColor(spyroColorPreset));

            Dictionary<string, object> crystal = new Dictionary<string, object>();
            crystal["enabled"] = crystalDragonRecolorEnabled;
            crystal["preset"] = crystalDragonColorPreset;
            crystal["previewHex"] = ColorToHex(CrystalDragonPresetColor(crystalDragonColorPreset));

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["editor"] = "NativeSpyroEditor";
            root["generatedAt"] = DateTime.UtcNow.ToString("s") + "Z";
            root["patchStatus"] = "pending-color-byte-map";
            root["activeCount"] = CountActivePlayerColorOptions();
            root["spyro"] = spyro;
            root["crystalDragon"] = crystal;

            File.WriteAllText(playerEditPath, serializer.Serialize(root), Encoding.UTF8);
            UpdatePlayerColorControls();
            if (showMessage && statusLabel != null)
                statusLabel.Text = "Saved " + CountActivePlayerColorOptions().ToString() + " player color option(s) to " + Path.GetFileName(playerEditPath) + ".";
        }

        private void LoadPlayerColorOptions(bool showMessage)
        {
            try
            {
                spyroRecolorEnabled = false;
                crystalDragonRecolorEnabled = false;
                spyroColorPreset = "Classic Purple";
                crystalDragonColorPreset = "Classic Green";

                if (File.Exists(playerEditPath))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(playerEditPath, Encoding.UTF8)) as Dictionary<string, object>;
                    if (root != null)
                    {
                        Dictionary<string, object> spyro = root.ContainsKey("spyro") ? root["spyro"] as Dictionary<string, object> : null;
                        Dictionary<string, object> crystal = root.ContainsKey("crystalDragon") ? root["crystalDragon"] as Dictionary<string, object> : null;
                        spyroRecolorEnabled = GeometryLoader.GetBool(spyro, "enabled", false);
                        crystalDragonRecolorEnabled = GeometryLoader.GetBool(crystal, "enabled", false);
                        spyroColorPreset = ValidComboValue(spyroColorBox, GeometryLoader.GetString(spyro, "preset", "Classic Purple"), "Classic Purple");
                        crystalDragonColorPreset = ValidComboValue(crystalDragonColorBox, GeometryLoader.GetString(crystal, "preset", "Classic Green"), "Classic Green");
                    }
                }

                UpdatePlayerColorControls();
                if (showMessage && statusLabel != null)
                    statusLabel.Text = "Loaded player color options. " + PlayerColorSummary();
            }
            catch (Exception ex)
            {
                if (showMessage)
                    MessageBox.Show(this, ex.Message, "Load player colors failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetPlayerColorOptions()
        {
            spyroRecolorEnabled = false;
            crystalDragonRecolorEnabled = false;
            spyroColorPreset = "Classic Purple";
            crystalDragonColorPreset = "Classic Green";
            UpdatePlayerColorControls();
            SavePlayerColorOptions(false);
            if (statusLabel != null)
                statusLabel.Text = "Reset player color options.";
        }

        private int CountActivePlayerColorOptions()
        {
            int count = 0;
            if (spyroRecolorEnabled) count++;
            if (crystalDragonRecolorEnabled) count++;
            return count;
        }

        private string PlayerColorSummary()
        {
            if (CountActivePlayerColorOptions() == 0)
                return "No player color patches selected.";
            List<string> parts = new List<string>();
            if (spyroRecolorEnabled) parts.Add("Spyro " + spyroColorPreset);
            if (crystalDragonRecolorEnabled) parts.Add("Crystal " + crystalDragonColorPreset);
            return "Selected: " + string.Join(", ", parts.ToArray()) + ".";
        }

        private static string SelectedComboText(ComboBox combo, string fallback)
        {
            if (combo == null || combo.SelectedItem == null) return fallback;
            string value = combo.SelectedItem.ToString();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string ValidComboValue(ComboBox combo, string value, string fallback)
        {
            if (combo == null) return fallback;
            if (string.IsNullOrEmpty(value)) return fallback;
            return combo.FindStringExact(value) >= 0 ? value : fallback;
        }

        private static void SelectComboValue(ComboBox combo, string value, string fallback)
        {
            if (combo == null || combo.Items.Count == 0) return;
            int index = combo.FindStringExact(value);
            if (index < 0) index = combo.FindStringExact(fallback);
            if (index < 0) index = 0;
            combo.SelectedIndex = index;
        }

        private void RunLiveMobyMove(bool revert)
        {
            if (mobys.Count == 0)
            {
                MessageBox.Show(this, "Load a level before writing live moby positions.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
            {
                MessageBox.Show(this, "Select a moby first.", "No moby selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Moby moby = mobys[selectedMobyIndex];
            if (!revert && !moby.IsEdited)
            {
                MessageBox.Show(this, "Move the selected moby before applying it to live DuckStation RAM.", "No live move to apply", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!revert)
                SaveEdits();

            string scriptPath = Path.Combine(workspace, "tools", "Move-DuckStationMoby.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing live mover script: " + scriptPath, "Live mover missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                List<int> liveTargets = GetLinkedMoveIndexes(selectedMobyIndex);
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -MobyIndexes");
                foreach (int targetIndex in liveTargets)
                    args.Append(" ").Append(mobys[targetIndex].Index.ToString());
                if (revert)
                    args.Append(" -Revert");
                else
                    args.Append(" -FromNativeEdit -Apply -HoldSeconds 3");
                args.Append(" -NativeEditsPath ").Append(QuoteArgument(editPath));
                args.Append(" -OriginalsPath ").Append(QuoteArgument(liveOriginalsPath));
                if (currentLevelId >= 0)
                    args.Append(" -ExpectedLevelId ").Append(currentLevelId.ToString());
                string catalogPath = Path.Combine(workspace, currentLevelKey + "-moby-catalog.json");
                if (File.Exists(catalogPath))
                    args.Append(" -CatalogPath ").Append(QuoteArgument(catalogPath));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                string targetText = liveTargets.Count > 1 ? "linked group " + LinkedMoveGroupText(selectedMobyIndex) : MobyId(moby);
                statusLabel.Text = revert
                    ? "Started live revert for " + targetText + "."
                    : "Started live DuckStation RAM apply for " + targetText + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start live mover", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunLiveTerrainMove(bool revert)
        {
            if (geometry == null || geometry.Polygons.Count == 0)
            {
                MessageBox.Show(this, "Load a level before writing live terrain vertices.", "No terrain loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedTerrainIndex < 0 || selectedTerrainIndex >= geometry.Polygons.Count)
            {
                MessageBox.Show(this, "Select a terrain face first.", "No terrain face selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
            if (!polygon.IsTerrainEdited)
            {
                MessageBox.Show(this, "Nudge the selected face Z before applying or reverting it in live DuckStation RAM.", "No terrain edit selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrEmpty(polygon.RuntimeKey) || string.IsNullOrEmpty(polygon.SectorOffset))
            {
                MessageBox.Show(this, "The selected terrain face does not have a runtime sector/face handle.", "Terrain handle missing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!revert)
                SaveEdits();

            string scriptPath = Path.Combine(workspace, "tools", "Move-DuckStationTerrainFace.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing live terrain mover script: " + scriptPath, "Live terrain mover missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -RuntimeKey ").Append(QuoteArgument(polygon.RuntimeKey));
                if (revert)
                    args.Append(" -Revert");
                else
                    args.Append(" -Apply -HoldSeconds 3");
                args.Append(" -TerrainEditsPath ").Append(QuoteArgument(terrainEditPath));
                args.Append(" -OriginalRamPath ").Append(QuoteArgument(currentRamPath));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = revert
                    ? "Started live terrain Z revert for face " + selectedTerrainIndex.ToString() + "."
                    : "Started live terrain Z apply for face " + selectedTerrainIndex.ToString() + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start live terrain mover", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPatchExporter(bool topRankedOnly)
        {
            if (!currentLevelSupportsSourcePatchers)
            {
                MessageBox.Show(this, currentLevelName + " source BIN export is not wired yet. Use Save Edits for now while we map this level.", "Exporter not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (mobys.Count == 0)
            {
                MessageBox.Show(this, "Load a level before creating a patch test BIN.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveEdits();
            if (topRankedOnly)
            {
                if (!HasAnySavedMobyEdits())
                {
                    MessageBox.Show(this, "Make and save at least one mapped-level moby edit before creating a loader BIN.", "No moby edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            else if (CountEditedMobys() == 0)
            {
                MessageBox.Show(this, "Make and save at least one moby edit before creating a loader BIN.", "No moby edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = topRankedOnly
                ? Path.Combine(workspace, "tools", "Export-SpyroLevelMobyPatchTest.ps1")
                : Path.Combine(workspace, "tools", "Export-StoneHillNativePatchTest.ps1");
            if (!topRankedOnly && !IsStoneHillLevel())
            {
                MessageBox.Show(this, currentLevelName + " only has the confirmed loader-table exporter wired right now. The old broad source-window diagnostic is Stone Hill-only.", "Broad exporter not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing patch exporter script: " + scriptPath, "Patch exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                string outName = topRankedOnly
                    ? "Spyro the Dragon (USA)-loaderpatchtest.bin"
                    : "Spyro the Dragon (USA)-nativepatchtest-broad.bin";
                string outPath = Path.Combine(workspace, outName);
                if (topRankedOnly)
                {
                    args.Append(" -LevelKey All");
                    args.Append(" -AppendPolicy GemsAndEnemies");
                }
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".patchplan.json"));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = topRankedOnly
                    ? "Started all mapped levels loader-table BIN export."
                    : "Started old broad source-window diagnostic export.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start patch exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPlayerColorPatchExporter()
        {
            SavePlayerColorOptions(false);
            MessageBox.Show(this, "Spyro/crystal color export is disabled for now. The WAD 83/WAD 85 actor-package tests and Artisans source-window tests did not affect the visible colors, and live RAM probes proved unsafe. The saved choices remain in spyro-player-edits.json for when the real color source is mapped.", "Color source not solved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
#if false
            if (CountActivePlayerColorOptions() == 0)
            {
                MessageBox.Show(this, "Choose at least one Spyro or crystal-dragon color option first.", "No color patch selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroPlayerColorPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing player color exporter script: " + scriptPath, "Color exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "The Spyro/crystal color source is not confirmed yet. The generated BIN is a disposable research test and may not visibly change the game.\n\nCreate it anyway?",
                    "Research color patch",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;

                List<string> targets = new List<string>();
                if (spyroRecolorEnabled) targets.Add("Spyro");
                if (crystalDragonRecolorEnabled) targets.Add("CrystalDragon");

                string outPath = Path.Combine(workspace, "Spyro the Dragon (USA)-colorpatchtest-actor85.bin");
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -PlayerEditsPath ").Append(QuoteArgument(playerEditPath));
                args.Append(" -Targets ").Append(QuoteArgument(string.Join(",", targets.ToArray())));
                args.Append(" -ActorHueFallback -SkipRawSourceWindows -ActorEntryIndexes 85");
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -CuePath ").Append(QuoteArgument(Path.ChangeExtension(outPath, ".cue")));
                args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".patchplan.json"));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started research-only player/crystal color BIN export.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start color patch exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#endif
        }

        private void RunSingleAppendExporter()
        {
            if (!currentLevelSupportsSourcePatchers)
            {
                MessageBox.Show(this, currentLevelName + " source BIN export is not wired yet.", "Exporter not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
            {
                MessageBox.Show(this, "Select one true-added object first.", "No added object selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Moby moby = mobys[selectedMobyIndex];
            if (!moby.IsAppendedRecord || moby.TrueIndex < SourceRecordCountForLevel(currentLevelKey))
            {
                MessageBox.Show(this, "Select a true-added object first. This test exporter only writes one appended source record and ignores other edits.", "Not a true-added object", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool externalChest = IsExternalChestTemplate(moby);
            bool externalLockedChest = IsExternalLockedChestTemplate(moby);
            if (externalLockedChest)
            {
                MessageBox.Show(this, "Locked chest true-adds are paused for the normal editor path. The current actor-package/root import soft-locks Artisans even when no chest row is appended, so I am keeping this out of the button path while we isolate package bytes vs root table vs actor-id table.", "Locked chest test paused", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (IsExternalSpringChestTemplate(moby))
            {
                RunSpringChestPairExporter(moby);
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "Create an isolated test BIN for only " + MobyId(moby) + " " + moby.DisplayLabel + "?\n\nThis ignores normal moves and every other added object. Use it to test one experimental true-add safely.",
                "Test one true-added object",
                MessageBoxButtons.YesNo,
                IsExperimentalTrueAddTemplate(moby) || externalChest ? MessageBoxIcon.Warning : MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            SaveEdits();

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroLevelMobyPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing patch exporter script: " + scriptPath, "Patch exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string outName = "Spyro the Dragon (USA)-" + currentLevelKey.ToLowerInvariant() + "-singleadd-T" + moby.TrueIndex.ToString() + ".bin";
                string outPath = Path.Combine(workspace, outName);
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -LevelKey ").Append(QuoteArgument(ScriptLevelKey(currentLevelKey)));
                AppendSourceImageArgument(args);
                args.Append(" -SingleAppendTrueIndex ").Append(moby.TrueIndex.ToString());
                args.Append(" -AppendPolicy All");
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -CuePath ").Append(QuoteArgument(Path.ChangeExtension(outPath, ".cue")));
                args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".patchplan.json"));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started isolated true-add BIN export for " + MobyId(moby) + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start single-add exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsDarkHollowNativeSpringChestAppend(Moby moby)
        {
            if (!IsDarkHollowLevel() || moby == null) return false;
            if (!moby.IsAppendedRecord) return false;
            return string.Equals(moby.AppendSourceLevelKey, "DarkHollow", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(moby.AppendRuntimeIdentityPolicy, "darkhollow-native-00c2-spring-chest", StringComparison.OrdinalIgnoreCase) &&
                IsDarkHollowNativeSpringChestRecord(moby);
        }

        private void RunDarkHollowNativeSpringChestExporter(Moby moby)
        {
            DialogResult result = MessageBox.Show(
                this,
                "Create an isolated Dark Hollow native spring chest BIN for " + MobyId(moby) + "?\n\nThis exports one local 0x00C2 spring chest clone and does not inject the Artisans/Stone Hill helper wrapper.",
                "Test Dark Hollow spring chest",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            SaveEdits();

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroLevelMobyPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing patch exporter script: " + scriptPath, "Patch exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string experimentsDir = Path.Combine(workspace, "_local", "experiments");
                Directory.CreateDirectory(experimentsDir);
                ApplyDarkHollowNativeSpringChestProfile(moby, null);

                string stem = "editor-darkhollow-native-spring-T" + moby.TrueIndex.ToString();
                string editsPath = Path.Combine(experimentsDir, stem + "-edits.json");
                string outPath = Path.Combine(experimentsDir, stem + ".bin");
                int editCount = MobyEditStore.Save(editsPath, new List<Moby>(new Moby[] { moby }), currentLevelName);
                if (editCount != 1)
                {
                    MessageBox.Show(this, "Dark Hollow spring chest export expected one saved record, but wrote " + editCount.ToString() + ". I stopped before creating a broken BIN.", "Dark Hollow spring chest", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -LevelKey DarkHollow");
                args.Append(" -NativeEditsPath ").Append(QuoteArgument(editsPath));
                AppendSourceImageArgument(args);
                args.Append(" -AppendPolicy All -SkipTreasureTotalPatch");
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -CuePath ").Append(QuoteArgument(Path.ChangeExtension(outPath, ".cue")));
                args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".patchplan.json"));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started Dark Hollow native spring chest BIN export for " + MobyId(moby) + ". Load the CUE, then flame or charge the chest.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start Dark Hollow spring chest exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSpringChestPairExporter(Moby selected)
        {
            string normalizedLevelKey = SpyroLevelCatalog.NormalizeKey(currentLevelKey);
            if (!string.Equals(normalizedLevelKey, "artisans", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedLevelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Spring chest pair export is currently official for Artisans and Stone Hill only. Dark Hollow is deferred because the helper can create a collectible sparkle there but the gem mesh is still invisible.", "Spring chest pair export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int controllerIndex;
            int shellIndex;
            if (!TryResolveSpringChestRuntimePair(selected, out controllerIndex, out shellIndex))
            {
                MessageBox.Show(this, "Select a spring chest shell or its paired controller first. New spring chests must be added with the Spring Chest Object Library template so the editor creates both records.", "Spring chest pair needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "Create an isolated " + currentLevelName + " spring chest pair BIN for " + MobyId(mobys[controllerIndex]) + "/" + MobyId(mobys[shellIndex]) + "?\n\nThis exports the paired controller/shell and injects the working in-game pop/collect patch automatically. After loading the CUE, flame or charge the chest; no separate Spring Helper pass is needed. This path is official for Artisans and Stone Hill.",
                "Test spring chest pair",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            SaveEdits();

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroSpringChestPairInGamePatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing spring chest in-game exporter script: " + scriptPath, "Patch exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string experimentsDir = Path.Combine(workspace, "_local", "experiments");
                Directory.CreateDirectory(experimentsDir);
                Moby controller = mobys[controllerIndex];
                Moby shell = mobys[shellIndex];
                PrepareSpringChestPair(controller, shell);
                ApplySpringChestPairExportProfile(controller, shell);
                AlignSpringChestControllerToShell(controller, shell);
                if (!EnsureSpringChestPairAppendMetadata(controller, shell))
                {
                    MessageBox.Show(this, "Spring chest pair export needs the new controller/shell pair created from the Spring Chest Object Library template. Select the new spring chest shell or controller, then run Test Selected Add BIN.", "Spring chest pair export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string stem = "editor-" + normalizedLevelKey + "-springpair-T" + controller.TrueIndex.ToString() + "-T" + shell.TrueIndex.ToString();
                string editsPath = Path.Combine(experimentsDir, stem + "-edits.json");
                string outPath = Path.Combine(experimentsDir, stem + ".bin");
                int pairEditCount = MobyEditStore.Save(editsPath, new List<Moby>(new Moby[] { controller, shell }), currentLevelName);
                if (pairEditCount != 2)
                {
                    MessageBox.Show(this, "Spring chest pair export expected two saved records, but wrote " + pairEditCount.ToString() + ". I stopped before creating a broken shell-only BIN.", "Spring chest pair export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -LevelKey ").Append(QuoteArgument(ScriptLevelKey(currentLevelKey)));
                args.Append(" -NativeEditsPath ").Append(QuoteArgument(editsPath));
                AppendSourceImageArgument(args);
                args.Append(" -RewardGemIdByte 0x").Append(RewardGemIdByteForSpringShell(shell).ToString("X2"));
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -CuePath ").Append(QuoteArgument(Path.ChangeExtension(outPath, ".cue")));
                args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".patchplan.json"));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started spring chest in-game BIN export for " + MobyId(mobys[controllerIndex]) + "/" + MobyId(mobys[shellIndex]) + ". Load the CUE, then flame or charge the chest.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start spring chest pair exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool EnsureSpringChestPairAppendMetadata(Moby controller, Moby shell)
        {
            if (controller == null || shell == null) return false;
            int sourceCount = SourceRecordCountForLevel(currentLevelKey);
            if (controller.TrueIndex < sourceCount || shell.TrueIndex < sourceCount) return false;

            controller.IsAppendedRecord = true;
            shell.IsAppendedRecord = true;

            controller.AppendSourceTrueIndex = 30;
            controller.AppendSourceIndex = -1;
            controller.AppendSourceLabel = "Spring Chest controller";
            controller.AppendSourceLevelKey = "TownSquare";
            controller.AppendSourceLevelName = "Town Square";
            controller.AppendSourceFamily = "springChest";
            controller.AppendLoaderTransformedDonor = true;
            controller.AppendDonorMapConfidence = "source-row-transform";
            controller.AppendDependencyRisk = "External spring chest donor. Export copies the donor row and applies the level-specific spring chest package profile.";

            shell.AppendSourceTrueIndex = 82;
            shell.AppendSourceIndex = -1;
            shell.AppendSourceLabel = "Spring Chest";
            shell.AppendSourceLevelKey = "TownSquare";
            shell.AppendSourceLevelName = "Town Square";
            shell.AppendSourceFamily = "springChest";
            shell.AppendLoaderTransformedDonor = true;
            shell.AppendDonorMapConfidence = "source-row-transform";
            shell.AppendDependencyRisk = "External spring chest donor. Export copies the donor row and applies the level-specific spring chest package profile.";

            return true;
        }

        private void RunObjectAddLab()
        {
            if (!currentLevelSupportsSourcePatchers)
            {
                MessageBox.Show(this, currentLevelName + " source BIN export is not wired yet.", "Object Add Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count)
            {
                MessageBox.Show(this, "Select the source object to test first. The lab will clone that selected source into a temporary experiment BIN.", "Object Add Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int sourceRecordCount = SourceRecordCountForLevel(currentLevelKey);
            Moby source = mobys[selectedMobyIndex];
            if (source == null || source.TrueIndex < 0 || source.TrueIndex >= sourceRecordCount)
            {
                MessageBox.Show(this, "Select an existing source-table moby first. The lab cannot use appended, runtime-only, or unmapped records as donors yet.", "Object Add Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int slotIndex = FindDefaultObjectAddLabSlotIndex(source.Index);
            string experiment;
            if (!ShowObjectAddLabDialog(source, slotIndex, out experiment))
                return;

            if (experiment == "slot" && (slotIndex < 0 || slotIndex >= mobys.Count))
            {
                MessageBox.Show(this, "I could not find a reusable same-level source slot for the slot-reuse test. Mark or select a helper/nonvisual slot, then try again.", "Object Add Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                bool append = experiment == "append";
                Moby target = append ? null : mobys[slotIndex];
                int targetTrueIndex = append ? sourceRecordCount : target.TrueIndex;
                int targetListIndex = append ? mobys.Count : target.Index;

                string experimentName = append ? "append" : "slotreuse";
                string fileStem = "object-add-lab-" + SafeFilePart(currentLevelKey) + "-" + experimentName + "-srcT" + source.TrueIndex.ToString();
                if (append)
                    fileStem += "-newT" + targetTrueIndex.ToString();
                else
                    fileStem += "-slotT" + targetTrueIndex.ToString();

                string editsPath = Path.Combine(workspace, fileStem + ".json");
                string outPath = Path.Combine(workspace, "Spyro the Dragon (USA)-" + fileStem + ".bin");
                WriteObjectAddLabEdits(editsPath, source, target, targetTrueIndex, targetListIndex, append);
                StartObjectAddLabExporter(editsPath, outPath, append, targetTrueIndex);

                statusLabel.Text = append
                    ? "Started Object Add Lab append BIN for source T" + source.TrueIndex.ToString() + "."
                    : "Started Object Add Lab slot-reuse BIN for source T" + source.TrueIndex.ToString() + " into T" + targetTrueIndex.ToString() + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start Object Add Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ShowObjectAddLabDialog(Moby source, int slotIndex, out string experiment)
        {
            experiment = null;
            Moby slot = (slotIndex >= 0 && slotIndex < mobys.Count) ? mobys[slotIndex] : null;

            using (Form dialog = new Form())
            using (TableLayoutPanel root = new TableLayoutPanel())
            using (Label info = new Label())
            using (Button appendButton = new Button())
            using (Button slotButton = new Button())
            using (Button cancelButton = new Button())
            {
                dialog.Text = "Object Add Lab";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(520, 220);

                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(12);
                root.ColumnCount = 2;
                root.RowCount = 4;
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

                StringBuilder message = new StringBuilder();
                message.AppendLine("Selected source: " + MobyId(source) + " " + source.DisplayLabel);
                message.AppendLine("Append test: clone this same-level source as one new source record.");
                if (slot != null)
                    message.AppendLine("Slot test: clone it into " + MobyId(slot) + " " + slot.DisplayLabel + " without growing the table.");
                else
                    message.AppendLine("Slot test: no reusable helper/nonvisual slot was found yet.");
                message.AppendLine();
                message.AppendLine("Each option writes a temporary lab edit JSON and a disposable BIN/CUE. Your real saved edits are not changed.");
                if (IsExperimentalTrueAddTemplate(source))
                    message.AppendLine("This source looks behavior-linked, so test only one BIN at a time.");

                info.Text = message.ToString();
                info.Dock = DockStyle.Fill;
                info.AutoSize = false;

                appendButton.Text = "Append New Record BIN";
                appendButton.Dock = DockStyle.Fill;
                appendButton.Click += delegate { dialog.Tag = "append"; dialog.DialogResult = DialogResult.OK; dialog.Close(); };

                slotButton.Text = "Slot-Reuse BIN";
                slotButton.Dock = DockStyle.Fill;
                slotButton.Enabled = slot != null;
                slotButton.Click += delegate { dialog.Tag = "slot"; dialog.DialogResult = DialogResult.OK; dialog.Close(); };

                cancelButton.Text = "Cancel";
                cancelButton.Dock = DockStyle.Fill;
                cancelButton.Click += delegate { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); };

                root.Controls.Add(info, 0, 0);
                root.SetColumnSpan(info, 2);
                root.Controls.Add(appendButton, 0, 1);
                root.SetColumnSpan(appendButton, 2);
                root.Controls.Add(slotButton, 0, 2);
                root.SetColumnSpan(slotButton, 2);
                root.Controls.Add(cancelButton, 0, 3);
                root.SetColumnSpan(cancelButton, 2);
                dialog.Controls.Add(root);
                dialog.AcceptButton = appendButton;
                dialog.CancelButton = cancelButton;

                DialogResult result = dialog.ShowDialog(this);
                experiment = dialog.Tag as string;
                return result == DialogResult.OK && !string.IsNullOrEmpty(experiment);
            }
        }

        private int FindDefaultObjectAddLabSlotIndex(int sourceIndex)
        {
            MobyChoice selectedSlot = SelectedChoice(addSlotBox);
            if (selectedSlot != null
                && selectedSlot.Index >= 0
                && selectedSlot.Index < mobys.Count
                && selectedSlot.Index != sourceIndex
                && CanUseAsAddSlot(mobys[selectedSlot.Index]))
                return selectedSlot.Index;

            for (int i = 0; i < mobys.Count; i++)
            {
                if (i == sourceIndex) continue;
                if (!CanUseAsAddSlot(mobys[i])) continue;
                if (IsLikelyReusableSlot(mobys[i]))
                    return i;
            }
            return -1;
        }

        private void WriteObjectAddLabEdits(string editsPath, Moby source, Moby target, int targetTrueIndex, int targetListIndex, bool append)
        {
            float labX = source.X + 128f;
            float labY = source.Y;
            float labZ = source.Z;
            float originalX = append || target == null ? 0f : target.OriginalX;
            float originalY = append || target == null ? 0f : target.OriginalY;
            float originalZ = append || target == null ? 0f : target.OriginalZ;

            Dictionary<string, object> mutation = new Dictionary<string, object>();
            mutation["mode"] = append ? "appendFromSource" : "cloneIntoSlot";
            mutation["sourceIndex"] = source.Index;
            mutation["sourceTrueIndex"] = source.TrueIndex;
            mutation["sourceLabel"] = source.DisplayLabel ?? "";
            if (append)
            {
                mutation["targetTrueIndex"] = targetTrueIndex;
                mutation["note"] = "Object Add Lab: append one same-level source record for an isolated runtime test.";
            }
            else
            {
                mutation["note"] = "Object Add Lab: clone the selected source into one same-level slot without growing the source table.";
            }

            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["index"] = targetListIndex;
            edit["trueIndex"] = targetTrueIndex;
            edit["label"] = source.DisplayLabel + (append ? " (lab append)" : " (lab slot reuse)");
            edit["typeHex"] = "0x" + source.Type.ToString("X2");
            edit["stateHex"] = "0x" + source.State.ToString("X2");
            edit["runtimeAddress"] = "0x00000000";
            edit["specialDataPointer"] = FormatAddress(source.SpecialDataPointer);
            edit["sourceByte36Hex"] = "0x" + source.SourceByte36.ToString("X2");
            edit["sourceByte37Hex"] = "0x" + source.SourceByte37.ToString("X2");
            edit["sourceByte4FHex"] = "0x" + source.SourceByte4F.ToString("X2");
            edit["flag4AHex"] = "0x" + source.Flag4A.ToString("X2");
            edit["flag4BHex"] = "0x" + source.Flag4B.ToString("X2");
            edit["patchStatus"] = append ? "object-add-lab-append" : "object-add-lab-slot-reuse";
            edit["patchLead"] = append
                ? "Object Add Lab append from source T" + source.TrueIndex.ToString()
                : "Object Add Lab clone source T" + source.TrueIndex.ToString() + " into slot T" + targetTrueIndex.ToString();
            edit["behaviorNote"] = "Temporary Object Add Lab experiment. This file is not the level's saved edit file.";
            edit["specialDataNote"] = append ? "Exporter may copy same-level source special data for this isolated append." : "Slot reuse avoids growing the source moby table.";
            edit["original"] = NewLabVector(originalX, originalY, originalZ);
            edit["edited"] = NewLabVector(labX, labY, labZ);
            edit["rawOriginal"] = NewLabRawVector(originalX, originalY, originalZ);
            edit["rawEdited"] = NewLabRawVector(labX, labY, labZ);
            edit["rawDelta"] = NewLabRawDelta(originalX, originalY, originalZ, labX, labY, labZ);
            edit["recordMutation"] = mutation;

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["generatedAt"] = DateTime.Now.ToString("s");
            root["editor"] = "NativeSpyroEditor Object Add Lab";
            root["levelName"] = currentLevelName;
            root["levelKey"] = currentLevelKey;
            root["note"] = "Temporary single-experiment Object Add Lab edits. Safe to delete after testing.";
            root["editCount"] = 1;
            root["edits"] = new object[] { edit };

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            File.WriteAllText(editsPath, serializer.Serialize(root), Encoding.UTF8);
        }

        private void StartObjectAddLabExporter(string editsPath, string outPath, bool append, int targetTrueIndex)
        {
            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroLevelMobyPatchTest.ps1");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("Missing patch exporter script.", scriptPath);

            StringBuilder args = new StringBuilder();
            args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
            args.Append(QuoteArgument(scriptPath));
            args.Append(" -LevelKey ").Append(QuoteArgument(ScriptLevelKey(currentLevelKey)));
            args.Append(" -NativeEditsPath ").Append(QuoteArgument(editsPath));
            AppendSourceImageArgument(args);
            args.Append(" -AppendPolicy All");
            args.Append(" -SkipTreasureTotalPatch");
            if (append)
            {
                args.Append(" -SingleAppendTrueIndex ").Append(targetTrueIndex.ToString());
            }
            args.Append(" -OutPath ").Append(QuoteArgument(outPath));
            args.Append(" -CuePath ").Append(QuoteArgument(Path.ChangeExtension(outPath, ".cue")));
            args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".patchplan.json"));

            StartWorkspaceProcess("powershell.exe", args.ToString());
        }

        private static Dictionary<string, object> NewLabVector(float x, float y, float z)
        {
            Dictionary<string, object> vector = new Dictionary<string, object>();
            vector["x"] = Math.Round(x, 4);
            vector["y"] = Math.Round(y, 4);
            vector["z"] = Math.Round(z, 4);
            return vector;
        }

        private static Dictionary<string, object> NewLabRawVector(float x, float y, float z)
        {
            Dictionary<string, object> vector = new Dictionary<string, object>();
            vector["x"] = ToRawCoordinate(x);
            vector["y"] = ToRawCoordinate(y);
            vector["z"] = ToRawCoordinate(z);
            return vector;
        }

        private static Dictionary<string, object> NewLabRawDelta(float originalX, float originalY, float originalZ, float editedX, float editedY, float editedZ)
        {
            Dictionary<string, object> delta = new Dictionary<string, object>();
            delta["x"] = ToRawCoordinate(editedX) - ToRawCoordinate(originalX);
            delta["y"] = ToRawCoordinate(editedY) - ToRawCoordinate(originalY);
            delta["z"] = ToRawCoordinate(editedZ) - ToRawCoordinate(originalZ);
            return delta;
        }

        private static string SafeFilePart(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            StringBuilder builder = new StringBuilder();
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
                else if (ch == '-' || ch == '_')
                    builder.Append(ch);
            }
            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private void RunTerrainPatchExporter()
        {
            if (!currentLevelSupportsSourcePatchers || !IsStoneHillLevel())
            {
                MessageBox.Show(this, currentLevelName + " terrain BIN export is not wired yet. Runtime inspection/edit saving is available first.", "Exporter not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (geometry == null || geometry.Polygons.Count == 0)
            {
                MessageBox.Show(this, "Load Stone Hill before creating a terrain patch test BIN.", "No terrain loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveEdits();
            if (CountEditedTerrainFaces() == 0 && CountCustomTerrainTextureImports() == 0)
            {
                MessageBox.Show(this, "Make and save at least one terrain height edit, texture-ID swap, or custom PNG texture import before creating a terrain BIN.", "No terrain edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Create-StoneHillRuntimeTerrainPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing terrain patch exporter script: " + scriptPath, "Terrain exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                if (!string.IsNullOrEmpty(currentRamPath) && File.Exists(currentRamPath))
                    args.Append(" -RamPath ").Append(QuoteArgument(currentRamPath));
                args.Append(" -TerrainEditsPath ").Append(QuoteArgument(terrainEditPath));
                if (!string.IsNullOrEmpty(customTerrainTexturesPath) && File.Exists(customTerrainTexturesPath))
                    args.Append(" -CustomTexturesPath ").Append(QuoteArgument(customTerrainTexturesPath));
                args.Append(" -OutPath ").Append(QuoteArgument(Path.Combine(workspace, "Spyro the Dragon (USA)-runtime-terrainpatchtest.bin")));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started exact runtime-sector terrain BIN export" + (CountCustomTerrainTextureImports() > 0 ? " with custom texture imports." : ".");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start terrain patch exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateLevelTextChoices(ComboBox box)
        {
            if (box == null) return;
            box.Items.Clear();
            LevelTextChoice[] choices = LevelTextChoice.All();
            for (int i = 0; i < choices.Length; i++)
                box.Items.Add(choices[i]);
            for (int i = 0; i < box.Items.Count; i++)
            {
                LevelTextChoice choice = box.Items[i] as LevelTextChoice;
                if (choice != null && string.Equals(choice.ScriptKey, "StoneHill", StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedIndex = i;
                    break;
                }
            }
            if (box.SelectedIndex < 0 && box.Items.Count > 0)
                box.SelectedIndex = 0;
        }

        private LevelTextChoice SelectedLevelTextChoice()
        {
            if (levelTextTargetBox == null) return null;
            return levelTextTargetBox.SelectedItem as LevelTextChoice;
        }

        private void UpdateLevelTextStatus()
        {
            if (levelTextStatusLabel == null || levelTextReplacementBox == null) return;
            LevelTextChoice target = SelectedLevelTextChoice();
            if (target == null)
            {
                levelTextStatusLabel.Text = "Choose a level text target.";
                if (createLevelTextPatchButton != null) createLevelTextPatchButton.Enabled = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(levelTextReplacementBox.Text))
                levelTextReplacementBox.Text = target.OriginalName;

            string replacement = levelTextReplacementBox.Text.Trim().ToUpperInvariant();
            int max = target.OriginalName.Length;
            bool ok = replacement.Length > 0 && replacement.Length <= max;
            if (createLevelTextPatchButton != null) createLevelTextPatchButton.Enabled = ok;
            levelTextStatusLabel.Text = ok
                ? "Writes " + target.DisplayName + " as '" + replacement + "' in the executable string table."
                : "Name is too long for this fixed slot. Max " + max.ToString() + " characters.";
        }

        private void RunLevelTextPatchExporter()
        {
            LevelTextChoice target = SelectedLevelTextChoice();
            if (target == null || levelTextReplacementBox == null || string.IsNullOrWhiteSpace(levelTextReplacementBox.Text))
            {
                MessageBox.Show(this, "Choose a target and enter a replacement name.", "Text patch choice missing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string replacement = levelTextReplacementBox.Text.Trim().ToUpperInvariant();
            if (replacement.Length > target.OriginalName.Length)
            {
                MessageBox.Show(this, "Replacement is too long for this fixed text slot. Use " + target.OriginalName.Length.ToString() + " characters or fewer.", "Text patch too long", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroLevelTextPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing level text exporter script: " + scriptPath, "Text exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string outputPrefix = Path.Combine(workspace, "Spyro the Dragon (USA)-text-" + target.Key + "-" + SafeFilePart(replacement));
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                args.Append(" -TargetLevelKey ").Append(QuoteArgument(target.ScriptKey));
                args.Append(" -ReplacementName ").Append(QuoteArgument(replacement));
                args.Append(" -OutputPrefix ").Append(QuoteArgument(outputPrefix));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                string cueName = Path.GetFileName(outputPrefix + ".cue");
                statusLabel.Text = "Started level text CUE export: " + target.DisplayName + " -> " + replacement + ".";
                if (levelTextStatusLabel != null)
                    levelTextStatusLabel.Text = "Writing " + cueName + ". Test fly-in first; portal labels may share this table.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start level text exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ExeStringCatalogPath()
        {
            return Path.Combine(Path.Combine(workspace, "_local"), Path.Combine("text", "spyro-exe-string-catalog.json"));
        }

        private void LoadExeStringChoices(bool showStatus)
        {
            if (exeStringBox == null) return;
            exeStringBox.Items.Clear();
            string catalogPath = ExeStringCatalogPath();
            if (!File.Exists(catalogPath))
            {
                if (exeStringStatusLabel != null)
                    exeStringStatusLabel.Text = "No word catalog yet. Click Build/Refresh Words.";
                if (createExeStringPatchButton != null)
                    createExeStringPatchButton.Enabled = false;
                return;
            }

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(catalogPath, Encoding.UTF8)) as Dictionary<string, object>;
                object[] strings = root != null && root.ContainsKey("strings") ? root["strings"] as object[] : null;
                if (strings != null)
                {
                    foreach (object item in strings)
                    {
                        Dictionary<string, object> row = item as Dictionary<string, object>;
                        if (row == null || !GetJsonBool(row, "safeFixedSlot", false)) continue;
                        string text = GetJsonString(row, "text", "");
                        string offset = GetJsonString(row, "offset", "");
                        string kind = GetJsonString(row, "kind", "unknown");
                        int length = GetJsonInt(row, "length", 0);
                        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(offset) || length <= 0) continue;
                        exeStringBox.Items.Add(new ExeStringChoice(text, offset, kind, length));
                    }
                }

                if (exeStringBox.Items.Count > 0)
                {
                    int preferred = 0;
                    for (int i = 0; i < exeStringBox.Items.Count; i++)
                    {
                        ExeStringChoice choice = exeStringBox.Items[i] as ExeStringChoice;
                        if (choice != null && string.Equals(choice.Text, "ENTERING %s...", StringComparison.OrdinalIgnoreCase))
                        {
                            preferred = i;
                            break;
                        }
                    }
                    exeStringBox.SelectedIndex = preferred;
                }

                if (showStatus && exeStringStatusLabel != null)
                    exeStringStatusLabel.Text = "Loaded " + exeStringBox.Items.Count.ToString() + " editable fixed-slot word(s).";
            }
            catch (Exception ex)
            {
                if (exeStringStatusLabel != null)
                    exeStringStatusLabel.Text = "Could not load word catalog: " + ex.Message;
            }
        }

        private ExeStringChoice SelectedExeStringChoice()
        {
            if (exeStringBox == null) return null;
            return exeStringBox.SelectedItem as ExeStringChoice;
        }

        private void UpdateExeStringStatus()
        {
            if (exeStringStatusLabel == null || exeStringReplacementBox == null) return;
            ExeStringChoice choice = SelectedExeStringChoice();
            if (choice == null)
            {
                exeStringStatusLabel.Text = "Build the word catalog to edit other fixed-length executable strings.";
                if (createExeStringPatchButton != null) createExeStringPatchButton.Enabled = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(exeStringReplacementBox.Text))
                exeStringReplacementBox.Text = choice.Text;

            string replacement = exeStringReplacementBox.Text.Trim();
            bool ok = replacement.Length > 0 && Encoding.ASCII.GetByteCount(replacement) <= choice.Length;
            if (createExeStringPatchButton != null) createExeStringPatchButton.Enabled = ok;
            exeStringStatusLabel.Text = ok
                ? choice.Kind + " @ " + choice.Offset + ", max " + choice.Length.ToString() + " bytes."
                : "Replacement is too long for this fixed slot. Max " + choice.Length.ToString() + " bytes.";
        }

        private void RunExeStringCatalogBuilder()
        {
            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroExeStringPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing executable text exporter script: " + scriptPath, "Text exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string catalogPath = ExeStringCatalogPath();
                string dir = Path.GetDirectoryName(catalogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                args.Append(" -CatalogOnly");
                args.Append(" -OutCatalogPath ").Append(QuoteArgument(catalogPath));

                string output = RunWorkspaceProcessAndCapture("powershell.exe", args.ToString());
                LoadExeStringChoices(true);
                statusLabel.Text = "Built executable word catalog.";
                if (exeStringStatusLabel != null && !string.IsNullOrWhiteSpace(output))
                    exeStringStatusLabel.Text = output.Replace("\r", " ").Replace("\n", " ").Trim();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not build word catalog", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunExeStringPatchExporter()
        {
            ExeStringChoice choice = SelectedExeStringChoice();
            if (choice == null || exeStringReplacementBox == null || string.IsNullOrWhiteSpace(exeStringReplacementBox.Text))
            {
                MessageBox.Show(this, "Choose a word and enter replacement text.", "Word patch choice missing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string replacement = exeStringReplacementBox.Text.Trim();
            if (Encoding.ASCII.GetByteCount(replacement) > choice.Length)
            {
                MessageBox.Show(this, "Replacement is too long for this fixed text slot. Use " + choice.Length.ToString() + " bytes or fewer.", "Word patch too long", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroExeStringPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing executable text exporter script: " + scriptPath, "Text exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string outputPrefix = Path.Combine(workspace, "Spyro the Dragon (USA)-text-" + SafeFilePart(choice.Text) + "-" + SafeFilePart(replacement));
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                args.Append(" -StringOffsetHex ").Append(QuoteArgument(choice.Offset));
                args.Append(" -OriginalText ").Append(QuoteArgument(choice.Text));
                args.Append(" -ReplacementText ").Append(QuoteArgument(replacement));
                args.Append(" -OutputPrefix ").Append(QuoteArgument(outputPrefix));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                string cueName = Path.GetFileName(outputPrefix + ".cue");
                statusLabel.Text = "Started word CUE export: " + choice.Text + " -> " + replacement + ".";
                if (exeStringStatusLabel != null)
                    exeStringStatusLabel.Text = "Writing " + cueName + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start word exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSkyboxPatchExporter()
        {
            SkyboxChoice target = SelectedSkyboxChoice(skyboxTargetBox);
            SkyboxChoice donor = SelectedSkyboxChoice(skyboxDonorBox);
            if (target == null || donor == null)
            {
                MessageBox.Show(this, "Choose both a target level and a donor skybox.", "Skybox choice missing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string blockReason;
            if (ShouldBlockSkyboxExportFromCatalog(target, donor, out blockReason))
            {
                MessageBox.Show(this, blockReason, "Skybox swap blocked by catalog", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (skyboxStatusLabel != null)
                    skyboxStatusLabel.Text = blockReason;
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroSkyboxPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing skybox exporter script: " + scriptPath, "Skybox exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string planDir = Path.Combine(workspace, "_skybox_probe");
                if (!Directory.Exists(planDir)) Directory.CreateDirectory(planDir);
                string planPath = Path.Combine(planDir, "whole-subfile-" + target.Key + "-from-" + donor.Key + ".patchplan.json");
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -TargetLevelKey ").Append(QuoteArgument(target.ScriptKey));
                args.Append(" -DonorLevelKey ").Append(QuoteArgument(donor.ScriptKey));
                args.Append(" -PlanOnly");
                args.Append(" -PlanPath ").Append(QuoteArgument(planPath));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started skybox whole-subfile plan: " + target.DisplayName + " <- " + donor.DisplayName + ".";
                if (skyboxStatusLabel != null)
                    skyboxStatusLabel.Text = "Plan-only output: " + Path.GetFileName(planPath) + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start skybox exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSkyColorPatchExporter()
        {
            SkyboxChoice target = SelectedSkyboxChoice(skyboxTargetBox);
            SkyColorPresetChoice preset = SelectedSkyColorPresetChoice();
            if (target == null || preset == null)
            {
                MessageBox.Show(this, "Choose Stone Hill and a sky color preset.", "Sky color choice missing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!string.Equals(SpyroLevelCatalog.NormalizeKey(target.Key), "stonehill", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Safe sky-color CUE export is currently wired for Stone Hill only. Other levels need their safe sky primitive records mapped first.", "Stone Hill only", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.Equals(preset.ScriptKey, "Custom", StringComparison.OrdinalIgnoreCase) &&
                (skyColorPaletteBox == null || string.IsNullOrWhiteSpace(skyColorPaletteBox.Text)))
            {
                MessageBox.Show(this, "Enter a custom palette like #081132 #314A8C #CDD5EA.", "Custom palette missing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Export-SpyroSkyPrimitiveColorPalette.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing sky color exporter script: " + scriptPath, "Sky color exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string outputPrefix = Path.Combine(workspace, "Spyro the Dragon (USA)-stonehill-skycolors-" + preset.FileSlug);
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -Preset ").Append(QuoteArgument(preset.ScriptKey));
                args.Append(" -OutputPrefix ").Append(QuoteArgument(outputPrefix));
                if (string.Equals(preset.ScriptKey, "Custom", StringComparison.OrdinalIgnoreCase))
                    args.Append(" -PaletteHex ").Append(QuoteArgument(skyColorPaletteBox.Text.Trim()));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                string cueName = Path.GetFileName(outputPrefix + ".cue");
                statusLabel.Text = "Started Stone Hill sky color CUE export: " + preset.DisplayName + ".";
                if (skyboxStatusLabel != null)
                    skyboxStatusLabel.Text = "Writing " + cueName + ". This preserves Stone Hill sky geometry and changes only safe color words.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start sky color exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ShouldBlockSkyboxExportFromCatalog(SkyboxChoice target, SkyboxChoice donor, out string message)
        {
            message = "";
            Dictionary<string, object> targetRow;
            Dictionary<string, object> donorRow;
            string catalogError;
            if (!TryReadSkyboxCatalogPair(target, donor, out targetRow, out donorRow, out catalogError))
                return false;

            string targetStatus = GetJsonString(targetRow, "status", "");
            string donorStatus = GetJsonString(donorRow, "status", "");
            if (!string.Equals(targetStatus, "cataloged", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(donorStatus, "cataloged", StringComparison.OrdinalIgnoreCase))
            {
                message = "Catalog needs a verified WAD probe for this pair before the editor will export it.";
                return true;
            }

            int targetSize = GetJsonInt(targetRow, "skySubfileSize", -1);
            int donorSize = GetJsonInt(donorRow, "skySubfileSize", -1);
            if (targetSize <= 0 || donorSize <= 0 || targetSize != donorSize)
            {
                message = "Catalog blocks this skybox swap because the sizes differ: target " + FormatHex(targetSize) + ", donor " + FormatHex(donorSize) + ".";
                return true;
            }
            return false;
        }

        private void RunSkyboxCatalogBuilder()
        {
            string scriptPath = Path.Combine(workspace, "tools", "New-SpyroSkyboxCatalog.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing skybox catalog script: " + scriptPath, "Skybox catalog missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string outPath = Path.Combine(workspace, "spyro-skybox-catalog.json");
                string markdownPath = Path.Combine(workspace, "spyro-skybox-catalog.md");
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -MarkdownPath ").Append(QuoteArgument(markdownPath));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started skybox catalog scan.";
                if (skyboxStatusLabel != null)
                    skyboxStatusLabel.Text = "Scanning skybox sizes and same-size donors.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start skybox catalog", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunCombinedPatchExporter()
        {
            if (!currentLevelSupportsSourcePatchers || !IsStoneHillLevel())
            {
                MessageBox.Show(this, currentLevelName + " combined BIN export is not wired yet. Use Save Edits while we decode the source tables.", "Exporter not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (mobys.Count == 0 || geometry == null || geometry.Polygons.Count == 0)
            {
                MessageBox.Show(this, "Load Stone Hill before creating a combined patch test BIN.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveEdits();
            int editedMobys = CountEditedMobys();
            int editedTerrain = CountEditedTerrainFaces();
            int customTextures = CountCustomTerrainTextureImports();
            if (editedMobys == 0 && editedTerrain == 0 && customTextures == 0)
            {
                MessageBox.Show(this, "Make and save at least one moby edit, terrain edit, or custom texture import before creating a combined BIN.", "No edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Create-StoneHillCombinedPatchTest.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing combined patch exporter script: " + scriptPath, "Combined exporter missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                if (!string.IsNullOrEmpty(customTerrainTexturesPath) && File.Exists(customTerrainTexturesPath))
                    args.Append(" -CustomTexturesPath ").Append(QuoteArgument(customTerrainTexturesPath));
                args.Append(" -OutPath ").Append(QuoteArgument(Path.Combine(workspace, "Spyro the Dragon (USA)-combinedpatchtest.bin")));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started combined moby + terrain BIN export" + (customTextures > 0 ? " with custom texture imports." : ".");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start combined patch exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunTeaserDemoExporter()
        {
            if (!HasLoadedLevel())
            {
                MessageBox.Show(this, "Load any mapped level first so the editor can save current edits before making the demo CUE.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Create-SpyroArtisansTeaserDemo.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing teaser demo builder: " + scriptPath, "Demo builder missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "Create a disposable Artisans teaser CUE that stacks saved mapped-level edits, one supported spring chest pair per Artisans/Stone Hill, Stone Hill terrain/custom texture imports when present, optional current level-text replacement, and the Stone Hill Night Keeper sky?\n\nThis does not overwrite your clean source image.",
                "Create teaser demo CUE",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SaveEdits();

                string demoDir = Path.Combine(Path.Combine(workspace, "_local"), "demo");
                Directory.CreateDirectory(demoDir);
                string outPath = Path.Combine(demoDir, "Spyro Artisans Teaser Demo.bin");

                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                AppendSourceImageArgument(args);
                args.Append(" -OutPath ").Append(QuoteArgument(outPath));
                args.Append(" -CuePath ").Append(QuoteArgument(Path.ChangeExtension(outPath, ".cue")));
                args.Append(" -PlanPath ").Append(QuoteArgument(outPath + ".demo-plan.json"));
                args.Append(" -SkyPreset StoneHillNightKeeper");

                LevelTextChoice textTarget = SelectedLevelTextChoice();
                if (textTarget != null && levelTextReplacementBox != null)
                {
                    string replacement = levelTextReplacementBox.Text.Trim().ToUpperInvariant();
                    if (replacement.Length > 0 && replacement.Length <= textTarget.OriginalName.Length && !string.Equals(replacement, textTarget.OriginalName, StringComparison.OrdinalIgnoreCase))
                    {
                        args.Append(" -LevelTextPatch ").Append(QuoteArgument(textTarget.ScriptKey + "=" + replacement));
                    }
                }

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started Artisans teaser demo CUE export. Output: " + Path.GetFileName(Path.ChangeExtension(outPath, ".cue")) + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start teaser demo exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSourceValidation()
        {
            if (!currentLevelSupportsSourcePatchers)
            {
                MessageBox.Show(this, currentLevelName + " source validation is not wired yet.", "Validation not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string scriptPath = Path.Combine(workspace, "tools", "Validate-StoneHillSourcePatch.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing source validation script: " + scriptPath, "Source validation missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string planPath = Path.Combine(workspace, "Spyro the Dragon (USA)-loaderpatchtest.bin.patchplan.json");
            if (!File.Exists(planPath))
                planPath = Path.Combine(workspace, "Spyro the Dragon (USA)-nativepatchtest-topranked.bin.patchplan.json");
            if (!File.Exists(planPath))
                planPath = Path.Combine(workspace, "stonehill-native-source-bin-smoke.planonly.json");

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -CaptureLive");
                args.Append(" -PatchPlanPath ").Append(QuoteArgument(planPath));
                args.Append(" -OutPath ").Append(QuoteArgument(Path.Combine(workspace, "stonehill-source-patch-live-validation.json")));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started source BIN live validation against DuckStation RAM.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start source validation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunBehaviorDiff()
        {
            if (!currentLevelSupportsSourcePatchers)
            {
                MessageBox.Show(this, currentLevelName + " behavior diff is not wired yet.", "Behavior diff not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string scriptPath = Path.Combine(workspace, "tools", "Compare-StoneHillMobyBehavior.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing behavior diff script: " + scriptPath, "Behavior diff missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -CaptureLive");
                args.Append(" -OutJsonPath ").Append(QuoteArgument(Path.Combine(workspace, "stonehill-moby-behavior-diff-current.json")));
                args.Append(" -OutMarkdownPath ").Append(QuoteArgument(Path.Combine(workspace, "stonehill-moby-behavior-diff-current.md")));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started live behavior diff for chest, gem, and fodder proof.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start behavior diff", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSpringChestRuntimeHelper()
        {
            if (!HasLoadedLevel())
            {
                MessageBox.Show(this, "Load Artisans before running the spring chest helper.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!string.Equals(SpyroLevelCatalog.NormalizeKey(currentLevelKey), "artisans", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "The spring chest helper is currently mapped for the Artisans T32/T38 import only. Other levels need their controller and shell row pair mapped before this helper can run safely there.", "Spring helper scope", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptPath = Path.Combine(workspace, "tools", "Run-SpyroSpringChestRuntimeHelper.ps1");
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, "Missing spring chest helper script: " + scriptPath, "Spring helper missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                int controllerTrueIndex = 32;
                int shellTrueIndex = 38;
                int rewardGemIdByte = 0x55;
                if (selectedMobyIndex >= 0 && selectedMobyIndex < mobys.Count)
                {
                    int controllerIndex;
                    int shellIndex;
                    if (TryResolveSpringChestRuntimePair(mobys[selectedMobyIndex], out controllerIndex, out shellIndex))
                    {
                        controllerTrueIndex = mobys[controllerIndex].TrueIndex;
                        shellTrueIndex = mobys[shellIndex].TrueIndex;
                        rewardGemIdByte = RewardGemIdByteForSpringShell(mobys[shellIndex]);
                    }
                }

                StringBuilder args = new StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
                args.Append(QuoteArgument(scriptPath));
                args.Append(" -OutPrefix ").Append(QuoteArgument(@".\_local\experiments\editor-spring-runtime-helper"));
                args.Append(" -ControllerIndex ").Append(controllerTrueIndex.ToString());
                args.Append(" -ShellIndex ").Append(shellTrueIndex.ToString());
                args.Append(" -RewardGemIdByte 0x").Append(rewardGemIdByte.ToString("X2"));
                args.Append(" -RepeatSeconds 90 -PostTriggerWatchSeconds 45");

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started Artisans spring chest helper for T" + controllerTrueIndex.ToString() + "/T" + shellTrueIndex.ToString() + " with reward 0x" + rewardGemIdByte.ToString("X2") + ". Flame or charge the chest, then collect the gem.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start spring chest helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static int RewardGemIdByteForSpringShell(Moby shell)
        {
            if (shell == null) return 0x55;
            int value = shell.HasRewardColorEdit ? shell.RewardByte53Override : shell.Flag4B;
            if (value < 0x53 || value > 0x57)
                value = 0x55;
            return value;
        }

        private bool TryResolveSpringChestRuntimePair(Moby selected, out int controllerIndex, out int shellIndex)
        {
            controllerIndex = -1;
            shellIndex = -1;
            if (selected == null) return false;

            int selectedIndex = mobys.IndexOf(selected);
            if (selectedIndex < 0) return false;

            int partnerIndex = -1;
            if (selected.SpringChestPartnerTrueIndex >= 0)
            {
                for (int i = 0; i < mobys.Count; i++)
                {
                    if (mobys[i] != null && mobys[i].TrueIndex == selected.SpringChestPartnerTrueIndex)
                    {
                        partnerIndex = i;
                        break;
                    }
                }
            }

            if (partnerIndex < 0)
            {
                if (selected.TrueIndex == 38 && selectedIndex >= 0)
                    partnerIndex = IndexOfTrueIndex(32);
                else if (selected.TrueIndex == 32 && selectedIndex >= 0)
                    partnerIndex = IndexOfTrueIndex(38);
            }

            if (partnerIndex < 0) return false;
            Moby partner = mobys[partnerIndex];
            if (partner == null) return false;

            bool selectedController = IsSpringChestControllerRole(selected);
            bool partnerController = IsSpringChestControllerRole(partner);
            if (selectedController && !partnerController)
            {
                controllerIndex = selectedIndex;
                shellIndex = partnerIndex;
                return true;
            }
            if (!selectedController && partnerController)
            {
                controllerIndex = partnerIndex;
                shellIndex = selectedIndex;
                return true;
            }

            if (selected.TrueIndex == 32 && partner.TrueIndex == 38)
            {
                controllerIndex = selectedIndex;
                shellIndex = partnerIndex;
                return true;
            }
            if (selected.TrueIndex == 38 && partner.TrueIndex == 32)
            {
                controllerIndex = partnerIndex;
                shellIndex = selectedIndex;
                return true;
            }

            controllerIndex = Math.Min(selectedIndex, partnerIndex);
            shellIndex = Math.Max(selectedIndex, partnerIndex);
            return true;
        }

        private int IndexOfTrueIndex(int trueIndex)
        {
            for (int i = 0; i < mobys.Count; i++)
            {
                if (mobys[i] != null && mobys[i].TrueIndex == trueIndex)
                    return i;
            }
            return -1;
        }

        private static bool IsSpringChestControllerRole(Moby moby)
        {
            if (moby == null) return false;
            if (string.Equals(moby.SpringChestPairRole, "controller", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(moby.SpringChestPairRole, "shell", StringComparison.OrdinalIgnoreCase)) return false;
            if (moby.AppendSourceTrueIndex == 30) return true;
            if (moby.SourceByte36 == 0xFE && moby.SourceByte37 == 0x01) return true;
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.AppendSourceLabel ?? "")).ToLowerInvariant();
            return text.IndexOf("controller", StringComparison.Ordinal) >= 0;
        }

        private void StartWorkspaceProcess(string fileName, string arguments)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(fileName, arguments);
            info.WorkingDirectory = workspace;
            info.UseShellExecute = true;
            System.Diagnostics.Process.Start(info);
        }

        private string RunWorkspaceProcessAndCapture(string fileName, string arguments)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(fileName, arguments);
            info.WorkingDirectory = workspace;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
                return string.IsNullOrWhiteSpace(output) ? error : output;
            }
        }

        private static string QuoteArgument(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private int LoadSavedEdits(bool showMessage)
        {
            if (!HasLoadedLevel())
            {
                if (showMessage)
                {
                    MessageBox.Show(this, "Choose a level from the dropdown before loading edits.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = "Choose a level before loading edits.";
                }
                return 0;
            }

            try
            {
                int count = MobyEditStore.Load(editPath, mobys, currentLevelKey);
                NormalizeAppendedTrueIndexes();
                savedTerrainEditCount = TerrainEditStore.Load(terrainEditPath, geometry == null ? null : geometry.Polygons);
                LoadPlayerColorOptions(false);
                savedEditCount = count;
                hasUnsavedEdits = false;
                BuildSelectionGroups();
                RefreshMobyList();
                UpdateInspector();
                canvas.Invalidate();
                if (showMessage)
                    statusLabel.Text = "Loaded " + count.ToString() + " saved moby edit(s), " + savedTerrainEditCount.ToString() + " terrain edit(s), and " + CountActivePlayerColorOptions().ToString() + " color option(s). " + TreasureSummaryText(CalculateTreasureSummary()) + ".";
                return count;
            }
            catch (Exception ex)
            {
                if (showMessage)
                    MessageBox.Show(this, ex.Message, "Load edits failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
        }

        private void ClearEdits()
        {
            ResetAllEdits();
        }

        private void ResetAllEdits()
        {
            if (mobys.Count == 0 && (geometry == null || geometry.Polygons.Count == 0)) return;
            int edited = CountEditedMobys();
            int terrainEdited = CountEditedTerrainFaces();
            if (edited == 0 && terrainEdited == 0)
            {
                statusLabel.Text = "No active edits to reset.";
                return;
            }
            DialogResult result = MessageBox.Show(
                this,
                "Reset all " + edited.ToString() + " moby edit(s) and " + terrainEdited.ToString() + " terrain edit(s) back to the original loaded " + currentLevelName + " state?",
                "Reset all " + currentLevelName + " edits",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
            for (int i = mobys.Count - 1; i >= 0; i--)
            {
                if (mobys[i].IsAppendedRecord)
                    mobys.RemoveAt(i);
                else
                    mobys[i].ResetToOriginal();
            }
            if (geometry != null)
            {
                foreach (TerrainPolygon polygon in geometry.Polygons)
                    polygon.ResetTerrainEdit();
            }
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Reset all active edits. Save Edits to write the reset state.";
        }

        private int CountEditedMobys()
        {
            int count = 0;
            foreach (Moby moby in mobys)
            {
                if (moby.IsEdited) count++;
            }
            return count;
        }

        private bool HasAnySavedMobyEdits()
        {
            foreach (LevelDefinition level in levelDefinitions)
            {
                if (level == null || string.IsNullOrEmpty(level.Key)) continue;
                string path = Path.Combine(workspace, level.Key + "-native-edits.json");
                if (!File.Exists(path)) continue;
                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                    if (root != null && root.ContainsKey("editCount") && Convert.ToInt32(root["editCount"]) > 0)
                        return true;
                }
                catch
                {
                }
            }
            return false;
        }

        private string ResolveSourceImagePath()
        {
            string rootImage = Path.Combine(workspace, "Spyro the Dragon (USA).bin");
            if (File.Exists(rootImage)) return rootImage;

            string localRoot = Path.Combine(workspace, "_local");
            if (Directory.Exists(localRoot))
            {
                try
                {
                    string[] matches = Directory.GetFiles(localRoot, "Spyro the Dragon (USA).bin", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                    {
                        Array.Sort(matches, delegate(string a, string b)
                        {
                            return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
                        });
                        return matches[0];
                    }
                }
                catch
                {
                }
            }

            return rootImage;
        }

        private void AppendSourceImageArgument(StringBuilder args)
        {
            string sourceImage = ResolveSourceImagePath();
            if (!string.IsNullOrEmpty(sourceImage) && File.Exists(sourceImage))
                args.Append(" -ImagePath ").Append(QuoteArgument(sourceImage));
        }

        private int CountEditedTerrainFaces()
        {
            if (geometry == null) return 0;
            int count = 0;
            foreach (TerrainPolygon polygon in geometry.Polygons)
            {
                if (polygon.IsTerrainEdited) count++;
            }
            return count;
        }

        private void FitGeometry()
        {
            if (geometry == null || canvas.ClientSize.Width < 10 || canvas.ClientSize.Height < 10)
                return;

            RectangleF bounds = geometry.Bounds;
            const float pad = 320f;
            RectangleF viewBounds = view3D ? Get3DViewBounds() : GetViewBounds(bounds);
            float width = Math.Max(100f, viewBounds.Width + pad * 2f);
            float height = Math.Max(100f, viewBounds.Height + pad * 2f);
            float zx = (canvas.ClientSize.Width - 80f) / width;
            float zy = (canvas.ClientSize.Height - 80f) / height;
            zoom = Clamp(Math.Min(zx, zy), 0.01f, 2.5f);
            pan = new PointF(40f - ((viewBounds.Left - pad) * zoom), 40f - ((viewBounds.Top - pad) * zoom));
            canvas.Invalidate();
        }

        private void ResetViewOrientation()
        {
            if (view3D)
                Reset3DOrientation();
        }

        private void Reset3DOrientation()
        {
            view3DYaw = Default3DYaw;
            view3DPitch = Default3DPitch;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private PointF WorldToScreen(float x, float y)
        {
            return WorldToScreen(x, y, 0f);
        }

        private PointF WorldToScreen(float x, float y, float z)
        {
            PointF view = WorldToView(x, y, z);
            return new PointF((view.X * zoom) + pan.X, (view.Y * zoom) + pan.Y);
        }

        private PointF ScreenToWorld(float x, float y)
        {
            float safeZoom = Math.Max(0.0001f, zoom);
            return ViewToWorld((x - pan.X) / safeZoom, (y - pan.Y) / safeZoom);
        }

        private PointF ScreenToWorldAtZ(float x, float y, float z)
        {
            float safeZoom = Math.Max(0.0001f, zoom);
            float viewX = (x - pan.X) / safeZoom;
            float viewY = (y - pan.Y) / safeZoom;
            if (!view3D)
                return ViewToWorld(viewX, viewY);

            float rx = viewX;
            float ry = (viewY + (z * View3DHeightScale)) / Math.Max(0.001f, view3DPitch);
            float cos = (float)Math.Cos(view3DYaw);
            float sin = (float)Math.Sin(view3DYaw);
            float worldViewX = (rx * cos) + (ry * sin);
            float worldViewY = (-rx * sin) + (ry * cos);
            return new PointF(mirrorViewX ? -worldViewX : worldViewX, mirrorViewY ? -worldViewY : worldViewY);
        }

        private PointF WorldToView(float x, float y)
        {
            return WorldToView(x, y, 0f);
        }

        private PointF WorldToView(float x, float y, float z)
        {
            if (view3D)
                return WorldToView3D(x, y, z);
            return new PointF(mirrorViewX ? -x : x, mirrorViewY ? -y : y);
        }

        private PointF WorldToView3D(float x, float y, float z)
        {
            float vx = mirrorViewX ? -x : x;
            float vy = mirrorViewY ? -y : y;
            float cos = (float)Math.Cos(view3DYaw);
            float sin = (float)Math.Sin(view3DYaw);
            float rx = (vx * cos) - (vy * sin);
            float ry = (vx * sin) + (vy * cos);
            return new PointF(rx, (ry * view3DPitch) - (z * View3DHeightScale));
        }

        private PointF ViewToWorld(float x, float y)
        {
            return new PointF(mirrorViewX ? -x : x, mirrorViewY ? -y : y);
        }

        private RectangleF GetViewBounds(RectangleF worldBounds)
        {
            PointF a = WorldToView(worldBounds.Left, worldBounds.Top);
            PointF b = WorldToView(worldBounds.Right, worldBounds.Top);
            PointF c = WorldToView(worldBounds.Left, worldBounds.Bottom);
            PointF d = WorldToView(worldBounds.Right, worldBounds.Bottom);
            float left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            float right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            float top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            float bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        private RectangleF GetWorldViewBounds(Size clientSize)
        {
            if (view3D)
                return geometry == null ? RectangleF.Empty : geometry.Bounds;
            PointF a = ScreenToWorld(0, 0);
            PointF b = ScreenToWorld(clientSize.Width, 0);
            PointF c = ScreenToWorld(0, clientSize.Height);
            PointF d = ScreenToWorld(clientSize.Width, clientSize.Height);
            float left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            float right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            float top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            float bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        private RectangleF Get3DViewBounds()
        {
            if (geometry == null) return RectangleF.Empty;
            bool any = false;
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            Action<PointF> include = delegate(PointF p)
            {
                any = true;
                left = Math.Min(left, p.X);
                top = Math.Min(top, p.Y);
                right = Math.Max(right, p.X);
                bottom = Math.Max(bottom, p.Y);
            };

            foreach (TerrainPolygon polygon in geometry.Polygons)
            {
                for (int i = 0; i < polygon.Points.Length; i++)
                    include(WorldToView3D(polygon.Points[i].X, polygon.Points[i].Y, polygon.ZValues[i]));
            }
            foreach (Moby moby in mobys)
                include(WorldToView3D(moby.X, moby.Y, moby.Z));

            return any ? RectangleF.FromLTRB(left, top, right, bottom) : GetViewBounds(geometry.Bounds);
        }

        internal void Draw(Graphics g, Size clientSize)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(view3D
                ? Color.FromArgb(56, 73, 63)
                : (useGameTerrainStyle ? Color.FromArgb(42, 63, 54) : Color.FromArgb(24, 26, 30)));

            if (geometry == null)
            {
                using (Brush b = new SolidBrush(Color.White))
                    g.DrawString("Choose a level from the dropdown to load the native renderer.", Font, b, 20, 20);
                return;
            }

            RectangleF worldView = GetWorldViewBounds(clientSize);

            bool fast = FastMode;
            if (showFaces && (!fast || editorMode == EditorMode.Terrain))
                DrawPolygons(g, worldView);
            if (showLines && !view3D)
                DrawEdges(g, worldView, fast);
            if (showContours && !fast)
                DrawHeightContours(g, worldView);
            if (showTextureDebugLabels && !fast)
                DrawTextureDebugLabels(g, worldView);
            if (showTextureFamilyHighlight && !fast)
                DrawTextureFamilyHighlight(g, worldView);
            if (editorMode == EditorMode.Terrain)
                DrawSelectedTerrain(g);
            DrawMobys(g, worldView);
            if (editorMode == EditorMode.Terrain || ClickPlaceEnabled)
                DrawTerrainHover(g);
            DrawOverlayText(g, fast);
            if (view3D)
                Draw3DCompass(g, clientSize);
            if ((showHeightTint || showContours) && !fast)
                DrawHeightLegend(g, clientSize);
        }

        private void DrawPolygons(Graphics g, RectangleF worldView)
        {
            int maxDraw = completeTerrainDraw ? geometry.Polygons.Count : (zoom < 0.035f ? 900 : (zoom < 0.08f ? 1400 : 2200));
            int step = Math.Max(1, (int)Math.Ceiling(geometry.Polygons.Count / (double)maxDraw));
            using (Pen outline = new Pen(Color.FromArgb(32, 0, 0, 0), 1f))
            {
                List<TerrainDrawItem> drawItems = new List<TerrainDrawItem>();
                for (int i = 0; i < geometry.Polygons.Count; i += step)
                {
                    TerrainPolygon polygon = geometry.Polygons[i];
                    if (!polygon.Bounds.IntersectsWith(worldView)) continue;
                    drawItems.Add(new TerrainDrawItem(polygon, TerrainDrawDepth(polygon)));
                }
                drawItems.Sort(delegate(TerrainDrawItem a, TerrainDrawItem b) { return a.Depth.CompareTo(b.Depth); });

                for (int i = 0; i < drawItems.Count; i++)
                {
                    TerrainPolygon polygon = drawItems[i].Polygon;
                    PointF[] pts = new PointF[polygon.Points.Length];
                    for (int p = 0; p < polygon.Points.Length; p++)
                        pts[p] = WorldToScreen(polygon.Points[p].X, polygon.Points[p].Y, polygon.ZValues[p]);
                    if (!TryDrawSourceTexturePolygon(g, polygon, pts))
                    {
                        using (Brush brush = new SolidBrush(view3D ? TerrainFillColor3D(polygon) : TerrainFillColor(polygon)))
                            g.FillPolygon(brush, pts);
                    }
                    if ((!view3D && zoom > 0.11f) || (view3D && zoom > 0.18f))
                        g.DrawPolygon(outline, pts);
                    if (polygon.IsTerrainEdited)
                    {
                        using (Pen editedPen = new Pen(Color.FromArgb(245, 255, 206, 72), 2f))
                            g.DrawPolygon(editedPen, pts);
                    }
                }
            }
        }

        private float TerrainDrawDepth(TerrainPolygon polygon)
        {
            if (view3D)
                return WorldToView3D(polygon.CenterX, polygon.CenterY, polygon.AvgZ).Y;

            return (TerrainSurfacePriority(polygon) * 100000f) + polygon.AvgZ;
        }

        private int TerrainSurfacePriority(TerrainPolygon polygon)
        {
            if (IsWaterSurface(polygon)) return 0;
            if (IsSandSurface(polygon)) return 1;
            if (IsDirtSurface(polygon)) return 2;
            if (IsGrassSurface(polygon)) return 3;
            if (IsStoneSurface(polygon)) return 4;
            if (IsCliffSurface(polygon)) return 5;
            return 3;
        }

        private bool TryDrawSourceTexturePolygon(Graphics g, TerrainPolygon polygon, PointF[] screenPoints)
        {
            if (!showSourceTextures || polygon == null || !polygon.HasTextureId)
                return false;

            CustomTerrainTexture customTexture;
            bool useCustomTexture = TryGetCustomTerrainTexture(polygon.TextureId, out customTexture);
            if (!useCustomTexture && terrainTextureAtlas == null && terrainTextureLumaAtlas == null)
                return false;

            Rectangle sourceRect = useCustomTexture
                ? CustomTerrainTextureSourceRect(customTexture, polygon)
                : TerrainTextureSourceRect(polygon);
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
                return false;

            PointF[] destPointsF = TextureDestinationPoints(polygon, screenPoints);
            if (destPointsF == null || destPointsF.Length != 3)
                return false;

            RectangleF dest = BoundsOfScreenPoints(destPointsF);
            if (dest.Width < 1f || dest.Height < 1f)
                return false;

            GraphicsState state = g.Save();
            try
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(screenPoints);
                    g.SetClip(path, CombineMode.Replace);
                    using (Brush baseFill = new SolidBrush(view3D ? TerrainFillColor3D(polygon) : TerrainFillColor(polygon)))
                        g.FillPolygon(baseFill, screenPoints);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;

                    Point[] destPoints = new Point[]
                    {
                        Point.Round(destPointsF[0]),
                        Point.Round(destPointsF[1]),
                        Point.Round(destPointsF[2])
                    };
                    Image sourceImage = useCustomTexture
                        ? customTexture.PreviewImage
                        : (useRawSourceTexture ? terrainTextureAtlas : (terrainTextureLumaAtlas ?? terrainTextureAtlas));
                    if (useRawSourceTexture || useCustomTexture)
                    {
                        g.DrawImage(sourceImage, destPoints, sourceRect, GraphicsUnit.Pixel);
                    }
                    else
                    {
                        using (ImageAttributes attrs = CreateSourceTextureAttributes(polygon))
                        {
                            g.DrawImage(sourceImage, destPoints, sourceRect, GraphicsUnit.Pixel, attrs);
                        }
                    }

                    Color tint = SourceTextureTint(polygon);
                    if (tint.A > 0)
                    {
                        using (Brush overlay = new SolidBrush(tint))
                            g.FillPolygon(overlay, screenPoints);
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
            return true;
        }

        private PointF[] TextureDestinationPoints(TerrainPolygon polygon, PointF[] screenPoints)
        {
            if (useTextureCornerMapping && polygon.HasTextureCorners)
            {
                return new PointF[]
                {
                    WorldToScreen(polygon.TextureTopLeft.X, polygon.TextureTopLeft.Y, polygon.TextureTopLeft.Z),
                    WorldToScreen(polygon.TextureTopRight.X, polygon.TextureTopRight.Y, polygon.TextureTopRight.Z),
                    WorldToScreen(polygon.TextureBottomLeft.X, polygon.TextureBottomLeft.Y, polygon.TextureBottomLeft.Z)
                };
            }

            if (screenPoints == null || screenPoints.Length < 3)
                return null;
            if (screenPoints.Length >= 4)
                return new PointF[] { screenPoints[3], screenPoints[2], screenPoints[0] };
            return new PointF[] { screenPoints[2], screenPoints[1], screenPoints[0] };
        }

        private ImageAttributes CreateSourceTextureAttributes(TerrainPolygon polygon)
        {
            Color light = SourceTextureLightColor(polygon);
            float r = SourceTextureChannelScale(light.R);
            float g = SourceTextureChannelScale(light.G);
            float b = SourceTextureChannelScale(light.B);
            ColorMatrix matrix = new ColorMatrix(new float[][]
            {
                new float[] { r, 0f, 0f, 0f, 0f },
                new float[] { 0f, g, 0f, 0f, 0f },
                new float[] { 0f, 0f, b, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0.96f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 1f }
            });
            ImageAttributes attrs = new ImageAttributes();
            attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            return attrs;
        }

        private static float SourceTextureChannelScale(int channel)
        {
            return Math.Max(0.16f, Math.Min(1.7f, channel / 168f));
        }

        private Color SourceTextureLightColor(TerrainPolygon polygon)
        {
            Color surface;
            if (TryStoneHillSurfaceColor(polygon, out surface))
                return surface;
            if (polygon != null && polygon.HasFaceColor)
                return polygon.FaceColor;
            if (showMaterialIds && polygon != null && polygon.HasTextureId)
                return polygon.MaterialFill;
            if (useGameTerrainStyle && polygon != null)
                return polygon.GameFill;
            return polygon != null ? polygon.HeightFill : Color.FromArgb(128, 150, 120);
        }

        private Rectangle TerrainTextureSourceRect(int textureId)
        {
            if (terrainTextureAtlas == null || textureId < 0)
                return Rectangle.Empty;

            int x = (textureId % TerrainTextureAtlasColumns) * terrainTextureTileSize;
            int y = (textureId / TerrainTextureAtlasColumns) * terrainTextureTileSize;
            if (x < 0 || y < 0 || x + terrainTextureTileSize > terrainTextureAtlas.Width || y + terrainTextureTileSize > terrainTextureAtlas.Height)
                return Rectangle.Empty;
            return new Rectangle(x, y, terrainTextureTileSize, terrainTextureTileSize);
        }

        private Rectangle TerrainTextureSourceRect(TerrainPolygon polygon)
        {
            if (polygon == null || !polygon.HasTextureId)
                return Rectangle.Empty;

            Rectangle whole = TerrainTextureSourceRect(polygon.TextureId);
            if (whole.IsEmpty || sourceTextureWindowMode == SourceTextureWindowMode.WholeTile)
                return whole;

            int cellSize = terrainTextureTileSize >= 128 ? 32 : Math.Max(16, terrainTextureTileSize / 2);
            int columns = Math.Max(1, terrainTextureTileSize / Math.Max(1, cellSize));
            int cellCount = Math.Max(1, columns * columns);
            int index = TextureWindowIndex(polygon);
            if (index < 0)
                return whole;

            index %= cellCount;
            if (index < 0) index += cellCount;
            return new Rectangle(
                whole.X + ((index % columns) * cellSize),
                whole.Y + ((index / columns) * cellSize),
                cellSize,
                cellSize);
        }

        private Rectangle CustomTerrainTextureSourceRect(CustomTerrainTexture texture, TerrainPolygon polygon)
        {
            if (texture == null || texture.PreviewImage == null)
                return Rectangle.Empty;

            Rectangle whole = new Rectangle(0, 0, texture.PreviewImage.Width, texture.PreviewImage.Height);
            if (sourceTextureWindowMode == SourceTextureWindowMode.WholeTile || polygon == null)
                return whole;

            int cellSize = texture.PreviewImage.Width >= 128 ? 32 : Math.Max(16, texture.PreviewImage.Width / 2);
            int columns = Math.Max(1, texture.PreviewImage.Width / Math.Max(1, cellSize));
            int rows = Math.Max(1, texture.PreviewImage.Height / Math.Max(1, cellSize));
            int cellCount = Math.Max(1, columns * rows);
            int index = TextureWindowIndex(polygon);
            if (index < 0)
                return whole;

            index %= cellCount;
            if (index < 0) index += cellCount;
            int x = (index % columns) * cellSize;
            int y = (index / columns) * cellSize;
            if (x >= texture.PreviewImage.Width || y >= texture.PreviewImage.Height)
                return whole;
            return new Rectangle(
                x,
                y,
                Math.Min(cellSize, texture.PreviewImage.Width - x),
                Math.Min(cellSize, texture.PreviewImage.Height - y));
        }

        private bool HasSourceTextureTile(int textureId)
        {
            CustomTerrainTexture custom;
            return TryGetCustomTerrainTexture(textureId, out custom) || !TerrainTextureSourceRect(textureId).IsEmpty;
        }

        private int TextureWindowIndex(TerrainPolygon polygon)
        {
            if (polygon == null)
                return -1;

            int word;
            switch (sourceTextureWindowMode)
            {
                case SourceTextureWindowMode.FaceDepthLow4:
                    return polygon.FaceDepth >= 0 ? (polygon.FaceDepth & 0x0F) : -1;
                case SourceTextureWindowMode.Word3High4:
                    return TryParsePackedWord(polygon.Word3, out word) ? ((word >> 7) & 0x0F) : -1;
                case SourceTextureWindowMode.Word4Mid4:
                    return TryParsePackedWord(polygon.Word4, out word) ? ((word >> 8) & 0x0F) : -1;
                case SourceTextureWindowMode.Word4High4:
                    return TryParsePackedWord(polygon.Word4, out word) ? ((word >> 12) & 0x0F) : -1;
                default:
                    return -1;
            }
        }

        private static bool TryParsePackedWord(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text))
                return false;
            string trimmed = text.Trim();
            try
            {
                if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    value = Convert.ToInt32(trimmed.Substring(2), 16);
                else
                    value = Convert.ToInt32(trimmed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Color SourceTextureTint(TerrainPolygon polygon)
        {
            if (useRawSourceTexture)
                return Color.FromArgb(0, 0, 0, 0);

            Color baseTint;
            int alpha;
            if (TryStoneHillSurfaceColor(polygon, out baseTint))
            {
                alpha = IsWaterSurface(polygon) ? (showHeightTint ? 72 : 54) : (showHeightTint ? 58 : 42);
            }
            else if (polygon.HasFaceColor)
            {
                baseTint = polygon.FaceColor;
                alpha = showHeightTint ? 40 : 22;
            }
            else if (showMaterialIds && polygon.HasTextureId)
            {
                baseTint = polygon.MaterialFill;
                alpha = showHeightTint ? 74 : 48;
            }
            else if (useGameTerrainStyle)
            {
                baseTint = polygon.GameFill;
                alpha = showHeightTint ? 104 : 82;
            }
            else
            {
                baseTint = polygon.HeightFill;
                alpha = showHeightTint ? 88 : 42;
            }
            return Color.FromArgb(alpha, baseTint.R, baseTint.G, baseTint.B);
        }

        private static RectangleF BoundsOfScreenPoints(PointF[] points)
        {
            if (points == null || points.Length == 0)
                return RectangleF.Empty;
            float minX = points[0].X, minY = points[0].Y, maxX = points[0].X, maxY = points[0].Y;
            for (int i = 1; i < points.Length; i++)
            {
                minX = Math.Min(minX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxX = Math.Max(maxX, points[i].X);
                maxY = Math.Max(maxY, points[i].Y);
            }
            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        private Color TerrainFillColor3D(TerrainPolygon polygon)
        {
            Color baseColor = TerrainFillColor(polygon);
            Color softened = BlendColor(baseColor, Color.FromArgb(baseColor.A, 105, 148, 108), 0.28f);
            return Color.FromArgb(232, softened.R, softened.G, softened.B);
        }

        private void DrawEdges(Graphics g, RectangleF worldView, bool fast)
        {
            int maxDraw = completeTerrainDraw && !fast ? geometry.Edges.Count : (fast ? 750 : (zoom < 0.035f ? 1100 : (zoom < 0.08f ? 1700 : 2600)));
            int step = Math.Max(1, (int)Math.Ceiling(geometry.Edges.Count / (double)maxDraw));
            Color lineColor = useGameTerrainStyle
                ? Color.FromArgb(editorMode == EditorMode.Terrain ? 190 : 95, 40, 56, 45)
                : Color.FromArgb(fast ? 165 : 215, 28, 220, 255);
            using (Pen pen = new Pen(lineColor, fast ? 1f : 1.25f))
            {
                for (int i = 0; i < geometry.Edges.Count; i += step)
                {
                    TerrainEdge edge = geometry.Edges[i];
                    if (!edge.Bounds.IntersectsWith(worldView)) continue;
                    PointF a = WorldToScreen(edge.X1, edge.Y1);
                    PointF b = WorldToScreen(edge.X2, edge.Y2);
                    g.DrawLine(pen, a, b);
                }
            }
        }

        private Color TerrainFillColor(TerrainPolygon polygon)
        {
            if (showMaterialIds && polygon.HasTextureId)
            {
                if (!showHeightTint)
                    return polygon.MaterialFill;
                return BlendColor(polygon.MaterialFill, Color.FromArgb(210, polygon.HeightFill.R, polygon.HeightFill.G, polygon.HeightFill.B), 0.24f);
            }
            if (!useGameTerrainStyle)
                return polygon.HeightFill;
            Color surface;
            if (TryStoneHillSurfaceColor(polygon, out surface))
            {
                Color fill = Color.FromArgb(polygon.GameFill.A, surface.R, surface.G, surface.B);
                if (!showHeightTint)
                    return fill;
                return BlendColor(fill, Color.FromArgb(210, polygon.HeightFill.R, polygon.HeightFill.G, polygon.HeightFill.B), IsWaterSurface(polygon) ? 0.05f : 0.08f);
            }
            if (!showHeightTint)
                return polygon.GameFill;
            return BlendColor(polygon.GameFill, Color.FromArgb(210, polygon.HeightFill.R, polygon.HeightFill.G, polygon.HeightFill.B), 0.34f);
        }

        private bool TryStoneHillSurfaceColor(TerrainPolygon polygon, out Color color)
        {
            color = Color.Empty;
            if (!useSurfaceColorAssist || polygon == null)
                return false;

            Color light = polygon.HasFaceColor ? polygon.FaceColor : polygon.GameFill;
            Color lightRgb = Color.FromArgb(255, light.R, light.G, light.B);
            string manual = TerrainMaterialOverrideFor(polygon);

            if (!string.IsNullOrEmpty(manual) && manual != "unknown")
            {
                color = SurfaceAssistColor(manual, lightRgb);
                return true;
            }
            if (manual == "unknown")
                return false;

            if (IsWaterSurface(polygon))
            {
                color = SurfaceAssistColor("water", lightRgb);
                return true;
            }
            if (IsCliffSurface(polygon))
            {
                color = SurfaceAssistColor("cliff", lightRgb);
                return true;
            }
            if (IsStoneSurface(polygon))
            {
                color = SurfaceAssistColor("stone", lightRgb);
                return true;
            }
            if (IsSandSurface(polygon))
            {
                color = SurfaceAssistColor("sand", lightRgb);
                return true;
            }
            if (IsDirtSurface(polygon))
            {
                color = SurfaceAssistColor("dirt", lightRgb);
                return true;
            }
            if (IsGrassSurface(polygon))
            {
                color = SurfaceAssistColor("grass", lightRgb);
                return true;
            }

            return false;
        }

        private static Color SurfaceAssistColor(string surface, Color lightRgb)
        {
            if (surface == "water")
                return BlendColor(Color.FromArgb(255, 34, 132, 194), lightRgb, 0.10f);
            if (surface == "stone")
                return BlendColor(Color.FromArgb(255, 132, 140, 138), lightRgb, 0.22f);
            if (surface == "sand")
                return BlendColor(Color.FromArgb(255, 178, 162, 108), lightRgb, 0.15f);
            if (surface == "dirt")
                return BlendColor(Color.FromArgb(255, 151, 132, 82), lightRgb, 0.18f);
            if (surface == "cliff")
                return BlendColor(Color.FromArgb(255, 178, 183, 121), lightRgb, 0.18f);
            if (surface == "grass")
                return BlendColor(Color.FromArgb(255, 70, 162, 70), lightRgb, 0.14f);
            return lightRgb;
        }

        private string StoneHillSurfaceKindText(TerrainPolygon polygon)
        {
            if (polygon == null) return "on";
            string manual = TerrainMaterialOverrideFor(polygon);
            if (!string.IsNullOrEmpty(manual))
                return "manual " + manual;
            if (IsWaterSurface(polygon)) return "water bias";
            if (IsCliffSurface(polygon)) return "cliff bias";
            if (IsStoneSurface(polygon)) return "stone bias";
            if (IsSandSurface(polygon)) return "sand bias";
            if (IsDirtSurface(polygon)) return "dry dirt bias";
            if (IsGrassSurface(polygon)) return "grass bias";
            return "on, no material override";
        }

        private string TerrainMaterialOverrideFor(TerrainPolygon polygon)
        {
            if (polygon == null || polygon.TextureId < 0)
                return "";
            string surface;
            return terrainMaterialOverrides.TryGetValue(polygon.TextureId, out surface) ? surface : "";
        }

        private int CountTerrainTextureFaces(int textureId)
        {
            if (geometry == null || textureId < 0) return 0;
            int count = 0;
            foreach (TerrainPolygon polygon in geometry.Polygons)
            {
                if (polygon.TextureId == textureId)
                    count++;
            }
            return count;
        }

        private bool IsWaterSurface(TerrainPolygon polygon)
        {
            if (polygon == null) return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "water") return true;
            if (IsExplicitNonWaterSurface(manual)) return false;
            if (IsPeacekeepersLevel())
                return IsPeacekeepersWaterSurface(polygon);
            if (!IsStoneHillLevel()) return IsGenericBlueWaterSurface(polygon);
            if (polygon.TextureId == 32)
                return true;
            float zSpread = polygon.MaxZ - polygon.MinZ;
            if (polygon.TextureId == 13 || polygon.TextureId == 14)
                return false;
            if (zSpread <= 20f && polygon.AvgZ >= 980f && polygon.AvgZ <= 1330f && polygon.TextureId == 25 && polygon.HasFaceColor && polygon.FaceColor.B >= polygon.FaceColor.R + 8 && polygon.FaceColor.B >= polygon.FaceColor.G + 8)
                return true;
            if (polygon.AvgZ < 1220f && polygon.HasFaceColor && polygon.FaceColor.B >= polygon.FaceColor.G + 14 && polygon.FaceColor.B >= polygon.FaceColor.R + 14)
                return true;
            return false;
        }

        private bool IsStoneSurface(TerrainPolygon polygon)
        {
            if (polygon == null) return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "stone") return true;
            if (IsExplicitNonStoneSurface(manual)) return false;
            if (IsPeacekeepersLevel())
                return IsPeacekeepersStoneSurface(polygon);
            if (!IsStoneHillLevel()) return false;
            if (IsStoneTextureId(polygon.TextureId))
                return true;
            if ((polygon.MaxZ - polygon.MinZ) > 220f)
                return true;
            return false;
        }

        private bool IsSandSurface(TerrainPolygon polygon)
        {
            if (polygon == null)
                return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "sand") return true;
            if (IsExplicitNonSandSurface(manual)) return false;
            if (!IsStoneHillLevel()) return false;
            return false;
        }

        private bool IsDirtSurface(TerrainPolygon polygon)
        {
            if (polygon == null)
                return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "dirt") return true;
            if (IsExplicitNonDirtSurface(manual)) return false;
            if (IsPeacekeepersLevel())
                return IsPeacekeepersDirtSurface(polygon);
            return false;
        }

        private bool IsCliffSurface(TerrainPolygon polygon)
        {
            if (polygon == null)
                return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "cliff") return true;
            if (IsExplicitNonCliffSurface(manual)) return false;
            if (IsPeacekeepersLevel())
                return IsPeacekeepersCliffSurface(polygon);
            return false;
        }

        private bool IsGrassSurface(TerrainPolygon polygon)
        {
            if (polygon == null)
                return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "grass") return true;
            if (IsExplicitNonGrassSurface(manual)) return false;
            if (!IsStoneHillLevel()) return false;
            if (IsWaterSurface(polygon) || IsSandSurface(polygon) || IsStoneSurface(polygon))
                return false;
            if (IsGrassTextureId(polygon.TextureId))
                return true;
            float zSpread = polygon.MaxZ - polygon.MinZ;
            if (zSpread <= 160f && polygon.AvgZ >= 1180f && polygon.TextureId >= 1 && polygon.TextureId <= 29)
                return true;
            return zSpread <= 120f && polygon.AvgZ >= 1120f;
        }

        private static bool IsExplicitNonWaterSurface(string manual)
        {
            return manual == "grass" || manual == "sand" || manual == "stone" || manual == "dirt" || manual == "cliff" || manual == "unknown";
        }

        private static bool IsExplicitNonStoneSurface(string manual)
        {
            return manual == "grass" || manual == "sand" || manual == "water" || manual == "dirt" || manual == "cliff" || manual == "unknown";
        }

        private static bool IsExplicitNonSandSurface(string manual)
        {
            return manual == "grass" || manual == "water" || manual == "stone" || manual == "dirt" || manual == "cliff" || manual == "unknown";
        }

        private static bool IsExplicitNonDirtSurface(string manual)
        {
            return manual == "grass" || manual == "water" || manual == "sand" || manual == "stone" || manual == "cliff" || manual == "unknown";
        }

        private static bool IsExplicitNonCliffSurface(string manual)
        {
            return manual == "grass" || manual == "water" || manual == "sand" || manual == "stone" || manual == "dirt" || manual == "unknown";
        }

        private static bool IsExplicitNonGrassSurface(string manual)
        {
            return manual == "water" || manual == "sand" || manual == "stone" || manual == "dirt" || manual == "cliff" || manual == "unknown";
        }

        private bool IsPeacekeepersWaterSurface(TerrainPolygon polygon)
        {
            if (polygon == null) return false;
            if (polygon.TextureId == 0 || polygon.TextureId == 1 || polygon.TextureId == 2)
                return true;
            return IsGenericBlueWaterSurface(polygon) && (polygon.MaxZ - polygon.MinZ) <= 72f;
        }

        private static bool IsGenericBlueWaterSurface(TerrainPolygon polygon)
        {
            if (polygon == null || !polygon.HasFaceColor) return false;
            return polygon.FaceColor.B >= polygon.FaceColor.R + 34
                && polygon.FaceColor.G >= polygon.FaceColor.R + 26
                && (polygon.MaxZ - polygon.MinZ) <= 96f;
        }

        private bool IsPeacekeepersCliffSurface(TerrainPolygon polygon)
        {
            if (polygon == null || IsWaterSurface(polygon)) return false;
            float zSpread = polygon.MaxZ - polygon.MinZ;
            if (zSpread >= 180f)
                return true;
            return polygon.TextureId == 30
                || polygon.TextureId == 31
                || polygon.TextureId == 32
                || polygon.TextureId == 33
                || polygon.TextureId == 34
                || polygon.TextureId == 35
                || polygon.TextureId == 36;
        }

        private bool IsPeacekeepersStoneSurface(TerrainPolygon polygon)
        {
            if (polygon == null || IsWaterSurface(polygon) || IsCliffSurface(polygon)) return false;
            return polygon.TextureId == 12
                || polygon.TextureId == 13
                || polygon.TextureId == 14
                || polygon.TextureId == 15
                || polygon.TextureId == 16
                || polygon.TextureId == 18
                || polygon.TextureId == 19
                || polygon.TextureId == 23
                || polygon.TextureId == 24
                || polygon.TextureId == 25
                || polygon.TextureId == 26;
        }

        private bool IsPeacekeepersDirtSurface(TerrainPolygon polygon)
        {
            if (polygon == null) return false;
            if (IsWaterSurface(polygon) || IsCliffSurface(polygon) || IsStoneSurface(polygon))
                return false;
            return polygon.HasTextureId;
        }

        private static bool IsGrassTextureId(int textureId)
        {
            return textureId == 1
                || textureId == 2
                || textureId == 3
                || textureId == 4
                || textureId == 5
                || textureId == 6
                || textureId == 10
                || textureId == 11
                || textureId == 12
                || textureId == 15
                || textureId == 16
                || textureId == 17
                || textureId == 18
                || textureId == 19
                || textureId == 20
                || textureId == 21
                || textureId == 22
                || textureId == 23
                || textureId == 24
                || textureId == 26
                || textureId == 27
                || textureId == 28
                || textureId == 29
                || textureId == 40;
        }

        private static bool IsStoneTextureId(int textureId)
        {
            return textureId == 13
                || textureId == 14
                || textureId == 30
                || textureId == 31
                || textureId == 33
                || textureId == 34
                || textureId == 35
                || textureId == 36
                || textureId == 37
                || textureId == 38
                || textureId == 39;
        }

        private static Color BlendColor(Color a, Color b, float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;
            float inv = 1f - amount;
            return Color.FromArgb(
                Math.Max(a.A, b.A),
                (int)Math.Round((a.R * inv) + (b.R * amount)),
                (int)Math.Round((a.G * inv) + (b.G * amount)),
                (int)Math.Round((a.B * inv) + (b.B * amount)));
        }

        private void DrawHeightContours(Graphics g, RectangleF worldView)
        {
            if (geometry == null || geometry.Polygons.Count == 0) return;
            float contourStep = zoom < 0.05f ? 256f : 128f;
            int maxDraw = completeTerrainDraw ? geometry.Polygons.Count : (zoom < 0.035f ? 900 : (zoom < 0.08f ? 1500 : 2600));
            int step = Math.Max(1, (int)Math.Ceiling(geometry.Polygons.Count / (double)maxDraw));
            using (Pen minor = new Pen(Color.FromArgb(useGameTerrainStyle ? 95 : 145, useGameTerrainStyle ? 245 : 255, useGameTerrainStyle ? 238 : 255, useGameTerrainStyle ? 180 : 255), 1f))
            using (Pen major = new Pen(Color.FromArgb(useGameTerrainStyle ? 150 : 210, 255, 255, useGameTerrainStyle ? 216 : 255), 1.7f))
            {
                for (int i = 0; i < geometry.Polygons.Count; i += step)
                {
                    TerrainPolygon polygon = geometry.Polygons[i];
                    if (!polygon.Bounds.IntersectsWith(worldView)) continue;
                    if (polygon.Points.Length < 3 || polygon.MaxZ - polygon.MinZ < 12f) continue;

                    float firstLevel = (float)(Math.Ceiling(polygon.MinZ / contourStep) * contourStep);
                    for (float level = firstLevel; level <= polygon.MaxZ; level += contourStep)
                    {
                        Pen pen = (((int)Math.Round(level) % 512) == 0) ? major : minor;
                        for (int t = 1; t < polygon.Points.Length - 1; t++)
                        {
                            PointF a = polygon.Points[0];
                            PointF b = polygon.Points[t];
                            PointF c = polygon.Points[t + 1];
                            float az = polygon.ZValues[0];
                            float bz = polygon.ZValues[t];
                            float cz = polygon.ZValues[t + 1];
                            DrawContourTriangle(g, pen, level, a, az, b, bz, c, cz);
                        }
                    }
                }
            }
        }

        private void DrawContourTriangle(Graphics g, Pen pen, float level, PointF a, float az, PointF b, float bz, PointF c, float cz)
        {
            PointF p1, p2;
            int count = 0;
            p1 = PointF.Empty;
            p2 = PointF.Empty;
            PointF hit;
            if (TryContourEdge(level, a, az, b, bz, out hit))
            {
                p1 = hit;
                count++;
            }
            if (TryContourEdge(level, b, bz, c, cz, out hit))
            {
                if (count == 0) p1 = hit; else p2 = hit;
                count++;
            }
            if (TryContourEdge(level, c, cz, a, az, out hit))
            {
                if (count == 0) p1 = hit; else p2 = hit;
                count++;
            }
            if (count >= 2)
                g.DrawLine(pen, WorldToScreen(p1.X, p1.Y, level), WorldToScreen(p2.X, p2.Y, level));
        }

        private static bool TryContourEdge(float level, PointF a, float az, PointF b, float bz, out PointF hit)
        {
            hit = PointF.Empty;
            float da = level - az;
            float db = level - bz;
            if ((da < 0f && db < 0f) || (da > 0f && db > 0f)) return false;
            float span = bz - az;
            if (Math.Abs(span) < 0.0001f) return false;
            float t = (level - az) / span;
            if (t < 0f || t > 1f) return false;
            hit = new PointF(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
            return true;
        }

        private void DrawSelectedTerrain(Graphics g)
        {
            if (geometry == null || selectedTerrainIndex < 0 || selectedTerrainIndex >= geometry.Polygons.Count)
                return;
            TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
            PointF[] pts = new PointF[polygon.Points.Length];
            for (int i = 0; i < polygon.Points.Length; i++)
                pts[i] = WorldToScreen(polygon.Points[i].X, polygon.Points[i].Y, polygon.ZValues[i]);

            using (Brush fill = new SolidBrush(Color.FromArgb(72, 255, 228, 92)))
            using (Pen outline = new Pen(Color.FromArgb(255, 255, 232, 96), 2.5f))
            using (Brush vertex = new SolidBrush(Color.White))
            using (Pen vertexOutline = new Pen(Color.FromArgb(24, 16, 18, 24), 1f))
            {
                g.FillPolygon(fill, pts);
                g.DrawPolygon(outline, pts);
                for (int i = 0; i < pts.Length; i++)
                {
                    RectangleF box = new RectangleF(pts[i].X - 3f, pts[i].Y - 3f, 6f, 6f);
                    g.FillEllipse(vertex, box);
                    g.DrawEllipse(vertexOutline, box);
                }
            }
        }

        private void DrawTextureFamilyHighlight(Graphics g, RectangleF worldView)
        {
            if (geometry == null || selectedTerrainIndex < 0 || selectedTerrainIndex >= geometry.Polygons.Count)
                return;

            TerrainPolygon selected = geometry.Polygons[selectedTerrainIndex];
            if (!selected.HasTextureId)
                return;

            using (Brush fill = new SolidBrush(Color.FromArgb(34, 255, 224, 96)))
            using (Pen outline = new Pen(Color.FromArgb(135, 255, 224, 96), 1.25f))
            {
                foreach (TerrainPolygon polygon in geometry.Polygons)
                {
                    if (object.ReferenceEquals(polygon, selected) || polygon.TextureId != selected.TextureId || !polygon.Bounds.IntersectsWith(worldView))
                        continue;

                    PointF[] pts = new PointF[polygon.Points.Length];
                    for (int i = 0; i < polygon.Points.Length; i++)
                        pts[i] = WorldToScreen(polygon.Points[i].X, polygon.Points[i].Y, polygon.ZValues[i]);
                    g.FillPolygon(fill, pts);
                    g.DrawPolygon(outline, pts);
                }
            }
        }

        private void DrawTerrainHover(Graphics g)
        {
            if (hoverTerrainIndex < 0 || !hasHoverTerrainZ) return;
            PointF s = WorldToScreen(hoverTerrainPoint.X, hoverTerrainPoint.Y, hoverTerrainZ);
            using (Pen pen = new Pen(Color.FromArgb(235, 255, 255, 255), 1.5f))
            using (Brush back = new SolidBrush(Color.FromArgb(190, 8, 12, 16)))
            using (Brush fore = new SolidBrush(Color.White))
            using (Font small = new Font("Segoe UI", 8f))
            {
                g.DrawLine(pen, s.X - 8, s.Y, s.X + 8, s.Y);
                g.DrawLine(pen, s.X, s.Y - 8, s.X, s.Y + 8);
                string text = "Z " + hoverTerrainZ.ToString("0");
                SizeF size = g.MeasureString(text, small);
                RectangleF rect = new RectangleF(s.X + 10, s.Y - 12, size.Width + 8, 20);
                g.FillRectangle(back, rect);
                g.DrawString(text, small, fore, rect.X + 4, rect.Y + 3);
            }
        }

        private void DrawHeightLegend(Graphics g, Size clientSize)
        {
            if (geometry == null || geometry.MaxZ <= geometry.MinZ) return;
            const int width = 180;
            const int height = 12;
            int x = Math.Max(12, clientSize.Width - width - 16);
            int y = Math.Max(44, clientSize.Height - 52);
            using (Brush back = new SolidBrush(Color.FromArgb(185, 8, 12, 16)))
            using (Brush fore = new SolidBrush(Color.White))
            using (Font small = new Font("Segoe UI", 8f))
            {
                g.FillRectangle(back, x - 8, y - 18, width + 16, 48);
                for (int i = 0; i < width; i++)
                {
                    double t = i / (double)Math.Max(1, width - 1);
                    double z = geometry.MinZ + ((geometry.MaxZ - geometry.MinZ) * t);
                    using (Pen p = new Pen(GeometryLoader.HeightColor(z, geometry.MinZ, geometry.MaxZ), 1f))
                        g.DrawLine(p, x + i, y, x + i, y + height);
                }
                g.DrawRectangle(Pens.White, x, y, width, height);
                g.DrawString("Z " + geometry.MinZ.ToString("0"), small, fore, x, y - 16);
                string max = geometry.MaxZ.ToString("0");
                SizeF maxSize = g.MeasureString(max, small);
                g.DrawString(max, small, fore, x + width - maxSize.Width, y - 16);
                g.DrawString(showContours ? "contours on" : "contours off", small, fore, x, y + height + 3);
            }
        }

        private void DrawTextureDebugLabels(Graphics g, RectangleF worldView)
        {
            if (geometry == null || geometry.Polygons.Count == 0 || zoom < 0.055f)
                return;

            int maxDraw = zoom < 0.09f ? 360 : 900;
            int step = Math.Max(1, (int)Math.Ceiling(geometry.Polygons.Count / (double)maxDraw));
            using (Font small = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (Brush back = new SolidBrush(Color.FromArgb(178, 8, 12, 16)))
            using (Brush fore = new SolidBrush(Color.FromArgb(242, 255, 255, 230)))
            {
                for (int i = 0; i < geometry.Polygons.Count; i += step)
                {
                    TerrainPolygon polygon = geometry.Polygons[i];
                    if (!polygon.Bounds.IntersectsWith(worldView) || !polygon.HasTextureId)
                        continue;

                    PointF center = WorldToScreen(polygon.CenterX, polygon.CenterY, polygon.AvgZ);
                    string label = "T" + polygon.TextureId.ToString() + " D" + polygon.FaceDepth.ToString();
                    if (polygon.FaceFlip) label += " F";
                    int window = TextureWindowIndex(polygon);
                    if (window >= 0) label += " W" + window.ToString();
                    SizeF size = g.MeasureString(label, small);
                    RectangleF rect = new RectangleF(center.X - (size.Width / 2f) - 3f, center.Y - 8f, size.Width + 6f, 17f);
                    g.FillRectangle(back, rect);
                    g.DrawString(label, small, fore, rect.X + 3f, rect.Y + 1f);
                }
            }
        }

        private void DrawMobys(Graphics g, RectangleF worldView)
        {
            using (Pen selectedPen = new Pen(Color.White, 2f))
            using (Pen linkedPen = new Pen(Color.FromArgb(120, 240, 255), 2f))
            using (Pen groupPen = new Pen(Color.FromArgb(255, 196, 86), 1.7f))
            using (Font small = new Font("Segoe UI", 8f))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush labelBack = new SolidBrush(Color.FromArgb(150, 10, 12, 16)))
            {
                List<int> drawOrder = BuildMobyDrawOrder(worldView);
                for (int orderIndex = 0; orderIndex < drawOrder.Count; orderIndex++)
                {
                    int i = drawOrder[orderIndex];
                    Moby m = mobys[i];
                    PointF s = WorldToScreen(m.X, m.Y, m.Z);
                    bool selected = i == selectedMobyIndex || i == dragMobyIndex;
                    bool linkedSelected = !selected && IsLinkedToSelected(i);
                    bool groupSelected = !selected && !linkedSelected && IsInActiveSelectionGroup(i);
                    bool priorityLabel = IsAlwaysTopMoby(m);
                    float r = selected ? 8f : (priorityLabel ? 7f : 6f);
                    if (showGroundCues && zoom > 0.035f)
                        DrawMobyGroundCue(g, m, s, r, selected || linkedSelected || groupSelected || m.IsEdited);
                    using (Brush brush = new SolidBrush(m.Color))
                    using (Pen iconPen = new Pen(Color.FromArgb(24, 16, 18, 24), 1.25f))
                        DrawMobyIcon(g, m, s, r, brush, iconPen);
                    if (m.IsEdited)
                    {
                        using (Pen editedPen = new Pen(Color.FromArgb(255, 224, 92), 2f))
                            g.DrawEllipse(editedPen, s.X - r - 4, s.Y - r - 4, (r + 4) * 2, (r + 4) * 2);
                    }
                    if (selected)
                        g.DrawEllipse(selectedPen, s.X - r - 2, s.Y - r - 2, (r + 2) * 2, (r + 2) * 2);
                    else if (linkedSelected)
                        g.DrawEllipse(linkedPen, s.X - r - 3, s.Y - r - 3, (r + 3) * 2, (r + 3) * 2);
                    else if (groupSelected)
                        g.DrawEllipse(groupPen, s.X - r - 3, s.Y - r - 3, (r + 3) * 2, (r + 3) * 2);

                    if (editorMode == EditorMode.Mobys && (showLabels || priorityLabel) && zoom > (priorityLabel ? 0.035f : 0.055f))
                    {
                        string label = MapLabelForMoby(m);
                        SizeF size = g.MeasureString(label, small);
                        RectangleF rect = new RectangleF(s.X + 9, s.Y - 10, size.Width + 8, 18);
                        g.FillRectangle(labelBack, rect);
                        g.DrawString(label, small, textBrush, rect.X + 4, rect.Y + 2);
                    }
                }
            }
        }

        private List<int> BuildMobyDrawOrder(RectangleF worldView)
        {
            List<int> drawOrder = new List<int>();
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby moby = mobys[i];
                if (moby != null && worldView.Contains(moby.X, moby.Y))
                    drawOrder.Add(i);
            }
            drawOrder.Sort(CompareMobyDrawOrder);
            return drawOrder;
        }

        private int CompareMobyDrawOrder(int a, int b)
        {
            int priority = MobyLayerPriority(a).CompareTo(MobyLayerPriority(b));
            if (priority != 0) return priority;
            return a.CompareTo(b);
        }

        private int MobyLayerPriority(int mobyIndex)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return 0;
            Moby moby = mobys[mobyIndex];
            int priority = 0;
            if (IsChestContentMarker(moby)) priority = Math.Max(priority, 20);
            if (IsAlwaysTopMoby(moby)) priority = Math.Max(priority, 80);
            if (IsInActiveSelectionGroup(mobyIndex)) priority = Math.Max(priority, 95);
            if (IsLinkedToSelected(mobyIndex)) priority = Math.Max(priority, 110);
            if (mobyIndex == selectedMobyIndex) priority = Math.Max(priority, 130);
            if (mobyIndex == dragMobyIndex) priority = Math.Max(priority, 140);
            return priority;
        }

        private static bool IsAlwaysTopMoby(Moby moby)
        {
            if (moby == null) return false;
            if (IsLinkedContentsChestLike(moby)) return true;
            MobyIconKind kind = GetMobyIconKind(moby);
            return kind == MobyIconKind.Chest || kind == MobyIconKind.SpringChest || kind == MobyIconKind.LockedChest || kind == MobyIconKind.BlastChest || kind == MobyIconKind.Dragon || kind == MobyIconKind.Pedestal;
        }

        private static string MapLabelForMoby(Moby moby)
        {
            if (moby == null) return "";
            string iconText = MobyIconText(moby);
            string label = moby.DisplayLabel;
            if (GetMobyIconKind(moby) == MobyIconKind.Dragon)
                label = DragonMapName(moby);
            return MobyId(moby) + (iconText == "Mby" ? " " : " " + iconText + " ") + label;
        }

        private static string DragonMapName(Moby moby)
        {
            string known = KnownDragonName(moby);
            if (!string.IsNullOrEmpty(known)) return known;
            string label = moby == null ? "" : moby.DisplayLabel;
            if (string.IsNullOrEmpty(label) || string.Equals(label, "Dragon", StringComparison.OrdinalIgnoreCase) || string.Equals(label, "Dragon/NPC?", StringComparison.OrdinalIgnoreCase))
                return "Dragon";
            return label;
        }

        private static string KnownDragonName(Moby moby)
        {
            if (moby == null) return "";
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.Zone ?? "") + " " + (moby.Evidence ?? "")).ToLowerInvariant();
            string[] names = new string[] { "Nestor", "Delbin", "Tomas", "Argus", "Astor", "Lindar", "Gildas", "Gavin" };
            for (int i = 0; i < names.Length; i++)
            {
                if (text.IndexOf(names[i].ToLowerInvariant(), StringComparison.Ordinal) >= 0)
                    return names[i] + (text.IndexOf(names[i].ToLowerInvariant() + "?", StringComparison.Ordinal) >= 0 ? "?" : "");
            }
            return "";
        }

        private void DrawMobyGroundCue(Graphics g, Moby moby, PointF screen, float radius, bool labelOffset)
        {
            float groundZ;
            if (!TryFindGroundZ(moby.X, moby.Y, moby.Z, out groundZ)) return;
            float offset = moby.Z - groundZ;
            float abs = Math.Abs(offset);
            Color color = abs <= 24f
                ? Color.FromArgb(115, 116, 235, 142)
                : (offset > 0f ? Color.FromArgb(145, 255, 207, 76) : Color.FromArgb(145, 96, 194, 255));
            using (Pen pen = new Pen(color, abs <= 24f ? 1.3f : 2f))
            {
                float cueRadius = radius + 5f + Math.Min(9f, abs / 96f);
                g.DrawEllipse(pen, screen.X - cueRadius, screen.Y - cueRadius, cueRadius * 2f, cueRadius * 2f);
                if (abs > 64f)
                    g.DrawLine(pen, screen.X - cueRadius, screen.Y, screen.X + cueRadius, screen.Y);
            }

            if (labelOffset && zoom > 0.055f)
            {
                string text = (offset >= 0f ? "+" : "") + offset.ToString("0") + "Z";
                using (Font small = new Font("Segoe UI", 7.5f))
                using (Brush back = new SolidBrush(Color.FromArgb(165, 8, 12, 16)))
                using (Brush fore = new SolidBrush(Color.White))
                {
                    SizeF size = g.MeasureString(text, small);
                    RectangleF rect = new RectangleF(screen.X - (size.Width / 2f) - 4f, screen.Y + radius + 8f, size.Width + 8f, 17f);
                    g.FillRectangle(back, rect);
                    g.DrawString(text, small, fore, rect.X + 4f, rect.Y + 2f);
                }
            }
        }

        private static void DrawMobyIcon(Graphics g, Moby moby, PointF center, float r, Brush brush, Pen outline)
        {
            RectangleF box = new RectangleF(center.X - r, center.Y - r, r * 2f, r * 2f);
            switch (GetMobyIconKind(moby))
            {
                case MobyIconKind.Gem:
                    PointF[] gem = new PointF[]
                    {
                        new PointF(center.X, center.Y - r - 1f),
                        new PointF(center.X + r + 1f, center.Y),
                        new PointF(center.X, center.Y + r + 1f),
                        new PointF(center.X - r - 1f, center.Y)
                    };
                    g.FillPolygon(brush, gem);
                    g.DrawPolygon(outline, gem);
                    break;
                case MobyIconKind.Chest:
                    RectangleF chest = new RectangleF(center.X - r - 1f, center.Y - (r * 0.72f), (r + 1f) * 2f, r * 1.44f);
                    g.FillRectangle(brush, chest);
                    g.DrawRectangle(outline, chest.X, chest.Y, chest.Width, chest.Height);
                    g.DrawLine(outline, chest.Left, center.Y, chest.Right, center.Y);
                    break;
                case MobyIconKind.SpringChest:
                    RectangleF springChest = new RectangleF(center.X - r - 1f, center.Y - (r * 0.68f), (r + 1f) * 2f, r * 1.36f);
                    g.FillRectangle(brush, springChest);
                    g.DrawRectangle(outline, springChest.X, springChest.Y, springChest.Width, springChest.Height);
                    g.DrawLine(outline, springChest.Left, center.Y, springChest.Right, center.Y);
                    g.DrawLine(outline, center.X - r * 0.58f, center.Y + r * 0.42f, center.X - r * 0.3f, center.Y + r * 0.12f);
                    g.DrawLine(outline, center.X - r * 0.3f, center.Y + r * 0.12f, center.X, center.Y + r * 0.42f);
                    g.DrawLine(outline, center.X, center.Y + r * 0.42f, center.X + r * 0.3f, center.Y + r * 0.12f);
                    g.DrawLine(outline, center.X + r * 0.3f, center.Y + r * 0.12f, center.X + r * 0.58f, center.Y + r * 0.42f);
                    DrawSmallRewardGem(g, moby, center.X, center.Y - r * 0.34f, Math.Max(3f, r * 0.46f), outline);
                    break;
                case MobyIconKind.LockedChest:
                    RectangleF lockedChest = new RectangleF(center.X - r - 1f, center.Y - (r * 0.72f), (r + 1f) * 2f, r * 1.44f);
                    g.FillRectangle(brush, lockedChest);
                    g.DrawRectangle(outline, lockedChest.X, lockedChest.Y, lockedChest.Width, lockedChest.Height);
                    g.DrawLine(outline, lockedChest.Left, center.Y, lockedChest.Right, center.Y);
                    RectangleF lockBody = new RectangleF(center.X - r * 0.28f, center.Y - r * 0.02f, r * 0.56f, r * 0.48f);
                    using (Brush lockBrush = new SolidBrush(Color.FromArgb(225, 34, 28, 22)))
                        g.FillRectangle(lockBrush, lockBody);
                    g.DrawRectangle(outline, lockBody.X, lockBody.Y, lockBody.Width, lockBody.Height);
                    g.DrawArc(outline, center.X - r * 0.34f, center.Y - r * 0.54f, r * 0.68f, r * 0.72f, 200f, 140f);
                    break;
                case MobyIconKind.BlastChest:
                    RectangleF blastChest = new RectangleF(center.X - r - 1f, center.Y - (r * 0.7f), (r + 1f) * 2f, r * 1.4f);
                    g.FillRectangle(brush, blastChest);
                    g.DrawRectangle(outline, blastChest.X, blastChest.Y, blastChest.Width, blastChest.Height);
                    g.DrawLine(outline, blastChest.Left, center.Y, blastChest.Right, center.Y);
                    g.DrawLine(outline, blastChest.Left + r * 0.22f, blastChest.Top + r * 0.18f, blastChest.Right - r * 0.22f, blastChest.Bottom - r * 0.18f);
                    g.DrawLine(outline, blastChest.Right - r * 0.22f, blastChest.Top + r * 0.18f, blastChest.Left + r * 0.22f, blastChest.Bottom - r * 0.18f);
                    using (Pen fusePen = new Pen(Color.FromArgb(230, 248, 222, 94), 1.4f))
                    {
                        g.DrawLine(fusePen, center.X + r * 0.52f, center.Y - r * 0.64f, center.X + r * 0.88f, center.Y - r * 1.05f);
                        g.DrawLine(fusePen, center.X + r * 0.78f, center.Y - r * 0.98f, center.X + r * 1.02f, center.Y - r * 0.86f);
                    }
                    DrawSmallRewardGem(g, moby, center.X, center.Y + r * 0.12f, Math.Max(3f, r * 0.42f), outline);
                    break;
                case MobyIconKind.Dragon:
                    PointF[] dragon = new PointF[]
                    {
                        new PointF(center.X, center.Y - r - 2f),
                        new PointF(center.X + r + 2f, center.Y + r),
                        new PointF(center.X - r - 2f, center.Y + r)
                    };
                    g.FillPolygon(brush, dragon);
                    g.DrawPolygon(outline, dragon);
                    break;
                case MobyIconKind.Pedestal:
                    g.FillEllipse(brush, box);
                    g.DrawEllipse(outline, box);
                    g.DrawLine(outline, center.X - r, center.Y, center.X + r, center.Y);
                    break;
                case MobyIconKind.Fairy:
                    PointF[] star = new PointF[]
                    {
                        new PointF(center.X, center.Y - r - 2f),
                        new PointF(center.X + 2f, center.Y - 2f),
                        new PointF(center.X + r + 2f, center.Y),
                        new PointF(center.X + 2f, center.Y + 2f),
                        new PointF(center.X, center.Y + r + 2f),
                        new PointF(center.X - 2f, center.Y + 2f),
                        new PointF(center.X - r - 2f, center.Y),
                        new PointF(center.X - 2f, center.Y - 2f)
                    };
                    g.FillPolygon(brush, star);
                    g.DrawPolygon(outline, star);
                    break;
                case MobyIconKind.Whirlwind:
                    g.FillEllipse(brush, box);
                    g.DrawArc(outline, center.X - r, center.Y - r, r * 2f, r * 2f, 35f, 285f);
                    g.DrawArc(outline, center.X - (r * 0.55f), center.Y - (r * 0.55f), r * 1.1f, r * 1.1f, 215f, 250f);
                    break;
                case MobyIconKind.Portal:
                    g.FillEllipse(brush, box);
                    g.DrawEllipse(outline, box);
                    g.DrawEllipse(outline, center.X - (r * 0.48f), center.Y - (r * 0.48f), r * 0.96f, r * 0.96f);
                    g.DrawLine(outline, center.X, center.Y - r, center.X, center.Y + r);
                    break;
                case MobyIconKind.Key:
                    g.FillEllipse(brush, center.X - r, center.Y - r * 0.65f, r * 1.3f, r * 1.3f);
                    g.DrawEllipse(outline, center.X - r, center.Y - r * 0.65f, r * 1.3f, r * 1.3f);
                    g.DrawLine(outline, center.X, center.Y, center.X + r + 3f, center.Y + r + 3f);
                    g.DrawLine(outline, center.X + r * 0.72f, center.Y + r * 0.72f, center.X + r * 1.15f, center.Y + r * 0.3f);
                    g.DrawLine(outline, center.X + r * 0.98f, center.Y + r * 0.98f, center.X + r * 1.42f, center.Y + r * 0.56f);
                    break;
                case MobyIconKind.Camera:
                    RectangleF cameraBody = new RectangleF(center.X - r, center.Y - r * 0.62f, r * 1.45f, r * 1.24f);
                    PointF[] cameraLens = new PointF[]
                    {
                        new PointF(center.X + r * 0.45f, center.Y - r * 0.36f),
                        new PointF(center.X + r + 2f, center.Y - r * 0.72f),
                        new PointF(center.X + r + 2f, center.Y + r * 0.72f),
                        new PointF(center.X + r * 0.45f, center.Y + r * 0.36f)
                    };
                    g.FillRectangle(brush, cameraBody);
                    g.FillPolygon(brush, cameraLens);
                    g.DrawRectangle(outline, cameraBody.X, cameraBody.Y, cameraBody.Width, cameraBody.Height);
                    g.DrawPolygon(outline, cameraLens);
                    g.DrawEllipse(outline, center.X - r * 0.42f, center.Y - r * 0.42f, r * 0.84f, r * 0.84f);
                    break;
                case MobyIconKind.Enemy:
                    PointF[] active = new PointF[]
                    {
                        new PointF(center.X - r, center.Y - r * 0.5f),
                        new PointF(center.X, center.Y - r - 1f),
                        new PointF(center.X + r, center.Y - r * 0.5f),
                        new PointF(center.X + r, center.Y + r * 0.5f),
                        new PointF(center.X, center.Y + r + 1f),
                        new PointF(center.X - r, center.Y + r * 0.5f)
                    };
                    g.FillPolygon(brush, active);
                    g.DrawPolygon(outline, active);
                    break;
                case MobyIconKind.Scenery:
                    g.FillRectangle(brush, center.X - r, center.Y - r, r * 2f, r * 2f);
                    g.DrawRectangle(outline, center.X - r, center.Y - r, r * 2f, r * 2f);
                    break;
                case MobyIconKind.Helper:
                    g.FillEllipse(brush, box);
                    g.DrawLine(outline, center.X - r, center.Y - r, center.X + r, center.Y + r);
                    g.DrawLine(outline, center.X + r, center.Y - r, center.X - r, center.Y + r);
                    break;
                default:
                    g.FillEllipse(brush, box);
                    g.DrawEllipse(outline, box);
                    break;
            }
        }

        private static void DrawSmallRewardGem(Graphics g, Moby moby, float x, float y, float r, Pen outline)
        {
            PointF[] gem = new PointF[]
            {
                new PointF(x, y - r),
                new PointF(x + r, y),
                new PointF(x, y + r),
                new PointF(x - r, y)
            };
            using (Brush gemBrush = new SolidBrush(GemDrawColorForReward(moby)))
                g.FillPolygon(gemBrush, gem);
            g.DrawPolygon(outline, gem);
        }

        private static Color GemDrawColorForReward(Moby moby)
        {
            int gemByte = 0;
            if (moby != null)
                gemByte = moby.HasRewardColorEdit ? moby.RewardByte53Override : moby.Flag4B;
            switch (gemByte)
            {
                case 0x54: return Color.FromArgb(46, 204, 113);
                case 0x55: return Color.FromArgb(52, 152, 219);
                case 0x56: return Color.FromArgb(241, 196, 15);
                case 0x57: return Color.FromArgb(155, 89, 182);
                case 0x53: return Color.FromArgb(231, 76, 60);
                default: return Color.FromArgb(245, 245, 238);
            }
        }

        private void DrawOverlayText(Graphics g, bool fast)
        {
            string text = string.Format(
                "Native renderer   Mode {0}   Zoom {1:P0}   {2}   Faces {3}   Lines {4}   Height {5}   Materials {6}   Textures {7}   View {8}   Mobys {9}",
                EditorModeText(),
                zoom,
                fast ? "fast nav" : "full draw",
                showFaces ? "on" : "off",
                showLines ? "on" : "off",
                HeightAssistText(),
                showMaterialIds ? "on" : "off",
                SourceTextureStatusText(),
                MirrorStatusText(),
                mobys.Count);
            RectangleF rect = new RectangleF(10, 10, 1210, 28);
            using (Brush back = new SolidBrush(Color.FromArgb(190, 8, 12, 16)))
            using (Brush fore = new SolidBrush(Color.White))
            {
                g.FillRectangle(back, rect);
                g.DrawString(text, Font, fore, rect.X + 8, rect.Y + 7);
            }
        }

        private void Draw3DCompass(Graphics g, Size clientSize)
        {
            PointF originView = WorldToView3D(0f, 0f, 0f);
            PointF xView = WorldToView3D(512f, 0f, 0f);
            PointF yView = WorldToView3D(0f, 512f, 0f);
            PointF xDir = NormalizeDirection(new PointF(xView.X - originView.X, xView.Y - originView.Y));
            PointF yDir = NormalizeDirection(new PointF(yView.X - originView.X, yView.Y - originView.Y));
            PointF origin = new PointF(clientSize.Width - 72f, 62f);
            using (Brush back = new SolidBrush(Color.FromArgb(165, 8, 12, 16)))
            using (Pen xPen = new Pen(Color.FromArgb(255, 255, 104, 84), 2f))
            using (Pen yPen = new Pen(Color.FromArgb(255, 120, 210, 255), 2f))
            using (Brush xBrush = new SolidBrush(Color.FromArgb(255, 255, 132, 112)))
            using (Brush yBrush = new SolidBrush(Color.FromArgb(255, 150, 225, 255)))
            using (Font small = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                g.FillEllipse(back, origin.X - 38f, origin.Y - 38f, 76f, 76f);
                DrawCompassAxis(g, xPen, xBrush, small, origin, xDir, "X");
                DrawCompassAxis(g, yPen, yBrush, small, origin, yDir, "Y");
            }
        }

        private static PointF NormalizeDirection(PointF direction)
        {
            float length = (float)Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y));
            if (length < 0.001f) return new PointF(1f, 0f);
            return new PointF(direction.X / length, direction.Y / length);
        }

        private static void DrawCompassAxis(Graphics g, Pen pen, Brush brush, Font font, PointF origin, PointF direction, string label)
        {
            float len = 27f;
            PointF end = new PointF(origin.X + (direction.X * len), origin.Y + (direction.Y * len));
            g.DrawLine(pen, origin, end);
            g.FillEllipse(brush, end.X - 3f, end.Y - 3f, 6f, 6f);
            g.DrawString(label, font, brush, end.X + (direction.X * 6f) - 4f, end.Y + (direction.Y * 6f) - 7f);
        }

        private string MirrorStatusText()
        {
            if (view3D) return "3D yaw " + (view3DYaw * 180.0 / Math.PI).ToString("0");
            if (mirrorViewX && mirrorViewY) return "flip X/Y";
            if (mirrorViewX) return "flip X";
            if (mirrorViewY) return "flip Y";
            return "raw";
        }

        private string HeightAssistText()
        {
            List<string> parts = new List<string>();
            if (showHeightTint) parts.Add("tint");
            if (showContours) parts.Add("contours");
            if (showGroundCues) parts.Add("ground");
            return parts.Count == 0 ? "off" : string.Join("+", parts.ToArray());
        }

        private bool TryShowCanvasContextMenu(Point location)
        {
            if (geometry == null) return false;

            int hit = HitMoby(location);
            if (hit >= 0)
            {
                SelectMoby(hit);
                ShowMobyContextMenu(hit, location);
                return true;
            }

            if (editorMode == EditorMode.Terrain)
            {
                if (view3D)
                    selectedTerrainIndex = HitTerrainPolygon3D(location);
                else
                {
                    selectedTerrainPoint = ScreenToWorld(location.X, location.Y);
                    selectedTerrainIndex = HitTerrainPolygon(selectedTerrainPoint);
                }

                if (selectedTerrainIndex >= 0)
                {
                    ShowTerrainContextMenu(selectedTerrainIndex, location);
                    UpdateInspector();
                    canvas.Invalidate();
                    return true;
                }
            }

            return false;
        }

        private void ShowMobyContextMenu(int mobyIndex, Point location)
        {
            ShowMobyContextMenu(mobyIndex, canvas, location);
        }

        private void ShowMobyContextMenu(int mobyIndex, Control menuTarget, Point location)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            Moby moby = mobys[mobyIndex];
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem identityItem = new ToolStripMenuItem("Edit editor name/type...");
            identityItem.Enabled = moby.TrueIndex >= 0;
            identityItem.Click += delegate { EditMobyIdentityOverride(mobyIndex); };
            menu.Items.Add(identityItem);
            ToolStripMenuItem contentsItem = new ToolStripMenuItem("Edit chest reward...");
            contentsItem.Enabled = CanOpenChestContentEditor(mobyIndex);
            contentsItem.Click += delegate { SelectMoby(mobyIndex); ShowChestContentsEditor(); };
            menu.Items.Add(contentsItem);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem undoItem = new ToolStripMenuItem(moby.IsEdited ? "Undo " + MobyId(moby) + " edit" : "No edit to undo");
            undoItem.Enabled = moby.IsEdited;
            undoItem.Click += delegate { UndoSingleMobyEdit(mobyIndex); };
            menu.Items.Add(undoItem);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem removeItem = new ToolStripMenuItem(moby.IsAppendedRecord ? "Remove new object" : "Hide/remove source object");
            removeItem.Enabled = moby.IsAppendedRecord || (moby.Patchable && moby.TrueIndex >= 0 && moby.TrueIndex < SourceRecordCountForLevel(currentLevelKey));
            removeItem.Click += delegate { HideMobySlot(mobyIndex); };
            menu.Items.Add(removeItem);
            menu.Show(menuTarget == null ? canvas : menuTarget, location);
        }

        private void EditMobyIdentityOverride(int mobyIndex)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            Moby moby = mobys[mobyIndex];
            if (moby.TrueIndex < 0)
            {
                statusLabel.Text = "This moby has no loader record index for a saved editor identity override.";
                return;
            }

            int signatureMatchCount = CountMobyIdentitySignatureMatches(moby);
            using (MobyIdentityOverrideDialog dialog = new MobyIdentityOverrideDialog(MobyId(moby), moby.DisplayLabel, moby.Kind, signatureMatchCount))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string label = CleanIdentityOverrideText(dialog.IdentityName, 80);
                string kind = CleanIdentityOverrideText(dialog.IdentityKind, 120);
                if (string.IsNullOrEmpty(label))
                {
                    MessageBox.Show(this, "Enter a name for this moby.", "Name required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    List<Moby> targets = dialog.ApplyToMatchingSignature ? FindMobyIdentitySignatureMatches(moby) : new List<Moby>(new Moby[] { moby });
                    foreach (Moby target in targets)
                        ApplyMobyIdentityOverride(target, label, kind);
                    SaveMobyIdentityOverrides(targets, label, kind);
                    BuildSelectionGroups();
                    RefreshMobyList();
                    SelectMoby(mobyIndex);
                    RefreshObjectAddChoices();
                    UpdateInspector();
                    canvas.Invalidate();
                    statusLabel.Text = "Saved editor identity override for " + targets.Count.ToString() + " moby record(s) to " + Path.GetFileName(MobyIdentityOverridesPath()) + ".";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Could not save editor identity", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Could not save editor identity override.";
                }
            }
        }

        private int CountMobyIdentitySignatureMatches(Moby seed)
        {
            return FindMobyIdentitySignatureMatches(seed).Count;
        }

        private List<Moby> FindMobyIdentitySignatureMatches(Moby seed)
        {
            List<Moby> matches = new List<Moby>();
            if (seed == null) return matches;
            if (!CanUseMobyIdentityBatchSignature(seed))
            {
                if (seed.TrueIndex >= 0)
                    matches.Add(seed);
                return matches;
            }
            for (int i = 0; i < mobys.Count; i++)
            {
                Moby candidate = mobys[i];
                if (IsMobyIdentitySignatureMatch(seed, candidate))
                    matches.Add(candidate);
            }
            return matches;
        }

        private static bool IsMobyIdentitySignatureMatch(Moby seed, Moby candidate)
        {
            if (seed == null || candidate == null) return false;
            if (candidate.TrueIndex < 0) return false;
            if (seed.Type != candidate.Type) return false;
            if (seed.SpecialDataPointer != candidate.SpecialDataPointer) return false;
            if (seed.Flag4A != candidate.Flag4A || seed.Flag4B != candidate.Flag4B) return false;
            if (seed.SpecialDataPointer == 0 && seed.State != candidate.State) return false;
            return true;
        }

        private static bool CanUseMobyIdentityBatchSignature(Moby seed)
        {
            if (seed == null || seed.TrueIndex < 0) return false;
            if (IsStandaloneGemIdentityBatchCandidate(seed)) return false;
            return seed.SpecialDataPointer != 0;
        }

        private static bool IsStandaloneGemIdentityBatchCandidate(Moby moby)
        {
            if (moby == null) return false;
            if (moby.Type != 0x18 || moby.SpecialDataPointer != 0 || moby.Flag4A != 0x40 || moby.Flag4B != 0xFF)
                return false;
            string text = MobySearchText(moby);
            return text.IndexOf("key", StringComparison.Ordinal) < 0;
        }

        private string MobyIdentityOverridesPath()
        {
            return Path.Combine(workspace, currentLevelKey + "-moby-user-overrides.json");
        }

        private static string CleanIdentityOverrideText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
                text = text.Replace("  ", " ");
            if (maxLength > 0 && text.Length > maxLength)
                text = text.Substring(0, maxLength).Trim();
            return text;
        }

        private void ApplyMobyIdentityOverride(Moby moby, string label, string kind)
        {
            if (moby == null) return;
            Color identityColor = ColorForIdentityText(label, kind, moby.Color);
            string evidence = "Manual editor identity override; runtime type/state unchanged.";

            if (moby.HasBaseIdentity)
            {
                moby.BaseLabel = label;
                moby.BaseKind = kind;
                moby.BaseConfidence = "user override";
                moby.BaseEvidence = evidence;
                moby.BaseColor = identityColor;
            }

            bool identityControlledByEdit = moby.HasGemColorEdit || moby.HasRewardColorEdit || moby.HasRecordCloneEdit || moby.HasHiddenSlotEdit;
            if (!identityControlledByEdit)
            {
                moby.Label = label;
                moby.Kind = kind;
                moby.Confidence = "user override";
                moby.Evidence = evidence;
                moby.Color = identityColor;
                if (!moby.HasBaseIdentity)
                    moby.CaptureBaseIdentity();
            }
        }

        private void SaveMobyIdentityOverride(Moby moby, string label, string kind)
        {
            SaveMobyIdentityOverrides(new List<Moby>(new Moby[] { moby }), label, kind);
        }

        private void SaveMobyIdentityOverrides(List<Moby> targets, string label, string kind)
        {
            string path = MobyIdentityOverridesPath();
            List<Dictionary<string, object>> entries = LoadMobyIdentityOverrideEntries(path);
            if (targets == null) return;

            foreach (Moby moby in targets)
            {
                if (moby == null || moby.TrueIndex < 0) continue;
                Dictionary<string, object> target = null;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (GetDictionaryInt(entries[i], "trueIndex", -1) == moby.TrueIndex)
                    {
                        target = entries[i];
                        break;
                    }
                }

                if (target == null)
                {
                    target = new Dictionary<string, object>();
                    entries.Add(target);
                }

                Color identityColor = ColorForIdentityText(label, kind, moby.Color);
                target["trueIndex"] = moby.TrueIndex;
                if (moby.LegacyIndex >= 0)
                    target["legacyIndex"] = moby.LegacyIndex;
                target["displayTargetLabel"] = label;
                target["candidateKind"] = kind;
                target["typeId"] = moby.Type;
                target["typeHex"] = "0x" + moby.Type.ToString("X2");
                target["stateHex"] = "0x" + moby.State.ToString("X2");
                target["specialDataPointer"] = FormatAddress(moby.SpecialDataPointer);
                target["flag4AHex"] = "0x" + moby.Flag4A.ToString("X2");
                target["flag4BHex"] = "0x" + moby.Flag4B.ToString("X2");
                if (moby.IsStandaloneGemSourceRecord)
                {
                    target["sourceByte36Hex"] = "0x" + moby.SourceByte36.ToString("X2");
                    target["sourceByte37Hex"] = "0x" + moby.SourceByte37.ToString("X2");
                    target["sourceByte4FHex"] = "0x" + moby.SourceByte4F.ToString("X2");
                }
                target["confidence"] = "user override";
                target["evidence"] = "Manual editor identity override saved from the right-click menu. Runtime type/state unchanged.";
                target["color"] = ColorToHtml(identityColor);
                target["updatedAt"] = DateTime.UtcNow.ToString("o");
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["generatedAt"] = DateTime.UtcNow.ToString("o");
            root["editor"] = "NativeSpyroEditor";
            root["levelKey"] = currentLevelKey;
            root["note"] = "Manual editor-only moby identity overrides. These change labels, categories, and map colors in the editor, not game runtime behavior.";
            root["mobys"] = entries.ToArray();

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            File.WriteAllText(path, serializer.Serialize(root), Encoding.UTF8);
        }

        private static List<Dictionary<string, object>> LoadMobyIdentityOverrideEntries(string path)
        {
            List<Dictionary<string, object>> entries = new List<Dictionary<string, object>>();
            if (!File.Exists(path)) return entries;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return entries;

            object[] rawEntries = GeometryLoader.GetArray(root, "mobys");
            foreach (object obj in rawEntries)
            {
                Dictionary<string, object> entry = obj as Dictionary<string, object>;
                if (entry != null)
                    entries.Add(new Dictionary<string, object>(entry));
            }
            return entries;
        }

        private static int GetDictionaryInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        private static Color ColorForIdentityText(string label, string kind, Color fallback)
        {
            string text = ((label ?? "") + " " + (kind ?? "")).ToLowerInvariant();
            if (HasAny(text, "purple gem", "25-gem", "gem (25)")) return Color.FromArgb(155, 89, 182);
            if (HasAny(text, "yellow gem", "10-gem", "gem (10)")) return Color.FromArgb(241, 196, 15);
            if (HasAny(text, "blue gem", "5-gem", "gem (5)")) return Color.FromArgb(52, 152, 219);
            if (HasAny(text, "green gem", "2-gem", "gem (2)")) return Color.FromArgb(46, 204, 113);
            if (HasAny(text, "red gem", "1-gem", "gem (1)")) return Color.FromArgb(231, 76, 60);
            if (HasAny(text, "gem", "treasure", "collectible")) return Color.FromArgb(241, 196, 15);
            if (HasAny(text, "dragon", "pedestal", "fairy")) return Color.FromArgb(166, 126, 255);
            if (HasAny(text, "portal", "return home", "balloonist", "transport npc")) return Color.FromArgb(62, 191, 196);
            if (HasAny(text, "camera", "view point", "viewpoint")) return Color.FromArgb(109, 169, 232);
            if (HasAny(text, "chest", "container", "box")) return Color.FromArgb(230, 126, 34);
            if (HasAny(text, "enemy", "gnorc", "fodder", "sheep", "shepherd", "shepard", "ram", "thief")) return Color.FromArgb(230, 91, 67);
            if (HasAny(text, "scenery", "grass", "flower", "tree", "lamp", "flag")) return Color.FromArgb(91, 176, 92);
            if (HasAny(text, "nonvisual", "invisible", "control", "helper")) return Color.FromArgb(150, 162, 166);
            return fallback.IsEmpty ? Color.FromArgb(255, 230, 80) : fallback;
        }

        private static string ColorToHtml(Color color)
        {
            if (color.IsEmpty)
                color = Color.FromArgb(255, 230, 80);
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        private void ShowTerrainContextMenu(int terrainIndex, Point location)
        {
            if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[terrainIndex];
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem undoItem = new ToolStripMenuItem(polygon.IsTerrainEdited ? "Undo terrain face edits" : "No terrain edit to undo");
            undoItem.Enabled = polygon.IsTerrainEdited;
            undoItem.Click += delegate { UndoTerrainFaceEdit(terrainIndex); };
            menu.Items.Add(undoItem);
            ToolStripMenuItem raiseItem = new ToolStripMenuItem("Raise Face Z +" + NudgeStep.ToString("0.##"));
            raiseItem.Click += delegate { selectedTerrainIndex = terrainIndex; NudgeSelectedTerrainFace(NudgeStep); };
            menu.Items.Add(raiseItem);
            ToolStripMenuItem lowerItem = new ToolStripMenuItem("Lower Face Z -" + NudgeStep.ToString("0.##"));
            lowerItem.Click += delegate { selectedTerrainIndex = terrainIndex; NudgeSelectedTerrainFace(-NudgeStep); };
            menu.Items.Add(lowerItem);
            menu.Items.Add(new ToolStripSeparator());

            int textureId = polygon.TextureId;
            string current = TerrainMaterialOverrideFor(polygon);
            ToolStripMenuItem title = new ToolStripMenuItem(textureId >= 0
                ? "Texture " + textureId.ToString() + " material label"
                : "No texture id");
            title.Enabled = false;
            menu.Items.Add(title);
            if (textureId >= 0)
            {
                ToolStripMenuItem copyTextureItem = new ToolStripMenuItem("Copy Texture ID " + textureId.ToString());
                copyTextureItem.Click += delegate { CopyTerrainTextureId(textureId); };
                menu.Items.Add(copyTextureItem);

                ToolStripMenuItem pasteTextureItem = new ToolStripMenuItem(copiedTerrainTextureId >= 0
                    ? "Paste Texture ID " + copiedTerrainTextureId.ToString() + " to This Face"
                    : "Paste Texture ID to This Face");
                pasteTextureItem.Enabled = copiedTerrainTextureId >= 0;
                pasteTextureItem.Click += delegate { SetTerrainFaceTexture(terrainIndex, copiedTerrainTextureId); };
                menu.Items.Add(pasteTextureItem);

                ToolStripMenuItem pasteAllTextureItem = new ToolStripMenuItem(copiedTerrainTextureId >= 0
                    ? "Replace All Original Texture " + polygon.OriginalTextureId.ToString() + " With " + copiedTerrainTextureId.ToString()
                    : "Replace All Matching Original Texture");
                pasteAllTextureItem.Enabled = copiedTerrainTextureId >= 0;
                pasteAllTextureItem.Click += delegate { ReplaceTerrainTextureFamily(polygon.OriginalTextureId, copiedTerrainTextureId); };
                menu.Items.Add(pasteAllTextureItem);

                ToolStripMenuItem undoTextureItem = new ToolStripMenuItem(polygon.HasTextureEdit
                    ? "Undo Texture ID Override"
                    : "No texture override to undo");
                undoTextureItem.Enabled = polygon.HasTextureEdit;
                undoTextureItem.Click += delegate { ResetTerrainFaceTexture(terrainIndex); };
                menu.Items.Add(undoTextureItem);
                menu.Items.Add(new ToolStripSeparator());

                AddTerrainMaterialMenuItem(menu, textureId, "Grass", "grass", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Water", "water", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Sand / Beach", "sand", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Dry Dirt", "dirt", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Stone / Building", "stone", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Cliff / Wall", "cliff", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Known Unknown", "unknown", current);
                ToolStripMenuItem clearItem = new ToolStripMenuItem("Clear Manual Label");
                clearItem.Enabled = !string.IsNullOrEmpty(current);
                clearItem.Click += delegate { SetTerrainMaterialOverride(textureId, ""); };
                menu.Items.Add(clearItem);
            }
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem copyHandleItem = new ToolStripMenuItem("Copy Runtime Face Handle");
            copyHandleItem.Click += delegate
            {
                string handle = "sector=" + polygon.SectorIndex.ToString()
                    + " face=" + polygon.FaceIndex.ToString()
                    + " detail=" + (string.IsNullOrEmpty(polygon.Detail) ? "?" : polygon.Detail)
                    + " sectorOffset=" + (string.IsNullOrEmpty(polygon.SectorOffset) ? "?" : polygon.SectorOffset)
                    + " faceOffset=" + (string.IsNullOrEmpty(polygon.FaceOffset) ? "?" : polygon.FaceOffset)
                    + " textureId=" + polygon.TextureId.ToString()
                    + " word3=" + polygon.Word3
                    + " word4=" + polygon.Word4;
                Clipboard.SetText(handle);
                statusLabel.Text = "Copied terrain runtime face handle.";
            };
            menu.Items.Add(copyHandleItem);
            menu.Show(canvas, location);
        }

        private void CopyTerrainTextureId(int textureId)
        {
            if (textureId < 0) return;
            copiedTerrainTextureId = textureId;
            statusLabel.Text = "Copied terrain texture id " + textureId.ToString() + ". Right-click another terrain face to paste it.";
            UpdateInspector();
        }

        private void SetTerrainFaceTexture(int terrainIndex, int textureId)
        {
            if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count || textureId < 0) return;
            TerrainPolygon polygon = geometry.Polygons[terrainIndex];
            polygon.ApplyTextureOverride(textureId);
            hasUnsavedEdits = true;
            selectedTerrainIndex = terrainIndex;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Set terrain face " + terrainIndex.ToString() + " texture preview to " + textureId.ToString() + ". Save Edits to keep this texture override.";
        }

        private void ResetTerrainFaceTexture(int terrainIndex)
        {
            if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[terrainIndex];
            polygon.ResetTextureEdit();
            hasUnsavedEdits = true;
            selectedTerrainIndex = terrainIndex;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Reset terrain face " + terrainIndex.ToString() + " texture preview to original texture " + polygon.OriginalTextureId.ToString() + ".";
        }

        private void ReplaceTerrainTextureFamily(int originalTextureId, int newTextureId)
        {
            if (geometry == null || originalTextureId < 0 || newTextureId < 0) return;
            int changed = 0;
            foreach (TerrainPolygon candidate in geometry.Polygons)
            {
                if (candidate == null || candidate.OriginalTextureId != originalTextureId) continue;
                candidate.ApplyTextureOverride(newTextureId);
                changed++;
            }
            if (changed <= 0) return;
            hasUnsavedEdits = true;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Set " + changed.ToString() + " terrain face texture preview(s) from original " + originalTextureId.ToString() + " to " + newTextureId.ToString() + ". Save Edits to keep the batch.";
        }

        private void ApplyDarkHollowTerrainPaletteMatch()
        {
            if (!HasLoadedLevel() || geometry == null)
            {
                MessageBox.Show(this, "Load Stone Hill first.", "No terrain loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!IsStoneHillLevel())
            {
                MessageBox.Show(this, "Dark Hollow terrain matching is currently wired to Stone Hill because Create Terrain BIN exports Stone Hill texture-id swaps.", "Stone Hill only", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LevelDefinition donorLevel = FindLevelDefinition("darkhollow");
            if (donorLevel == null)
            {
                MessageBox.Show(this, "Dark Hollow is not in the level catalog.", "Missing donor level", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string overlayPath = GetLevelGeometryPath(donorLevel.Key);
            if (!File.Exists(overlayPath))
            {
                MessageBox.Show(this, MissingLevelAssetMessage(donorLevel.DisplayName, "geometry overlay"), "Missing Dark Hollow capture", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "Apply a full Stone Hill terrain texture-id remap matched to Dark Hollow captured terrain colors?\n\nThis stays exportable by choosing Stone Hill texture slots only. Save Edits, then Create Terrain BIN to test it in game.",
                "Apply Dark Hollow terrain match",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                GeometryCandidate donorGeometry = GeometryLoader.LoadFirstCandidate(overlayPath);
                Dictionary<int, TerrainTextureChoice> currentByTexture = BuildTerrainTextureChoiceMap(currentLevelKey, currentLevelName, geometry);
                Dictionary<int, TerrainTextureChoice> donorByTexture = BuildTerrainTextureChoiceMap(donorLevel.Key, donorLevel.DisplayName, donorGeometry);
                if (currentByTexture.Count == 0 || donorByTexture.Count == 0)
                {
                    MessageBox.Show(this, "The terrain overlays do not expose texture IDs to match.", "No texture IDs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                List<TerrainTextureChoice> currentChoices = new List<TerrainTextureChoice>(currentByTexture.Values);
                List<TerrainTextureChoice> donorChoices = new List<TerrainTextureChoice>(donorByTexture.Values);
                Dictionary<int, int> remap = new Dictionary<int, int>();
                int changedFamilies = 0;
                foreach (KeyValuePair<int, TerrainTextureChoice> pair in currentByTexture)
                {
                    TerrainTextureChoice nearestDonor = FindNearestTerrainTextureChoice(pair.Value.SampleColor, donorChoices);
                    TerrainTextureChoice nearestCurrent = nearestDonor == null ? null : FindNearestTerrainTextureChoice(nearestDonor.SampleColor, currentChoices);
                    if (nearestCurrent == null)
                        continue;

                    remap[pair.Key] = nearestCurrent.TextureId;
                    if (nearestCurrent.TextureId != pair.Key)
                        changedFamilies++;
                }

                int changedFaces = 0;
                foreach (TerrainPolygon polygon in geometry.Polygons)
                {
                    if (polygon == null || polygon.OriginalTextureId < 0) continue;
                    int newTextureId;
                    if (!remap.TryGetValue(polygon.OriginalTextureId, out newTextureId))
                        continue;
                    if (polygon.TextureId != newTextureId)
                        changedFaces++;
                    polygon.ApplyTextureOverride(newTextureId);
                }

                SelectTerrainDonorLevel(donorLevel.Key);
                RefreshTerrainTextureLibrary();
                UpdateInspector();
                canvas.Invalidate();

                if (changedFaces > 0)
                {
                    hasUnsavedEdits = true;
                    statusLabel.Text = "Applied Dark Hollow terrain match: " + changedFaces.ToString() + " Stone Hill face(s) across " + changedFamilies.ToString() + " texture family swap(s). Save Edits, then Create Terrain BIN.";
                }
                else
                {
                    statusLabel.Text = "Dark Hollow terrain match found no better same-level Stone Hill texture swaps. Cross-level texture import is the next step.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not apply Dark Hollow terrain match", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static TerrainTextureChoice FindNearestTerrainTextureChoice(Color color, List<TerrainTextureChoice> choices)
        {
            if (choices == null || choices.Count == 0)
                return null;

            TerrainTextureChoice best = null;
            int bestScore = int.MaxValue;
            foreach (TerrainTextureChoice choice in choices)
            {
                if (choice == null) continue;
                int score = ColorDistanceSquared(color, choice.SampleColor);
                if (best == null || score < bestScore)
                {
                    best = choice;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int ColorDistanceSquared(Color a, Color b)
        {
            int dr = (int)a.R - (int)b.R;
            int dg = (int)a.G - (int)b.G;
            int db = (int)a.B - (int)b.B;
            return (dr * dr) + (dg * dg) + (db * db);
        }

        private void UndoTerrainFaceEdit(int terrainIndex)
        {
            if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[terrainIndex];
            polygon.ResetTerrainEdit();
            hasUnsavedEdits = true;
            selectedTerrainIndex = terrainIndex;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Undid terrain face " + terrainIndex.ToString() + " edits. Save Edits to keep the reset.";
        }

        private void AddTerrainMaterialMenuItem(ContextMenuStrip menu, int textureId, string label, string surface, string current)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Checked = string.Equals(current, surface, StringComparison.OrdinalIgnoreCase);
            item.Click += delegate { SetTerrainMaterialOverride(textureId, surface); };
            menu.Items.Add(item);
        }

        private void UndoSingleMobyEdit(int mobyIndex)
        {
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            Moby moby = mobys[mobyIndex];
            if (!moby.IsEdited)
            {
                statusLabel.Text = MobyId(moby) + " has no active edit to undo.";
                return;
            }

            if (moby.IsAppendedRecord)
            {
                RemoveAppendedMoby(mobyIndex, "Removed new object " + MobyId(moby) + ". Save Edits to keep it removed.");
                return;
            }

            moby.ResetToOriginal();
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyList();
            SelectMoby(mobyIndex);
            statusLabel.Text = "Undid edit for " + MobyId(moby) + ". Save Edits to keep the reset.";
        }

        internal void CanvasMouseDown(MouseEventArgs e)
        {
            canvas.Focus();
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                rightContextPending = e.Button == MouseButtons.Right;
                rightDragActive = false;
                rightMouseDownPoint = e.Location;
                if (view3D && (ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    rotating3D = true;
                    lastMouse = e.Location;
                    canvas.Cursor = Cursors.Cross;
                    StartFastMode(300);
                    return;
                }
                panning = true;
                lastMouse = e.Location;
                canvas.Cursor = Cursors.SizeAll;
                StartFastMode(300);
                return;
            }

            if (e.Button != MouseButtons.Left) return;
            if (editorMode == EditorMode.Terrain)
            {
                SelectMoby(-1);
                if (view3D) SelectTerrainAt3D(e.Location);
                else SelectTerrainAt(e.Location);
                return;
            }

            int hit = HitMoby(e.Location);
            if (hit >= 0)
            {
                SelectMoby(hit);
                if (DragLockEnabled)
                {
                    statusLabel.Text = "Selected " + MobyId(mobys[hit]) + " with drag lock on.";
                    canvas.Invalidate();
                    return;
                }
                dragMobyIndex = hit;
                dragStartedIn3D = view3D;
                dragPlaneZ = mobys[hit].Z;
                PointF world = dragStartedIn3D ? ScreenToWorldAtZ(e.X, e.Y, dragPlaneZ) : ScreenToWorld(e.X, e.Y);
                dragOffset = new PointF(mobys[hit].X - world.X, mobys[hit].Y - world.Y);
                statusLabel.Text = "Dragging " + MobyId(mobys[hit]) + " " + mobys[hit].DisplayLabel + (dragStartedIn3D ? " in 3D view" : "") + " at X " + mobys[hit].X.ToString("0.0") + ", Y " + mobys[hit].Y.ToString("0.0");
                canvas.Invalidate();
            }
            else if (ClickPlaceEnabled && selectedMobyIndex >= 0)
            {
                PointF world = ScreenToWorld(e.X, e.Y);
                MoveSelectedToWorldPoint(world.X, world.Y);
                UpdateTerrainHover(e.Location);
                canvas.Invalidate();
            }
            else
            {
                SelectMoby(-1);
                UpdateTerrainHover(e.Location);
                statusLabel.Text = "No moby selected.";
                canvas.Invalidate();
            }
        }

        internal void CanvasMouseMove(MouseEventArgs e)
        {
            if (rotating3D)
            {
                if (rightContextPending && !RightMouseDragExceeded(e.Location))
                    return;
                rightContextPending = false;
                rightDragActive = true;
                StartFastMode(300);
                view3DYaw += (e.X - lastMouse.X) * 0.008f;
                view3DPitch = Clamp(view3DPitch + ((e.Y - lastMouse.Y) * 0.0035f), 0.25f, 0.95f);
                lastMouse = e.Location;
                canvas.Invalidate();
                statusLabel.Text = "3D view yaw " + (view3DYaw * 180.0 / Math.PI).ToString("0") + " pitch " + view3DPitch.ToString("0.00") + ".";
                return;
            }
            if (panning)
            {
                if (rightContextPending && !RightMouseDragExceeded(e.Location))
                    return;
                rightContextPending = false;
                rightDragActive = true;
                StartFastMode(300);
                pan = new PointF(pan.X + (e.X - lastMouse.X), pan.Y + (e.Y - lastMouse.Y));
                lastMouse = e.Location;
                canvas.Invalidate();
                return;
            }

            if (dragMobyIndex >= 0)
            {
                PointF world = dragStartedIn3D ? ScreenToWorldAtZ(e.X, e.Y, dragPlaneZ) : ScreenToWorld(e.X, e.Y);
                SetMobyPosition(dragMobyIndex, world.X + dragOffset.X, world.Y + dragOffset.Y, mobys[dragMobyIndex].Z, false, true, true);
                return;
            }

            if (editorMode == EditorMode.Terrain || (ClickPlaceEnabled && selectedMobyIndex >= 0))
                UpdateTerrainHover(e.Location);
        }

        internal void CanvasMouseUp(MouseEventArgs e)
        {
            bool showContextMenu = e.Button == MouseButtons.Right && rightContextPending && !rightDragActive && !RightMouseDragExceeded(e.Location);
            panning = false;
            rotating3D = false;
            rightContextPending = false;
            rightDragActive = false;
            if (dragMobyIndex >= 0)
            {
                foreach (int targetIndex in GetLinkedMoveIndexes(dragMobyIndex))
                    RefreshMobyListRow(targetIndex);
            }
            dragMobyIndex = -1;
            dragStartedIn3D = false;
            canvas.Cursor = Cursors.Default;
            if (showContextMenu)
                TryShowCanvasContextMenu(e.Location);
            canvas.Invalidate();
        }

        internal void CanvasMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || editorMode != EditorMode.Mobys)
                return;

            int hit = HitMoby(e.Location);
            if (hit < 0) return;
            SelectMoby(hit);
            dragMobyIndex = -1;
            if (CanOpenChestContentEditor(hit))
            {
                ShowChestContentsEditor();
                canvas.Invalidate();
            }
        }

        private bool RightMouseDragExceeded(Point location)
        {
            int dx = location.X - rightMouseDownPoint.X;
            int dy = location.Y - rightMouseDownPoint.Y;
            return (dx * dx) + (dy * dy) > 36;
        }

        internal void CanvasMouseWheel(MouseEventArgs e)
        {
            if (geometry == null) return;
            StartFastMode(260);
            float oldZoom = Math.Max(0.0001f, zoom);
            PointF beforeView = new PointF((e.X - pan.X) / oldZoom, (e.Y - pan.Y) / oldZoom);
            float factor = e.Delta > 0 ? 1.18f : 1f / 1.18f;
            zoom = Clamp(zoom * factor, 0.006f, 3.5f);
            pan = new PointF(e.X - (beforeView.X * zoom), e.Y - (beforeView.Y * zoom));
            canvas.Invalidate();
        }

        private int HitMoby(Point screen)
        {
            int best = -1;
            int bestPriority = int.MinValue;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < mobys.Count; i++)
            {
                PointF s = WorldToScreen(mobys[i].X, mobys[i].Y, mobys[i].Z);
                float dx = screen.X - s.X;
                float dy = screen.Y - s.Y;
                float distance = (dx * dx) + (dy * dy);
                if (distance > 144f) continue;

                int priority = MobyLayerPriority(i);
                if (priority > bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    best = i;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void SelectTerrainAt(Point screen)
        {
            if (geometry == null) return;
            PointF world = ScreenToWorld(screen.X, screen.Y);
            selectedTerrainPoint = world;
            selectedTerrainIndex = HitTerrainPolygon(world);
            UpdateInspector();
            canvas.Invalidate();
            if (selectedTerrainIndex >= 0)
            {
                TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
                float z;
                bool hasZ = polygon.TryGetZ(world.X, world.Y, out z);
                statusLabel.Text = "Selected terrain face " + selectedTerrainIndex.ToString() + " at X " + world.X.ToString("0.0") + ", Y " + world.Y.ToString("0.0") + (hasZ ? ", Z " + z.ToString("0.0") : "") + ".";
            }
            else
            {
                statusLabel.Text = "No terrain face under cursor.";
            }
        }

        private void SelectTerrainAt3D(Point screen)
        {
            if (geometry == null) return;
            selectedTerrainIndex = HitTerrainPolygon3D(screen);
            if (selectedTerrainIndex >= 0)
            {
                TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
                selectedTerrainPoint = new PointF(polygon.CenterX, polygon.CenterY);
            }
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = selectedTerrainIndex >= 0
                ? "Selected terrain face " + selectedTerrainIndex.ToString() + " in 3D view."
                : "No terrain face under cursor.";
        }

        private void UpdateTerrainHover(Point screen)
        {
            if (geometry == null)
            {
                hoverTerrainIndex = -1;
                hasHoverTerrainZ = false;
                return;
            }

            if (view3D)
            {
                hoverTerrainIndex = HitTerrainPolygon3D(screen);
                hasHoverTerrainZ = false;
                if (hoverTerrainIndex >= 0)
                {
                    TerrainPolygon polygon = geometry.Polygons[hoverTerrainIndex];
                    hoverTerrainPoint = new PointF(polygon.CenterX, polygon.CenterY);
                    hoverTerrainZ = polygon.AvgZ;
                    hasHoverTerrainZ = true;
                    if (editorMode == EditorMode.Terrain)
                        statusLabel.Text = "Terrain hover: face " + hoverTerrainIndex.ToString() + ", avg Z " + hoverTerrainZ.ToString("0.0") + ".";
                }
                canvas.Invalidate();
                return;
            }

            PointF world = ScreenToWorld(screen.X, screen.Y);
            hoverTerrainPoint = world;
            hoverTerrainIndex = HitTerrainPolygon(world);
            hasHoverTerrainZ = false;
            if (hoverTerrainIndex >= 0)
            {
                TerrainPolygon polygon = geometry.Polygons[hoverTerrainIndex];
                hasHoverTerrainZ = polygon.TryGetZ(world.X, world.Y, out hoverTerrainZ);
                if (editorMode == EditorMode.Terrain && hasHoverTerrainZ)
                    statusLabel.Text = "Terrain hover: X " + world.X.ToString("0.0") + ", Y " + world.Y.ToString("0.0") + ", Z " + hoverTerrainZ.ToString("0.0") + ".";
            }
            canvas.Invalidate();
        }

        private int HitTerrainPolygon(PointF world)
        {
            if (geometry == null) return -1;
            int best = -1;
            float bestZ = float.MinValue;
            for (int i = 0; i < geometry.Polygons.Count; i++)
            {
                TerrainPolygon polygon = geometry.Polygons[i];
                if (!polygon.Bounds.Contains(world) || !polygon.ContainsXY(world.X, world.Y))
                    continue;
                if (polygon.AvgZ >= bestZ)
                {
                    best = i;
                    bestZ = polygon.AvgZ;
                }
            }
            return best;
        }

        private int HitTerrainPolygon3D(Point screen)
        {
            if (geometry == null) return -1;
            int best = -1;
            float bestDepth = float.MinValue;
            PointF test = new PointF(screen.X, screen.Y);
            for (int i = 0; i < geometry.Polygons.Count; i++)
            {
                TerrainPolygon polygon = geometry.Polygons[i];
                PointF[] pts = new PointF[polygon.Points.Length];
                for (int p = 0; p < polygon.Points.Length; p++)
                    pts[p] = WorldToScreen(polygon.Points[p].X, polygon.Points[p].Y, polygon.ZValues[p]);
                if (!ScreenPointInPolygon(test, pts)) continue;
                float depth = WorldToView3D(polygon.CenterX, polygon.CenterY, polygon.AvgZ).Y;
                if (depth >= bestDepth)
                {
                    bestDepth = depth;
                    best = i;
                }
            }
            return best;
        }

        private static bool ScreenPointInPolygon(PointF point, PointF[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return false;
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; i++)
            {
                float yi = polygon[i].Y;
                float yj = polygon[j].Y;
                if (((yi > point.Y) != (yj > point.Y)) &&
                    (point.X < ((polygon[j].X - polygon[i].X) * (point.Y - yi) / ((yj - yi) == 0f ? 0.0001f : (yj - yi))) + polygon[i].X))
                    inside = !inside;
                j = i;
            }
            return inside;
        }

        private sealed class CanvasView : Control
        {
            private readonly EditorForm owner;

            public CanvasView(EditorForm owner)
            {
                this.owner = owner;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                TabStop = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                owner.Draw(e.Graphics, ClientSize);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                owner.CanvasMouseDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                owner.CanvasMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                owner.CanvasMouseUp(e);
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                owner.CanvasMouseDoubleClick(e);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                owner.CanvasMouseWheel(e);
            }

            protected override bool IsInputKey(Keys keyData)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.PageUp || key == Keys.PageDown)
                    return true;
                return base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                owner.CanvasKeyDown(e);
                if (!e.Handled)
                    base.OnKeyDown(e);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                Focus();
            }
        }
    }

    internal static class GeometryLoader
    {
        public static GeometryCandidate LoadFirstCandidate(string path)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("candidates"))
                throw new InvalidDataException("Geometry overlay has no candidates.");
            object[] candidates = root["candidates"] as object[];
            if (candidates == null || candidates.Length == 0)
                throw new InvalidDataException("Geometry overlay has no candidates.");
            Dictionary<string, object> c = candidates[0] as Dictionary<string, object>;
            if (c == null)
                throw new InvalidDataException("First geometry candidate is invalid.");

            GeometryCandidate result = new GeometryCandidate();
            result.Name = GetString(c, "runtimeAddress", "geometry");
            result.Bounds = ReadBounds(c.ContainsKey("projectedBounds") ? c["projectedBounds"] as Dictionary<string, object> : null);

            object[] polyObjects = GetArray(c, "polygons");
            double minZ = 0.0;
            double maxZ = 1.0;
            Dictionary<string, object> heightBounds = c.ContainsKey("heightBounds") ? c["heightBounds"] as Dictionary<string, object> : null;
            if (heightBounds != null)
            {
                minZ = GetDouble(heightBounds, "minZ", 0);
                maxZ = GetDouble(heightBounds, "maxZ", 1);
            }
            result.MinZ = (float)minZ;
            result.MaxZ = (float)maxZ;

            foreach (object obj in polyObjects)
            {
                Dictionary<string, object> p = obj as Dictionary<string, object>;
                if (p == null) continue;
                object[] points = GetArray(p, "points");
                if (points.Length < 3) continue;
                PointF[] pts = new PointF[points.Length];
                float[] zs = new float[points.Length];
                double sumZ = 0.0;
                double polyMinZ = double.MaxValue;
                double polyMaxZ = double.MinValue;
                for (int i = 0; i < points.Length; i++)
                {
                    Dictionary<string, object> point = points[i] as Dictionary<string, object>;
                    pts[i] = new PointF((float)GetDouble(point, "x", 0), (float)GetDouble(point, "y", 0));
                    zs[i] = (float)GetDouble(point, "z", 0);
                    sumZ += zs[i];
                    polyMinZ = Math.Min(polyMinZ, zs[i]);
                    polyMaxZ = Math.Max(polyMaxZ, zs[i]);
                }
                double avgZ = GetDouble(p, "avgZ", sumZ / Math.Max(1, points.Length));
                int textureId = GetInt(p, "textureId", -1);
                bool faceFlip = GetBool(p, "flip", false);
                int faceDepth = GetInt(p, "depth", -1);
                int sectorIndex = GetInt(p, "sectorIndex", -1);
                int faceIndex = GetInt(p, "faceIndex", -1);
                string sectorOffset = GetString(p, "sectorOffset", "");
                string faceOffset = GetString(p, "faceOffset", "");
                string detail = GetString(p, "detail", "");
                string word3 = GetString(p, "word3", "");
                string word4 = GetString(p, "word4", "");
                Color faceColor;
                bool hasFaceColor = TryReadColor(p, "faceColor", out faceColor);
                Dictionary<string, object> textureCorners = GetDictionary(p, "textureCorners");
                TerrainPoint3 textureTopLeft = ReadPoint3(GetDictionary(textureCorners, "topLeft"));
                TerrainPoint3 textureTopRight = ReadPoint3(GetDictionary(textureCorners, "topRight"));
                TerrainPoint3 textureBottomLeft = ReadPoint3(GetDictionary(textureCorners, "bottomLeft"));
                result.Polygons.Add(new TerrainPolygon(
                    pts,
                    zs,
                    (float)avgZ,
                    HeightColor(avgZ, minZ, maxZ),
                    StoneHillTerrainColor(avgZ, minZ, maxZ, polyMaxZ - polyMinZ),
                    textureId,
                    StoneHillMaterialColor(textureId),
                    faceFlip,
                    faceDepth,
                    word3,
                    word4,
                    ReadIntArray(p, "vertexIndexes"),
                    ReadIntArray(p, "colourIndexes"),
                    hasFaceColor,
                    faceColor,
                    textureTopLeft,
                    textureTopRight,
                    textureBottomLeft,
                    sectorIndex,
                    faceIndex,
                    sectorOffset,
                    faceOffset,
                    detail));
            }

            object[] edgeObjects = GetArray(c, "edges");
            foreach (object obj in edgeObjects)
            {
                Dictionary<string, object> edge = obj as Dictionary<string, object>;
                if (edge == null) continue;
                result.Edges.Add(new TerrainEdge(
                    (float)GetDouble(edge, "x1", 0),
                    (float)GetDouble(edge, "y1", 0),
                    (float)GetDouble(edge, "x2", 0),
                    (float)GetDouble(edge, "y2", 0)));
            }

            if (result.Bounds.Width <= 1 || result.Bounds.Height <= 1)
                result.Bounds = ComputeBounds(result.Polygons, result.Edges);
            return result;
        }

        private static RectangleF ReadBounds(Dictionary<string, object> b)
        {
            if (b == null) return RectangleF.Empty;
            float minX = (float)GetDouble(b, "minX", 0);
            float maxX = (float)GetDouble(b, "maxX", 0);
            float minY = (float)GetDouble(b, "minY", 0);
            float maxY = (float)GetDouble(b, "maxY", 0);
            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        private static RectangleF ComputeBounds(List<TerrainPolygon> polygons, List<TerrainEdge> edges)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (TerrainPolygon p in polygons)
            {
                minX = Math.Min(minX, p.Bounds.Left);
                minY = Math.Min(minY, p.Bounds.Top);
                maxX = Math.Max(maxX, p.Bounds.Right);
                maxY = Math.Max(maxY, p.Bounds.Bottom);
            }
            foreach (TerrainEdge e in edges)
            {
                minX = Math.Min(minX, e.Bounds.Left);
                minY = Math.Min(minY, e.Bounds.Top);
                maxX = Math.Max(maxX, e.Bounds.Right);
                maxY = Math.Max(maxY, e.Bounds.Bottom);
            }
            if (minX == float.MaxValue) return new RectangleF(0, 0, 2400, 1600);
            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        internal static Color HeightColor(double value, double min, double max)
        {
            double t = max <= min ? 0.5 : (value - min) / (max - min);
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            int r, g, b;
            if (t < 0.5)
            {
                double local = t / 0.5;
                r = Lerp(44, 52, local);
                g = Lerp(124, 158, local);
                b = Lerp(88, 158, local);
            }
            else
            {
                double local = (t - 0.5) / 0.5;
                r = Lerp(52, 190, local);
                g = Lerp(158, 172, local);
                b = Lerp(158, 112, local);
            }
            return Color.FromArgb(92, r, g, b);
        }

        private static Color StoneHillTerrainColor(double value, double min, double max, double zSpread)
        {
            double t = max <= min ? 0.5 : (value - min) / (max - min);
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            if (zSpread > 280)
            {
                int wall = Lerp(106, 154, t);
                return Color.FromArgb(180, wall, wall + 4, Math.Min(185, wall + 18));
            }
            if (value < 850)
                return Color.FromArgb(174, 52, 100, 126);
            if (value < 1120)
                return Color.FromArgb(176, 118, 122, 132);
            if (value < 1650)
            {
                int r = Lerp(68, 100, t);
                int g = Lerp(126, 174, t);
                int b = Lerp(68, 86, t);
                return Color.FromArgb(182, r, g, b);
            }
            return Color.FromArgb(178, Lerp(112, 154, t), Lerp(144, 170, t), Lerp(72, 90, t));
        }

        private static Color StoneHillMaterialColor(int textureId)
        {
            if (textureId < 0)
                return Color.FromArgb(176, 118, 122, 132);

            int[,] palette = new int[,]
            {
                { 83, 164, 96 }, { 202, 180, 82 }, { 96, 150, 196 }, { 180, 128, 198 },
                { 210, 112, 86 }, { 104, 184, 170 }, { 195, 146, 72 }, { 142, 166, 96 },
                { 170, 112, 154 }, { 92, 138, 118 }, { 208, 96, 118 }, { 112, 178, 210 },
                { 156, 138, 82 }, { 116, 158, 132 }, { 188, 132, 104 }, { 126, 146, 188 }
            };
            int index = textureId % palette.GetLength(0);
            int band = textureId / palette.GetLength(0);
            double factor = 0.88 + ((band % 4) * 0.08);
            return Color.FromArgb(
                184,
                ClampByte(palette[index, 0] * factor),
                ClampByte(palette[index, 1] * factor),
                ClampByte(palette[index, 2] * factor));
        }

        private static int ClampByte(double value)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private static int Lerp(int a, int b, double t)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(a + ((b - a) * t))));
        }

        internal static object[] GetArray(Dictionary<string, object> dict, string name)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return new object[0];
            object[] arr = dict[name] as object[];
            return arr ?? new object[0];
        }

        internal static Dictionary<string, object> GetDictionary(Dictionary<string, object> dict, string name)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return null;
            return dict[name] as Dictionary<string, object>;
        }

        internal static int[] ReadIntArray(Dictionary<string, object> dict, string name)
        {
            object[] raw = GetArray(dict, name);
            int[] values = new int[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                try { values[i] = Convert.ToInt32(raw[i]); }
                catch { values[i] = -1; }
            }
            return values;
        }

        internal static TerrainPoint3 ReadPoint3(Dictionary<string, object> dict)
        {
            if (dict == null) return null;
            return new TerrainPoint3(
                (float)GetDouble(dict, "x", 0),
                (float)GetDouble(dict, "y", 0),
                (float)GetDouble(dict, "z", 0));
        }

        internal static bool TryReadColor(Dictionary<string, object> dict, string name, out Color color)
        {
            color = Color.Empty;
            Dictionary<string, object> c = GetDictionary(dict, name);
            if (c == null) return false;
            int r = GetInt(c, "r", -1);
            int g = GetInt(c, "g", -1);
            int b = GetInt(c, "b", -1);
            int a = GetInt(c, "a", 255);
            if (r < 0 || g < 0 || b < 0) return false;
            color = Color.FromArgb(
                Math.Max(0, Math.Min(255, a)),
                Math.Max(0, Math.Min(255, r)),
                Math.Max(0, Math.Min(255, g)),
                Math.Max(0, Math.Min(255, b)));
            return true;
        }

        internal static string GetString(Dictionary<string, object> dict, string name, string fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            return Convert.ToString(dict[name]);
        }

        internal static double GetDouble(Dictionary<string, object> dict, string name, double fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToDouble(dict[name]); }
            catch { return fallback; }
        }

        internal static int GetInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        internal static bool GetBool(Dictionary<string, object> dict, string name, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToBoolean(dict[name]); }
            catch { return fallback; }
        }
    }

    internal static class MobyLoader
    {
        public static List<Moby> Load(string ramPath)
        {
            byte[] ram = File.ReadAllBytes(ramPath);
            List<Moby> result = new List<Moby>();
            if (ram.Length < 0x7582C) return result;
            uint pointer = BitConverter.ToUInt32(ram, 0x75828);
            uint dynamicPointer = BitConverter.ToUInt32(ram, 0x7573C);
            int start = (int)(pointer & 0x001FFFFF);
            int dynamicStart = (int)(dynamicPointer & 0x001FFFFF);
            if (start < 0 || start + 0x58 > ram.Length) return result;

            int count = 512;
            if (dynamicStart > start && dynamicStart <= ram.Length && ((dynamicStart - start) % 0x58) == 0)
                count = Math.Min(512, (dynamicStart - start) / 0x58);

            for (int i = 0; i < count; i++)
            {
                int offset = start + (i * 0x58);
                if (offset + 0x58 > ram.Length) break;
                int rawX = BitConverter.ToInt32(ram, offset + 0x0C);
                int rawY = BitConverter.ToInt32(ram, offset + 0x10);
                int rawZ = BitConverter.ToInt32(ram, offset + 0x14);
                int type = ram[offset + 0x50];
                int state = ram[offset + 0x51];
                uint specialDataPointer = BitConverter.ToUInt32(ram, offset + 0x08);
                float x = rawX / 16f;
                float y = rawY / 16f;
                float z = rawZ / 16f;
                int legacyIndex = GetLegacyAliasIndex(i);
                result.Add(new Moby
                {
                    Index = i,
                    TrueIndex = i,
                    LegacyIndex = legacyIndex,
                    X = x,
                    Y = y,
                    Z = z,
                    OriginalX = x,
                    OriginalY = y,
                    OriginalZ = z,
                    Type = type,
                    State = state,
                    RuntimeAddress = (uint)(0x80000000u + (uint)offset),
                    SpecialDataPointer = specialDataPointer,
                    SourceByte36 = ram[offset + 0x36],
                    SourceByte37 = ram[offset + 0x37],
                    SourceByte4F = ram[offset + 0x4F],
                    Flag4A = ram[offset + 0x52],
                    Flag4B = ram[offset + 0x53],
                    Color = ColorForType(type),
                    Label = FallbackLabel(type),
                    PatchStatus = "loader-table-patchable",
                    PatchLead = LoaderTablePatchLead(i),
                    PatchPriority = 1
                });
            }
            return result;
        }

        public static List<Moby> LoadCached(string cachePath)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(cachePath, Encoding.UTF8)) as Dictionary<string, object>;
            object[] entries = GeometryLoader.GetArray(root, "mobys");
            List<Moby> result = new List<Moby>();

            for (int i = 0; i < entries.Length; i++)
            {
                Dictionary<string, object> entry = entries[i] as Dictionary<string, object>;
                if (entry == null) continue;

                int trueIndex = GeometryLoader.GetInt(entry, "trueIndex", GeometryLoader.GetInt(entry, "index", i));
                int type = FlexibleInt(entry, "typeHex", GeometryLoader.GetInt(entry, "type", 0));
                int state = FlexibleInt(entry, "stateHex", GeometryLoader.GetInt(entry, "state", 0));
                float x = (float)GeometryLoader.GetDouble(entry, "x", 0);
                float y = (float)GeometryLoader.GetDouble(entry, "y", 0);
                float z = (float)GeometryLoader.GetDouble(entry, "z", 0);

                result.Add(new Moby
                {
                    Index = GeometryLoader.GetInt(entry, "index", trueIndex),
                    TrueIndex = trueIndex,
                    LegacyIndex = GeometryLoader.GetInt(entry, "legacyIndex", GetLegacyAliasIndex(trueIndex)),
                    X = x,
                    Y = y,
                    Z = z,
                    OriginalX = x,
                    OriginalY = y,
                    OriginalZ = z,
                    Type = type,
                    State = state,
                    RuntimeAddress = (uint)FlexibleInt64(entry, "runtimeAddress", 0),
                    SpecialDataPointer = (uint)FlexibleInt64(entry, "specialDataPointer", 0),
                    SourceByte36 = FlexibleInt(entry, "sourceByte36Hex", 0),
                    SourceByte37 = FlexibleInt(entry, "sourceByte37Hex", 0),
                    SourceByte4F = FlexibleInt(entry, "sourceByte4FHex", 0),
                    Flag4A = FlexibleInt(entry, "flag4AHex", 0),
                    Flag4B = FlexibleInt(entry, "flag4BHex", 0),
                    Color = ColorForType(type),
                    Label = FallbackLabel(type),
                    PatchStatus = "portable-cache",
                    PatchLead = "Loaded from editor-cache; source patch status is applied after level load.",
                    PatchPriority = 0
                });
            }

            return result;
        }

        private static int FlexibleInt(Dictionary<string, object> entry, string name, int fallback)
        {
            long value = FlexibleInt64(entry, name, fallback);
            if (value < int.MinValue || value > int.MaxValue)
                return fallback;
            return (int)value;
        }

        private static long FlexibleInt64(Dictionary<string, object> entry, string name, long fallback)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null)
                return fallback;

            object raw = entry[name];
            try
            {
                string text = Convert.ToString(raw).Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt64(text.Substring(2), 16);
                return Convert.ToInt64(raw);
            }
            catch
            {
                return fallback;
            }
        }

        internal static int GetLegacyAliasIndex(int trueIndex)
        {
            int numerator = (trueIndex * 0x58) + 8;
            if (numerator >= 0 && (numerator % 0x50) == 0)
                return numerator / 0x50;
            return -1;
        }

        internal static bool TryGetLoaderTableTrueIndex(int legacyIndex, out int trueIndex)
        {
            int numerator = (legacyIndex * 0x50) - 8;
            if (numerator >= 0 && (numerator % 0x58) == 0)
            {
                trueIndex = numerator / 0x58;
                return true;
            }

            trueIndex = -1;
            return false;
        }

        internal static string LoaderTablePatchLead(int trueIndex)
        {
            return "WAD entry 12, true record " + trueIndex.ToString() + ", XYZ +0x0C/+0x10/+0x14";
        }

        private static bool PlausibleMoby(byte[] ram, int offset)
        {
            int type = ram[offset + 0x48];
            int state = ram[offset + 0x49];
            if (type <= 0 || type > 0x7F) return false;
            if (state > 0x7F) return false;
            int x = BitConverter.ToInt32(ram, offset + 4);
            int y = BitConverter.ToInt32(ram, offset + 8);
            int z = BitConverter.ToInt32(ram, offset + 12);
            if (Math.Abs((long)x) > 4000000 || Math.Abs((long)y) > 4000000 || Math.Abs((long)z) > 4000000) return false;
            if (Math.Abs((long)x) < 16 && Math.Abs((long)y) < 16 && Math.Abs((long)z) < 16) return false;
            return true;
        }

        private static Color ColorForType(int type)
        {
            if (type == 0x20) return Color.FromArgb(244, 212, 77);
            if (type == 0x30) return Color.FromArgb(183, 140, 255);
            if (type == 0x18) return Color.FromArgb(255, 140, 90);
            if (type == 0x0A || type == 0x33 || type == 0x52) return Color.FromArgb(143, 166, 184);
            return Color.FromArgb(93, 173, 226);
        }

        internal static string FallbackLabel(int type)
        {
            if (type == 0x20) return "Treasure/gem";
            if (type == 0x30) return "Dragon/NPC?";
            if (type == 0x18) return "Enemy/object?";
            if (type == 0x33) return "Chest/helper?";
            if (type == 0x0A || type == 0x52) return "Helper/system";
            return "0x" + type.ToString("X2");
        }
    }

    internal static class MobyMetadataLoader
    {
        public static int Apply(string workspace, List<Moby> mobys)
        {
            return Apply(workspace, mobys, "stonehill", true);
        }

        public static int Apply(string workspace, List<Moby> mobys, string levelKey, bool includeStoneHillMetadata)
        {
            Dictionary<int, MobyMetadata> metadata = new Dictionary<int, MobyMetadata>();
            Dictionary<string, MobyMetadata> globalSignatureMetadata = BuildGlobalSignatureMetadata(workspace);
            if (includeStoneHillMetadata)
            {
                MergeFile(Path.Combine(workspace, "stonehill-full-moby-inventory.json"), "mobys", metadata, false);
                MergeFile(Path.Combine(workspace, "stonehill-moby-catalog.json"), "mobys", metadata, true);
                MergeFile(Path.Combine(workspace, "stonehill-replica-map-plan.json"), "mobyMarkers", metadata, true);
                MergePatchMatrix(Path.Combine(workspace, "stonehill-patch-test-matrix.json"), metadata);
                MergeSpecialData(Path.Combine(workspace, "stonehill-moby-special-data.json"), metadata);
                MergeFile(Path.Combine(workspace, "stonehill-gem-value-overrides.json"), "mobys", metadata, false);
                MergeFile(Path.Combine(workspace, "stonehill-moby-identity-audit.json"), "records", metadata, false);
                MergeFile(Path.Combine(workspace, "stonehill-live-validation-overrides.json"), "mobys", metadata, true);
            }
            else if (!string.IsNullOrEmpty(levelKey))
            {
                MergeFile(Path.Combine(workspace, levelKey + "-moby-catalog.json"), "mobys", metadata, false);
                MergeFile(Path.Combine(workspace, levelKey + "-moby-identity-audit.json"), "records", metadata, false);
                MergeFile(Path.Combine(workspace, levelKey + "-live-validation-overrides.json"), "mobys", metadata, false);
            }
            if (!string.IsNullOrEmpty(levelKey))
                MergeFile(Path.Combine(workspace, levelKey + "-moby-user-overrides.json"), "mobys", metadata, false);

            int applied = 0;
            foreach (Moby moby in mobys)
            {
                MobyMetadata item = null;
                int lookupIndex = moby.TrueIndex >= 0 ? moby.TrueIndex : moby.Index;
                bool appliedMetadata = false;
                if (metadata.TryGetValue(lookupIndex, out item))
                {
                    if (ApplyLevelMetadataToMoby(moby, item))
                        applied++;
                    appliedMetadata = true;
                }

                MobyMetadata globalItem;
                string signatureKey = GlobalSignatureKey(moby);
                if (!string.IsNullOrEmpty(signatureKey)
                    && globalSignatureMetadata.TryGetValue(signatureKey, out globalItem)
                    && ShouldApplyGlobalSignatureMetadata(item, moby))
                {
                    if (ApplyGlobalMetadataToMoby(moby, globalItem))
                    {
                        applied++;
                        appliedMetadata = true;
                    }
                }

                if (string.IsNullOrEmpty(moby.Label))
                {
                    moby.Label = MobyLoader.FallbackLabel(moby.Type);
                }
                if (moby.ShouldUseSourceGemIdentity(appliedMetadata ? item : null) && moby.ApplySourceGemIdentity())
                {
                    if (!appliedMetadata)
                        applied++;
                }
                if (includeStoneHillMetadata)
                    ApplyLoaderTablePatchMetadata(moby);
            }
            return applied;
        }

        private static bool ApplyLevelMetadataToMoby(Moby moby, MobyMetadata item)
        {
            if (moby == null || item == null) return false;
            bool labeled = false;
            if (!string.IsNullOrEmpty(item.Label))
            {
                moby.Label = item.Label;
                labeled = true;
            }
            moby.Zone = item.Zone;
            moby.Kind = item.Kind;
            moby.Confidence = item.Confidence;
            moby.Evidence = item.Evidence;
            moby.BehaviorNote = item.BehaviorNote;
            moby.SpecialDataNote = item.SpecialDataNote;
            moby.PatchStatus = item.PatchStatus;
            moby.PatchLead = item.PatchLead;
            moby.PatchPriority = item.PatchPriority;
            if (item.HasColor)
                moby.Color = item.Color;
            return labeled;
        }

        private static bool ApplyGlobalMetadataToMoby(Moby moby, MobyMetadata item)
        {
            if (moby == null || item == null || string.IsNullOrEmpty(item.Label)) return false;
            moby.Label = item.Label;
            moby.Kind = item.Kind;
            moby.Confidence = "global signature: " + (string.IsNullOrEmpty(item.Confidence) ? "known moby identity" : item.Confidence);
            string evidence = "Reused known moby identity from matching type/special-data signature across levels.";
            if (!string.IsNullOrEmpty(item.Evidence))
                evidence += " " + item.Evidence;
            moby.Evidence = CleanEvidence(evidence);
            if (item.HasColor)
                moby.Color = item.Color;
            return true;
        }

        private static bool ShouldApplyGlobalSignatureMetadata(MobyMetadata currentMetadata, Moby moby)
        {
            if (moby == null) return false;
            if (currentMetadata == null) return true;
            if (currentMetadata.IsUserOverride) return false;
            return IsWeakIdentityMetadata(currentMetadata, moby);
        }

        private static bool IsWeakIdentityMetadata(MobyMetadata metadata, Moby moby)
        {
            if (metadata == null) return true;
            string label = metadata.Label ?? "";
            string text = (label + " " + (metadata.Kind ?? "") + " " + (metadata.Confidence ?? "")).ToLowerInvariant();
            if (string.IsNullOrEmpty(label)) return true;
            if (moby != null && string.Equals(label, MobyLoader.FallbackLabel(moby.Type), StringComparison.OrdinalIgnoreCase)) return true;
            return text.IndexOf("0x", StringComparison.Ordinal) >= 0
                || text.IndexOf("object?", StringComparison.Ordinal) >= 0
                || text.IndexOf("enemy/object", StringComparison.Ordinal) >= 0
                || text.IndexOf("gem treasure/gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("treasure/gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("unknown", StringComparison.Ordinal) >= 0
                || text.IndexOf("candidate", StringComparison.Ordinal) >= 0;
        }

        private static void ApplyLoaderTablePatchMetadata(Moby moby)
        {
            if (moby == null || moby.TrueIndex < 0) return;
            moby.PatchStatus = "loader-table-patchable";
            moby.PatchLead = MobyLoader.LoaderTablePatchLead(moby.TrueIndex);
            if (moby.PatchPriority <= 0)
                moby.PatchPriority = 1;
        }

        private static void MergeFile(string path, string arrayName, Dictionary<int, MobyMetadata> metadata, bool indexIsLegacyByDefault)
        {
            if (!File.Exists(path)) return;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return;

            object[] entries = GeometryLoader.GetArray(root, arrayName);
            foreach (object obj in entries)
            {
                Dictionary<string, object> entry = obj as Dictionary<string, object>;
                if (entry == null) continue;
                int index = ResolveMetadataIndex(entry, indexIsLegacyByDefault);
                if (index < 0) continue;
                MobyMetadata incoming = BuildMetadata(entry);
                MergeMetadataAtIndex(metadata, index, incoming);
            }
        }

        private static Dictionary<string, MobyMetadata> BuildGlobalSignatureMetadata(string workspace)
        {
            Dictionary<string, MobyMetadata> result = new Dictionary<string, MobyMetadata>();
            Dictionary<string, int> priorities = new Dictionary<string, int>();
            HashSet<string> blocked = new HashSet<string>();
            if (string.IsNullOrEmpty(workspace) || !Directory.Exists(workspace))
                return result;

            foreach (string path in Directory.GetFiles(workspace, "*-moby-catalog.json"))
                MergeGlobalSignatureFile(path, "mobys", result, priorities, blocked, 1);
            foreach (string path in Directory.GetFiles(workspace, "*-moby-user-overrides.json"))
                MergeGlobalSignatureFile(path, "mobys", result, priorities, blocked, 3);

            foreach (string key in blocked)
                result.Remove(key);
            return result;
        }

        private static void MergeGlobalSignatureFile(string path, string arrayName, Dictionary<string, MobyMetadata> result, Dictionary<string, int> priorities, HashSet<string> blocked, int filePriority)
        {
            if (!File.Exists(path)) return;
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return;

            object[] entries = GeometryLoader.GetArray(root, arrayName);
            foreach (object obj in entries)
            {
                Dictionary<string, object> entry = obj as Dictionary<string, object>;
                if (entry == null) continue;

                string key = GlobalSignatureKey(entry);
                if (string.IsNullOrEmpty(key) || blocked.Contains(key)) continue;

                int type = MetadataTypeFromEntry(entry);
                MobyMetadata incoming = BuildMetadata(entry);
                if (!IsShareableGlobalSignatureMetadata(type, incoming)) continue;

                int priority = incoming.IsUserOverride ? Math.Max(filePriority, 3) : filePriority;
                MobyMetadata existing;
                int existingPriority;
                if (!result.TryGetValue(key, out existing))
                {
                    result[key] = CloneMetadata(incoming);
                    priorities[key] = priority;
                    continue;
                }
                if (!priorities.TryGetValue(key, out existingPriority))
                    existingPriority = 0;

                if (GlobalIdentityLabel(existing) == GlobalIdentityLabel(incoming))
                {
                    MergeMetadata(existing, incoming);
                    priorities[key] = Math.Max(existingPriority, priority);
                }
                else if (priority > existingPriority)
                {
                    result[key] = CloneMetadata(incoming);
                    priorities[key] = priority;
                }
                else if (priority == existingPriority)
                {
                    blocked.Add(key);
                    result.Remove(key);
                    priorities.Remove(key);
                }
            }
        }

        private static bool IsShareableGlobalSignatureMetadata(int type, MobyMetadata metadata)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.Label)) return false;
            if (type == 0x00 || type == 0x18) return false;

            string text = (metadata.Label + " " + metadata.Kind + " " + metadata.Confidence).ToLowerInvariant();
            if (ContainsAny(text, "object?", "unknown", "candidate", "related to", "scenery", "tree", "grass", "flower", "lamp", "flag", "fountain", "camera", "nonvisual", "invisible", "helper", "control"))
            {
                if (!ContainsAny(text, "dragon", "pedestal", "portal", "return home", "locked chest", "spring chest"))
                    return false;
            }

            return ContainsAny(text,
                "chest", "box", "container", "enemy", "gnorc", "norc", "torro", "bull", "fodder", "sheep", "chicken", "ram", "shepherd", "shepard", "thief", "theif",
                "key", "dragon", "pedestal", "whirlwind", "fairy", "portal", "return home", "balloonist", "transport npc");
        }

        private static string GlobalIdentityLabel(MobyMetadata metadata)
        {
            if (metadata == null) return "";
            string label = metadata.Label ?? "";
            return label.Trim().ToLowerInvariant();
        }

        private static MobyMetadata CloneMetadata(MobyMetadata source)
        {
            if (source == null) return null;
            MobyMetadata clone = new MobyMetadata();
            clone.Label = source.Label;
            clone.Zone = source.Zone;
            clone.Kind = source.Kind;
            clone.Confidence = source.Confidence;
            clone.IsUserOverride = source.IsUserOverride;
            clone.Evidence = source.Evidence;
            clone.BehaviorNote = source.BehaviorNote;
            clone.SpecialDataNote = source.SpecialDataNote;
            clone.PatchStatus = source.PatchStatus;
            clone.PatchLead = source.PatchLead;
            clone.PatchPriority = source.PatchPriority;
            clone.HasColor = source.HasColor;
            clone.Color = source.Color;
            return clone;
        }

        private static string GlobalSignatureKey(Moby moby)
        {
            if (moby == null) return "";
            if (moby.SpecialDataPointer == 0) return "";
            if (moby.Type == 0x00 || moby.Type == 0x18) return "";
            return moby.Type.ToString("X2") + "|" + moby.SpecialDataPointer.ToString("X8") + "|" + moby.Flag4A.ToString("X2");
        }

        private static string GlobalSignatureKey(Dictionary<string, object> entry)
        {
            int type = MetadataTypeFromEntry(entry);
            uint specialDataPointer = MetadataAddressFromEntry(entry, "specialDataPointer");
            int flag4A = MetadataByteFromEntry(entry, "flag4A", "flag4AHex");
            if (type < 0 || specialDataPointer == 0 || flag4A < 0) return "";
            if (type == 0x00 || type == 0x18) return "";
            return type.ToString("X2") + "|" + specialDataPointer.ToString("X8") + "|" + flag4A.ToString("X2");
        }

        private static int MetadataTypeFromEntry(Dictionary<string, object> entry)
        {
            int type = GetInt(entry, "typeId", -1);
            if (type < 0)
                type = TypeFromHex(GeometryLoader.GetString(entry, "typeHex", ""));
            return type;
        }

        private static int MetadataByteFromEntry(Dictionary<string, object> entry, string intName, string hexName)
        {
            int value = GetInt(entry, intName, -1);
            if (value >= 0) return value;
            return TypeFromHex(GeometryLoader.GetString(entry, hexName, ""));
        }

        private static uint MetadataAddressFromEntry(Dictionary<string, object> entry, string name)
        {
            if (entry == null || !entry.ContainsKey(name) || entry[name] == null) return 0;
            object raw = entry[name];
            try
            {
                if (raw is int || raw is long || raw is uint || raw is ulong)
                    return Convert.ToUInt32(raw);
            }
            catch
            {
                return 0;
            }

            string text = Convert.ToString(raw);
            if (string.IsNullOrEmpty(text)) return 0;
            text = text.Trim();
            try
            {
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return uint.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber);
                uint value;
                if (uint.TryParse(text, out value))
                    return value;
                if (uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out value))
                    return value;
            }
            catch
            {
            }
            return 0;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null) return false;
            for (int i = 0; i < terms.Length; i++)
            {
                string term = terms[i];
                if (!string.IsNullOrEmpty(term) && text.IndexOf(term, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static void MergeMetadataAtIndex(Dictionary<int, MobyMetadata> metadata, int index, MobyMetadata incoming)
        {
            if (index < 0 || incoming == null) return;
            MobyMetadata existing;
            if (metadata.TryGetValue(index, out existing))
                MergeMetadata(existing, incoming);
            else
                metadata[index] = incoming;
        }

        private static int ResolveMetadataIndex(Dictionary<string, object> entry, bool indexIsLegacyByDefault)
        {
            int trueIndex = GetInt(entry, "trueIndex", -1);
            if (trueIndex >= 0) return trueIndex;

            int legacyIndex = GetInt(entry, "legacyIndex", -1);
            if (legacyIndex >= 0)
            {
                int mapped = ConvertLegacyIndexToTrue(legacyIndex);
                if (mapped >= 0) return mapped;
            }

            int index = GetInt(entry, "index", -1);
            if (index < 0) return -1;
            if (indexIsLegacyByDefault)
            {
                int mapped = ConvertLegacyIndexToTrue(index);
                if (mapped >= 0) return mapped;
                return -1;
            }
            return index;
        }

        private static int ConvertLegacyIndexToTrue(int legacyIndex)
        {
            int numerator = (legacyIndex * 0x50) - 8;
            if (numerator >= 0 && (numerator % 0x58) == 0)
                return numerator / 0x58;
            return -1;
        }

        private static void MergeMetadata(MobyMetadata target, MobyMetadata source)
        {
            if (target == null || source == null) return;
            if (!string.IsNullOrEmpty(source.Label)) target.Label = source.Label;
            if (!string.IsNullOrEmpty(source.Zone)) target.Zone = source.Zone;
            if (!string.IsNullOrEmpty(source.Kind)) target.Kind = source.Kind;
            if (!string.IsNullOrEmpty(source.Confidence)) target.Confidence = source.Confidence;
            if (source.IsUserOverride) target.IsUserOverride = true;
            if (!string.IsNullOrEmpty(source.Evidence)) target.Evidence = source.Evidence;
            if (!string.IsNullOrEmpty(source.BehaviorNote)) target.BehaviorNote = source.BehaviorNote;
            if (!string.IsNullOrEmpty(source.SpecialDataNote)) target.SpecialDataNote = source.SpecialDataNote;
            if (!string.IsNullOrEmpty(source.PatchStatus)) target.PatchStatus = source.PatchStatus;
            if (!string.IsNullOrEmpty(source.PatchLead)) target.PatchLead = source.PatchLead;
            if (source.PatchPriority > 0) target.PatchPriority = source.PatchPriority;
            if (source.HasColor)
            {
                target.Color = source.Color;
                target.HasColor = true;
            }
        }

        private static void MergePatchMatrix(string path, Dictionary<int, MobyMetadata> metadata)
        {
            if (!File.Exists(path)) return;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return;

            object[] tests = GeometryLoader.GetArray(root, "rankedPatchTests");
            foreach (object obj in tests)
            {
                Dictionary<string, object> test = obj as Dictionary<string, object>;
                if (test == null) continue;
                int index = ResolveMetadataIndex(test, true);
                if (index < 0) continue;

                MobyMetadata item;
                if (!metadata.TryGetValue(index, out item))
                {
                    item = new MobyMetadata();
                    metadata[index] = item;
                }

                string label = CleanLabel(GeometryLoader.GetString(test, "currentLabel", ""));
                if (!string.IsNullOrEmpty(label))
                    item.Label = label;
                string kind = CleanLabel(GeometryLoader.GetString(test, "candidateKind", ""));
                if (!string.IsNullOrEmpty(kind))
                    item.Kind = kind;
                item.PatchPriority = GetInt(test, "priority", 0);

                Dictionary<string, object> lead = test.ContainsKey("sourceLead") ? test["sourceLead"] as Dictionary<string, object> : null;
                string status = CleanLabel(GeometryLoader.GetString(lead, "status", ""));
                item.PatchStatus = string.IsNullOrEmpty(status) ? "missing-source-window" : status;
                string window = CleanLabel(GeometryLoader.GetString(lead, "wadRelativeWindow", ""));
                string axis = CleanLabel(GeometryLoader.GetString(lead, "axisSet", ""));
                int subfile = GetInt(lead, "assetSubfileIndex", -1);
                int score = GetInt(lead, "score", 0);
                StringBuilder summary = new StringBuilder();
                if (item.PatchPriority > 0) summary.Append("priority ").Append(item.PatchPriority.ToString());
                if (!string.IsNullOrEmpty(window))
                {
                    if (summary.Length > 0) summary.Append(", ");
                    summary.Append("WAD ").Append(window);
                }
                if (subfile >= 0)
                {
                    if (summary.Length > 0) summary.Append(", ");
                    summary.Append("subfile ").Append(subfile.ToString());
                }
                if (!string.IsNullOrEmpty(axis))
                {
                    if (summary.Length > 0) summary.Append(", ");
                    summary.Append("axis ").Append(axis);
                }
                if (score > 0)
                {
                    if (summary.Length > 0) summary.Append(", ");
                    summary.Append("score ").Append(score.ToString());
                }
                item.PatchLead = summary.ToString();
            }
        }

        private static void MergeSpecialData(string path, Dictionary<int, MobyMetadata> metadata)
        {
            if (!File.Exists(path)) return;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return;

            object[] records = GeometryLoader.GetArray(root, "records");
            foreach (object obj in records)
            {
                Dictionary<string, object> record = obj as Dictionary<string, object>;
                if (record == null) continue;
                int index = ResolveMetadataIndex(record, true);
                if (index < 0) continue;
                if (!GetBool(record, "validMainRamPointer", false)) continue;

                MobyMetadata item;
                if (!metadata.TryGetValue(index, out item))
                {
                    item = new MobyMetadata();
                    metadata[index] = item;
                }

                string pointer = CleanLabel(GeometryLoader.GetString(record, "specialDataPointer", ""));
                int pointerCount = GeometryLoader.GetArray(record, "pointerFields").Length;
                string matchText = BuildSourceMatchSummary(record);

                StringBuilder note = new StringBuilder();
                note.Append("Special data");
                if (!string.IsNullOrEmpty(pointer))
                    note.Append(" ").Append(pointer);
                note.Append(": ").Append(pointerCount.ToString()).Append(" linked pointer");
                if (pointerCount != 1) note.Append("s");
                if (!string.IsNullOrEmpty(matchText))
                    note.Append("; WAD match ").Append(matchText);
                item.SpecialDataNote = CleanEvidence(note.ToString());
            }
        }

        private static string BuildSourceMatchSummary(Dictionary<string, object> record)
        {
            List<string> parts = new List<string>();
            foreach (object obj in GeometryLoader.GetArray(record, "sourceMatches"))
            {
                Dictionary<string, object> match = obj as Dictionary<string, object>;
                if (match == null) continue;
                if (GetInt(match, "count", 0) <= 0) continue;
                string kind = CleanLabel(GeometryLoader.GetString(match, "kind", ""));
                string offset = FirstString(GeometryLoader.GetArray(match, "wadRelativeOffsets"));
                if (string.IsNullOrEmpty(kind)) continue;
                if (!string.IsNullOrEmpty(offset))
                    parts.Add(kind + "@" + offset);
                else
                    parts.Add(kind);
                if (parts.Count >= 2) break;
            }
            return string.Join(", ", parts.ToArray());
        }

        private static string FirstString(object[] values)
        {
            if (values == null || values.Length == 0) return "";
            return Convert.ToString(values[0]);
        }

        private static MobyMetadata BuildMetadata(Dictionary<string, object> entry)
        {
            int type = GetInt(entry, "typeId", -1);
            if (type < 0)
                type = TypeFromHex(GeometryLoader.GetString(entry, "typeHex", ""));

            string confidence = CleanLabel(GeometryLoader.GetString(entry, "confidence", ""));
            bool preserveUserText = confidence.IndexOf("user override", StringComparison.OrdinalIgnoreCase) >= 0;

            string label = CleanMetadataText(GeometryLoader.GetString(entry, "displayTargetLabel", ""), preserveUserText, 80);
            if (string.IsNullOrEmpty(label))
                label = CleanMetadataText(GeometryLoader.GetString(entry, "displayLabel", ""), preserveUserText, 80);
            if (string.IsNullOrEmpty(label))
                label = CleanMetadataText(GeometryLoader.GetString(entry, "label", ""), preserveUserText, 80);
            if (string.IsNullOrEmpty(label))
            {
                Dictionary<string, object> guess = entry.ContainsKey("publicTargetGuess") ? entry["publicTargetGuess"] as Dictionary<string, object> : null;
                label = CleanLabel(GeometryLoader.GetString(guess, "label", ""));
            }
            if (string.IsNullOrEmpty(label))
                label = CandidateNames(entry);
            if (string.IsNullOrEmpty(label))
                label = MobyLoader.FallbackLabel(type);

            MobyMetadata metadata = new MobyMetadata();
            metadata.Label = label;
            metadata.Zone = CleanLabel(GeometryLoader.GetString(entry, "zoneLabel", ""));
            if (string.IsNullOrEmpty(metadata.Zone))
                metadata.Zone = CleanLabel(GeometryLoader.GetString(entry, "zone", ""));
            metadata.Kind = CleanMetadataText(GeometryLoader.GetString(entry, "candidateKind", ""), preserveUserText, 120);
            if (string.IsNullOrEmpty(metadata.Kind))
                metadata.Kind = CleanMetadataText(GeometryLoader.GetString(entry, "kind", ""), preserveUserText, 120);
            metadata.Confidence = confidence;
            metadata.IsUserOverride = preserveUserText;
            metadata.Evidence = CleanEvidence(GeometryLoader.GetString(entry, "evidence", ""));
            metadata.BehaviorNote = CleanEvidence(GeometryLoader.GetString(entry, "behaviorNote", ""));
            if (string.IsNullOrEmpty(metadata.BehaviorNote))
                metadata.BehaviorNote = CandidateBehaviorNote(entry);
            metadata.SpecialDataNote = CleanEvidence(GeometryLoader.GetString(entry, "specialDataNote", ""));
            metadata.PatchStatus = CleanLabel(GeometryLoader.GetString(entry, "patchStatus", ""));
            metadata.PatchLead = CleanEvidence(GeometryLoader.GetString(entry, "patchLead", ""));
            metadata.PatchPriority = GetInt(entry, "patchPriority", 0);
            Color color;
            if (TryParseColor(GeometryLoader.GetString(entry, "color", ""), out color))
            {
                metadata.Color = color;
                metadata.HasColor = true;
            }
            return metadata;
        }

        private static string CandidateNames(Dictionary<string, object> entry)
        {
            object[] candidates = GeometryLoader.GetArray(entry, "publicTargetCandidates");
            List<string> names = new List<string>();
            foreach (object candidate in candidates)
            {
                Dictionary<string, object> item = candidate as Dictionary<string, object>;
                if (item == null) continue;
                string name = CleanLabel(GeometryLoader.GetString(item, "name", ""));
                if (name.Length == 0) continue;
                if (!names.Contains(name)) names.Add(name);
                if (names.Count >= 2) break;
            }
            return names.Count == 0 ? "" : string.Join("/", names.ToArray());
        }

        private static string CandidateBehaviorNote(Dictionary<string, object> entry)
        {
            object[] candidates = GeometryLoader.GetArray(entry, "publicTargetCandidates");
            foreach (object candidate in candidates)
            {
                Dictionary<string, object> item = candidate as Dictionary<string, object>;
                if (item == null) continue;
                string category = GeometryLoader.GetString(item, "category", "");
                if (!string.Equals(category, "behavior", StringComparison.OrdinalIgnoreCase)) continue;

                string reason = CleanEvidence(GeometryLoader.GetString(item, "reason", ""));
                if (!string.IsNullOrEmpty(reason)) return reason;
                string name = CleanLabel(GeometryLoader.GetString(item, "name", ""));
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return "";
        }

        private static bool TryParseColor(string text, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                color = ColorTranslator.FromHtml(text.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int GetInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        private static bool GetBool(Dictionary<string, object> dict, string name, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToBoolean(dict[name]); }
            catch { return fallback; }
        }

        private static int TypeFromHex(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            int value;
            return int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out value) ? value : -1;
        }

        private static string CleanLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            label = label.Trim();
            label = label.Replace("Stone Hill ", "");
            label = RemoveCatalogSuffixes(label);
            label = label.Replace("Treasure/gem placement candidate", "Treasure/gem");
            label = label.Replace("Internal helper/state record", "Helper/system");
            label = label.Replace("Collected gem/collectible", "Collected gem");
            label = label.Replace("Shepherd enemy", "Shepherd");
            label = label.Replace("shepherd enemy actor", "shepherd actor");
            label = label.Replace("Shepard", "Shepherd");
            label = label.Replace("Nonvisual control/placeholder", "Nonvisual control");
            label = label.Replace("Already-active whirlwind", "Active whirlwind");
            label = label.Replace("Already activated Whirlwind", "Active whirlwind");
            while (label.IndexOf("  ", StringComparison.Ordinal) >= 0)
                label = label.Replace("  ", " ");
            if (label.Length > 52)
                label = label.Substring(0, 49) + "...";
            return label;
        }

        private static string CleanMetadataText(string text, bool preserveUserText, int maxLength)
        {
            if (!preserveUserText)
                return CleanLabel(text);
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
                text = text.Replace("  ", " ");
            if (maxLength > 0 && text.Length > maxLength)
                text = text.Substring(0, maxLength).Trim();
            return text;
        }

        private static string RemoveCatalogSuffixes(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            string[] suffixes = new string[]
            {
                " (safe-ground observed)",
                " (wide-row observed)",
                " (raised-row observed)",
                " (single-row observed)",
                " (batch observed)",
                " (live-tested)",
                " (live-confirmed)",
                " (live-inferred)",
                " (signature-confirmed)",
                " (cluster)",
                " (observed)"
            };
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < suffixes.Length; i++)
                {
                    if (label.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        label = label.Substring(0, label.Length - suffixes[i].Length).Trim();
                        changed = true;
                    }
                }
            }
            return label;
        }

        private static string CleanEvidence(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
                text = text.Replace("  ", " ");
            if (text.Length > 260)
                text = text.Substring(0, 257) + "...";
            return text;
        }
    }

    internal sealed class MobyMetadata
    {
        public string Label;
        public string Zone;
        public string Kind;
        public string Confidence;
        public bool IsUserOverride;
        public string Evidence;
        public string BehaviorNote;
        public string SpecialDataNote;
        public string PatchStatus;
        public string PatchLead;
        public int PatchPriority;
        public bool HasColor;
        public Color Color;
    }

    internal sealed class LevelTextChoice
    {
        public readonly string Key;
        public readonly string ScriptKey;
        public readonly string DisplayName;
        public readonly string OriginalName;

        public LevelTextChoice(string key, string scriptKey, string displayName, string originalName)
        {
            Key = key;
            ScriptKey = scriptKey;
            DisplayName = displayName;
            OriginalName = originalName;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static LevelTextChoice[] All()
        {
            return new LevelTextChoice[]
            {
                new LevelTextChoice("artisans", "Artisans", "Artisans", "ARTISANS"),
                new LevelTextChoice("stonehill", "StoneHill", "Stone Hill", "STONE HILL"),
                new LevelTextChoice("darkhollow", "DarkHollow", "Dark Hollow", "DARK HOLLOW"),
                new LevelTextChoice("townsquare", "TownSquare", "Town Square", "TOWN SQUARE"),
                new LevelTextChoice("sunnyflight", "SunnyFlight", "Sunny Flight", "SUNNY FLIGHT"),
                new LevelTextChoice("drycanyon", "DryCanyon", "Dry Canyon", "DRY CANYON"),
                new LevelTextChoice("clifftown", "CliffTown", "Cliff Town", "CLIFF TOWN"),
                new LevelTextChoice("icecavern", "IceCavern", "Ice Cavern", "ICE CAVERN"),
                new LevelTextChoice("doctorshemp", "DoctorShemp", "Doctor Shemp", "DOCTOR SHEMP"),
                new LevelTextChoice("nightflight", "NightFlight", "Night Flight", "NIGHT FLIGHT"),
                new LevelTextChoice("peacekeepers", "PeaceKeepers", "Peace Keepers", "PEACE KEEPERS"),
                new LevelTextChoice("magiccrafters", "MagicCrafters", "Magic Crafters", "MAGIC CRAFTERS"),
                new LevelTextChoice("alpineridge", "AlpineRidge", "Alpine Ridge", "ALPINE RIDGE"),
                new LevelTextChoice("highcaves", "HighCaves", "High Caves", "HIGH CAVES"),
                new LevelTextChoice("wizardpeak", "WizardPeak", "Wizard Peak", "WIZARD PEAK"),
                new LevelTextChoice("blowhard", "Blowhard", "Blowhard", "BLOWHARD"),
                new LevelTextChoice("crystalflight", "CrystalFlight", "Crystal Flight", "CRYSTAL FLIGHT"),
                new LevelTextChoice("beastmakers", "BeastMakers", "Beast Makers", "BEAST MAKERS"),
                new LevelTextChoice("terracevillage", "TerraceVillage", "Terrace Village", "TERRACE VILLAGE"),
                new LevelTextChoice("mistybog", "MistyBog", "Misty Bog", "MISTY BOG"),
                new LevelTextChoice("treetops", "TreeTops", "Tree Tops", "TREE TOPS"),
                new LevelTextChoice("metalhead", "Metalhead", "Metalhead", "METALHEAD"),
                new LevelTextChoice("wildflight", "WildFlight", "Wild Flight", "WILD FLIGHT"),
                new LevelTextChoice("dreamweavers", "DreamWeavers", "Dream Weavers", "DREAM WEAVERS"),
                new LevelTextChoice("darkpassage", "DarkPassage", "Dark Passage", "DARK PASSAGE"),
                new LevelTextChoice("loftycastle", "LoftyCastle", "Lofty Castle", "LOFTY CASTLE"),
                new LevelTextChoice("hauntedtowers", "HauntedTowers", "Haunted Towers", "HAUNTED TOWERS"),
                new LevelTextChoice("icyflight", "IcyFlight", "Icy Flight", "ICY FLIGHT"),
                new LevelTextChoice("gnastyworld", "GnastyWorld", "Gnasty's World", "GNASTY'S WORLD"),
                new LevelTextChoice("gnorccove", "GnorcCove", "Gnorc Cove", "GNORC COVE"),
                new LevelTextChoice("twilightharbor", "TwilightHarbor", "Twilight Harbor", "TWILIGHT HARBOR"),
                new LevelTextChoice("gnastygnorc", "GnastyGnorc", "Gnasty Gnorc", "GNASTY GNORC"),
                new LevelTextChoice("gnastyloot", "GnastyLoot", "Gnasty's Loot", "GNASTY'S LOOT")
            };
        }
    }

    internal sealed class ExeStringChoice
    {
        public readonly string Text;
        public readonly string Offset;
        public readonly string Kind;
        public readonly int Length;

        public ExeStringChoice(string text, string offset, string kind, int length)
        {
            Text = text ?? "";
            Offset = offset ?? "";
            Kind = string.IsNullOrEmpty(kind) ? "unknown" : kind;
            Length = length;
        }

        public override string ToString()
        {
            string text = Text.Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 64) text = text.Substring(0, 61) + "...";
            return "[" + Kind + "] " + text + " (" + Offset + ", " + Length.ToString() + ")";
        }
    }

    internal sealed class SkyboxChoice
    {
        public readonly string Key;
        public readonly string ScriptKey;
        public readonly string DisplayName;

        public SkyboxChoice(string key, string scriptKey, string displayName)
        {
            Key = key;
            ScriptKey = scriptKey;
            DisplayName = displayName;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static SkyboxChoice[] All()
        {
            return new SkyboxChoice[]
            {
                new SkyboxChoice("stonehill", "StoneHill", "Stone Hill"),
                new SkyboxChoice("darkhollow", "DarkHollow", "Dark Hollow"),
                new SkyboxChoice("townsquare", "TownSquare", "Town Square"),
                new SkyboxChoice("toasty", "Toasty", "Toasty"),
                new SkyboxChoice("sunnyflight", "SunnyFlight", "Sunny Flight"),
                new SkyboxChoice("peacekeepers", "PeaceKeepers", "Peace Keepers"),
                new SkyboxChoice("drycanyon", "DryCanyon", "Dry Canyon"),
                new SkyboxChoice("clifftown", "CliffTown", "Cliff Town"),
                new SkyboxChoice("icecavern", "IceCavern", "Ice Cavern"),
                new SkyboxChoice("doctorshemp", "DoctorShemp", "Doctor Shemp"),
                new SkyboxChoice("nightflight", "NightFlight", "Night Flight"),
                new SkyboxChoice("magiccrafters", "MagicCrafters", "Magic Crafters"),
                new SkyboxChoice("alpineridge", "AlpineRidge", "Alpine Ridge"),
                new SkyboxChoice("highcaves", "HighCaves", "High Caves"),
                new SkyboxChoice("wizardpeak", "WizardPeak", "Wizard Peak"),
                new SkyboxChoice("blowhard", "Blowhard", "Blowhard"),
                new SkyboxChoice("crystalflight", "CrystalFlight", "Crystal Flight"),
                new SkyboxChoice("beastmakers", "BeastMakers", "Beast Makers"),
                new SkyboxChoice("terracevillage", "TerraceVillage", "Terrace Village"),
                new SkyboxChoice("mistybog", "MistyBog", "Misty Bog"),
                new SkyboxChoice("treetops", "TreeTops", "Tree Tops"),
                new SkyboxChoice("metalhead", "Metalhead", "Metalhead"),
                new SkyboxChoice("wildflight", "WildFlight", "Wild Flight"),
                new SkyboxChoice("dreamweavers", "DreamWeavers", "Dream Weavers"),
                new SkyboxChoice("darkpassage", "DarkPassage", "Dark Passage"),
                new SkyboxChoice("loftycastle", "LoftyCastle", "Lofty Castle"),
                new SkyboxChoice("hauntedtowers", "HauntedTowers", "Haunted Towers"),
                new SkyboxChoice("jacques", "Jacques", "Jacques"),
                new SkyboxChoice("icyflight", "IcyFlight", "Icy Flight"),
                new SkyboxChoice("gnastysworld", "GnastysWorld", "Gnasty's World"),
                new SkyboxChoice("gnorccove", "GnorcCove", "Gnorc Cove"),
                new SkyboxChoice("twilightharbor", "TwilightHarbor", "Twilight Harbor"),
                new SkyboxChoice("gnastygnorc", "GnastyGnorc", "Gnasty Gnorc"),
                new SkyboxChoice("gnastysloot", "GnastysLoot", "Gnasty's Loot")
            };
        }
    }

    internal sealed class SkyColorPresetChoice
    {
        public readonly string ScriptKey;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string FileSlug;

        public SkyColorPresetChoice(string scriptKey, string displayName, string description, string fileSlug)
        {
            ScriptKey = scriptKey;
            DisplayName = displayName;
            Description = description;
            FileSlug = fileSlug;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static SkyColorPresetChoice[] All()
        {
            return new SkyColorPresetChoice[]
            {
                new SkyColorPresetChoice("DarkHollowMixed", "Dark Hollow Mixed", "the blue-gray Dark Hollow feel from the 3rd test cue.", "darkhollow-mixed"),
                new SkyColorPresetChoice("StoneHillNight", "Stone Hill Night", "a custom blue night palette on the safe Stone Hill sky pieces.", "stonehill-night"),
                new SkyColorPresetChoice("StoneHillBestNight", "Stone Hill Best Night", "the current best Stone Hill night build: base sky combined plus the loaded 0x8FB98 cleanup.", "stonehill-best-night"),
                new SkyColorPresetChoice("StoneHillNightKeeper", "Stone Hill Night Keeper", "the current keeper night build: dark loaded Stone Hill sky and water, with the small fly-in cyan strip left for later.", "stonehill-night-keeper"),
                new SkyColorPresetChoice("StoneHillMoonAtmosphere", "Moon Atmosphere", "experimental keeper plus pale moon word and purple-blue Stone Hill cloud shading; color words only.", "stonehill-moon-atmosphere"),
                new SkyColorPresetChoice("StoneHillMoonStarProbe", "Moon + Star Probe", "experimental Moon Atmosphere plus a few tiny loaded-level spot words recolored as star candidates.", "stonehill-moon-star-probe"),
                new SkyColorPresetChoice("StoneHillStableNight", "Stone Hill Stable Night", "a conservative low-contrast night grade on the proven Stone Hill primitive-color records.", "stonehill-stable-night"),
                new SkyColorPresetChoice("StoneHillDeepNight", "Stone Hill Deep Night", "a broader night grade on the known safe Stone Hill sky records.", "stonehill-deep-night"),
                new SkyColorPresetChoice("StoneHillFullNight", "Stone Hill Full Night", "the widest RGB-only night grade for the portal/fly-in and loaded Stone Hill sky records.", "stonehill-full-night"),
                new SkyColorPresetChoice("StoneHillSmoothNight", "Stone Hill Smooth Night", "a lower-contrast full night grade that hides the bright Stone Hill triangle shapes.", "stonehill-smooth-night"),
                new SkyColorPresetChoice("StoneHillFlatNight", "Stone Hill Flat Night", "a full night pass that collapses Stone Hill sky pieces to one dark blue.", "stonehill-flat-night"),
                new SkyColorPresetChoice("DarkHollowDark", "Dark Hollow Dark", "the darkest sampled Dark Hollow sky colors.", "darkhollow-dark"),
                new SkyColorPresetChoice("DarkHollowMid", "Dark Hollow Mid", "brighter Dark Hollow sky colors.", "darkhollow-mid"),
                new SkyColorPresetChoice("DarkHollowPlus19Dark", "Dark Hollow Broad", "a broader color-only pass that may affect more panels.", "darkhollow-plus19-dark"),
                new SkyColorPresetChoice("Custom", "Custom Palette", "your typed #RRGGBB palette, repeated across the safe sky pieces.", "custom")
            };
        }
    }
    internal static class MobyEditStore
    {
        public static int Save(string path, List<Moby> mobys, string levelName)
        {
            List<Dictionary<string, object>> edits = new List<Dictionary<string, object>>();
            foreach (Moby moby in mobys)
            {
                if (!moby.IsEdited) continue;
                Dictionary<string, object> edit = new Dictionary<string, object>();
                edit["index"] = moby.Index;
                if (moby.TrueIndex >= 0)
                    edit["trueIndex"] = moby.TrueIndex;
                if (moby.LegacyIndex >= 0)
                    edit["legacyIndex"] = moby.LegacyIndex;
                edit["label"] = moby.DisplayLabel;
                edit["typeHex"] = "0x" + moby.Type.ToString("X2");
                edit["stateHex"] = "0x" + moby.State.ToString("X2");
                edit["runtimeAddress"] = FormatAddress(moby.RuntimeAddress);
                edit["specialDataPointer"] = FormatAddress(moby.SpecialDataPointer);
                edit["sourceByte36Hex"] = "0x" + moby.SourceByte36.ToString("X2");
                edit["sourceByte37Hex"] = "0x" + moby.SourceByte37.ToString("X2");
                edit["sourceByte4FHex"] = "0x" + moby.SourceByte4F.ToString("X2");
                edit["flag4AHex"] = "0x" + moby.Flag4A.ToString("X2");
                edit["flag4BHex"] = "0x" + moby.Flag4B.ToString("X2");
                edit["patchStatus"] = moby.PatchStatus ?? "";
                edit["patchLead"] = moby.PatchLead ?? "";
                edit["behaviorNote"] = moby.BehaviorNote ?? "";
                edit["specialDataNote"] = moby.SpecialDataNote ?? "";
                edit["original"] = NewVector(moby.OriginalX, moby.OriginalY, moby.OriginalZ);
                edit["edited"] = NewVector(moby.X, moby.Y, moby.Z);
                edit["rawOriginal"] = NewRawVector(moby.OriginalX, moby.OriginalY, moby.OriginalZ);
                edit["rawEdited"] = NewRawVector(moby.X, moby.Y, moby.Z);
                edit["rawDelta"] = NewRawDelta(moby);
                if (!string.IsNullOrEmpty(moby.SpringChestPairRole) && moby.SpringChestPartnerTrueIndex >= 0)
                    edit["springChestRuntimeHelper"] = NewSpringChestRuntimeHelperEdit(moby);
                List<Dictionary<string, object>> sourceByteEdits = new List<Dictionary<string, object>>();
                if (moby.HasGemColorEdit)
                {
                    edit["gemColorEdit"] = NewGemColorEdit(moby);
                    sourceByteEdits.AddRange(NewGemSourceByteEdits(moby));
                }
                if (moby.HasRewardColorEdit)
                {
                    edit["rewardColorEdit"] = NewRewardColorEdit(moby);
                    sourceByteEdits.AddRange(NewRewardSourceByteEdits(moby));
                }
                if (moby.HasChestContentLinkEdit)
                    edit["chestContentLinkEdit"] = NewChestContentLinkEdit(moby);
                if (moby.HasRecordCloneEdit)
                    edit["recordMutation"] = NewRecordCloneMutation(moby);
                else if (moby.IsAppendedRecord)
                    edit["recordMutation"] = NewAppendMutation(moby);
                else if (moby.HasHiddenSlotEdit)
                    edit["recordMutation"] = NewHideMutation(moby);
                if (sourceByteEdits.Count > 0)
                    edit["sourceByteEdits"] = sourceByteEdits;
                edits.Add(edit);
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["generatedAt"] = DateTime.Now.ToString("s");
            root["editor"] = "NativeSpyroEditor";
            root["levelName"] = string.IsNullOrEmpty(levelName) ? "Unknown" : levelName;
            root["note"] = "Native editor " + root["levelName"] + " records. XYZ edits and supported source-byte edits are editor-side records; only levels with wired source patchers can export BIN patches from this file.";
            root["editCount"] = edits.Count;
            root["edits"] = edits;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            File.WriteAllText(path, serializer.Serialize(root), Encoding.UTF8);
            return edits.Count;
        }

        public static int Load(string path, List<Moby> mobys, string levelKey)
        {
            if (!File.Exists(path)) return 0;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return 0;

            Dictionary<int, Moby> byIndex = new Dictionary<int, Moby>();
            Dictionary<int, Moby> byTrueIndex = new Dictionary<int, Moby>();
            foreach (Moby moby in mobys)
            {
                byIndex[moby.Index] = moby;
                if (moby.TrueIndex >= 0)
                    byTrueIndex[moby.TrueIndex] = moby;
            }

            int applied = 0;
            int sourceRecordCount = SourceRecordCountForLevel(levelKey);
            object[] edits = GeometryLoader.GetArray(root, "edits");
            foreach (object obj in edits)
            {
                Dictionary<string, object> edit = obj as Dictionary<string, object>;
                if (edit == null) continue;
                int index = GetInt(edit, "index", -1);
                int trueIndex = GetInt(edit, "trueIndex", -1);
                int legacyIndex = GetInt(edit, "legacyIndex", -1);
                Dictionary<string, object> recordMutation = edit.ContainsKey("recordMutation") ? edit["recordMutation"] as Dictionary<string, object> : null;
                string mutationMode = GetString(recordMutation, "mode", "");
                if (string.Equals(mutationMode, "appendFromSource", StringComparison.OrdinalIgnoreCase))
                {
                    int sourceTrueIndex = GetInt(recordMutation, "sourceTrueIndex", -1);
                    string sourceLevelKey = GetString(recordMutation, "sourceLevelKey", "");
                    bool externalSource = !string.IsNullOrEmpty(sourceLevelKey) && SpyroLevelCatalog.NormalizeKey(sourceLevelKey) != SpyroLevelCatalog.NormalizeKey(levelKey);
                    int appendTrueIndex = trueIndex >= sourceRecordCount ? trueIndex : NextAppendedTrueIndex(mobys, sourceRecordCount);
                    Moby appended;
                    if (externalSource)
                    {
                        appended = CreateAppendedFromSavedEdit(edit, recordMutation, mobys.Count, appendTrueIndex);
                    }
                    else
                    {
                        if (sourceTrueIndex < 0 || sourceTrueIndex >= sourceRecordCount) continue;
                        Moby source;
                        if (!byTrueIndex.TryGetValue(sourceTrueIndex, out source)) continue;
                        appended = Moby.CreateAppendedFromSource(source, mobys.Count, appendTrueIndex);
                    }
                    ApplyEditToMoby(appended, edit, byTrueIndex, byIndex);
                    mobys.Add(appended);
                    byIndex[appended.Index] = appended;
                    applied++;
                    continue;
                }

                Moby moby;
                if (trueIndex >= 0)
                {
                    if (!byTrueIndex.TryGetValue(trueIndex, out moby)) continue;
                }
                else if (legacyIndex >= 0)
                {
                    int mappedTrueIndex;
                    if (!MobyLoader.TryGetLoaderTableTrueIndex(legacyIndex, out mappedTrueIndex) || !byTrueIndex.TryGetValue(mappedTrueIndex, out moby)) continue;
                }
                else
                {
                    int mappedTrueIndex;
                    if (MobyLoader.TryGetLoaderTableTrueIndex(index, out mappedTrueIndex) && byTrueIndex.TryGetValue(mappedTrueIndex, out moby))
                    {
                    }
                    else if (!byIndex.TryGetValue(index, out moby))
                    {
                        continue;
                    }
                }
                ApplyEditToMoby(moby, edit, byTrueIndex, byIndex);
                applied++;
            }
            return applied;
        }

        private static Moby CreateAppendedFromSavedEdit(Dictionary<string, object> edit, Dictionary<string, object> recordMutation, int listIndex, int appendTrueIndex)
        {
            Moby moby = new Moby();
            moby.Index = listIndex;
            moby.TrueIndex = appendTrueIndex;
            moby.LegacyIndex = -1;
            moby.X = 0;
            moby.Y = 0;
            moby.Z = 0;
            moby.OriginalX = 0;
            moby.OriginalY = 0;
            moby.OriginalZ = 0;
            moby.Type = GetFlexibleInt(edit, "typeHex", GetInt(edit, "typeId", 0));
            moby.State = GetFlexibleInt(edit, "stateHex", 0);
            moby.RuntimeAddress = 0;
            moby.SpecialDataPointer = (uint)GetFlexibleInt64(edit, "specialDataPointer", 0);
            moby.SourceByte36 = GetFlexibleInt(edit, "sourceByte36Hex", 0);
            moby.SourceByte37 = GetFlexibleInt(edit, "sourceByte37Hex", 0);
            moby.SourceByte4F = GetFlexibleInt(edit, "sourceByte4FHex", 0);
            moby.Flag4A = GetFlexibleInt(edit, "flag4AHex", 0);
            moby.Flag4B = GetFlexibleInt(edit, "flag4BHex", 0);
            moby.Color = FallbackTemplateColor(moby.Type);
            moby.Label = GetString(edit, "label", GetString(recordMutation, "sourceLabel", "Object Library object"));
            moby.Zone = "Object Library";
            moby.Kind = GetString(recordMutation, "sourceFamily", "Object Library template");
            moby.Confidence = "loaded object library true-add";
            moby.Evidence = "saved cross-level append from " + GetString(recordMutation, "sourceLevelName", GetString(recordMutation, "sourceLevelKey", "")) + " T" + GetInt(recordMutation, "sourceTrueIndex", -1).ToString();
            moby.BehaviorNote = GetString(edit, "behaviorNote", "");
            moby.SpecialDataNote = GetString(edit, "specialDataNote", "");
            moby.PatchStatus = GetString(edit, "patchStatus", "append-patchable");
            moby.PatchLead = GetString(edit, "patchLead", "append source record T" + appendTrueIndex.ToString());
            moby.PatchPriority = 1;
            moby.IsAppendedRecord = true;
            moby.AppendSourceTrueIndex = GetInt(recordMutation, "sourceTrueIndex", -1);
            moby.AppendSourceIndex = GetInt(recordMutation, "sourceIndex", -1);
            moby.AppendSourceLabel = GetString(recordMutation, "sourceLabel", moby.Label);
            moby.AppendSourceLevelKey = GetString(recordMutation, "sourceLevelKey", "");
            moby.AppendSourceLevelName = GetString(recordMutation, "sourceLevelName", "");
            moby.AppendSourceFamily = GetString(recordMutation, "sourceFamily", "");
            moby.AppendLoaderTransformedDonor = GetBool(recordMutation, "loaderTransformedDonor", false);
            moby.AppendDonorMapConfidence = GetString(recordMutation, "donorMapConfidence", "");
            moby.AppendDependencyRisk = GetString(recordMutation, "dependencyRisk", "");
            moby.AppendPackageImportProfile = GetString(recordMutation, "packageImportProfile", "");
            moby.AppendRuntimeIdentityPolicy = GetString(recordMutation, "runtimeIdentityPolicy", "");
            Dictionary<string, object> springHelper = edit.ContainsKey("springChestRuntimeHelper") ? edit["springChestRuntimeHelper"] as Dictionary<string, object> : null;
            if (springHelper != null)
            {
                moby.SpringChestPairRole = GetString(springHelper, "role", "");
                moby.SpringChestPartnerTrueIndex = GetInt(springHelper, "partnerTrueIndex", -1);
            }
            moby.CaptureBaseIdentity();
            return moby;
        }

        private static Color FallbackTemplateColor(int type)
        {
            if (type == 0x20) return Color.FromArgb(244, 212, 77);
            if (type == 0x18) return Color.FromArgb(255, 140, 90);
            if (type == 0x00) return Color.FromArgb(143, 166, 184);
            return Color.FromArgb(93, 173, 226);
        }

        private static bool ApplyEditToMoby(Moby moby, Dictionary<string, object> edit, Dictionary<int, Moby> byTrueIndex, Dictionary<int, Moby> byIndex)
        {
            if (moby == null || edit == null) return false;
            bool changed = false;
            Dictionary<string, object> edited = edit.ContainsKey("edited") ? edit["edited"] as Dictionary<string, object> : null;
            if (edited != null)
            {
                moby.X = (float)GeometryLoader.GetDouble(edited, "x", moby.X);
                moby.Y = (float)GeometryLoader.GetDouble(edited, "y", moby.Y);
                moby.Z = (float)GeometryLoader.GetDouble(edited, "z", moby.Z);
                changed = true;
            }

            Dictionary<string, object> recordMutation = edit.ContainsKey("recordMutation") ? edit["recordMutation"] as Dictionary<string, object> : null;
            string mutationMode = GetString(recordMutation, "mode", "");
            if (!moby.IsAppendedRecord)
            {
                if (string.Equals(mutationMode, "cloneIntoSlot", StringComparison.OrdinalIgnoreCase))
                {
                    int sourceTrueIndex = GetInt(recordMutation, "sourceTrueIndex", -1);
                    int sourceIndex = GetInt(recordMutation, "sourceIndex", -1);
                    Moby source;
                    if ((sourceTrueIndex >= 0 && byTrueIndex.TryGetValue(sourceTrueIndex, out source))
                        || (sourceIndex >= 0 && byIndex.TryGetValue(sourceIndex, out source)))
                    {
                        moby.SetRecordCloneOverride(source);
                        changed = true;
                    }
                }
                else if (string.Equals(mutationMode, "hide", StringComparison.OrdinalIgnoreCase))
                {
                    moby.SetHiddenSlotOverride();
                    changed = true;
                }
            }

            Dictionary<string, object> gemColorEdit = edit.ContainsKey("gemColorEdit") ? edit["gemColorEdit"] as Dictionary<string, object> : null;
            string gemColor = GetString(gemColorEdit, "color", "");
            if (string.IsNullOrEmpty(gemColor))
                gemColor = InferGemColorFromSourceByteEdits(edit);
            if (Moby.IsSupportedGemColor(gemColor))
            {
                moby.SetGemColorOverride(gemColor);
                changed = true;
            }

            Dictionary<string, object> rewardColorEdit = edit.ContainsKey("rewardColorEdit") ? edit["rewardColorEdit"] as Dictionary<string, object> : null;
            string rewardColor = GetString(rewardColorEdit, "color", "");
            if (string.IsNullOrEmpty(rewardColor))
                rewardColor = InferRewardColorFromSourceByteEdits(edit);
            if (Moby.IsSupportedGemColor(rewardColor))
            {
                if (IsContainedGemMarkerEdit(moby, rewardColorEdit))
                    moby.SetContainedGemColorOverride(rewardColor);
                else
                    moby.SetRewardColorOverride(rewardColor);
                changed = true;
            }

            Dictionary<string, object> chestContentLinkEdit = edit.ContainsKey("chestContentLinkEdit") ? edit["chestContentLinkEdit"] as Dictionary<string, object> : null;
            if (chestContentLinkEdit != null)
            {
                Dictionary<string, object> rawOffset = chestContentLinkEdit.ContainsKey("rawOffset") ? chestContentLinkEdit["rawOffset"] as Dictionary<string, object> : null;
                moby.SetChestContentLinkOverride(
                    GetInt(chestContentLinkEdit, "chestTrueIndex", -1),
                    GetInt(chestContentLinkEdit, "chestIndex", -1),
                    GetString(chestContentLinkEdit, "chestLabel", ""),
                    GetInt(rawOffset, "x", 0),
                    GetInt(rawOffset, "y", 0),
                    GetInt(rawOffset, "z", 0));
                changed = true;
            }

            return changed;
        }

        private static Dictionary<string, object> NewVector(float x, float y, float z)
        {
            Dictionary<string, object> vector = new Dictionary<string, object>();
            vector["x"] = Math.Round(x, 4);
            vector["y"] = Math.Round(y, 4);
            vector["z"] = Math.Round(z, 4);
            return vector;
        }

        private static Dictionary<string, object> NewRawVector(float x, float y, float z)
        {
            Dictionary<string, object> vector = new Dictionary<string, object>();
            vector["x"] = ToRawCoordinate(x);
            vector["y"] = ToRawCoordinate(y);
            vector["z"] = ToRawCoordinate(z);
            return vector;
        }

        private static Dictionary<string, object> NewRawDelta(Moby moby)
        {
            Dictionary<string, object> vector = new Dictionary<string, object>();
            vector["x"] = ToRawCoordinate(moby.X) - ToRawCoordinate(moby.OriginalX);
            vector["y"] = ToRawCoordinate(moby.Y) - ToRawCoordinate(moby.OriginalY);
            vector["z"] = ToRawCoordinate(moby.Z) - ToRawCoordinate(moby.OriginalZ);
            return vector;
        }

        private static Dictionary<string, object> NewGemColorEdit(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["mode"] = "standalone-gem";
            edit["color"] = moby.GemColorName;
            edit["value"] = moby.GemValueOverride;
            edit["sourceByte36Hex"] = "0x" + moby.GemSourceByte36Override.ToString("X2");
            edit["sourceByte4FHex"] = "0x" + moby.GemSourceByte4FOverride.ToString("X2");
            return edit;
        }

        private static Dictionary<string, object> NewRewardColorEdit(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            bool containedGem = IsContainedGemMarker(moby);
            bool lockedChestReward = IsLockedChestRewardMarker(moby);
            bool springChestReward = IsSpringChestRewardMarker(moby);
            edit["mode"] = containedGem ? "contained-gem-marker" : (lockedChestReward ? "locked-chest-reward-byte" : (springChestReward ? "spring-chest-reward-byte" : "type20-reward-byte"));
            edit["color"] = moby.RewardColorName;
            edit["value"] = moby.RewardValueOverride;
            edit["sourceByte53Hex"] = "0x" + moby.RewardByte53Override.ToString("X2");
            edit["validation"] = containedGem
                ? "Inferred for inactive locked-chest content markers. Patch writes the marker +0x53 contained-gem value byte."
                : (lockedChestReward
                    ? "Locked chest controller reward byte. Patch writes +0x53 so the chest's own spawned gem matches its linked contents."
                    : (springChestReward
                        ? "Spring chest reward byte. Patch writes +0x53 so the spring chest itself spawns the selected gem value."
                        : "Confirmed on Artisans Flame/Charge chest T50; expected to apply to matching type 0x20 reward-bearing chests/enemies."));
            return edit;
        }

        private static Dictionary<string, object> NewRecordCloneMutation(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["mode"] = "cloneIntoSlot";
            edit["sourceIndex"] = moby.RecordCloneSourceIndex;
            edit["sourceTrueIndex"] = moby.RecordCloneSourceTrueIndex;
            edit["sourceLabel"] = moby.RecordCloneSourceLabel ?? "";
            edit["note"] = "Slot reuse/add: clone the source loader record into this target slot, then write the target XYZ and supported source-byte edits.";
            return edit;
        }

        private static Dictionary<string, object> NewAppendMutation(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["mode"] = "appendFromSource";
            edit["sourceIndex"] = moby.AppendSourceIndex;
            edit["sourceTrueIndex"] = moby.AppendSourceTrueIndex;
            edit["sourceLabel"] = moby.AppendSourceLabel ?? "";
            if (!string.IsNullOrEmpty(moby.AppendSourceLevelKey))
                edit["sourceLevelKey"] = moby.AppendSourceLevelKey;
            if (!string.IsNullOrEmpty(moby.AppendSourceLevelName))
                edit["sourceLevelName"] = moby.AppendSourceLevelName;
            if (!string.IsNullOrEmpty(moby.AppendSourceFamily))
                edit["sourceFamily"] = moby.AppendSourceFamily;
            if (!string.IsNullOrEmpty(moby.AppendPackageImportProfile))
                edit["packageImportProfile"] = moby.AppendPackageImportProfile;
            if (moby.AppendLoaderTransformedDonor)
            {
                edit["loaderTransformedDonor"] = true;
                edit["runtimeIdentityPolicy"] = string.IsNullOrEmpty(moby.AppendRuntimeIdentityPolicy) ? "force-template-runtime-identity-bytes" : moby.AppendRuntimeIdentityPolicy;
            }
            else if (!string.IsNullOrEmpty(moby.AppendRuntimeIdentityPolicy))
                edit["runtimeIdentityPolicy"] = moby.AppendRuntimeIdentityPolicy;
            if (!string.IsNullOrEmpty(moby.AppendDonorMapConfidence))
                edit["donorMapConfidence"] = moby.AppendDonorMapConfidence;
            if (!string.IsNullOrEmpty(moby.AppendDependencyRisk))
                edit["dependencyRisk"] = moby.AppendDependencyRisk;
            edit["targetTrueIndex"] = moby.TrueIndex;
            edit["note"] = "True add: increment the level source moby count and append a cloned source record at this target true index.";
            return edit;
        }

        private static Dictionary<string, object> NewSpringChestRuntimeHelperEdit(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["mode"] = "spring-chest-runtime-helper-pair";
            edit["role"] = moby.SpringChestPairRole ?? "";
            edit["partnerTrueIndex"] = moby.SpringChestPartnerTrueIndex;
            edit["note"] = "The Artisans/Stone Hill spring chest test exporter uses this pair to inject the in-game pop, collectible reward gem, and post-collect hide behavior.";
            return edit;
        }

        private static Dictionary<string, object> NewHideMutation(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["mode"] = "hide";
            edit["note"] = "Soft remove: keep the source-table record but move XYZ far out of bounds.";
            return edit;
        }

        private static List<Dictionary<string, object>> NewGemSourceByteEdits(Moby moby)
        {
            List<Dictionary<string, object>> edits = new List<Dictionary<string, object>>();
            edits.Add(NewSourceByteEdit(0x36, moby.GemSourceByte36Override, "gem-id-byte"));
            edits.Add(NewSourceByteEdit(0x4F, moby.GemSourceByte4FOverride, "gem-value-byte"));
            return edits;
        }

        private static List<Dictionary<string, object>> NewRewardSourceByteEdits(Moby moby)
        {
            List<Dictionary<string, object>> edits = new List<Dictionary<string, object>>();
            edits.Add(NewSourceByteEdit(0x53, moby.RewardByte53Override, IsContainedGemMarker(moby) ? "contained-gem-id-byte" : "type20-reward-gem-id-byte"));
            return edits;
        }

        private static Dictionary<string, object> NewChestContentLinkEdit(Moby moby)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["mode"] = "contained-gem-chest-link";
            edit["chestIndex"] = moby.ChestContentLinkIndex;
            edit["chestTrueIndex"] = moby.ChestContentLinkTrueIndex;
            edit["chestLabel"] = moby.ChestContentLinkLabel ?? "";
            edit["rawOffset"] = NewRawVectorFromRaw(moby.ChestContentRawOffsetX, moby.ChestContentRawOffsetY, moby.ChestContentRawOffsetZ);
            edit["note"] = "Patch contained-gem special data +0x00 to the owning chest true index and +0x04/+0x08/+0x0C to the gem explosion offset.";
            return edit;
        }

        private static Dictionary<string, object> NewRawVectorFromRaw(int x, int y, int z)
        {
            Dictionary<string, object> vector = new Dictionary<string, object>();
            vector["x"] = x;
            vector["y"] = y;
            vector["z"] = z;
            return vector;
        }

        private static bool IsContainedGemMarkerEdit(Moby moby, Dictionary<string, object> rewardColorEdit)
        {
            string mode = GetString(rewardColorEdit, "mode", "");
            if (string.Equals(mode, "contained-gem-marker", StringComparison.OrdinalIgnoreCase))
                return true;
            return IsContainedGemMarker(moby);
        }

        private static bool IsContainedGemMarker(Moby moby)
        {
            if (moby == null) return false;
            if (moby.Type != 0x00 || moby.Flag4A != 0xFF)
                return false;
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.Evidence ?? "")).ToLowerInvariant();
            return text.IndexOf("chest content", StringComparison.Ordinal) >= 0
                || text.IndexOf("contained gem", StringComparison.Ordinal) >= 0
                || text.IndexOf("gem explosion", StringComparison.Ordinal) >= 0
                || text.IndexOf("linked reward marker", StringComparison.Ordinal) >= 0
                || text.IndexOf("reward marker", StringComparison.Ordinal) >= 0;
        }

        private static bool IsLockedChestRewardMarker(Moby moby)
        {
            if (moby == null || GemValueFromSavedGemIdByte(moby.Flag4B) <= 0) return false;
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.Evidence ?? "")).ToLowerInvariant();
            return text.IndexOf("locked chest", StringComparison.Ordinal) >= 0
                || text.IndexOf("locked container", StringComparison.Ordinal) >= 0
                || text.IndexOf("unlock chest", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSpringChestRewardMarker(Moby moby)
        {
            if (moby == null) return false;
            if (moby.Type != 0x20 || moby.Flag4A != 0x10)
                return false;
            string text = ((moby.DisplayLabel ?? "") + " " + (moby.Kind ?? "") + " " + (moby.Evidence ?? "")).ToLowerInvariant();
            return text.IndexOf("spring chest", StringComparison.Ordinal) >= 0 || moby.SpecialDataPointer == 0x8016B9F8;
        }

        private static int GemValueFromSavedGemIdByte(int value)
        {
            switch (value)
            {
                case 0x53: return 1;
                case 0x54: return 2;
                case 0x55: return 5;
                case 0x56: return 10;
                case 0x57: return 25;
                default: return 0;
            }
        }

        private static Dictionary<string, object> NewSourceByteEdit(int offset, int value, string field)
        {
            Dictionary<string, object> edit = new Dictionary<string, object>();
            edit["offset"] = offset;
            edit["offsetHex"] = "0x" + offset.ToString("X2");
            edit["value"] = value;
            edit["valueHex"] = "0x" + value.ToString("X2");
            edit["field"] = field;
            return edit;
        }

        private static string InferGemColorFromSourceByteEdits(Dictionary<string, object> edit)
        {
            int byte36 = -1;
            int byte4F = -1;
            object[] sourceEdits = GeometryLoader.GetArray(edit, "sourceByteEdits");
            foreach (object obj in sourceEdits)
            {
                Dictionary<string, object> sourceEdit = obj as Dictionary<string, object>;
                if (sourceEdit == null) continue;
                int offset = GetInt(sourceEdit, "offset", -1);
                int value = GetInt(sourceEdit, "value", -1);
                if (offset == 0x36) byte36 = value;
                if (offset == 0x4F) byte4F = value;
            }
            if (byte36 == 0x54 || byte4F == 0x02) return "green";
            if (byte36 == 0x53 || byte4F == 0x01) return "red";
            if (byte36 == 0x55 || byte4F == 0x03) return "blue";
            if (byte36 == 0x56 || byte4F == 0x04) return "yellow";
            if (byte36 == 0x57 || byte4F == 0x05) return "purple";
            return "";
        }

        private static string InferRewardColorFromSourceByteEdits(Dictionary<string, object> edit)
        {
            int byte53 = -1;
            object[] sourceEdits = GeometryLoader.GetArray(edit, "sourceByteEdits");
            foreach (object obj in sourceEdits)
            {
                Dictionary<string, object> sourceEdit = obj as Dictionary<string, object>;
                if (sourceEdit == null) continue;
                int offset = GetInt(sourceEdit, "offset", -1);
                int value = GetInt(sourceEdit, "value", -1);
                if (offset == 0x53) byte53 = value;
            }
            if (byte53 == 0x54) return "green";
            if (byte53 == 0x53) return "red";
            if (byte53 == 0x55) return "blue";
            if (byte53 == 0x56) return "yellow";
            if (byte53 == 0x57) return "purple";
            return "";
        }

        private static int SourceRecordCountForLevel(string levelKey)
        {
            return SpyroLevelCatalog.SourceRecordCountForKey(levelKey);
        }

        private static int NextAppendedTrueIndex(List<Moby> mobys, int sourceRecordCount)
        {
            int next = sourceRecordCount;
            if (mobys == null) return next;
            foreach (Moby moby in mobys)
            {
                if (moby != null && moby.IsAppendedRecord && moby.TrueIndex >= next)
                    next = moby.TrueIndex + 1;
            }
            return next;
        }

        private static int ToRawCoordinate(float value)
        {
            return (int)Math.Round(value * 16f);
        }

        private static string FormatAddress(uint value)
        {
            return "0x" + value.ToString("X8");
        }

        private static int GetInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        private static bool GetBool(Dictionary<string, object> dict, string name, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToBoolean(dict[name]); }
            catch { return fallback; }
        }

        private static int GetFlexibleInt(Dictionary<string, object> dict, string name, int fallback)
        {
            long value = GetFlexibleInt64(dict, name, fallback);
            if (value < int.MinValue || value > int.MaxValue) return fallback;
            return (int)value;
        }

        private static long GetFlexibleInt64(Dictionary<string, object> dict, string name, long fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try
            {
                string text = Convert.ToString(dict[name]).Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt64(text.Substring(2), 16);
                if (text.Length == 0) return fallback;
                return Convert.ToInt64(dict[name]);
            }
            catch { return fallback; }
        }

        private static string GetString(Dictionary<string, object> dict, string name, string fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            return Convert.ToString(dict[name]);
        }
    }

    internal static class TerrainEditStore
    {
        public static int Save(string path, List<TerrainPolygon> polygons, string levelName)
        {
            List<Dictionary<string, object>> edits = new List<Dictionary<string, object>>();
            if (polygons != null)
            {
                foreach (TerrainPolygon polygon in polygons)
                {
                    if (polygon == null || !polygon.IsTerrainEdited) continue;
                    Dictionary<string, object> edit = new Dictionary<string, object>();
                    edit["runtimeKey"] = polygon.RuntimeKey;
                    edit["sectorIndex"] = polygon.SectorIndex;
                    edit["faceIndex"] = polygon.FaceIndex;
                    edit["detail"] = polygon.Detail;
                    edit["sectorOffset"] = polygon.SectorOffset;
                    edit["faceOffset"] = polygon.FaceOffset;
                    edit["textureId"] = polygon.OriginalTextureId;
                    if (polygon.HasTextureEdit)
                    {
                        edit["textureEditMode"] = "texture-id-preview";
                        edit["textureIdOriginal"] = polygon.OriginalTextureId;
                        edit["textureIdEdited"] = polygon.TextureId;
                    }
                    edit["word3"] = polygon.Word3;
                    edit["word4"] = polygon.Word4;
                    edit["deltaZ"] = polygon.TerrainEditDeltaZ;
                    edit["vertexIndexes"] = IntArray(polygon.VertexIndexes);
                    edit["originalZ"] = FloatArray(polygon.OriginalZValues);
                    edit["editedZ"] = FloatArray(polygon.ZValues);
                    edits.Add(edit);
                }
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["generatedAt"] = DateTime.Now.ToString("s");
            root["editor"] = "NativeSpyroEditor";
            root["levelName"] = string.IsNullOrEmpty(levelName) ? "Unknown" : levelName;
            root["note"] = "Editor-side terrain edits keyed by runtime scene-sector face handles. Z edits and same-level texture-id swaps are ready for the Stone Hill runtime/source terrain patch exporter. Custom PNG texture-page imports are saved separately in <level>-custom-terrain-textures.json.";
            root["editCount"] = edits.Count;
            root["edits"] = edits;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            File.WriteAllText(path, serializer.Serialize(root), Encoding.UTF8);
            return edits.Count;
        }

        public static int Load(string path, List<TerrainPolygon> polygons)
        {
            if (polygons != null)
            {
                foreach (TerrainPolygon polygon in polygons)
                    polygon.ResetTerrainEdit();
            }
            if (!File.Exists(path) || polygons == null) return 0;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null) return 0;

            Dictionary<string, TerrainPolygon> byKey = new Dictionary<string, TerrainPolygon>();
            foreach (TerrainPolygon polygon in polygons)
            {
                if (polygon != null && !string.IsNullOrEmpty(polygon.RuntimeKey))
                    byKey[polygon.RuntimeKey] = polygon;
            }

            int applied = 0;
            foreach (object obj in GeometryLoader.GetArray(root, "edits"))
            {
                Dictionary<string, object> edit = obj as Dictionary<string, object>;
                if (edit == null) continue;
                string key = GetString(edit, "runtimeKey", "");
                if (string.IsNullOrEmpty(key))
                    key = GetInt(edit, "sectorIndex", -1).ToString() + ":" + GetInt(edit, "faceIndex", -1).ToString() + ":" + GetString(edit, "detail", "");
                TerrainPolygon polygon;
                if (!byKey.TryGetValue(key, out polygon)) continue;
                polygon.ApplyTerrainDeltaZ(GetFloat(edit, "deltaZ", 0f));
                int textureIdEdited = GetInt(edit, "textureIdEdited", -1);
                if (textureIdEdited >= 0)
                    polygon.ApplyTextureOverride(textureIdEdited);
                applied++;
            }
            return applied;
        }

        private static object[] IntArray(int[] values)
        {
            if (values == null) return new object[0];
            object[] result = new object[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = values[i];
            return result;
        }

        private static object[] FloatArray(float[] values)
        {
            if (values == null) return new object[0];
            object[] result = new object[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = values[i];
            return result;
        }

        private static int GetInt(Dictionary<string, object> dict, string name, int fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToInt32(dict[name]); }
            catch { return fallback; }
        }

        private static float GetFloat(Dictionary<string, object> dict, string name, float fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            try { return Convert.ToSingle(dict[name]); }
            catch { return fallback; }
        }

        private static string GetString(Dictionary<string, object> dict, string name, string fallback)
        {
            if (dict == null || !dict.ContainsKey(name) || dict[name] == null) return fallback;
            return Convert.ToString(dict[name]);
        }
    }

    internal sealed class TerrainTextureChoice
    {
        public readonly string LevelKey;
        public readonly string LevelName;
        public readonly int TextureId;
        public readonly Color SampleColor;
        public int FaceCount;

        public TerrainTextureChoice(string levelKey, string levelName, int textureId, TerrainPolygon sample)
        {
            LevelKey = levelKey ?? "";
            LevelName = string.IsNullOrEmpty(levelName) ? LevelKey : levelName;
            TextureId = textureId;
            SampleColor = sample != null && sample.HasFaceColor
                ? sample.FaceColor
                : (sample != null ? sample.MaterialFill : Color.LightGray);
        }

        public string SampleText
        {
            get { return LevelName + " texture " + TextureId.ToString(); }
        }

        public string DisplayText
        {
            get { return LevelName + " texture ID " + TextureId.ToString(); }
        }

        public override string ToString()
        {
            return "ID " + TextureId.ToString() + " - " + FaceCount.ToString() + " faces - " + LevelName;
        }
    }

    internal sealed class CustomTerrainTexture
    {
        public readonly int TextureId;
        public readonly string SourceImagePath;
        public readonly string SourceImageName;
        public readonly string DescriptorTier;
        public readonly int TileSize;
        public readonly Bitmap PreviewImage;

        public CustomTerrainTexture(int textureId, string sourceImagePath, string sourceImageName, string descriptorTier, int tileSize, Bitmap previewImage)
        {
            TextureId = textureId;
            SourceImagePath = sourceImagePath ?? "";
            SourceImageName = string.IsNullOrEmpty(sourceImageName) ? Path.GetFileName(SourceImagePath) : sourceImageName;
            DescriptorTier = string.IsNullOrEmpty(descriptorTier) ? "hqData" : descriptorTier;
            TileSize = tileSize > 0 ? tileSize : 64;
            PreviewImage = previewImage;
        }
    }

    internal sealed class GeometryCandidate
    {
        public string Name;
        public RectangleF Bounds;
        public float MinZ;
        public float MaxZ;
        public readonly List<TerrainPolygon> Polygons = new List<TerrainPolygon>();
        public readonly List<TerrainEdge> Edges = new List<TerrainEdge>();
    }

    internal sealed class TerrainPoint3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public TerrainPoint3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    internal sealed class TerrainPolygon
    {
        public readonly PointF[] Points;
        public readonly float[] ZValues;
        public readonly float[] OriginalZValues;
        public float AvgZ;
        public float MinZ;
        public float MaxZ;
        public float TerrainEditDeltaZ;
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly RectangleF Bounds;
        public readonly Color HeightFill;
        public readonly Color GameFill;
        public readonly int OriginalTextureId;
        public int TextureId;
        public readonly Color MaterialFill;
        public readonly int SectorIndex;
        public readonly int FaceIndex;
        public readonly string SectorOffset;
        public readonly string FaceOffset;
        public readonly string Detail;
        public readonly bool FaceFlip;
        public readonly int FaceDepth;
        public readonly string Word3;
        public readonly string Word4;
        public readonly int[] VertexIndexes;
        public readonly int[] ColourIndexes;
        public readonly bool HasFaceColor;
        public readonly Color FaceColor;
        public readonly TerrainPoint3 TextureTopLeft;
        public readonly TerrainPoint3 TextureTopRight;
        public readonly TerrainPoint3 TextureBottomLeft;

        public bool HasTextureId
        {
            get { return TextureId >= 0; }
        }

        public bool HasTextureEdit
        {
            get { return OriginalTextureId >= 0 && TextureId != OriginalTextureId; }
        }

        public bool HasTextureCorners
        {
            get { return TextureTopLeft != null && TextureTopRight != null && TextureBottomLeft != null; }
        }

        public TerrainPolygon(PointF[] points, float[] zValues, float avgZ, Color heightFill, Color gameFill, int textureId, Color materialFill, bool faceFlip, int faceDepth, string word3, string word4, int[] vertexIndexes, int[] colourIndexes, bool hasFaceColor, Color faceColor, TerrainPoint3 textureTopLeft, TerrainPoint3 textureTopRight, TerrainPoint3 textureBottomLeft, int sectorIndex, int faceIndex, string sectorOffset, string faceOffset, string detail)
        {
            Points = points;
            ZValues = zValues;
            OriginalZValues = zValues == null ? new float[0] : (float[])zValues.Clone();
            AvgZ = avgZ;
            HeightFill = heightFill;
            GameFill = gameFill;
            OriginalTextureId = textureId;
            TextureId = textureId;
            MaterialFill = materialFill;
            SectorIndex = sectorIndex;
            FaceIndex = faceIndex;
            SectorOffset = sectorOffset ?? "";
            FaceOffset = faceOffset ?? "";
            Detail = detail ?? "";
            FaceFlip = faceFlip;
            FaceDepth = faceDepth;
            Word3 = word3 ?? "";
            Word4 = word4 ?? "";
            VertexIndexes = vertexIndexes ?? new int[0];
            ColourIndexes = colourIndexes ?? new int[0];
            HasFaceColor = hasFaceColor;
            FaceColor = faceColor;
            TextureTopLeft = textureTopLeft;
            TextureTopRight = textureTopRight;
            TextureBottomLeft = textureBottomLeft;
            MinZ = zValues != null && zValues.Length > 0 ? zValues[0] : avgZ;
            MaxZ = MinZ;
            float sumX = 0f;
            float sumY = 0f;
            if (points != null)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    sumX += points[i].X;
                    sumY += points[i].Y;
                }
            }
            if (zValues != null)
            {
                for (int i = 0; i < zValues.Length; i++)
                {
                    MinZ = Math.Min(MinZ, zValues[i]);
                    MaxZ = Math.Max(MaxZ, zValues[i]);
                }
            }
            CenterX = points != null && points.Length > 0 ? sumX / points.Length : 0f;
            CenterY = points != null && points.Length > 0 ? sumY / points.Length : 0f;
            Bounds = BoundsOf(points);
        }

        public bool IsTerrainEdited
        {
            get { return Math.Abs(TerrainEditDeltaZ) > 0.001f || HasTextureEdit; }
        }

        public string RuntimeKey
        {
            get { return SectorIndex.ToString() + ":" + FaceIndex.ToString() + ":" + Detail; }
        }

        public void ApplyTerrainDeltaZ(float deltaZ)
        {
            TerrainEditDeltaZ = deltaZ;
            if (ZValues != null && OriginalZValues != null)
            {
                int count = Math.Min(ZValues.Length, OriginalZValues.Length);
                for (int i = 0; i < count; i++)
                    ZValues[i] = OriginalZValues[i] + deltaZ;
            }
            RecomputeZStats();
        }

        public void ResetTerrainEdit()
        {
            ApplyTerrainDeltaZ(0f);
            ResetTextureEdit();
        }

        public void ApplyTextureOverride(int textureId)
        {
            if (textureId < 0) return;
            TextureId = textureId;
        }

        public void ResetTextureEdit()
        {
            TextureId = OriginalTextureId;
        }

        private void RecomputeZStats()
        {
            if (ZValues == null || ZValues.Length == 0)
                return;
            float sum = 0f;
            MinZ = ZValues[0];
            MaxZ = ZValues[0];
            for (int i = 0; i < ZValues.Length; i++)
            {
                sum += ZValues[i];
                MinZ = Math.Min(MinZ, ZValues[i]);
                MaxZ = Math.Max(MaxZ, ZValues[i]);
            }
            AvgZ = sum / ZValues.Length;
        }

        public bool TryGetZ(float x, float y, out float z)
        {
            z = AvgZ;
            if (Points == null || ZValues == null || Points.Length < 3 || ZValues.Length < Points.Length)
                return false;

            for (int i = 1; i < Points.Length - 1; i++)
            {
                if (TryInterpolateTriangle(
                    Points[0], ZValues[0],
                    Points[i], ZValues[i],
                    Points[i + 1], ZValues[i + 1],
                    x, y, out z))
                    return true;
            }

            if (ContainsPoint(x, y))
            {
                z = AvgZ;
                return true;
            }
            return false;
        }

        public bool ContainsXY(float x, float y)
        {
            return ContainsPoint(x, y);
        }

        private static bool TryInterpolateTriangle(PointF a, float az, PointF b, float bz, PointF c, float cz, float x, float y, out float z)
        {
            z = 0f;
            float denom = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
            if (Math.Abs(denom) < 0.0001f) return false;

            float u = (((b.Y - c.Y) * (x - c.X)) + ((c.X - b.X) * (y - c.Y))) / denom;
            float v = (((c.Y - a.Y) * (x - c.X)) + ((a.X - c.X) * (y - c.Y))) / denom;
            float w = 1f - u - v;
            const float eps = -0.001f;
            if (u < eps || v < eps || w < eps) return false;
            z = (u * az) + (v * bz) + (w * cz);
            return true;
        }

        private bool ContainsPoint(float x, float y)
        {
            bool inside = false;
            int j = Points.Length - 1;
            for (int i = 0; i < Points.Length; i++)
            {
                float yi = Points[i].Y;
                float yj = Points[j].Y;
                if (((yi > y) != (yj > y)) &&
                    (x < ((Points[j].X - Points[i].X) * (y - yi) / ((yj - yi) == 0f ? 0.0001f : (yj - yi))) + Points[i].X))
                    inside = !inside;
                j = i;
            }
            return inside;
        }

        private static RectangleF BoundsOf(PointF[] points)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < points.Length; i++)
            {
                minX = Math.Min(minX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxX = Math.Max(maxX, points[i].X);
                maxY = Math.Max(maxY, points[i].Y);
            }
            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }
    }

    internal sealed class TerrainDrawItem
    {
        public readonly TerrainPolygon Polygon;
        public readonly float Depth;

        public TerrainDrawItem(TerrainPolygon polygon, float depth)
        {
            Polygon = polygon;
            Depth = depth;
        }
    }

    internal sealed class TerrainEdge
    {
        public readonly float X1;
        public readonly float Y1;
        public readonly float X2;
        public readonly float Y2;
        public readonly RectangleF Bounds;

        public TerrainEdge(float x1, float y1, float x2, float y2)
        {
            X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
            Bounds = RectangleF.FromLTRB(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
        }
    }

    internal sealed class MobyIdentityOverrideDialog : Form
    {
        private readonly TextBox nameBox;
        private readonly TextBox kindBox;
        private readonly CheckBox signatureBox;

        public string IdentityName
        {
            get { return nameBox.Text; }
        }

        public string IdentityKind
        {
            get { return kindBox.Text; }
        }

        public bool ApplyToMatchingSignature
        {
            get { return signatureBox != null && signatureBox.Checked && signatureBox.Enabled; }
        }

        public MobyIdentityOverrideDialog(string mobyId, string currentName, string currentKind, int signatureMatchCount)
        {
            Text = "Edit " + (string.IsNullOrEmpty(mobyId) ? "moby" : mobyId) + " editor identity";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 210);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 2;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            Label nameLabel = new Label();
            nameLabel.Text = "Editor name";
            nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            nameLabel.Dock = DockStyle.Fill;
            root.Controls.Add(nameLabel, 0, 0);

            nameBox = new TextBox();
            nameBox.Dock = DockStyle.Fill;
            nameBox.Text = currentName ?? "";
            root.Controls.Add(nameBox, 1, 0);

            Label kindLabel = new Label();
            kindLabel.Text = "Editor type/kind";
            kindLabel.TextAlign = ContentAlignment.MiddleLeft;
            kindLabel.Dock = DockStyle.Fill;
            root.Controls.Add(kindLabel, 0, 1);

            kindBox = new TextBox();
            kindBox.Dock = DockStyle.Fill;
            kindBox.Text = currentKind ?? "";
            root.Controls.Add(kindBox, 1, 1);

            signatureBox = new CheckBox();
            signatureBox.Dock = DockStyle.Fill;
            signatureBox.TextAlign = ContentAlignment.MiddleLeft;
            signatureBox.Text = signatureMatchCount > 1
                ? "Apply to " + signatureMatchCount.ToString() + " matching records"
                : "No safe matching behavior batch";
            signatureBox.Enabled = signatureMatchCount > 1;
            root.SetColumnSpan(signatureBox, 2);
            root.Controls.Add(signatureBox, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.Padding = new Padding(0, 10, 0, 0);
            root.SetColumnSpan(buttons, 2);
            root.Controls.Add(buttons, 0, 3);

            Button okButton = new Button();
            okButton.Text = "Save";
            okButton.Width = 86;
            okButton.DialogResult = DialogResult.OK;
            buttons.Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 86;
            cancelButton.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }
    }

    internal sealed class SelectionGroup
    {
        public readonly string Key;
        public readonly string Name;
        public readonly bool LinkedMove;
        public readonly List<int> Members = new List<int>();

        public SelectionGroup(string key, string name, bool linkedMove)
        {
            Key = key ?? "";
            Name = name ?? "";
            LinkedMove = linkedMove;
        }

        public string DisplayName
        {
            get { return Name + " (" + Members.Count.ToString() + ")"; }
        }

        public void Add(int mobyIndex)
        {
            if (mobyIndex < 0) return;
            if (!Members.Contains(mobyIndex))
                Members.Add(mobyIndex);
        }

        public bool Contains(int mobyIndex)
        {
            return Members.Contains(mobyIndex);
        }

        public void Sort()
        {
            Members.Sort();
        }
    }

    internal sealed class MobyChoice
    {
        public readonly int Index;
        public readonly string Text;
        public readonly ObjectTemplate Template;

        public MobyChoice(int index, string text)
            : this(index, text, null)
        {
        }

        public MobyChoice(int index, string text, ObjectTemplate template)
        {
            Index = index;
            Text = text ?? "";
            Template = template;
        }

        public bool IsExternalTemplate
        {
            get { return Template != null; }
        }

        public override string ToString()
        {
            return Text;
        }
    }

    internal sealed class Moby
    {
        public int Index;
        public int TrueIndex = -1;
        public int LegacyIndex = -1;
        public float X;
        public float Y;
        public float Z;
        public float OriginalX;
        public float OriginalY;
        public float OriginalZ;
        public int Type;
        public int State;
        public uint RuntimeAddress;
        public uint SpecialDataPointer;
        public int SourceByte36;
        public int SourceByte37;
        public int SourceByte4F;
        public int Flag4A;
        public int Flag4B;
        public Color Color;
        public string Label;
        public string Zone;
        public string Kind;
        public string Confidence;
        public string Evidence;
        public string BehaviorNote;
        public string SpecialDataNote;
        public string PatchStatus;
        public string PatchLead;
        public int PatchPriority;
        public bool HasGroundOffset;
        public float GroundOffset;
        public float OriginalGroundOffset;
        public bool HasBaseIdentity;
        public string BaseLabel;
        public string BaseKind;
        public string BaseConfidence;
        public string BaseEvidence;
        public Color BaseColor;
        public int BaseType;
        public int BaseState;
        public uint BaseSpecialDataPointer;
        public int BaseSourceByte36;
        public int BaseSourceByte37;
        public int BaseSourceByte4F;
        public int BaseFlag4A;
        public int BaseFlag4B;
        public string GemColorOverride;
        public int GemValueOverride;
        public int GemSourceByte36Override = -1;
        public int GemSourceByte4FOverride = -1;
        public string RewardColorOverride;
        public int RewardValueOverride;
        public int RewardByte53Override = -1;
        public bool HasHiddenSlotEdit;
        public int RecordCloneSourceTrueIndex = -1;
        public int RecordCloneSourceIndex = -1;
        public string RecordCloneSourceLabel;
        public bool IsAppendedRecord;
        public int AppendSourceTrueIndex = -1;
        public int AppendSourceIndex = -1;
        public string AppendSourceLabel;
        public string AppendSourceLevelKey;
        public string AppendSourceLevelName;
        public string AppendSourceFamily;
        public bool AppendLoaderTransformedDonor;
        public string AppendDonorMapConfidence;
        public string AppendDependencyRisk;
        public string AppendPackageImportProfile;
        public string AppendRuntimeIdentityPolicy;
        public string SpringChestPairRole;
        public int SpringChestPartnerTrueIndex = -1;
        public int ChestContentLinkTrueIndex = -1;
        public int ChestContentLinkIndex = -1;
        public string ChestContentLinkLabel;
        public int ChestContentRawOffsetX;
        public int ChestContentRawOffsetY;
        public int ChestContentRawOffsetZ;

        public string DisplayLabel
        {
            get
            {
                return string.IsNullOrEmpty(Label) ? "0x" + Type.ToString("X2") : Label;
            }
        }

        public bool IsEdited
        {
            get
            {
                return IsAppendedRecord || HasPositionEdit || HasGemColorEdit || HasRewardColorEdit || HasHiddenSlotEdit || HasRecordCloneEdit || HasChestContentLinkEdit;
            }
        }

        public bool HasPositionEdit
        {
            get
            {
                return Math.Abs(X - OriginalX) > 0.01f || Math.Abs(Y - OriginalY) > 0.01f || Math.Abs(Z - OriginalZ) > 0.01f;
            }
        }

        public bool HasGemColorEdit
        {
            get { return !string.IsNullOrEmpty(GemColorOverride); }
        }

        public bool IsStandaloneGemSourceRecord
        {
            get { return Type == 0x18 && SpecialDataPointer == 0 && Flag4A == 0x40 && Flag4B == 0xFF; }
        }

        public string SourceGemColorName
        {
            get { return GemColorFromSourceBytes(SourceByte36, SourceByte4F); }
        }

        public string GemColorName
        {
            get { return string.IsNullOrEmpty(GemColorOverride) ? "" : GemColorOverride; }
        }

        public bool HasRewardColorEdit
        {
            get { return !string.IsNullOrEmpty(RewardColorOverride); }
        }

        public string RewardColorName
        {
            get { return string.IsNullOrEmpty(RewardColorOverride) ? "" : RewardColorOverride; }
        }

        public bool HasRecordCloneEdit
        {
            get { return RecordCloneSourceTrueIndex >= 0; }
        }

        public bool HasChestContentLinkEdit
        {
            get { return ChestContentLinkTrueIndex >= 0; }
        }

        public bool Patchable
        {
            get
            {
                return !string.IsNullOrEmpty(PatchStatus) && PatchStatus.IndexOf("patchable", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static Moby CreateAppendedFromSource(Moby source, int listIndex, int appendTrueIndex)
        {
            if (source == null) throw new ArgumentNullException("source");
            Moby moby = new Moby();
            moby.Index = listIndex;
            moby.TrueIndex = appendTrueIndex;
            moby.LegacyIndex = -1;
            moby.X = source.X;
            moby.Y = source.Y;
            moby.Z = source.Z;
            moby.OriginalX = source.X;
            moby.OriginalY = source.Y;
            moby.OriginalZ = source.Z;
            moby.Type = source.Type;
            moby.State = source.State;
            moby.RuntimeAddress = 0;
            moby.SpecialDataPointer = source.SpecialDataPointer;
            moby.SourceByte36 = source.SourceByte36;
            moby.SourceByte37 = source.SourceByte37;
            moby.SourceByte4F = source.SourceByte4F;
            moby.Flag4A = source.Flag4A;
            moby.Flag4B = source.Flag4B;
            moby.Color = source.Color;
            moby.Label = source.DisplayLabel + " (new)";
            moby.Zone = source.Zone;
            moby.Kind = source.Kind;
            moby.Confidence = "editor true-add";
            moby.Evidence = "new source record appended from donor T" + source.TrueIndex.ToString() + " by Create Loader BIN";
            moby.BehaviorNote = source.BehaviorNote;
            moby.SpecialDataNote = source.SpecialDataNote;
            moby.PatchStatus = "append-patchable";
            moby.PatchLead = "append source record T" + appendTrueIndex.ToString() + " from donor T" + source.TrueIndex.ToString();
            moby.PatchPriority = 1;
            moby.IsAppendedRecord = true;
            moby.AppendSourceTrueIndex = source.TrueIndex;
            moby.AppendSourceIndex = source.Index;
            moby.AppendSourceLabel = source.DisplayLabel;
            moby.AppendSourceLevelKey = source.AppendSourceLevelKey;
            moby.AppendSourceLevelName = source.AppendSourceLevelName;
            moby.AppendSourceFamily = source.AppendSourceFamily;
            moby.AppendPackageImportProfile = source.AppendPackageImportProfile;
            moby.AppendRuntimeIdentityPolicy = source.AppendRuntimeIdentityPolicy;
            moby.HasGroundOffset = source.HasGroundOffset;
            moby.GroundOffset = source.GroundOffset;
            moby.OriginalGroundOffset = source.OriginalGroundOffset;
            moby.CaptureBaseIdentity();
            if (source.HasGemColorEdit)
                moby.SetGemColorOverride(source.GemColorName);
            if (source.HasRewardColorEdit)
                moby.SetRewardColorOverride(source.RewardColorName);
            return moby;
        }

        public static Moby CreateSpringChestControllerFromTemplate(ObjectTemplate template, int listIndex, int appendTrueIndex)
        {
            if (template == null) throw new ArgumentNullException("template");
            Moby moby = CreateAppendedFromTemplate(template, listIndex, appendTrueIndex);
            moby.SourceByte36 = 0xFE;
            moby.SourceByte37 = 0x01;
            moby.SourceByte4F = 0x00;
            moby.Flag4A = 0x10;
            moby.Flag4B = 0x53;
            moby.Label = "Spring Chest controller (new)";
            moby.Kind = "Spring Chest controller";
            moby.Confidence = "editor object library spring chest pair";
            moby.Evidence = "new paired spring chest controller cloned from Town Square T30 by Test Selected Add BIN";
            moby.BehaviorNote = "Paired with the visible spring chest shell. The Artisans in-game patch uses this controller row for hit/pop state.";
            moby.PatchLead = "append source record T" + appendTrueIndex.ToString() + " from Town Square spring controller T30";
            moby.AppendSourceTrueIndex = 30;
            moby.AppendSourceIndex = -1;
            moby.AppendSourceLabel = "Spring Chest controller";
            moby.AppendSourceFamily = "springChest";
            moby.CaptureBaseIdentity();
            return moby;
        }

        public static Moby CreateAppendedFromTemplate(ObjectTemplate template, int listIndex, int appendTrueIndex)
        {
            if (template == null) throw new ArgumentNullException("template");
            Moby moby = new Moby();
            moby.Index = listIndex;
            moby.TrueIndex = appendTrueIndex;
            moby.LegacyIndex = -1;
            moby.X = 0;
            moby.Y = 0;
            moby.Z = 0;
            moby.OriginalX = 0;
            moby.OriginalY = 0;
            moby.OriginalZ = 0;
            moby.Type = template.Type;
            moby.State = template.State;
            moby.RuntimeAddress = 0;
            moby.SpecialDataPointer = template.SpecialDataPointer;
            moby.SourceByte36 = template.SourceByte36;
            moby.SourceByte37 = template.SourceByte37;
            moby.SourceByte4F = template.SourceByte4F;
            moby.Flag4A = template.Flag4A;
            moby.Flag4B = template.Flag4B;
            moby.Color = template.Color;
            moby.Label = template.DisplayName + " (new)";
            moby.Zone = "Object Library";
            moby.Kind = string.IsNullOrEmpty(template.Kind) ? template.Family : template.Kind;
            moby.Confidence = "editor object library true-add";
            moby.Evidence = "new source record appended from " + template.SourceDescription + " by Create Loader BIN";
            moby.BehaviorNote = template.Note;
            if (!string.IsNullOrEmpty(template.DependencyRisk))
                moby.BehaviorNote = (string.IsNullOrEmpty(moby.BehaviorNote) ? "" : moby.BehaviorNote + " ") + template.DependencyRisk;
            moby.SpecialDataNote = "External donor source record; special data is copied from the user's legal ROM during export when present.";
            if (template.LoaderTransformed)
                moby.SpecialDataNote += " Loader-transformed identity bytes are saved with the edit and forced during selected-add export.";
            moby.PatchStatus = "append-patchable";
            moby.PatchLead = "append source record T" + appendTrueIndex.ToString() + " from " + template.SourceDescription;
            moby.PatchPriority = 1;
            moby.IsAppendedRecord = true;
            moby.AppendSourceTrueIndex = template.SourceTrueIndex;
            moby.AppendSourceIndex = -1;
            moby.AppendSourceLabel = template.DisplayName;
            moby.AppendSourceLevelKey = template.SourceLevelKey;
            moby.AppendSourceLevelName = template.SourceLevelName;
            moby.AppendSourceFamily = template.Family;
            moby.AppendLoaderTransformedDonor = template.LoaderTransformed;
            moby.AppendDonorMapConfidence = template.DonorMapConfidence;
            moby.AppendDependencyRisk = template.DependencyRisk;
            moby.CaptureBaseIdentity();
            return moby;
        }

        public void CaptureBaseIdentity()
        {
            BaseLabel = Label;
            BaseKind = Kind;
            BaseConfidence = Confidence;
            BaseEvidence = Evidence;
            BaseColor = Color;
            BaseType = Type;
            BaseState = State;
            BaseSpecialDataPointer = SpecialDataPointer;
            BaseSourceByte36 = SourceByte36;
            BaseSourceByte37 = SourceByte37;
            BaseSourceByte4F = SourceByte4F;
            BaseFlag4A = Flag4A;
            BaseFlag4B = Flag4B;
            HasBaseIdentity = true;
        }

        private void RestoreBaseIdentity()
        {
            if (!HasBaseIdentity) return;
            Label = BaseLabel;
            Kind = BaseKind;
            Confidence = BaseConfidence;
            Evidence = BaseEvidence;
            Color = BaseColor;
            Type = BaseType;
            State = BaseState;
            SpecialDataPointer = BaseSpecialDataPointer;
            SourceByte36 = BaseSourceByte36;
            SourceByte37 = BaseSourceByte37;
            SourceByte4F = BaseSourceByte4F;
            Flag4A = BaseFlag4A;
            Flag4B = BaseFlag4B;
        }

        public void SetGemColorOverride(string color)
        {
            if (!HasBaseIdentity)
                CaptureBaseIdentity();

            string normalized = NormalizeGemColor(color);
            GemColorOverride = normalized;
            GemValueOverride = GemValueForColor(normalized);
            GemSourceByte36Override = GemIdByteForColor(normalized);
            GemSourceByte4FOverride = GemValueByteForColor(normalized);
            Label = GemDisplayName(normalized);
            Kind = normalized + " " + GemValueOverride.ToString() + "-gem collectible";
            Color = GemColorForColor(normalized);
            Confidence = "editor override";
            Evidence = "source bytes +0x36/+0x4F will be patched by Create Loader BIN";
        }

        public void SetRewardColorOverride(string color)
        {
            if (!HasBaseIdentity)
                CaptureBaseIdentity();

            string normalized = NormalizeGemColor(color);
            RewardColorOverride = normalized;
            RewardValueOverride = GemValueForColor(normalized);
            RewardByte53Override = GemIdByteForColor(normalized);
            Confidence = "editor reward override";
            Evidence = "source reward byte +0x53 will be patched by Create Loader BIN";
        }

        public void SetContainedGemColorOverride(string color)
        {
            if (!HasBaseIdentity)
                CaptureBaseIdentity();

            string normalized = NormalizeGemColor(color);
            RewardColorOverride = normalized;
            RewardValueOverride = GemValueForColor(normalized);
            RewardByte53Override = GemIdByteForColor(normalized);
            Label = "Chest content: " + GemDisplayName(normalized);
            Kind = "chest contained " + normalized + " " + RewardValueOverride.ToString() + "-gem reward marker";
            Color = GemColorForColor(normalized);
            Confidence = "editor chest-content override";
            Evidence = "source contained-gem byte +0x53 will be patched by Create Loader BIN";
        }

        public void SetChestContentLinkOverride(Moby chest)
        {
            if (chest == null) return;
            SetChestContentLinkOverride(
                chest.TrueIndex,
                chest.Index,
                chest.DisplayLabel,
                ToRawCoordinate(X) - ToRawCoordinate(chest.X),
                ToRawCoordinate(Y) - ToRawCoordinate(chest.Y),
                ToRawCoordinate(Z) - ToRawCoordinate(chest.Z));
        }

        public void SetChestContentLinkOverride(int chestTrueIndex, int chestIndex, string chestLabel, int rawOffsetX, int rawOffsetY, int rawOffsetZ)
        {
            if (chestTrueIndex < 0) return;
            ChestContentLinkTrueIndex = chestTrueIndex;
            ChestContentLinkIndex = chestIndex;
            ChestContentLinkLabel = chestLabel ?? "";
            ChestContentRawOffsetX = rawOffsetX;
            ChestContentRawOffsetY = rawOffsetY;
            ChestContentRawOffsetZ = rawOffsetZ;
            Confidence = "editor chest-content link override";
            Evidence = "source contained-gem special data +0x00/+0x04/+0x08/+0x0C will be patched by Create Loader BIN";
        }

        public void ClearChestContentLinkOverride()
        {
            ChestContentLinkTrueIndex = -1;
            ChestContentLinkIndex = -1;
            ChestContentLinkLabel = null;
            ChestContentRawOffsetX = 0;
            ChestContentRawOffsetY = 0;
            ChestContentRawOffsetZ = 0;
        }

        public void SetRecordCloneOverride(Moby source)
        {
            if (source == null) return;
            if (!HasBaseIdentity)
                CaptureBaseIdentity();

            HasHiddenSlotEdit = false;
            RecordCloneSourceTrueIndex = source.TrueIndex;
            RecordCloneSourceIndex = source.Index;
            RecordCloneSourceLabel = source.DisplayLabel;
            ClearChestContentLinkOverride();
            GemColorOverride = null;
            GemValueOverride = 0;
            GemSourceByte36Override = -1;
            GemSourceByte4FOverride = -1;
            RewardColorOverride = null;
            RewardValueOverride = 0;
            RewardByte53Override = -1;

            Type = source.Type;
            State = source.State;
            SpecialDataPointer = source.SpecialDataPointer;
            SourceByte36 = source.SourceByte36;
            SourceByte37 = source.SourceByte37;
            SourceByte4F = source.SourceByte4F;
            Flag4A = source.Flag4A;
            Flag4B = source.Flag4B;
            Label = source.DisplayLabel + " (cloned type)";
            Kind = source.Kind;
            Zone = source.Zone;
            Color = source.Color;
            Confidence = "editor record clone";
            Evidence = "source record T" + source.TrueIndex.ToString() + " will be cloned into this slot by Create Loader BIN";
        }

        public void SetHiddenSlotOverride()
        {
            if (!HasBaseIdentity)
                CaptureBaseIdentity();

            HasHiddenSlotEdit = true;
            RecordCloneSourceTrueIndex = -1;
            RecordCloneSourceIndex = -1;
            RecordCloneSourceLabel = null;
            ClearChestContentLinkOverride();
            GemColorOverride = null;
            GemValueOverride = 0;
            GemSourceByte36Override = -1;
            GemSourceByte4FOverride = -1;
            RewardColorOverride = null;
            RewardValueOverride = 0;
            RewardByte53Override = -1;
            X = -30000f;
            Y = -30000f;
            Z = -30000f;
            Label = "Hidden slot: " + (string.IsNullOrEmpty(BaseLabel) ? DisplayLabel : BaseLabel);
            Kind = "hidden source-table slot";
            Color = System.Drawing.Color.FromArgb(120, 120, 120);
            Confidence = "editor hidden";
            Evidence = "source XYZ will be moved out of bounds by Create Loader BIN";
        }

        public bool ShouldUseSourceGemIdentity(MobyMetadata metadata)
        {
            if (!IsStandaloneGemSourceRecord) return false;
            if (string.IsNullOrEmpty(SourceGemColorName)) return false;
            if (metadata == null) return true;
            return metadata.IsUserOverride && IsGenericGemIdentityText(metadata.Label, metadata.Kind);
        }

        public bool ApplySourceGemIdentity()
        {
            string normalized = SourceGemColorName;
            if (string.IsNullOrEmpty(normalized)) return false;
            int value = GemValueForColor(normalized);
            Label = GemDisplayName(normalized);
            Kind = normalized + " " + value.ToString() + "-gem collectible";
            Color = GemColorForColor(normalized);
            Confidence = "source gem bytes";
            Evidence = "source bytes +0x36/+0x4F identify this standalone gem value";
            return true;
        }

        private static bool IsGenericGemIdentityText(string label, string kind)
        {
            string labelText = string.IsNullOrEmpty(label) ? "" : label.Trim().ToLowerInvariant();
            string kindText = string.IsNullOrEmpty(kind) ? "" : kind.Trim().ToLowerInvariant();
            string text = (labelText + " " + kindText).Trim();
            if (text.IndexOf("gem", StringComparison.Ordinal) < 0 && text.IndexOf("treasure", StringComparison.Ordinal) < 0)
                return false;
            if (text.IndexOf("chest", StringComparison.Ordinal) >= 0 || text.IndexOf("reward", StringComparison.Ordinal) >= 0 || text.IndexOf("key", StringComparison.Ordinal) >= 0)
                return false;

            if (labelText == "gem" || labelText == "gem collectible" || labelText == "treasure/gem" || labelText == "gem treasure/gem")
                return true;
            if (labelText == "red gem" || labelText == "red gem (1)") return true;
            if (labelText == "green gem" || labelText == "green gem (2)") return true;
            if (labelText == "blue gem" || labelText == "blue gem (5)") return true;
            if (labelText == "yellow gem" || labelText == "yellow gem (10)") return true;
            if (labelText == "purple gem" || labelText == "purple gem (25)") return true;
            if (kindText == "gem" || kindText == "gems" || kindText == "gem collectible")
                return true;
            return kindText.EndsWith("-gem collectible", StringComparison.Ordinal);
        }

        public static bool IsSupportedGemColor(string color)
        {
            if (string.IsNullOrEmpty(color)) return false;
            string normalized = color.Trim().ToLowerInvariant();
            return normalized == "red"
                || normalized == "green"
                || normalized == "blue"
                || normalized == "yellow"
                || normalized == "purple";
        }

        private static string NormalizeGemColor(string color)
        {
            if (string.Equals(color, "green", StringComparison.OrdinalIgnoreCase)) return "green";
            if (string.Equals(color, "blue", StringComparison.OrdinalIgnoreCase)) return "blue";
            if (string.Equals(color, "yellow", StringComparison.OrdinalIgnoreCase)) return "yellow";
            if (string.Equals(color, "purple", StringComparison.OrdinalIgnoreCase)) return "purple";
            return "red";
        }

        private static int GemValueForColor(string normalized)
        {
            if (normalized == "green") return 2;
            if (normalized == "blue") return 5;
            if (normalized == "yellow") return 10;
            if (normalized == "purple") return 25;
            return 1;
        }

        private static int GemIdByteForColor(string normalized)
        {
            if (normalized == "green") return 0x54;
            if (normalized == "blue") return 0x55;
            if (normalized == "yellow") return 0x56;
            if (normalized == "purple") return 0x57;
            return 0x53;
        }

        private static int GemValueByteForColor(string normalized)
        {
            if (normalized == "green") return 0x02;
            if (normalized == "blue") return 0x03;
            if (normalized == "yellow") return 0x04;
            if (normalized == "purple") return 0x05;
            return 0x01;
        }

        private static string GemDisplayName(string normalized)
        {
            if (normalized == "green") return "Green gem (2)";
            if (normalized == "blue") return "Blue gem (5)";
            if (normalized == "yellow") return "Yellow gem (10)";
            if (normalized == "purple") return "Purple gem (25)";
            return "Red gem (1)";
        }

        private static string GemColorFromSourceBytes(int sourceByte36, int sourceByte4F)
        {
            switch (sourceByte4F)
            {
                case 0x01: return "red";
                case 0x02: return "green";
                case 0x03: return "blue";
                case 0x04: return "yellow";
                case 0x05: return "purple";
            }
            switch (sourceByte36)
            {
                case 0x53: return "red";
                case 0x54: return "green";
                case 0x55: return "blue";
                case 0x56: return "yellow";
                case 0x57: return "purple";
            }
            return "";
        }

        private static Color GemColorForColor(string normalized)
        {
            if (normalized == "green") return System.Drawing.Color.FromArgb(46, 204, 113);
            if (normalized == "blue") return System.Drawing.Color.FromArgb(52, 152, 219);
            if (normalized == "yellow") return System.Drawing.Color.FromArgb(241, 196, 15);
            if (normalized == "purple") return System.Drawing.Color.FromArgb(155, 89, 182);
            return System.Drawing.Color.FromArgb(231, 76, 60);
        }

        public void ClearGemColorOverride()
        {
            GemColorOverride = null;
            GemValueOverride = 0;
            GemSourceByte36Override = -1;
            GemSourceByte4FOverride = -1;
            if (HasBaseIdentity && !HasRewardColorEdit && !HasRecordCloneEdit && !HasHiddenSlotEdit)
                RestoreBaseIdentity();
        }

        public void ClearRewardColorOverride()
        {
            RewardColorOverride = null;
            RewardValueOverride = 0;
            RewardByte53Override = -1;
            if (HasBaseIdentity && !HasGemColorEdit && !HasRecordCloneEdit && !HasHiddenSlotEdit)
                RestoreBaseIdentity();
        }

        public void ClearRecordMutationOverride()
        {
            HasHiddenSlotEdit = false;
            RecordCloneSourceTrueIndex = -1;
            RecordCloneSourceIndex = -1;
            RecordCloneSourceLabel = null;
            if (HasBaseIdentity && !HasGemColorEdit && !HasRewardColorEdit)
                RestoreBaseIdentity();
        }

        public void ResetToOriginal()
        {
            X = OriginalX;
            Y = OriginalY;
            Z = OriginalZ;
            GroundOffset = OriginalGroundOffset;
            ClearGemColorOverride();
            ClearRewardColorOverride();
            ClearChestContentLinkOverride();
            ClearRecordMutationOverride();
            RestoreBaseIdentity();
        }

        private static int ToRawCoordinate(float value)
        {
            return (int)Math.Round(value * 16f);
        }
    }
}
