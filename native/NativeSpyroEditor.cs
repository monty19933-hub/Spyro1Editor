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
                    GeometryCandidate geometry = GeometryLoader.LoadFirstCandidate(Path.Combine(workspace, "stonehill-runtime-scene-editor-overlay.json"));
                    List<Moby> mobys = MobyLoader.Load(Path.Combine(workspace, "stonehill-before-gem-clean.bin"));
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

        private static string FindWorkspace()
        {
            string current = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(current, "stonehill-runtime-scene-editor-overlay.json")))
                return current;

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(exeDir, "stonehill-runtime-scene-editor-overlay.json")))
                return exeDir;

            string parent = Path.GetFullPath(Path.Combine(exeDir, ".."));
            if (File.Exists(Path.Combine(parent, "stonehill-runtime-scene-editor-overlay.json")))
                return parent;

            return current;
        }
    }

    internal sealed class EditorForm : Form
    {
        private readonly string workspace;
        private string editPath;
        private string terrainEditPath;
        private string terrainMaterialOverridesPath;
        private string liveOriginalsPath;
        private string currentRamPath;
        private string currentLevelName = "Stone Hill";
        private string currentLevelKey = "stonehill";
        private int currentLevelId = 0x0B;
        private bool currentLevelSupportsSourcePatchers = true;
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
        private readonly ToolStripComboBox nudgeStepBox;
        private readonly ToolStripMenuItem mirrorXButton;
        private readonly ToolStripMenuItem mirrorYButton;
        private readonly Timer settleTimer;
        private ListView mobyList;
        private ComboBox catalogFilterBox;
        private TextBox catalogSearchBox;
        private Label catalogSummaryLabel;
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
        private Button copyMutationSourceButton;
        private Button pasteMutationButton;
        private Button hideSelectedButton;
        private Button liveApplyButton;
        private Button liveRevertButton;
        private Button patchTopRankedButton;
        private Button patchBroadButton;
        private Button combinedPatchButton;
        private Button validateSourceButton;
        private Button behaviorDiffButton;

        private GeometryCandidate geometry;
        private readonly List<Moby> mobys = new List<Moby>();
        private readonly List<SelectionGroup> selectionGroups = new List<SelectionGroup>();
        private readonly Dictionary<int, string> terrainMaterialOverrides = new Dictionary<int, string>();
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
        private bool updatingGroupSelection;
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
        private const string CatalogHelpers = "Helpers";
        private const string CatalogPortals = "Audio/Portal";
        private const string CatalogOther = "Other";

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

        public EditorForm(string workspace)
        {
            this.workspace = workspace;
            editPath = Path.Combine(workspace, "stonehill-native-edits.json");
            terrainEditPath = Path.Combine(workspace, "stonehill-terrain-edits.json");
            terrainMaterialOverridesPath = Path.Combine(workspace, "stonehill-terrain-material-overrides.json");
            liveOriginalsPath = Path.Combine(workspace, "stonehill-live-moby-originals.json");
            currentRamPath = Path.Combine(workspace, "stonehill-before-gem-clean.bin");
            Text = "Spyro Native Level Editor - Stone Hill Prototype";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1420;
            Height = 900;
            MinimumSize = new Size(980, 680);

            ToolStrip tools = new ToolStrip();
            tools.GripStyle = ToolStripGripStyle.Hidden;
            ToolStripButton loadButton = new ToolStripButton("Load Stone Hill");
            ToolStripButton loadArtisansButton = new ToolStripButton("Load Artisans");
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
            surfaceColorAssistButton = new ToolStripMenuItem("Stone Hill Surface Assist") { CheckOnClick = true, Checked = true };
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
            loadArtisansButton.ToolTipText = "Load the current Artisans Home World RAM capture and decoded runtime terrain overlay.";
            resetEditsToolButton.ToolTipText = "Reset all active moby and terrain edits back to the original loaded level state.";
            mobyModeButton.ToolTipText = "Edit moby/object positions and object parameters.";
            terrainModeButton.ToolTipText = "Inspect terrain faces and vertices without moving mobys.";
            view3DButton.ToolTipText = "Show an angled height view. Right-drag pans; Shift+right-drag rotates. Object dragging remains top-down only.";
            terrainStyleButton.ToolTipText = "Use a Stone Hill-like terrain palette instead of pure height debug colors.";
            sourceTextureButton.ToolTipText = "Draw WAD-source 64x64 texture tiles on decoded terrain faces when the Stone Hill atlas is available.";
            surfaceColorAssistButton.ToolTipText = "Bias known Stone Hill surface IDs toward grass, water, or stone while keeping the texture/detail data visible.";
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
            linkedMoveButton.ToolTipText = "Move known linked records together, such as dragon/pedestal/helper clusters.";
            groundSnapButton.ToolTipText = "When moving in X/Y, keep each moby at its original terrain-ground offset.";
            snapGroundButton.ToolTipText = "Snap the selected moby, and any linked records, to the terrain height at their current X/Y.";
            clickPlaceButton.ToolTipText = "Place the selected moby or linked group on the clicked terrain point.";
            dragLockButton.ToolTipText = "Select mobys without starting a drag.";
            nudgeStepBox.ToolTipText = "Arrow-key placement step in world units.";

            loadButton.Click += delegate { LoadStoneHill(); };
            loadArtisansButton.Click += delegate { LoadArtisans(); };
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

            tools.Items.Add(loadButton);
            tools.Items.Add(loadArtisansButton);
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
                BeginInvoke(new MethodInvoker(delegate { LoadStoneHill(); }));
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
            root.RowCount = 8;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270f));
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
            root.Controls.Add(groupTools, 0, 2);

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
            mobyList.Resize += delegate { AdjustMobyListColumns(); };
            root.Controls.Add(mobyList, 0, 3);

            selectedTitleLabel = new Label();
            selectedTitleLabel.Dock = DockStyle.Fill;
            selectedTitleLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            selectedTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            selectedTitleLabel.AutoEllipsis = true;
            selectedTitleLabel.Text = "No moby selected";
            root.Controls.Add(selectedTitleLabel, 0, 4);

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
            root.Controls.Add(details, 0, 5);

            TableLayoutPanel buttons = new TableLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.ColumnCount = 2;
            buttons.RowCount = 12;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < buttons.RowCount; i++)
                buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / buttons.RowCount));
            saveEditsButton = NewPanelButton("Save Edits");
            loadEditsButton = NewPanelButton("Load Edits");
            resetSelectedButton = NewPanelButton("Reset Selected");
            clearEditsButton = NewPanelButton("Reset All Edits");
            setRedGemButton = NewPanelButton("Set Red 1");
            setGreenGemButton = NewPanelButton("Set Green 2");
            setBlueGemButton = NewPanelButton("Set Blue 5");
            setYellowGemButton = NewPanelButton("Set Yellow 10");
            setPurpleGemButton = NewPanelButton("Set Purple 25");
            copyMutationSourceButton = NewPanelButton("Copy Obj");
            pasteMutationButton = NewPanelButton("Clone/Add");
            hideSelectedButton = NewPanelButton("Remove Slot");
            liveApplyButton = NewPanelButton("Live Apply");
            liveRevertButton = NewPanelButton("Live Revert");
            patchTopRankedButton = NewPanelButton("Create Loader BIN");
            patchBroadButton = NewPanelButton("Create Terrain BIN");
            combinedPatchButton = NewPanelButton("Create Combined BIN");
            validateSourceButton = NewPanelButton("Validate Source");
            behaviorDiffButton = NewPanelButton("Behavior Diff");
            saveEditsButton.Click += delegate { SaveEdits(); };
            loadEditsButton.Click += delegate { LoadSavedEdits(true); };
            resetSelectedButton.Click += delegate { ResetSelectedMoby(); };
            clearEditsButton.Click += delegate { ResetAllEdits(); };
            setRedGemButton.Click += delegate { SetSelectedGemColor("red"); };
            setGreenGemButton.Click += delegate { SetSelectedGemColor("green"); };
            setBlueGemButton.Click += delegate { SetSelectedGemColor("blue"); };
            setYellowGemButton.Click += delegate { SetSelectedGemColor("yellow"); };
            setPurpleGemButton.Click += delegate { SetSelectedGemColor("purple"); };
            copyMutationSourceButton.Click += delegate { CopySelectedMutationSource(); };
            pasteMutationButton.Click += delegate { PasteMutationIntoSelected(); };
            hideSelectedButton.Click += delegate { HideSelectedMobySlot(); };
            liveApplyButton.Click += delegate { if (editorMode == EditorMode.Terrain) RunLiveTerrainMove(false); else RunLiveMobyMove(false); };
            liveRevertButton.Click += delegate { if (editorMode == EditorMode.Terrain) RunLiveTerrainMove(true); else RunLiveMobyMove(true); };
            patchTopRankedButton.Click += delegate { RunPatchExporter(true); };
            patchBroadButton.Click += delegate { RunTerrainPatchExporter(); };
            combinedPatchButton.Click += delegate { RunCombinedPatchExporter(); };
            validateSourceButton.Click += delegate { RunSourceValidation(); };
            behaviorDiffButton.Click += delegate { RunBehaviorDiff(); };
            buttons.Controls.Add(saveEditsButton, 0, 0);
            buttons.Controls.Add(loadEditsButton, 1, 0);
            buttons.Controls.Add(resetSelectedButton, 0, 1);
            buttons.Controls.Add(clearEditsButton, 1, 1);
            buttons.Controls.Add(liveApplyButton, 0, 2);
            buttons.Controls.Add(liveRevertButton, 1, 2);
            buttons.Controls.Add(patchTopRankedButton, 0, 3);
            buttons.Controls.Add(patchBroadButton, 1, 3);
            buttons.Controls.Add(combinedPatchButton, 0, 4);
            buttons.SetColumnSpan(combinedPatchButton, 2);
            buttons.Controls.Add(validateSourceButton, 0, 5);
            buttons.SetColumnSpan(validateSourceButton, 2);
            buttons.Controls.Add(setRedGemButton, 0, 6);
            buttons.Controls.Add(setGreenGemButton, 1, 6);
            buttons.Controls.Add(setBlueGemButton, 0, 7);
            buttons.Controls.Add(setYellowGemButton, 1, 7);
            buttons.Controls.Add(setPurpleGemButton, 0, 8);
            buttons.SetColumnSpan(setPurpleGemButton, 2);
            buttons.Controls.Add(copyMutationSourceButton, 0, 9);
            buttons.Controls.Add(hideSelectedButton, 1, 9);
            buttons.Controls.Add(pasteMutationButton, 0, 10);
            buttons.SetColumnSpan(pasteMutationButton, 2);
            buttons.Controls.Add(behaviorDiffButton, 0, 11);
            buttons.SetColumnSpan(behaviorDiffButton, 2);
            root.Controls.Add(buttons, 0, 6);

            notesBox = new TextBox();
            notesBox.Dock = DockStyle.Fill;
            notesBox.Multiline = true;
            notesBox.ReadOnly = true;
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.BackColor = Color.White;
            notesBox.Font = new Font("Consolas", 8.5f);
            notesBox.Text = "Select a moby to inspect its decoded identity and edit status.";
            root.Controls.Add(notesBox, 0, 7);

            return root;
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

        private void LoadStoneHill()
        {
            LoadLevel("Stone Hill", "stonehill", true, true);
        }

        private void LoadArtisans()
        {
            LoadLevel("Artisans", "artisans", false, true);
        }

        private void LoadLevel(string levelName, string levelKey, bool applyStoneHillMetadata, bool supportsSourcePatchers)
        {
            try
            {
                if (!ConfirmDiscardEdits("reload " + levelName))
                    return;

                string geometryPath = GetLevelGeometryPath(levelKey);
                string ramPath = GetLevelRamPath(levelKey);
                if (!File.Exists(geometryPath))
                    throw new FileNotFoundException(MissingLevelAssetMessage(levelName, "geometry overlay"), geometryPath);
                if (!File.Exists(ramPath))
                    throw new FileNotFoundException(MissingLevelAssetMessage(levelName, "RAM dump"), ramPath);

                currentLevelName = levelName;
                currentLevelKey = levelKey;
                currentLevelId = ExpectedLevelIdForKey(levelKey);
                currentLevelSupportsSourcePatchers = supportsSourcePatchers;
                mutationClipboardMobyIndex = -1;
                editPath = Path.Combine(workspace, levelKey + "-native-edits.json");
                terrainEditPath = Path.Combine(workspace, levelKey + "-terrain-edits.json");
                terrainMaterialOverridesPath = Path.Combine(workspace, levelKey + "-terrain-material-overrides.json");
                liveOriginalsPath = Path.Combine(workspace, levelKey + "-live-moby-originals.json");
                currentRamPath = ramPath;
                Text = "Spyro Native Level Editor - " + levelName;
                if (inspectorHeaderLabel != null)
                    inspectorHeaderLabel.Text = levelName + " Objects";
                UpdateLevelActionButtons();

                Cursor = Cursors.WaitCursor;
                statusLabel.Text = "Loading " + levelName + " geometry...";
                statusStrip.Refresh();
                geometry = GeometryLoader.LoadFirstCandidate(geometryPath);
                int materialOverrideCount = LoadTerrainMaterialOverrides();
                LoadTerrainTextureAtlas();
                mobys.Clear();
                mobys.AddRange(MobyLoader.Load(ramPath));
                int namedMobys = MobyMetadataLoader.Apply(workspace, mobys, levelKey, applyStoneHillMetadata);
                if (supportsSourcePatchers)
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
                BuildSelectionGroups();
                hasUnsavedEdits = false;
                RefreshMobyList();
                SelectMoby(mobys.Count > 0 ? 0 : -1);
                FitGeometry();
                statusLabel.Text = string.Format(
                    "Loaded {0}: {1} faces, {2} lines, {3} mobys, {4} named, {5} moby edit(s), {6} terrain edit(s), {7} material labels. Wheel zoom, right-drag pan, left-drag mobys.",
                    levelName,
                    geometry.Polygons.Count,
                    geometry.Edges.Count,
                    mobys.Count,
                    namedMobys,
                    savedEditCount,
                    savedTerrainEditCount,
                    materialOverrideCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Load level failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Load failed";
            }
            finally
            {
                Cursor = Cursors.Default;
                canvas.Invalidate();
            }
        }

        private string GetLevelGeometryPath(string levelKey)
        {
            return Path.Combine(workspace, levelKey + "-runtime-scene-editor-overlay.json");
        }

        private string GetLevelRamPath(string levelKey)
        {
            if (string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(workspace, "stonehill-before-gem-clean.bin");
            return Path.Combine(workspace, levelKey + "-before-clean.bin");
        }

        private static int ExpectedLevelIdForKey(string levelKey)
        {
            if (string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase)) return 0x0B;
            if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase)) return 0x0A;
            return -1;
        }

        private static string MissingLevelAssetMessage(string levelName, string assetName)
        {
            if (string.Equals(levelName, "Artisans", StringComparison.OrdinalIgnoreCase))
                return "Missing Artisans " + assetName + ". Stand in Artisans in DuckStation and run Capture Artisans Workbench From DuckStation.bat once to refresh the cached editor files.";
            return "Missing " + levelName + " " + assetName + ". This should be a cached editor file, not a live DuckStation requirement.";
        }

        private void UpdateLevelActionButtons()
        {
            bool mobySourcePatchers = currentLevelSupportsSourcePatchers;
            bool stoneHillTerrainPatchers = IsStoneHillLevel();
            if (patchTopRankedButton != null) patchTopRankedButton.Enabled = mobySourcePatchers;
            if (patchBroadButton != null) patchBroadButton.Enabled = stoneHillTerrainPatchers;
            if (combinedPatchButton != null) combinedPatchButton.Enabled = stoneHillTerrainPatchers;
            if (validateSourceButton != null) validateSourceButton.Enabled = stoneHillTerrainPatchers;
            if (behaviorDiffButton != null) behaviorDiffButton.Enabled = stoneHillTerrainPatchers;
        }

        private bool IsStoneHillLevel()
        {
            return string.Equals(currentLevelKey, "stonehill", StringComparison.OrdinalIgnoreCase);
        }

        private static string ScriptLevelKey(string levelKey)
        {
            if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase)) return "Artisans";
            return "StoneHill";
        }

        private static int SourceRecordCountForLevel(string levelKey)
        {
            if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase)) return 174;
            if (string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase)) return 195;
            return 0;
        }

        private static string SourcePatchLeadForLevel(string levelKey, int trueIndex)
        {
            if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase))
                return "WAD entry 10, true record " + trueIndex.ToString() + ", XYZ +0x0C/+0x10/+0x14";
            return MobyLoader.LoaderTablePatchLead(trueIndex);
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
            if (surface == "grass" || surface == "water" || surface == "stone" || surface == "unknown")
                return surface;
            if (surface == "sand" || surface == "beach")
                return "sand";
            return "";
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

            string closePath = Path.Combine(workspace, "stonehill-texture-atlas-close-from-wad-subfile00.png");
            string standardPath = Path.Combine(workspace, "stonehill-texture-atlas-from-wad-subfile00.png");
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
                SaveEdits();
            return !hasUnsavedEdits;
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
            }
            finally
            {
                mobyList.EndUpdate();
                updatingSelection = false;
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

            int behaviorGroups = AddBehaviorLinkSelectionGroups();
            if (behaviorGroups == 0)
                AddSelectionGroupByTrueIndexes("Linked: Dragon platform set", "linked:dragon-platform", true, new int[] { 86, 140, 185 });
            AddSelectionGroupByPredicate("Special: Dragons and pedestals", "special:dragons", false, delegate(Moby moby) { return string.Equals(CatalogCategory(moby), CatalogDragons, StringComparison.Ordinal); });
            AddSelectionGroupByPredicate("Special: Whirlwinds and exits", "special:whirlwinds-exits", false, delegate(Moby moby)
            {
                string category = CatalogCategory(moby);
                return string.Equals(category, CatalogWhirlwinds, StringComparison.Ordinal) || string.Equals(category, CatalogPortals, StringComparison.Ordinal);
            });
            AddSelectionGroupByPredicate("Special: Nonvisual controls", "special:controls", false, delegate(Moby moby) { return string.Equals(CatalogCategory(moby), CatalogHelpers, StringComparison.Ordinal); });

            AddIdentitySelectionGroups();
            AddAreaSelectionGroups();
            RefreshSelectionGroupBox(preferredKey);
        }

        private int AddBehaviorLinkSelectionGroups()
        {
            string path = Path.Combine(workspace, "stonehill-behavior-links.json");
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
                case MobyIconKind.Dragon:
                case MobyIconKind.Pedestal:
                case MobyIconKind.Fairy:
                    return CatalogDragons;
                case MobyIconKind.Whirlwind: return CatalogWhirlwinds;
                case MobyIconKind.Key: return CatalogKeys;
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
                case MobyIconKind.Dragon: return "Drg";
                case MobyIconKind.Pedestal: return "Ped";
                case MobyIconKind.Fairy: return "Lgt";
                case MobyIconKind.Whirlwind: return "Whl";
                case MobyIconKind.Key: return "Key";
                case MobyIconKind.Enemy: return "Act";
                case MobyIconKind.Scenery: return "Scn";
                case MobyIconKind.Portal: return "Prt";
                case MobyIconKind.Helper: return "Ctl";
                default: return "Mby";
            }
        }

        private static string GemIconText(Moby moby)
        {
            string text = MobySearchText(moby);
            if (HasAny(text, "purple gem", "25-gem", "25 value", "gem (25)", "(25)")) return "P25";
            if (HasAny(text, "yellow gem", "10-gem", "10 value", "gem (10)", "(10)")) return "Y10";
            if (HasAny(text, "blue gem", "5-gem", "5 value", "gem (5)", "(5)")) return "B5";
            if (HasAny(text, "green gem", "2-gem", "2 value", "gem (2)", "(2)")) return "G2";
            if (HasAny(text, "red gem", "1-gem", "1 value", "gem (1)", "(1)")) return "R1";
            return "Gem";
        }

        private enum MobyIconKind
        {
            Generic,
            Gem,
            Chest,
            Dragon,
            Pedestal,
            Fairy,
            Whirlwind,
            Key,
            Enemy,
            Scenery,
            Portal,
            Helper
        }

        private static MobyIconKind GetMobyIconKind(Moby moby)
        {
            if (moby == null) return MobyIconKind.Generic;
            string text = MobySearchText(moby);
            if (text.IndexOf("whirlwind", StringComparison.Ordinal) >= 0) return MobyIconKind.Whirlwind;
            if (text.IndexOf("fairy", StringComparison.Ordinal) >= 0 || text.IndexOf("pedestal light", StringComparison.Ordinal) >= 0 || text.IndexOf("pedestal-light", StringComparison.Ordinal) >= 0) return MobyIconKind.Fairy;
            if (text.IndexOf("dragon pedestal", StringComparison.Ordinal) >= 0 || text.IndexOf("pedestal", StringComparison.Ordinal) >= 0) return MobyIconKind.Pedestal;
            if (text.IndexOf("dragon", StringComparison.Ordinal) >= 0) return MobyIconKind.Dragon;
            if (text.IndexOf("key", StringComparison.Ordinal) >= 0) return MobyIconKind.Key;
            if (text.IndexOf("chest", StringComparison.Ordinal) >= 0 || text.IndexOf("container", StringComparison.Ordinal) >= 0 || text.IndexOf("box", StringComparison.Ordinal) >= 0) return MobyIconKind.Chest;
            if (text.IndexOf("gem", StringComparison.Ordinal) >= 0 || text.IndexOf("treasure", StringComparison.Ordinal) >= 0 || text.IndexOf("collectible", StringComparison.Ordinal) >= 0) return MobyIconKind.Gem;
            if (text.IndexOf("enemy", StringComparison.Ordinal) >= 0 || text.IndexOf("fodder", StringComparison.Ordinal) >= 0 || text.IndexOf("sheep", StringComparison.Ordinal) >= 0 || text.IndexOf("shepherd", StringComparison.Ordinal) >= 0 || text.IndexOf("shepard", StringComparison.Ordinal) >= 0 || text.IndexOf("ram", StringComparison.Ordinal) >= 0 || text.IndexOf("thief", StringComparison.Ordinal) >= 0) return MobyIconKind.Enemy;
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
            else
                moby.SetRewardColorOverride(color);
            hasUnsavedEdits = true;
            BuildSelectionGroups();
            RefreshMobyListRow(selectedMobyIndex);
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = standaloneGem
                ? "Set " + MobyId(moby) + " to " + moby.GemColorName + " gem (" + moby.GemValueOverride.ToString() + "). Save Edits, then Create Loader BIN."
                : "Set " + MobyId(moby) + " reward drop to " + moby.RewardColorName + " gem (" + moby.RewardValueOverride.ToString() + "). Save Edits, then Create Loader BIN.";
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
            Moby moby = mobys[selectedMobyIndex];
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
            RefreshMobyListRow(selectedMobyIndex);
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
            if (moby.Type != 0x20 || moby.Flag4A != 0x10)
                return false;
            if (moby.HasRewardColorEdit)
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
            if (moby.HasHiddenSlotEdit) return "removed by hiding source-table slot";
            if (moby.HasRecordCloneEdit && moby.HasGemColorEdit) return "cloned object type and gem value edited";
            if (moby.HasRecordCloneEdit && moby.HasRewardColorEdit) return "cloned object type and reward edited";
            if (moby.HasRecordCloneEdit && moby.HasPositionEdit) return "cloned object type and moved";
            if (moby.HasRecordCloneEdit) return "cloned object type into this slot";
            if (moby.HasPositionEdit && moby.HasGemColorEdit && moby.HasRewardColorEdit) return "moved, gem value edited, and reward edited";
            if (moby.HasPositionEdit && moby.HasRewardColorEdit) return "moved and reward edited";
            if (moby.HasPositionEdit && moby.HasGemColorEdit) return "moved and gem value edited";
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
                    ? "Terrain Mode: click a face to inspect vertices and height."
                    : "Moby Mode: click or drag objects to edit placement.";
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
                if (m.HasGroundOffset)
                    notes.AppendLine("Ground Z offset: " + m.GroundOffset.ToString("0.00") + " from terrain" + (GroundSnapEnabled ? " (auto)" : " (off)"));
                if (m.HasGemColorEdit)
                    notes.AppendLine("Gem color edit: " + m.GemColorName + " gem (" + m.GemValueOverride.ToString() + "), source +0x36=0x" + m.GemSourceByte36Override.ToString("X2") + " and +0x4F=0x" + m.GemSourceByte4FOverride.ToString("X2"));
                else if (CanEditGemColor(m))
                    notes.AppendLine("Gem color source bytes: red 0x53/01, green 0x54/02, blue 0x55/03, yellow 0x56/04, purple 0x57/05.");
                if (m.HasRewardColorEdit)
                    notes.AppendLine("Reward color edit: drops " + m.RewardColorName + " gem (" + m.RewardValueOverride.ToString() + "), source +0x53=0x" + m.RewardByte53Override.ToString("X2"));
                else if (CanEditRewardColor(m))
                    notes.AppendLine("Reward byte: type 0x20 +0x53 controls gem drop color/value. Red 0x53, green 0x54, blue 0x55, yellow 0x56, purple 0x57.");
                if (m.HasRecordCloneEdit)
                    notes.AppendLine("Object type edit: clone source record T" + m.RecordCloneSourceTrueIndex.ToString() + " " + m.RecordCloneSourceLabel + " into this slot, keeping current XYZ.");
                if (m.HasHiddenSlotEdit)
                    notes.AppendLine("Object remove edit: hide this slot by moving it out of the level.");
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
                notes.AppendLine("Create Loader BIN writes saved Stone Hill + Artisans moby edits into one disposable disc image. Fresh-load that CUE for permanent placement, collision, and rewards.");
                notes.AppendLine("Copy Obj/Clone-Add performs slot reuse: it adds or changes gems, chests, enemies, and scenery by cloning one source record into another selected slot. Remove Slot is the current safe remove.");
                if (IsStoneHillLevel())
                {
                    notes.AppendLine("Create Terrain BIN writes saved terrain Z edits through the exact runtime scene-sector source found in the WAD.");
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
                    "Current terrain source is the runtime scene-sector geometry overlay. Saved terrain height edits can be exported with Create Terrain BIN.";
                return;
            }

            TerrainPolygon polygon = geometry.Polygons[selectedTerrainIndex];
            if (zBox != null) zBox.Enabled = true;
            float terrainZ;
            bool hasZ = polygon.TryGetZ(selectedTerrainPoint.X, selectedTerrainPoint.Y, out terrainZ);
            selectedTitleLabel.Text = "Terrain Face " + selectedTerrainIndex.ToString();
            identityLabel.Text = polygon.Points.Length.ToString() + " vertices  Avg Z " + polygon.AvgZ.ToString("0.0");
            patchLabel.Text = polygon.IsTerrainEdited ? "Terrain edit ready for live/source BIN" : "Runtime geometry view";
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
                notes.AppendLine("Texture/material id: " + polygon.TextureId.ToString());
                notes.AppendLine("Texture id face count: " + CountTerrainTextureFaces(polygon.TextureId).ToString());
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
            notes.AppendLine("Source texture display: " + (showSourceTextures ? "on" : "off") + (terrainTextureAtlas == null ? " (atlas not loaded)" : " (" + terrainTextureAtlasTier + ")"));
            notes.AppendLine("Terrain draw coverage: " + (completeTerrainDraw ? "complete" : "sampled at distant zoom"));
            notes.AppendLine("Height assist: " + HeightAssistText());
            notes.AppendLine();
            notes.AppendLine("Vertices:");
            for (int i = 0; i < polygon.Points.Length; i++)
                notes.AppendLine("  V" + i.ToString() + ": " + polygon.Points[i].X.ToString("0.0") + ", " + polygon.Points[i].Y.ToString("0.0") + ", " + polygon.ZValues[i].ToString("0.0"));
            notes.AppendLine();
            notes.AppendLine("Terrain editing: PageUp/PageDown nudges the selected face Z by the toolbar step. The Z box sets the selected face average height. Live Apply writes the selected face's terrain vertex Z into DuckStation RAM. Create Terrain BIN writes saved terrain Z edits into a fresh-loadable CUE/BIN; Create Combined BIN includes object edits too.");
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
            string id = "T" + moby.TrueIndex.ToString();
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
            SetMobyPosition(selectedMobyIndex, m.OriginalX, m.OriginalY, m.OriginalZ, true, true, false);
            m.ClearGemColorOverride();
            BuildSelectionGroups();
            RefreshMobyListRow(selectedMobyIndex);
            UpdateInspector();
            canvas.Invalidate();
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
            else if (IsDragonPlatformMoby(mobys[listIndex]))
            {
                AddLinkedTrueIndex(result, 86);
                AddLinkedTrueIndex(result, 140);
                AddLinkedTrueIndex(result, 185);
            }

            result.Sort();
            return result;
        }

        private void AddLinkedTrueIndex(List<int> result, int trueIndex)
        {
            for (int i = 0; i < mobys.Count; i++)
            {
                if (mobys[i].TrueIndex != trueIndex) continue;
                if (!result.Contains(i))
                    result.Add(i);
                return;
            }
        }

        private bool IsLinkedToSelected(int listIndex)
        {
            if (selectedMobyIndex < 0 || selectedMobyIndex >= mobys.Count) return false;
            if (!LinkedMoveEnabled) return false;
            List<int> group = GetLinkedMoveIndexes(selectedMobyIndex);
            return group.Contains(listIndex);
        }

        private static bool IsDragonPlatformMoby(Moby moby)
        {
            if (moby == null) return false;
            if (moby.TrueIndex == 86 || moby.TrueIndex == 140 || moby.TrueIndex == 185)
                return true;
            string text = ((moby.Zone ?? "") + " " + (moby.Kind ?? "") + " " + (moby.DisplayLabel ?? "")).ToLowerInvariant();
            return text.IndexOf("dragon platform", StringComparison.Ordinal) >= 0;
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
            try
            {
                savedEditCount = MobyEditStore.Save(editPath, mobys, currentLevelName);
                savedTerrainEditCount = TerrainEditStore.Save(terrainEditPath, geometry == null ? null : geometry.Polygons, currentLevelName);
                hasUnsavedEdits = false;
                RefreshMobyList();
                UpdateInspector();
                statusLabel.Text = "Saved " + savedEditCount.ToString() + " moby edit(s) and " + savedTerrainEditCount.ToString() + " terrain edit(s).";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save edits failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                    MessageBox.Show(this, "Make and save at least one Stone Hill or Artisans moby edit before creating a loader BIN.", "No moby edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string outName = topRankedOnly
                    ? "Spyro the Dragon (USA)-loaderpatchtest.bin"
                    : "Spyro the Dragon (USA)-nativepatchtest-broad.bin";
                string outPath = Path.Combine(workspace, outName);
                if (topRankedOnly)
                {
                    args.Append(" -LevelKey All");
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
            if (CountEditedTerrainFaces() == 0)
            {
                MessageBox.Show(this, "Make and save at least one terrain Z edit before creating a terrain BIN.", "No terrain edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                args.Append(" -OutPath ").Append(QuoteArgument(Path.Combine(workspace, "Spyro the Dragon (USA)-runtime-terrainpatchtest.bin")));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started exact runtime-sector terrain BIN export.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start terrain patch exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (editedMobys == 0 && editedTerrain == 0)
            {
                MessageBox.Show(this, "Make and save at least one moby or terrain edit before creating a combined BIN.", "No edits", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                args.Append(" -OutPath ").Append(QuoteArgument(Path.Combine(workspace, "Spyro the Dragon (USA)-combinedpatchtest.bin")));

                StartWorkspaceProcess("powershell.exe", args.ToString());
                statusLabel.Text = "Started combined moby + terrain BIN export.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start combined patch exporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void StartWorkspaceProcess(string fileName, string arguments)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(fileName, arguments);
            info.WorkingDirectory = workspace;
            info.UseShellExecute = true;
            System.Diagnostics.Process.Start(info);
        }

        private static string QuoteArgument(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private int LoadSavedEdits(bool showMessage)
        {
            try
            {
                int count = MobyEditStore.Load(editPath, mobys);
                savedTerrainEditCount = TerrainEditStore.Load(terrainEditPath, geometry == null ? null : geometry.Polygons);
                savedEditCount = count;
                hasUnsavedEdits = false;
                BuildSelectionGroups();
                RefreshMobyList();
                UpdateInspector();
                canvas.Invalidate();
                if (showMessage)
                    statusLabel.Text = "Loaded " + count.ToString() + " saved moby edit(s) and " + savedTerrainEditCount.ToString() + " terrain edit(s).";
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
            foreach (Moby moby in mobys)
                moby.ResetToOriginal();
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
            foreach (string levelKey in new string[] { "stonehill", "artisans" })
            {
                string path = Path.Combine(workspace, levelKey + "-native-edits.json");
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
                    g.DrawString("Click Load Stone Hill to load the native renderer.", Font, b, 20, 20);
                return;
            }

            RectangleF worldView = GetWorldViewBounds(clientSize);

            bool fast = FastMode;
            if (showFaces && !fast)
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
            if (IsGrassSurface(polygon)) return 2;
            if (IsStoneSurface(polygon)) return 3;
            return 2;
        }

        private bool TryDrawSourceTexturePolygon(Graphics g, TerrainPolygon polygon, PointF[] screenPoints)
        {
            if (!showSourceTextures || (terrainTextureAtlas == null && terrainTextureLumaAtlas == null) || polygon == null || !polygon.HasTextureId)
                return false;

            Rectangle sourceRect = TerrainTextureSourceRect(polygon);
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
                    Image sourceImage = useRawSourceTexture ? terrainTextureAtlas : (terrainTextureLumaAtlas ?? terrainTextureAtlas);
                    if (useRawSourceTexture)
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

        private bool HasSourceTextureTile(int textureId)
        {
            return !TerrainTextureSourceRect(textureId).IsEmpty;
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

            if (IsWaterSurface(polygon))
            {
                color = BlendColor(Color.FromArgb(255, 42, 124, 188), lightRgb, 0.12f);
                return true;
            }
            if (IsStoneSurface(polygon))
            {
                color = BlendColor(Color.FromArgb(255, 130, 136, 146), lightRgb, 0.24f);
                return true;
            }
            if (IsSandSurface(polygon))
            {
                color = BlendColor(Color.FromArgb(255, 165, 156, 118), lightRgb, 0.16f);
                return true;
            }
            if (IsGrassSurface(polygon))
            {
                color = BlendColor(Color.FromArgb(255, 70, 162, 70), lightRgb, 0.14f);
                return true;
            }

            return false;
        }

        private string StoneHillSurfaceKindText(TerrainPolygon polygon)
        {
            if (polygon == null) return "on";
            string manual = TerrainMaterialOverrideFor(polygon);
            if (!string.IsNullOrEmpty(manual))
                return "manual " + manual;
            if (IsWaterSurface(polygon)) return "water bias";
            if (IsStoneSurface(polygon)) return "stone bias";
            if (IsSandSurface(polygon)) return "sand bias";
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
            if (manual == "grass" || manual == "sand" || manual == "stone" || manual == "unknown") return false;
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
            if (manual == "grass" || manual == "sand" || manual == "water" || manual == "unknown") return false;
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
            if (manual == "grass" || manual == "water" || manual == "stone" || manual == "unknown") return false;
            return false;
        }

        private bool IsGrassSurface(TerrainPolygon polygon)
        {
            if (polygon == null)
                return false;
            string manual = TerrainMaterialOverrideFor(polygon);
            if (manual == "grass") return true;
            if (manual == "water" || manual == "sand" || manual == "stone" || manual == "unknown") return false;
            if (IsWaterSurface(polygon) || IsSandSurface(polygon) || IsStoneSurface(polygon))
                return false;
            if (IsGrassTextureId(polygon.TextureId))
                return true;
            float zSpread = polygon.MaxZ - polygon.MinZ;
            if (zSpread <= 160f && polygon.AvgZ >= 1180f && polygon.TextureId >= 1 && polygon.TextureId <= 29)
                return true;
            return zSpread <= 120f && polygon.AvgZ >= 1120f;
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
                for (int i = 0; i < mobys.Count; i++)
                {
                    Moby m = mobys[i];
                    if (!worldView.Contains(m.X, m.Y)) continue;
                    PointF s = WorldToScreen(m.X, m.Y, m.Z);
                    bool selected = i == selectedMobyIndex || i == dragMobyIndex;
                    bool linkedSelected = !selected && IsLinkedToSelected(i);
                    bool groupSelected = !selected && !linkedSelected && IsInActiveSelectionGroup(i);
                    float r = selected ? 8f : 6f;
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

                    if (editorMode == EditorMode.Mobys && showLabels && zoom > 0.055f)
                    {
                        string iconText = MobyIconText(m);
                        string label = MobyId(m) + (iconText == "Mby" ? " " : " " + iconText + " ") + m.DisplayLabel;
                        SizeF size = g.MeasureString(label, small);
                        RectangleF rect = new RectangleF(s.X + 9, s.Y - 10, size.Width + 8, 18);
                        g.FillRectangle(labelBack, rect);
                        g.DrawString(label, small, textBrush, rect.X + 4, rect.Y + 2);
                    }
                }
            }
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
            if (mobyIndex < 0 || mobyIndex >= mobys.Count) return;
            Moby moby = mobys[mobyIndex];
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem undoItem = new ToolStripMenuItem(moby.IsEdited ? "Undo " + MobyId(moby) + " edit" : "No edit to undo");
            undoItem.Enabled = moby.IsEdited;
            undoItem.Click += delegate { UndoSingleMobyEdit(mobyIndex); };
            menu.Items.Add(undoItem);
            menu.Show(canvas, location);
        }

        private void ShowTerrainContextMenu(int terrainIndex, Point location)
        {
            if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[terrainIndex];
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem undoItem = new ToolStripMenuItem(polygon.IsTerrainEdited ? "Undo terrain face Z edit" : "No terrain edit to undo");
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
                AddTerrainMaterialMenuItem(menu, textureId, "Grass", "grass", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Water", "water", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Sand / Beach", "sand", current);
                AddTerrainMaterialMenuItem(menu, textureId, "Stone / Building", "stone", current);
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

        private void UndoTerrainFaceEdit(int terrainIndex)
        {
            if (geometry == null || terrainIndex < 0 || terrainIndex >= geometry.Polygons.Count) return;
            TerrainPolygon polygon = geometry.Polygons[terrainIndex];
            polygon.ResetTerrainEdit();
            hasUnsavedEdits = true;
            selectedTerrainIndex = terrainIndex;
            UpdateInspector();
            canvas.Invalidate();
            statusLabel.Text = "Undid terrain face " + terrainIndex.ToString() + " Z edit. Save Edits to keep the reset.";
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
            for (int i = mobys.Count - 1; i >= 0; i--)
            {
                PointF s = WorldToScreen(mobys[i].X, mobys[i].Y, mobys[i].Z);
                float dx = screen.X - s.X;
                float dy = screen.Y - s.Y;
                if ((dx * dx) + (dy * dy) <= 144f)
                    return i;
            }
            return -1;
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

            int applied = 0;
            foreach (Moby moby in mobys)
            {
                MobyMetadata item;
                int lookupIndex = moby.TrueIndex >= 0 ? moby.TrueIndex : moby.Index;
                if (metadata.TryGetValue(lookupIndex, out item))
                {
                    if (!string.IsNullOrEmpty(item.Label))
                    {
                        moby.Label = item.Label;
                        applied++;
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
                }
                else if (string.IsNullOrEmpty(moby.Label))
                {
                    moby.Label = MobyLoader.FallbackLabel(moby.Type);
                }
                if (includeStoneHillMetadata)
                    ApplyLoaderTablePatchMetadata(moby);
            }
            return applied;
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

            string label = CleanLabel(GeometryLoader.GetString(entry, "displayTargetLabel", ""));
            if (string.IsNullOrEmpty(label))
                label = CleanLabel(GeometryLoader.GetString(entry, "displayLabel", ""));
            if (string.IsNullOrEmpty(label))
                label = CleanLabel(GeometryLoader.GetString(entry, "label", ""));
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
            metadata.Kind = CleanLabel(GeometryLoader.GetString(entry, "candidateKind", ""));
            if (string.IsNullOrEmpty(metadata.Kind))
                metadata.Kind = CleanLabel(GeometryLoader.GetString(entry, "kind", ""));
            metadata.Confidence = CleanLabel(GeometryLoader.GetString(entry, "confidence", ""));
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
        public string Evidence;
        public string BehaviorNote;
        public string SpecialDataNote;
        public string PatchStatus;
        public string PatchLead;
        public int PatchPriority;
        public bool HasColor;
        public Color Color;
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
                if (moby.HasRecordCloneEdit)
                    edit["recordMutation"] = NewRecordCloneMutation(moby);
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

        public static int Load(string path, List<Moby> mobys)
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
            object[] edits = GeometryLoader.GetArray(root, "edits");
            foreach (object obj in edits)
            {
                Dictionary<string, object> edit = obj as Dictionary<string, object>;
                if (edit == null) continue;
                int index = GetInt(edit, "index", -1);
                int trueIndex = GetInt(edit, "trueIndex", -1);
                int legacyIndex = GetInt(edit, "legacyIndex", -1);
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
                    moby.SetRewardColorOverride(rewardColor);
                    changed = true;
                }

                if (changed)
                    applied++;
            }
            return applied;
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
            edit["mode"] = "type20-reward-byte";
            edit["color"] = moby.RewardColorName;
            edit["value"] = moby.RewardValueOverride;
            edit["sourceByte53Hex"] = "0x" + moby.RewardByte53Override.ToString("X2");
            edit["validation"] = "Confirmed on Artisans Flame/Charge chest T50; expected to apply to matching type 0x20 reward-bearing chests/enemies.";
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
            edits.Add(NewSourceByteEdit(0x53, moby.RewardByte53Override, "type20-reward-gem-id-byte"));
            return edits;
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
                    edit["textureId"] = polygon.TextureId;
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
            root["note"] = "Editor-side terrain face Z edits keyed by runtime scene-sector face handles. These are ready for runtime/source terrain patch experiments.";
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
        public readonly int TextureId;
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
            get { return Math.Abs(TerrainEditDeltaZ) > 0.001f; }
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
                return HasPositionEdit || HasGemColorEdit || HasRewardColorEdit || HasHiddenSlotEdit || HasRecordCloneEdit;
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

        public bool Patchable
        {
            get
            {
                return !string.IsNullOrEmpty(PatchStatus) && PatchStatus.IndexOf("patchable", StringComparison.OrdinalIgnoreCase) >= 0;
            }
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

        public void SetRecordCloneOverride(Moby source)
        {
            if (source == null) return;
            if (!HasBaseIdentity)
                CaptureBaseIdentity();

            HasHiddenSlotEdit = false;
            RecordCloneSourceTrueIndex = source.TrueIndex;
            RecordCloneSourceIndex = source.Index;
            RecordCloneSourceLabel = source.DisplayLabel;
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
            ClearRecordMutationOverride();
            RestoreBaseIdentity();
        }
    }
}
