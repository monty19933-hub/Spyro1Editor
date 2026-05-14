param(
    [switch]$SmokeTest,
    [switch]$SmokeTestStoneHillWorkbench
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$script:ProjectPath = $null
$script:Dirty = $false
$script:Refreshing = $false
$script:SelectedEntityId = $null
$script:DragEntityId = $null
$script:DragOffset = New-Object System.Drawing.PointF(0, 0)
$script:Zoom = 0.22
$script:Pan = New-Object System.Drawing.PointF(40, 42)
$script:PanningCanvas = $false
$script:PanDragLast = New-Object System.Drawing.Point(0, 0)
$script:ShowGeometryFaces = $true
$script:ShowGeometryEdges = $true
$script:ShowGeometryPoints = $false
$script:GeometryFaceColorMode = "height"
$script:FastViewportUntil = [DateTime]::MinValue
$script:ViewportSettleTimer = $null
$script:FastViewportModeEnabled = $true
$script:StoneHillTargetPickerItems = @()
$script:StoneHillTargetBox = $null
$script:ObjectsTab = $null

$script:MusicTracks = @(
    @{ Id = "artisans_home"; Name = "Artisans Home" },
    @{ Id = "stone_hill"; Name = "Stone Hill" },
    @{ Id = "dark_hollow"; Name = "Dark Hollow" },
    @{ Id = "town_square"; Name = "Town Square" },
    @{ Id = "peace_keepers_home"; Name = "Peace Keepers Home" },
    @{ Id = "dry_canyon"; Name = "Dry Canyon" },
    @{ Id = "cliff_town"; Name = "Cliff Town" },
    @{ Id = "magic_crafters_home"; Name = "Magic Crafters Home" },
    @{ Id = "alpine_ridge"; Name = "Alpine Ridge" },
    @{ Id = "beast_makers_home"; Name = "Beast Makers Home" },
    @{ Id = "tree_tops"; Name = "Tree Tops" },
    @{ Id = "dream_weavers_home"; Name = "Dream Weavers Home" },
    @{ Id = "haunted_towers"; Name = "Haunted Towers" },
    @{ Id = "gnasty_gnorc"; Name = "Gnasty Gnorc" }
)

$script:Palettes = @(
    @{ Name = "Artisans Day"; Background = "#7AB5FF"; Terrain = "#6CC46F"; Accent = "#E8CE6D" },
    @{ Name = "Peace Keepers Sunset"; Background = "#EE9C54"; Terrain = "#C98345"; Accent = "#3C6F8F" },
    @{ Name = "Magic Crafters Frost"; Background = "#84D6F1"; Terrain = "#8E83C9"; Accent = "#F5F8FF" },
    @{ Name = "Beast Makers Swamp"; Background = "#445D46"; Terrain = "#6B8A3A"; Accent = "#C9B45E" },
    @{ Name = "Dream Weavers Night"; Background = "#272B68"; Terrain = "#7046A6"; Accent = "#F0D35A" },
    @{ Name = "Gnasty Industrial"; Background = "#33373C"; Terrain = "#6E6C57"; Accent = "#D65943" }
)

function New-EditorProject {
    $entities = @(
        [ordered]@{ id = [guid]::NewGuid().ToString(); type = "Gem"; name = "Red Gem 01"; x = 450; y = 420; z = 0; color = "#D93C36"; notes = "Move with drag or numeric fields." },
        [ordered]@{ id = [guid]::NewGuid().ToString(); type = "Gem"; name = "Green Gem 01"; x = 760; y = 620; z = 0; color = "#27A84A"; notes = "" },
        [ordered]@{ id = [guid]::NewGuid().ToString(); type = "Enemy"; name = "Shepherd"; x = 1180; y = 860; z = 0; color = "#7D4F2A"; notes = "Enemy spawn anchor." },
        [ordered]@{ id = [guid]::NewGuid().ToString(); type = "Dragon"; name = "Rescued Dragon"; x = 1740; y = 1000; z = 0; color = "#E9D14C"; notes = "Dragon statue location." }
    )

    return [ordered]@{
        format = "spyro-level-editor-project-v1"
        game = "Spyro the Dragon (PS1)"
        levelName = "Artisans Test Garden"
        selectedLevel = $null
        source = "Prototype editable project. Import extracted map/object data here."
        dimensions = [ordered]@{ width = 2400; height = 1600 }
        palette = [ordered]@{
            name = "Artisans Day"
            background = "#7AB5FF"
            terrain = "#6CC46F"
            accent = "#E8CE6D"
        }
        music = [ordered]@{
            id = "artisans_home"
            name = "Artisans Home"
        }
        discImage = [ordered]@{
            path = ""
            fileName = ""
            sizeBytes = 0
            sha1 = ""
            format = ""
            serial = ""
            volumeId = ""
            importedAt = ""
            analysis = @()
            spyroWad = $null
            stoneHill = $null
            files = @()
        }
        binaryPatches = @()
        geometryOverlay = $null
        geometryOverlayCandidateIndex = 0
        stoneHillPublicRouteEdges = @()
        entities = $entities
    }
}

$script:Project = New-EditorProject

function Convert-HexToColor([string]$Hex) {
    if ([string]::IsNullOrWhiteSpace($Hex)) {
        return [System.Drawing.Color]::Gray
    }
    try {
        return [System.Drawing.ColorTranslator]::FromHtml($Hex)
    }
    catch {
        return [System.Drawing.Color]::Gray
    }
}

function Convert-ColorToHex([System.Drawing.Color]$Color) {
    return "#{0:X2}{1:X2}{2:X2}" -f $Color.R, $Color.G, $Color.B
}

function New-UiColor([int]$R, [int]$G, [int]$B) {
    return [System.Drawing.Color]::FromArgb($R, $G, $B)
}

function New-ActionButton([string]$Text, [int]$Width, [scriptblock]$OnClick) {
    $button = New-Object System.Windows.Forms.Button
    $button.Text = $Text
    $button.Width = $Width
    $button.Height = 32
    $button.MinimumSize = New-Object System.Drawing.Size([Math]::Min($Width, 76), 30)
    $button.FlatStyle = "Flat"
    $button.BackColor = New-UiColor 248 249 251
    $button.ForeColor = New-UiColor 30 36 44
    $button.AutoEllipsis = $true
    $button.Margin = New-Object System.Windows.Forms.Padding(0, 0, 8, 0)
    $button.Add_Click($OnClick)
    return $button
}

function New-FieldLabel([string]$Text) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $Text
    $label.Dock = "Fill"
    $label.TextAlign = "MiddleLeft"
    $label.AutoEllipsis = $true
    $label.ForeColor = New-UiColor 68 74 84
    $label.Margin = New-Object System.Windows.Forms.Padding(0, 4, 8, 4)
    return $label
}

function Ensure-ProjectShape {
    if ($null -eq $script:Project.entities) {
        $script:Project | Add-Member -NotePropertyName entities -NotePropertyValue @()
    }
    if ($null -eq $script:Project.dimensions) {
        $script:Project | Add-Member -NotePropertyName dimensions -NotePropertyValue ([pscustomobject]@{ width = 2400; height = 1600 })
    }
    if ($null -eq $script:Project.PSObject.Properties["selectedLevel"]) {
        $script:Project | Add-Member -NotePropertyName selectedLevel -NotePropertyValue $null
    }
    if ($null -eq $script:Project.palette) {
        $script:Project | Add-Member -NotePropertyName palette -NotePropertyValue ([pscustomobject]@{ name = "Artisans Day"; background = "#7AB5FF"; terrain = "#6CC46F"; accent = "#E8CE6D" })
    }
    if ($null -eq $script:Project.music) {
        $script:Project | Add-Member -NotePropertyName music -NotePropertyValue ([pscustomobject]@{ id = "artisans_home"; name = "Artisans Home" })
    }
    if ($null -eq $script:Project.discImage) {
            $script:Project | Add-Member -NotePropertyName discImage -NotePropertyValue ([pscustomobject]@{ path = ""; fileName = ""; sizeBytes = 0; sha1 = ""; format = ""; serial = ""; volumeId = ""; importedAt = ""; analysis = @(); spyroWad = $null; stoneHill = $null; files = @() })
    }
    if ($script:Project.discImage -is [System.Collections.IDictionary]) {
        if (-not $script:Project.discImage.Contains("analysis")) { $script:Project.discImage["analysis"] = @() }
        if (-not $script:Project.discImage.Contains("spyroWad")) { $script:Project.discImage["spyroWad"] = $null }
        if (-not $script:Project.discImage.Contains("stoneHill")) { $script:Project.discImage["stoneHill"] = $null }
    }
    else {
        if ($null -eq $script:Project.discImage.PSObject.Properties["analysis"]) {
            $script:Project.discImage | Add-Member -NotePropertyName analysis -NotePropertyValue @()
        }
        if ($null -eq $script:Project.discImage.PSObject.Properties["spyroWad"]) {
            $script:Project.discImage | Add-Member -NotePropertyName spyroWad -NotePropertyValue $null
        }
        if ($null -eq $script:Project.discImage.PSObject.Properties["stoneHill"]) {
            $script:Project.discImage | Add-Member -NotePropertyName stoneHill -NotePropertyValue $null
        }
    }
    if ($null -eq $script:Project.binaryPatches) {
        $script:Project | Add-Member -NotePropertyName binaryPatches -NotePropertyValue @()
    }
    if ($script:Project -is [System.Collections.IDictionary]) {
        if (-not $script:Project.Contains("geometryOverlay")) { $script:Project["geometryOverlay"] = $null }
        if (-not $script:Project.Contains("geometryOverlayCandidateIndex")) { $script:Project["geometryOverlayCandidateIndex"] = 0 }
        if (-not $script:Project.Contains("stoneHillPublicRouteEdges")) { $script:Project["stoneHillPublicRouteEdges"] = @() }
    }
    else {
        if ($null -eq $script:Project.PSObject.Properties["geometryOverlay"]) {
            $script:Project | Add-Member -NotePropertyName geometryOverlay -NotePropertyValue $null
        }
        if ($null -eq $script:Project.PSObject.Properties["geometryOverlayCandidateIndex"]) {
            $script:Project | Add-Member -NotePropertyName geometryOverlayCandidateIndex -NotePropertyValue 0
        }
        if ($null -eq $script:Project.PSObject.Properties["stoneHillPublicRouteEdges"]) {
            $script:Project | Add-Member -NotePropertyName stoneHillPublicRouteEdges -NotePropertyValue @()
        }
    }
}

function Get-EntityById([string]$Id) {
    foreach ($entity in $script:Project.entities) {
        if ((Get-ObjectField $entity "id" "") -eq $Id) {
            return $entity
        }
    }
    return $null
}

function Get-ObjectField($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $value = $null
    $found = $false
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) {
            $value = $Object[$Name]
            $found = $true
        }
    }
    else {
        $property = $Object.PSObject.Properties[$Name]
        if ($null -ne $property) {
            $value = $property.Value
            $found = $true
        }
    }
    if (-not $found -or $null -eq $value) { return $Default }
    if ($value -is [System.Array] -and $value.Count -gt 0) { return $value[0] }
    return $value
}

function Set-ObjectField($Object, [string]$Name, $Value) {
    if ($Object -is [System.Collections.IDictionary]) {
        $Object[$Name] = $Value
        return
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
    else {
        $Object.$Name = $Value
    }
}

function Get-MobyTypeKey($Entity) {
    if ($null -eq $Entity) { return "" }
    $typeIdValue = Get-ObjectField $Entity "mobyTypeId" $null
    if ($null -eq $typeIdValue) { return "" }
    $typeId = [int](Get-NumberField $Entity "mobyTypeId" 0)
    $state = [int](Get-NumberField $Entity "mobyState" 0)
    $index = [int](Get-NumberField $Entity "runtimeMobyIndex" -1)
    $table = [string](Get-ObjectField $Entity "runtimeMobyTable" "level")
    $tablePrefix = switch ($table) {
        "dynamic" { "D" }
        "special-data" { "S" }
        default { "L" }
    }
    if ($index -ge 0) {
        return "{0}#{1} 0x{2:X2}/S{3:X2}" -f $tablePrefix, $index, $typeId, $state
    }
    return "0x{0:X2} / S{1:X2}" -f $typeId, $state
}

function Get-MobyTypeHintById([int]$TypeId) {
    switch ($TypeId) {
        0x20 { return [ordered]@{ name = "Likely gem/collectible"; confidence = "strong"; color = "#F4D44D"; note = "Confirmed by the available Stone Hill before/after gem collection RAM sample." } }
        0x30 { return [ordered]@{ name = "Dragon/NPC candidate"; confidence = "weak"; color = "#B78CFF"; note = "Placement-like Stone Hill records with special-data pointers; needs a save/free-dragon RAM sample before naming." } }
        0x18 { return [ordered]@{ name = "Active object/enemy candidate"; confidence = "weak"; color = "#FF8C5A"; note = "Repeated placed records; compare focused ram/shepherd/fodder samples before assigning enemy names." } }
        0x0A { return [ordered]@{ name = "Level helper/system candidate"; confidence = "weak"; color = "#8FA6B8"; note = "Helper-looking placeholder record; not yet an editable placement target." } }
        0x33 { return [ordered]@{ name = "Level helper/system candidate"; confidence = "weak"; color = "#8FA6B8"; note = "Helper-looking placeholder record; not yet an editable placement target." } }
        0x52 { return [ordered]@{ name = "Level helper/system candidate"; confidence = "weak"; color = "#8FA6B8"; note = "Helper-looking placeholder record; not yet an editable placement target." } }
        default { return [ordered]@{ name = "Unknown moby"; confidence = "unknown"; color = "#5DADE2"; note = "Needs focused before/after RAM samples." } }
    }
}

function Get-MobyTypeNameById([int]$TypeId) {
    return [string](Get-MobyTypeHintById $TypeId).name
}

function Get-MobyTypeColorById([int]$TypeId) {
    return [string](Get-MobyTypeHintById $TypeId).color
}

function Get-MobyTypeNoteById([int]$TypeId) {
    $hint = Get-MobyTypeHintById $TypeId
    if ([string]$hint.confidence -eq "strong") {
        return "Type hint: $($hint.name); $($hint.note) "
    }
    return "Type hint: $($hint.name) ($($hint.confidence)); $($hint.note) "
}

function Get-MobyTypeLabel($Entity) {
    $key = Get-MobyTypeKey $Entity
    if ([string]::IsNullOrWhiteSpace($key)) { return "" }
    $typeId = [int](Get-NumberField $Entity "mobyTypeId" 0)
    return "$(Get-MobyTypeNameById $typeId) $key"
}

function Get-EntityTypeText($Entity) {
    $type = [string](Get-ObjectField $Entity "type" "Object")
    if ($type -eq "Moby") {
        $mobyType = [int](Get-NumberField $Entity "mobyTypeId" -1)
        if ($mobyType -ge 0) {
            $name = Get-MobyTypeNameById $mobyType
            if ($name -ne "Unknown moby") {
                return "$name 0x$('{0:X2}' -f $mobyType)"
            }
            return "Moby 0x{0:X2}" -f $mobyType
        }
    }
    return $type
}

function Get-MobyTypeDistributionText {
    $runtimeMobys = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" })
    if ($runtimeMobys.Count -eq 0) { return "Moby types: none" }

    $groups = @($runtimeMobys | Group-Object { "0x{0:X2}" -f [int](Get-NumberField $_ "mobyTypeId" 0) } | Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, @{ Expression = { $_.Name }; Descending = $false } | Select-Object -First 10)
    $parts = @()
    foreach ($group in $groups) {
        $typeId = [Convert]::ToInt32(([string]$group.Name).Substring(2), 16)
        $hint = Get-MobyTypeNameById $typeId
        $parts += "$($group.Name)x$($group.Count) $hint"
    }
    return "Moby types: " + ($parts -join ", ")
}

function Get-RuntimeMobyCanvasStats {
    $runtimeMobys = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" })
    $placements = @($runtimeMobys | Where-Object { Test-PlacementLikeEntity $_ })
    $placeholders = $runtimeMobys.Count - $placements.Count
    $sparse = @($runtimeMobys | Where-Object { (Get-ObjectField $_ "runtimeConfidence" "") -eq "sparse-table-candidate" }).Count
    $width = [double](Get-NumberField $script:Project.dimensions "width" 2400)
    $height = [double](Get-NumberField $script:Project.dimensions "height" 1600)
    $offCanvas = @($runtimeMobys | Where-Object {
        $x = [double](Get-NumberField $_ "x" 0)
        $y = [double](Get-NumberField $_ "y" 0)
        ($x -lt 0 -or $y -lt 0 -or $x -gt $width -or $y -gt $height)
    }).Count
    return [ordered]@{
        runtime = $runtimeMobys.Count
        placements = $placements.Count
        placeholders = $placeholders
        sparse = $sparse
        offCanvas = $offCanvas
    }
}

function Test-PlacementLikeEntity($Entity) {
    if ($null -eq $Entity) { return $false }
    $raw = Get-ObjectField $Entity "rawPosition" $null
    if ($null -ne $raw) {
        $rawX = [int](Get-NumberField $raw "x" 0)
        $rawY = [int](Get-NumberField $raw "y" 0)
        $rawZ = [int](Get-NumberField $raw "z" 0)
        if ($rawX -eq 0 -and $rawY -eq 4096 -and $rawZ -eq 0) { return $false }
    }
    $x = [double](Get-NumberField $Entity "x" 0)
    $y = [double](Get-NumberField $Entity "y" 0)
    return ($x -gt 0 -or $y -gt 512)
}

function Get-NumberField($Object, [string]$Name, [double]$Default = 0) {
    $value = Get-ObjectField $Object $Name $Default
    if ($value -is [System.Array] -and $value.Count -gt 0) { $value = $value[0] }
    try {
        return [double]$value
    }
    catch {
        return $Default
    }
}

function Get-ObjectFieldArray($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    $value = $null
    $found = $false
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) {
            $value = $Object[$Name]
            $found = $true
        }
    }
    else {
        $property = $Object.PSObject.Properties[$Name]
        if ($null -ne $property) {
            $value = $property.Value
            $found = $true
        }
    }
    if (-not $found -or $null -eq $value) { return @() }
    return @($value)
}

function Convert-ToNumber($Value, [double]$Default = 0) {
    if ($Value -is [System.Array] -and $Value.Count -gt 0) { $Value = $Value[0] }
    try {
        return [double]$Value
    }
    catch {
        return $Default
    }
}

function Set-Dirty([bool]$Value) {
    $script:Dirty = $Value
    Update-Title
}

function Update-Title {
    if ($null -eq $script:Form) { return }
    $name = $script:Project.levelName
    if ([string]::IsNullOrWhiteSpace($name)) { $name = "Untitled Level" }
    $suffix = ""
    if ($script:Dirty) { $suffix = " *" }
    $script:Form.Text = "Spyro PS1 Level Editor - $name$suffix"
}

function World-ToScreen([float]$X, [float]$Y) {
    $zoom = [double](Convert-ToNumber $script:Zoom 0.22)
    $panX = [double](Convert-ToNumber $script:Pan.X 40)
    $panY = [double](Convert-ToNumber $script:Pan.Y 42)
    return New-Object System.Drawing.PointF(([float](($X * $zoom) + $panX)), ([float](($Y * $zoom) + $panY)))
}

function Screen-ToWorld([float]$X, [float]$Y) {
    $zoom = [double][Math]::Max(0.001, (Convert-ToNumber $script:Zoom 0.22))
    $panX = [double](Convert-ToNumber $script:Pan.X 40)
    $panY = [double](Convert-ToNumber $script:Pan.Y 42)
    return New-Object System.Drawing.PointF(([float](($X - $panX) / $zoom)), ([float](($Y - $panY) / $zoom)))
}

function Set-CanvasZoom([double]$NewZoom, $AnchorX = $null, $AnchorY = $null) {
    if ($null -eq $script:Canvas) {
        $script:Zoom = [Math]::Max(0.008, [Math]::Min(3.0, $NewZoom))
        return
    }
    $oldZoom = [Math]::Max(0.001, [double]$script:Zoom)
    $anchorScreenX = if ($null -ne $AnchorX) { [double]$AnchorX } else { [Math]::Max(1, $script:Canvas.ClientSize.Width / 2) }
    $anchorScreenY = if ($null -ne $AnchorY) { [double]$AnchorY } else { [Math]::Max(1, $script:Canvas.ClientSize.Height / 2) }
    $worldX = ($anchorScreenX - [double]$script:Pan.X) / $oldZoom
    $worldY = ($anchorScreenY - [double]$script:Pan.Y) / $oldZoom
    $script:Zoom = [Math]::Max(0.008, [Math]::Min(3.0, $NewZoom))
    $script:Pan = New-Object System.Drawing.PointF(([float]($anchorScreenX - ($worldX * $script:Zoom))), ([float]($anchorScreenY - ($worldY * $script:Zoom))))
    $script:Canvas.Invalidate()
}

function Get-GeometryLayerStatus {
    $faces = if ($script:ShowGeometryFaces) { "faces on" } else { "faces off" }
    $edges = if ($script:ShowGeometryEdges) { "lines on" } else { "lines off" }
    $points = if ($script:ShowGeometryPoints) { "points on" } else { "points off" }
    $mode = if ([string]$script:GeometryFaceColorMode -eq "material") { "materials" } else { "height" }
    return "$faces, $edges, $points, face color $mode"
}

function Toggle-GeometryLayer([string]$Layer) {
    switch ($Layer) {
        "faces" { $script:ShowGeometryFaces = -not [bool]$script:ShowGeometryFaces }
        "edges" { $script:ShowGeometryEdges = -not [bool]$script:ShowGeometryEdges }
        "points" { $script:ShowGeometryPoints = -not [bool]$script:ShowGeometryPoints }
    }
    if ($null -ne $script:Canvas) { $script:Canvas.Invalidate() }
    if ($null -ne $script:StatusLabel) {
        $script:StatusLabel.Text = "Terrain layers: $(Get-GeometryLayerStatus)"
    }
}

function Toggle-GeometryFaceColorMode {
    if ([string]$script:GeometryFaceColorMode -eq "material") {
        $script:GeometryFaceColorMode = "height"
    }
    else {
        $script:GeometryFaceColorMode = "material"
    }
    if ($null -ne $script:Canvas) { $script:Canvas.Invalidate() }
    if ($null -ne $script:StatusLabel) {
        $script:StatusLabel.Text = "Terrain layers: $(Get-GeometryLayerStatus)"
    }
}

function Test-FastViewportMode {
    if (-not [bool]$script:FastViewportModeEnabled) { return $false }
    return ((Get-Date) -lt $script:FastViewportUntil)
}

function Start-FastViewportMode([int]$Milliseconds = 260) {
    if (-not [bool]$script:FastViewportModeEnabled) { return }
    $script:FastViewportUntil = (Get-Date).AddMilliseconds($Milliseconds)
    if ($null -ne $script:ViewportSettleTimer) {
        $script:ViewportSettleTimer.Stop()
        $script:ViewportSettleTimer.Interval = [Math]::Max(60, $Milliseconds + 45)
        $script:ViewportSettleTimer.Start()
    }
}

function Reset-CanvasView {
    if ($null -eq $script:Canvas) { return }
    $levelWidth = [Math]::Max(200, (Get-NumberField $script:Project.dimensions "width" 2400))
    $levelHeight = [Math]::Max(200, (Get-NumberField $script:Project.dimensions "height" 1600))
    $availableWidth = [Math]::Max(200, $script:Canvas.ClientSize.Width - 80)
    $availableHeight = [Math]::Max(160, $script:Canvas.ClientSize.Height - 80)
    $script:Zoom = [Math]::Max(0.02, [Math]::Min(0.7, [Math]::Min(($availableWidth / $levelWidth), ($availableHeight / $levelHeight))))
    $script:Pan = New-Object System.Drawing.PointF(40, 40)
    $script:Canvas.Invalidate()
}

function Hit-TestEntity([int]$ScreenX, [int]$ScreenY) {
    for ($i = $script:Project.entities.Count - 1; $i -ge 0; $i--) {
        $entity = $script:Project.entities[$i]
        $pt = World-ToScreen (Get-NumberField $entity "x" 0) (Get-NumberField $entity "y" 0)
        $dx = $ScreenX - $pt.X
        $dy = $ScreenY - $pt.Y
        if (($dx * $dx + $dy * $dy) -le 144) {
            return $entity
        }
    }
    return $null
}

function Refresh-EntityList {
    if ($null -eq $script:EntityList) { return }
    $script:EntityList.BeginUpdate()
    $script:EntityList.Items.Clear()
    foreach ($entity in $script:Project.entities) {
        $item = New-Object System.Windows.Forms.ListViewItem([string](Get-ObjectField $entity "name" "Object"))
        [void]$item.SubItems.Add((Get-EntityTypeText $entity))
        [void]$item.SubItems.Add((Get-MobyTypeKey $entity))
        [void]$item.SubItems.Add(("{0}, {1}, {2}" -f [int](Get-NumberField $entity "x" 0), [int](Get-NumberField $entity "y" 0), [int](Get-NumberField $entity "z" 0)))
        [void]$item.SubItems.Add([string](Get-ObjectField $entity "patchStatus" ""))
        $item.Tag = [string](Get-ObjectField $entity "id" "")
        if ($item.Tag -eq $script:SelectedEntityId) { $item.Selected = $true }
        [void]$script:EntityList.Items.Add($item)
    }
    $script:EntityList.EndUpdate()
    Resize-EntityListColumns
    Refresh-ObjectSummary
}

function Resize-EntityListColumns {
    if ($null -eq $script:EntityList -or $script:EntityList.Columns.Count -lt 5) { return }
    $width = [Math]::Max(320, $script:EntityList.ClientSize.Width - 24)
    $script:EntityList.Columns[1].Width = 80
    $script:EntityList.Columns[2].Width = 122
    $script:EntityList.Columns[3].Width = 126
    $script:EntityList.Columns[4].Width = 170
    $script:EntityList.Columns[0].Width = [Math]::Max(120, $width - $script:EntityList.Columns[1].Width - $script:EntityList.Columns[2].Width - $script:EntityList.Columns[3].Width - $script:EntityList.Columns[4].Width)
}

function Format-StoneHillIndexList([object[]]$Indexes, [int]$Max = 10) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Where-Object { $_ -ge 0 } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    $shown = @($items | Select-Object -First $Max | ForEach-Object { "L$_" })
    if ($items.Count -gt $shown.Count) {
        return "$($shown -join ', ') +$($items.Count - $shown.Count)"
    }
    return ($shown -join ", ")
}

function Test-StoneHillEntityCandidateMatch($Entity, [string[]]$Patterns) {
    if ($null -eq $Entity -or $Patterns.Count -eq 0) { return $false }

    $texts = New-Object System.Collections.Generic.List[string]
    foreach ($field in @("stoneHillDisplayTargetLabel", "candidateKind", "stoneHillWorkingRole", "stoneHillMapZone")) {
        $value = [string](Get-ObjectField $Entity $field "")
        if (-not [string]::IsNullOrWhiteSpace($value)) { [void]$texts.Add($value) }
    }

    $guess = Get-ObjectField $Entity "stoneHillPublicTargetGuess" $null
    foreach ($field in @("label")) {
        $value = [string](Get-ObjectField $guess $field "")
        if (-not [string]::IsNullOrWhiteSpace($value)) { [void]$texts.Add($value) }
    }

    foreach ($candidate in @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetCandidates")) {
        foreach ($field in @("name", "category")) {
            $value = [string](Get-ObjectField $candidate $field "")
            if (-not [string]::IsNullOrWhiteSpace($value)) { [void]$texts.Add($value) }
        }
    }

    foreach ($text in $texts.ToArray()) {
        foreach ($pattern in $Patterns) {
            if ($text -like $pattern) { return $true }
        }
    }
    return $false
}

function Get-StoneHillRuntimeIndexesByCandidate([string[]]$Patterns) {
    return @($script:Project.entities | Where-Object {
        (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" -and
        (Test-StoneHillEntityCandidateMatch $_ $Patterns)
    } | ForEach-Object { [int](Get-NumberField $_ "runtimeMobyIndex" -1) })
}

function Get-StoneHillTargetLookupBrief {
    $runtimeMobys = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" })
    if ($runtimeMobys.Count -eq 0) { return @() }

    $treasure = @($runtimeMobys | Where-Object {
        [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x20 -and (Test-PlacementLikeEntity $_)
    } | ForEach-Object { [int](Get-NumberField $_ "runtimeMobyIndex" -1) })

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("Target lookup: treasure/gems $(Format-StoneHillIndexList $treasure 8); Astor $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Astor')) 4); Lindar/Gildas $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Lindar', 'Gildas')) 4); Gavin $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Gavin')) 4)")
    [void]$lines.Add("Target lookup: blue thief $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Blue thief')) 4); ram/shepherd $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Ram', 'Shepherd', 'Ram or shepherd')) 8); locked chest $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Locked chest*')) 4); hidden key $(Format-StoneHillIndexList (Get-StoneHillRuntimeIndexesByCandidate @('Hidden beach key')) 4)")
    return @($lines.ToArray())
}

function Get-StoneHillSourceWindowCount($Entity) {
    $lead = Get-ObjectField $Entity "stoneHillSourceLead" $null
    if ($null -eq $lead) {
        $marker = Get-ObjectField $Entity "stoneHillReplicaMarker" $null
        $lead = Get-ObjectField $marker "sourceLead" $null
    }
    if ($null -eq $lead) { return 0 }
    $scaledCount = [int](Get-NumberField $lead "scaledWindowCount" -1)
    if ($scaledCount -ge 0) { return $scaledCount }
    return @(Get-ObjectFieldArray $lead "scaledAxisWindows").Count
}

function Get-StoneHillPatchPriorityRank([string]$Priority) {
    switch ($Priority) {
        "high" { return 0 }
        "medium" { return 1 }
        "low" { return 2 }
        default { return 9 }
    }
}

function Get-StoneHillEntityPatchPriority($Entity) {
    $marker = Get-ObjectField $Entity "stoneHillReplicaMarker" $null
    $priority = Get-ObjectField (Get-ObjectField $marker "sourcePatchPriority" $null) "priority" ""
    if ([string]::IsNullOrWhiteSpace([string]$priority) -and (Get-StoneHillSourceWindowCount $Entity) -gt 0) {
        return "unranked"
    }
    return [string]$priority
}

function Get-StoneHillCandidateSummary($Entity) {
    $candidates = @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetCandidates")
    if ($candidates.Count -eq 0) {
        $guess = Get-ObjectField $Entity "stoneHillPublicTargetGuess" $null
        $label = [string](Get-ObjectField $guess "label" "")
        if (-not [string]::IsNullOrWhiteSpace($label)) { return $label }
        return ""
    }

    $names = @($candidates | Select-Object -First 3 | ForEach-Object {
        $name = [string](Get-ObjectField $_ "name" "")
        $confidence = [string](Get-ObjectField $_ "confidence" "")
        if ([string]::IsNullOrWhiteSpace($name)) {
            $null
        }
        elseif (-not [string]::IsNullOrWhiteSpace($confidence)) {
            "$name ($confidence)"
        }
        else {
            $name
        }
    } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    return ($names -join " / ")
}

function Get-StoneHillSelectedProofText($Entity) {
    if ($null -eq $Entity) { return "" }
    if ((Get-ObjectField $Entity "origin" "") -ne "runtime-moby-ram-dump") { return "" }
    if ($null -eq (Get-ObjectField $Entity "stoneHillReplicaMarker" $null) -and @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetCandidates").Count -eq 0) { return "" }

    $index = [int](Get-NumberField $Entity "runtimeMobyIndex" -1)
    $typeHex = "0x{0:X2}" -f [int](Get-NumberField $Entity "mobyTypeId" 0)
    $label = [string](Get-ObjectField $Entity "stoneHillDisplayTargetLabel" "")
    if ([string]::IsNullOrWhiteSpace($label)) { $label = Get-StoneHillCandidateSummary $Entity }
    if ([string]::IsNullOrWhiteSpace($label)) { $label = [string](Get-ObjectField $Entity "candidateKind" "Stone Hill moby") }

    $sourceStatus = [string](Get-ObjectField $Entity "stoneHillSourceStatus" "")
    if ([string]::IsNullOrWhiteSpace($sourceStatus)) {
        $sourceStatus = $(if ((Get-StoneHillSourceWindowCount $Entity) -gt 0) { "scaled-int16 source-window lead" } else { "no scaled source window" })
    }
    $priority = Get-StoneHillEntityPatchPriority $Entity
    $guess = Get-ObjectField $Entity "stoneHillPublicTargetGuess" $null
    $next = [string](Get-ObjectField $guess "nextValidation" "")
    if ([string]::IsNullOrWhiteSpace($next)) {
        $next = "Capture a focused before/after RAM sample or patch-test the source lead on a disposable BIN."
    }

    $prefix = $(if ($index -ge 0) { "L$index" } else { "runtime moby" })
    $priorityText = $(if (-not [string]::IsNullOrWhiteSpace($priority) -and $priority -ne "none") { "; patch priority $priority" } else { "" })
    return "Stone Hill selected: $prefix $typeHex -> $label; $sourceStatus$priorityText. Next proof: $next"
}

function Get-StoneHillWorkbenchBriefLines {
    $runtimeMobys = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" })
    if ($runtimeMobys.Count -eq 0) { return @() }

    $hasStoneHillPlan = (@($runtimeMobys | Where-Object {
        $null -ne (Get-ObjectField $_ "stoneHillReplicaMarker" $null) -or @(Get-ObjectFieldArray $_ "stoneHillPublicTargetCandidates").Count -gt 0
    }).Count -gt 0)
    if (-not $hasStoneHillPlan) { return @() }

    $moveNow = @($runtimeMobys | Where-Object {
        [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x20 -and (Test-PlacementLikeEntity $_)
    } | ForEach-Object { [int](Get-NumberField $_ "runtimeMobyIndex" -1) })

    $sourceLeadMobys = @($runtimeMobys | Where-Object { (Get-StoneHillSourceWindowCount $_) -gt 0 })
    $highPriority = @($sourceLeadMobys | Where-Object { (Get-StoneHillEntityPatchPriority $_) -eq "high" } | ForEach-Object { [int](Get-NumberField $_ "runtimeMobyIndex" -1) })
    $dragonCandidates = @($runtimeMobys | Where-Object { [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x30 } | ForEach-Object { [int](Get-NumberField $_ "runtimeMobyIndex" -1) })
    $enemyCandidates = @($runtimeMobys | Where-Object { [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x18 } | ForEach-Object { [int](Get-NumberField $_ "runtimeMobyIndex" -1) })

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("Stone Hill brief: move confirmed treasure-class mobys now: $(Format-StoneHillIndexList $moveNow 10)")
    [void]$lines.Add("Patch-testable source leads: $($sourceLeadMobys.Count) total; high priority: $(Format-StoneHillIndexList $highPriority 8)")
    [void]$lines.Add("Identity buckets still need proof: dragons $(Format-StoneHillIndexList $dragonCandidates 6); enemies/thief $(Format-StoneHillIndexList $enemyCandidates 8); Gavin/key/chest need focused RAM pairs.")
    foreach ($line in Get-StoneHillTargetLookupBrief) {
        [void]$lines.Add($line)
    }
    return @($lines.ToArray())
}

function Refresh-ObjectSummary {
    if ($null -eq $script:ObjectSummaryBox) { return }
    $count = @($script:Project.entities).Count
    $runtimeCount = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" }).Count
    $exactLinks = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "patchStatus" "") -eq "disc-linked-candidate" }).Count
    $ambiguousLinks = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "patchStatus" "") -eq "ambiguous-wad-coordinate-match" }).Count
    $rankedLinks = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "patchStatus" "") -eq "disc-linked-ranked-candidate" }).Count
    $probeLinks = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "patchStatus" "") -eq "probe-match-no-exact-triple" }).Count
    $missingLinks = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "patchStatus" "") -eq "no-wad-coordinate-match" }).Count
    $publicHintCount = @($script:Project.entities | Where-Object { @(Get-ObjectFieldArray $_ "stoneHillPublicTargetHints").Count -gt 0 }).Count
    $routeAnchorCount = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "stone-hill-public-route-anchor" }).Count
    $routeEdgeCount = @(Get-ObjectFieldArray $script:Project "stoneHillPublicRouteEdges").Count
    $mobyCanvasStats = Get-RuntimeMobyCanvasStats
    if ($count -eq 0) {
        $script:ObjectSummaryBox.Text = "No objects loaded."
        return
    }

    $selected = Get-EntityById $script:SelectedEntityId
    $selectedText = "None selected"
    if ($null -ne $selected) {
        $selectedText = "{0} | {1} | {2}" -f (Get-ObjectField $selected "name" "Object"), (Get-ObjectField $selected "runtimeAddress" "manual"), (Get-ObjectField $selected "patchStatus" "")
    }
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($line in @(
        "Objects loaded: $count",
        "Runtime mobys: $runtimeCount",
        "Runtime placements: $($mobyCanvasStats.placements) placed, $($mobyCanvasStats.placeholders) placeholder/helper, $($mobyCanvasStats.sparse) sparse, $($mobyCanvasStats.offCanvas) off-canvas",
        "Stone Hill route map: $routeAnchorCount anchor(s), $routeEdgeCount route edge(s)",
        "WAD links: $exactLinks exact, $rankedLinks ranked, $ambiguousLinks ambiguous, $probeLinks probe-only, $missingLinks missing",
        "Public-route hints: $publicHintCount tentative marker hint(s)",
        (Get-MobyTypeDistributionText),
        "Selected: $selectedText"
    )) {
        [void]$lines.Add($line)
    }
    foreach ($line in Get-StoneHillWorkbenchBriefLines) {
        [void]$lines.Add($line)
    }
    $selectedProof = Get-StoneHillSelectedProofText $selected
    if (-not [string]::IsNullOrWhiteSpace($selectedProof)) {
        [void]$lines.Add($selectedProof)
    }
    $script:ObjectSummaryBox.Text = ($lines.ToArray() -join [Environment]::NewLine)
}

function Format-HexBytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    if ($Bytes.Length -eq 0) { return "" }
    $start = [Math]::Max(0, $Offset)
    $end = [Math]::Min($Bytes.Length, $start + $Length)
    $parts = @()
    for ($i = $start; $i -lt $end; $i++) {
        $parts += ("{0:X2}" -f $Bytes[$i])
    }
    return ($parts -join " ")
}

function Get-ByteWindowProfile([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $stats = Get-ByteWindowStats $Bytes $Offset $Length
    return $stats.profile
}

function Get-ByteWindowStats([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    if ($Bytes.Length -eq 0) {
        return [ordered]@{ profile = "empty"; shape = "empty"; uniqueCount = 0; zeroPct = 0; score = 9999 }
    }
    $start = [Math]::Max(0, $Offset)
    $end = [Math]::Min($Bytes.Length, $start + $Length)
    $count = [Math]::Max(1, $end - $start)
    $zeroCount = 0
    $unique = @{}
    for ($i = $start; $i -lt $end; $i++) {
        if ($Bytes[$i] -eq 0) { $zeroCount++ }
        $unique[[int]$Bytes[$i]] = $true
    }
    $zeroPct = [Math]::Round(($zeroCount * 100.0) / $count, 1)
    $uniqueCount = $unique.Keys.Count
    $shape = "mixed"
    if ($zeroPct -gt 45) { $shape = "zero-heavy/structured" }
    elseif ($uniqueCount -gt 48) { $shape = "high-entropy/packed" }
    elseif ($uniqueCount -lt 18) { $shape = "low-variety/table-like" }
    $score = [Math]::Round(($uniqueCount - ($zeroPct / 5.0)), 2)
    return [ordered]@{
        profile = "$shape, unique=$uniqueCount, zeros=$zeroPct%, score=$score"
        shape = $shape
        uniqueCount = $uniqueCount
        zeroPct = $zeroPct
        score = $score
    }
}

function Get-SelectedLevelAssetBytes {
    if ([string]::IsNullOrWhiteSpace($script:Project.discImage.path) -or -not (Test-Path -LiteralPath $script:Project.discImage.path)) { return $null }
    if ($null -eq $script:Project.selectedLevel -or $null -eq $script:Project.selectedLevel.assetOffset) { return $null }
    $wadEntry = @($script:Project.discImage.files | Where-Object { $_.path -eq "WAD.WAD" } | Select-Object -First 1)
    if ($wadEntry.Count -eq 0) { return $null }
    $layout = Detect-DiscLayout $script:Project.discImage.path
    $stream = [System.IO.File]::OpenRead($script:Project.discImage.path)
    try {
        return Read-DiscFileBytes $stream $layout ([int]$wadEntry[0].lba) ([int64]$script:Project.selectedLevel.assetOffset) ([int]$script:Project.selectedLevel.assetSize)
    }
    finally {
        $stream.Dispose()
    }
}

function Refresh-WadCandidateView {
    if ($null -eq $script:WadCandidateBox) { return }
    $entity = Get-EntityById $script:SelectedEntityId
    if ($null -eq $entity) {
        $script:WadCandidateBox.Text = "Select a runtime moby to inspect WAD/source candidates."
        return
    }

    $candidates = @(Get-ObjectFieldArray $entity "wadLinkCandidates")
    $link = Get-ObjectField $entity "wadLink" $null
    $sourceLead = Get-ObjectField $entity "stoneHillSourceLead" $null
    if ($candidates.Count -eq 0 -and $null -ne $link) {
        $candidates = @([int64](Get-NumberField $link "assetRelativeOffset" 0))
    }
    if ($candidates.Count -eq 0) {
        if ($null -ne $sourceLead) {
            $script:WadCandidateBox.Text = Format-StoneHillSourceLeadReport $entity $sourceLead $null
            return
        }
        $script:WadCandidateBox.Text = "No WAD/source candidate offsets for selected object. Run Link WAD or load the Stone Hill workbench source-link report."
        return
    }

    try {
        $assetBytes = Get-SelectedLevelAssetBytes
        if ($null -eq $assetBytes) {
            if ($null -ne $sourceLead) {
                $script:WadCandidateBox.Text = Format-StoneHillSourceLeadReport $entity $sourceLead $null
                return
            }
            $script:WadCandidateBox.Text = "Selected level asset bytes are not available."
            return
        }

        $lines = New-Object System.Collections.Generic.List[string]
        $entityName = Get-ObjectField $entity "name" "Object"
        $runtimeAddress = Get-ObjectField $entity "runtimeAddress" ""
        $mobyTypeText = "0x{0:X2}" -f [int](Get-NumberField $entity "mobyTypeId" 0)
        $mobyStateText = "0x{0:X2}" -f [int](Get-NumberField $entity "mobyState" 0)
        [void]$lines.Add("Selected: $entityName")
        [void]$lines.Add("Runtime: $runtimeAddress  Type: $mobyTypeText  State: $mobyStateText")
        if ($null -ne $sourceLead) {
            $windows = @(Get-ObjectFieldArray $sourceLead "scaledAxisWindows")
            [void]$lines.Add("Stone Hill source leads: $($windows.Count) scaled-int16 window(s), lead-only until byte-level validation")
        }
        [void]$lines.Add("Candidates: $($candidates.Count)")
        [void]$lines.Add("")
        foreach ($candidate in $candidates | Select-Object -First 8) {
            $offset = [int]$candidate
            $windowStart = [Math]::Max(0, $offset - 32)
            $hex = Format-HexBytes $assetBytes $windowStart 96
            $profile = Get-ByteWindowProfile $assetBytes $windowStart 96
            $isBest = $false
            if ($null -ne $link -and [int64](Get-NumberField $link "assetRelativeOffset" -1) -eq [int64]$candidate) {
                $isBest = $true
            }
            $bestText = $(if ($isBest) { "  BEST" } else { "" })
            [void]$lines.Add("asset+0x$('{0:X}' -f $offset)  window asset+0x$('{0:X}' -f $windowStart)$bestText")
            [void]$lines.Add("  profile: $profile")
            if (($offset + 12) -le $assetBytes.Length) {
                [void]$lines.Add("  int32 @candidate: $(Get-Int32LE $assetBytes $offset), $(Get-Int32LE $assetBytes ($offset + 4)), $(Get-Int32LE $assetBytes ($offset + 8))")
            }
            [void]$lines.Add("  $hex")
            [void]$lines.Add("")
        }
        if ($null -ne $sourceLead) {
            [void]$lines.Add("Stone Hill source-window leads")
            [void]$lines.Add("------------------------------")
            [void]$lines.Add((Format-StoneHillSourceLeadReport $entity $sourceLead $assetBytes))
        }
        $script:WadCandidateBox.Text = ($lines.ToArray() -join [Environment]::NewLine)
    }
    catch {
        $script:WadCandidateBox.Text = "Could not inspect WAD/source candidates: $($_.Exception.Message)"
    }
}

function Convert-HexTextToInt64([string]$HexText, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($HexText)) { return $Default }
    $clean = $HexText.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    try {
        return [Convert]::ToInt64($clean, 16)
    }
    catch {
        return $Default
    }
}

function Format-StoneHillSourceLeadReport($Entity, $SourceLead, [byte[]]$AssetBytes) {
    $lines = New-Object System.Collections.Generic.List[string]
    $entityName = Get-ObjectField $Entity "name" "Object"
    $runtimeAddress = Get-ObjectField $Entity "runtimeAddress" ""
    $mobyTypeText = "0x{0:X2}" -f [int](Get-NumberField $Entity "mobyTypeId" 0)
    $mobyStateText = "0x{0:X2}" -f [int](Get-NumberField $Entity "mobyState" 0)
    $candidateKind = [string](Get-ObjectField $SourceLead "candidateKind" (Get-ObjectField $Entity "candidateKind" "unidentified moby"))
    $confidence = [string](Get-ObjectField $SourceLead "confidence" (Get-ObjectField $Entity "catalogConfidence" "unknown"))
    $placementLike = [bool](Get-ObjectField $SourceLead "placementLike" $false)
    $placeholder = [bool](Get-ObjectField $SourceLead "placeholderPosition" $false)
    $windows = @(Get-ObjectFieldArray $SourceLead "scaledAxisWindows")
    $exactMatches = @(Get-ObjectFieldArray $SourceLead "exactMatches")

    [void]$lines.Add("Selected: $entityName")
    [void]$lines.Add("Runtime: $runtimeAddress  Type: $mobyTypeText  State: $mobyStateText")
    [void]$lines.Add("Catalog: $candidateKind; confidence $confidence; placement-like=$placementLike; placeholder=$placeholder")
    $mapZone = [string](Get-ObjectField $Entity "stoneHillMapZone" "")
    $workingRole = [string](Get-ObjectField $Entity "stoneHillWorkingRole" "")
    $displayTargetLabel = [string](Get-ObjectField $Entity "stoneHillDisplayTargetLabel" "")
    $targetGuess = Get-ObjectField $Entity "stoneHillPublicTargetGuess" $null
    $targetCandidates = @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetCandidates")
    $publicHints = @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetHints")
    if (-not [string]::IsNullOrWhiteSpace($mapZone) -or -not [string]::IsNullOrWhiteSpace($workingRole) -or -not [string]::IsNullOrWhiteSpace($displayTargetLabel) -or $targetCandidates.Count -gt 0 -or $null -ne $targetGuess -or $publicHints.Count -gt 0) {
        [void]$lines.Add("Map plan: $mapZone")
        if (-not [string]::IsNullOrWhiteSpace($displayTargetLabel)) {
            [void]$lines.Add("Display target: $displayTargetLabel")
        }
        if (-not [string]::IsNullOrWhiteSpace($workingRole)) {
            [void]$lines.Add("Role: $workingRole")
        }
        foreach ($candidate in ($targetCandidates | Select-Object -First 5)) {
            $candidateName = [string](Get-ObjectField $candidate "name" "")
            $candidateCategory = [string](Get-ObjectField $candidate "category" "")
            $candidateConfidence = [string](Get-ObjectField $candidate "confidence" "")
            if (-not [string]::IsNullOrWhiteSpace($candidateName)) {
                [void]$lines.Add("Candidate target: $candidateName ($candidateCategory, $candidateConfidence)")
            }
        }
        if ($null -ne $targetGuess) {
            $guessLabel = [string](Get-ObjectField $targetGuess "label" "")
            $guessConfidence = [string](Get-ObjectField $targetGuess "confidence" "")
            $guessTarget = [string](Get-ObjectField $targetGuess "publicTarget" "")
            $guessRationale = [string](Get-ObjectField $targetGuess "rationale" "")
            $guessNext = [string](Get-ObjectField $targetGuess "nextValidation" "")
            if (-not [string]::IsNullOrWhiteSpace($guessLabel)) {
                [void]$lines.Add("Best public target guess: $guessLabel ($guessConfidence)")
            }
            if (-not [string]::IsNullOrWhiteSpace($guessTarget)) {
                [void]$lines.Add("Target: $guessTarget")
            }
            if (-not [string]::IsNullOrWhiteSpace($guessRationale)) {
                [void]$lines.Add("Why: $guessRationale")
            }
            if (-not [string]::IsNullOrWhiteSpace($guessNext)) {
                [void]$lines.Add("Next validation: $guessNext")
            }
        }
        foreach ($hint in ($publicHints | Select-Object -First 4)) {
            [void]$lines.Add("Public hint: $hint")
        }
    }
    $specialData = Get-ObjectField $Entity "stoneHillSpecialData" $null
    if ($null -ne $specialData) {
        $validSpecial = [bool](Get-ObjectField $specialData "validMainRamPointer" $false)
        $specialPointer = [string](Get-ObjectField $specialData "specialDataPointer" "")
        $ramOffset = [string](Get-ObjectField $specialData "ramOffset" "")
        [void]$lines.Add("Special data: $(if ($validSpecial) { 'valid' } else { 'not valid' }) pointer $specialPointer RAM $ramOffset")
        $leadFields = @(Get-ObjectFieldArray $specialData "leadInt16Fields" | Select-Object -First 10)
        if ($leadFields.Count -gt 0) {
            [void]$lines.Add("Special int16 fields: $($leadFields -join ', ')")
        }
        $pointerFields = @(Get-ObjectFieldArray $specialData "pointerFields" | Select-Object -First 5)
        foreach ($field in $pointerFields) {
            $fieldOffset = [string](Get-ObjectField $field "offset" "")
            $fieldPointer = [string](Get-ObjectField $field "pointer" "")
            if (-not [string]::IsNullOrWhiteSpace($fieldPointer)) {
                [void]$lines.Add("Special pointer field $fieldOffset -> $fieldPointer")
            }
        }
        $specialMatches = @(Get-ObjectFieldArray $specialData "sourceMatches" | Where-Object { [int](Get-NumberField $_ "count" 0) -gt 0 } | Select-Object -First 5)
        foreach ($match in $specialMatches) {
            $kind = [string](Get-ObjectField $match "kind" "")
            $count = [int](Get-NumberField $match "count" 0)
            $offsets = @(Get-ObjectFieldArray $match "wadRelativeOffsets" | Select-Object -First 4)
            [void]$lines.Add("Special WAD match: $kind count=$count offsets=$($offsets -join ', ')")
        }
    }
    [void]$lines.Add("Status: source-window leads only; not a verified patch offset yet.")
    [void]$lines.Add("")

    if ($exactMatches.Count -gt 0) {
        [void]$lines.Add("Exact coordinate probes:")
        foreach ($match in $exactMatches | Select-Object -First 8) {
            $kind = [string](Get-ObjectField $match "kind" "")
            $count = [int](Get-NumberField $match "count" 0)
            $offsets = @(Get-ObjectFieldArray $match "offsets" | Select-Object -First 6)
            [void]$lines.Add("  $kind count=$count offsets=$($offsets -join ', ')")
        }
        [void]$lines.Add("")
    }

    if ($windows.Count -eq 0) {
        [void]$lines.Add("No scaled-int16 source windows were found for this moby.")
        return ($lines.ToArray() -join [Environment]::NewLine)
    }

    [void]$lines.Add("Scaled-int16 source-window leads:")
    foreach ($window in $windows | Select-Object -First 8) {
        $assetWindow = [string](Get-ObjectField $window "assetRelativeWindow" "")
        $subfile = [int](Get-NumberField $window "assetSubfileIndex" -1)
        $axisCount = [int](Get-NumberField $window "axisCount" 0)
        $spread = [int](Get-NumberField $window "spread" 0)
        $values = @(Get-ObjectFieldArray $window "values")
        $typeOffsets = @(Get-ObjectFieldArray $window "typeByteOffsets")
        $stateOffsets = @(Get-ObjectFieldArray $window "stateByteOffsets")
        $wadWindow = [string](Get-ObjectField $window "wadRelativeWindow" "")
        $wadText = $(if (-not [string]::IsNullOrWhiteSpace($wadWindow)) { " wad=$wadWindow" } else { "" })
        [void]$lines.Add("  window $assetWindow$wadText subfile=$subfile axes=$axisCount spread=$spread")
        if ($values.Count -gt 0) { [void]$lines.Add("    values: $($values -join ', ')") }
        if ($typeOffsets.Count -gt 0) { [void]$lines.Add("    type byte near: $($typeOffsets -join ', ')") }
        if ($stateOffsets.Count -gt 0) { [void]$lines.Add("    state byte near: $($stateOffsets -join ', ')") }
        if ($null -ne $AssetBytes) {
            $offset = [int](Convert-HexTextToInt64 $assetWindow -1)
            if ($offset -ge 0 -and $offset -lt $AssetBytes.Length) {
                $length = [Math]::Min(64, $AssetBytes.Length - $offset)
                [void]$lines.Add("    bytes: $(Format-HexBytes $AssetBytes $offset $length)")
            }
        }
    }
    return ($lines.ToArray() -join [Environment]::NewLine)
}

function Format-StoneHillTargetIndexes($Indexes) {
    $items = @($Indexes | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($items.Count -eq 0) { return "none" }
    return (($items | ForEach-Object { "L$_" }) -join ", ")
}

function Format-StoneHillTargetBoardText($Board) {
    $lines = New-Object System.Collections.Generic.List[string]
    $generatedAt = [string](Get-ObjectField $Board "generatedAt" "")
    [void]$lines.Add("Stone Hill target board")
    if (-not [string]::IsNullOrWhiteSpace($generatedAt)) {
        [void]$lines.Add("Generated: $generatedAt")
    }
    [void]$lines.Add("")
    [void]$lines.Add("Use this lookup while the Stone Hill workbench is loaded. L# is the runtime moby marker shown in the editor.")
    [void]$lines.Add("Only treasure/gem class is strong right now; other names are route-level hypotheses awaiting focused RAM diffs.")
    [void]$lines.Add("")

    $readiness = Get-ObjectField $Board "currentEditorReadiness" $null
    if ($null -ne $readiness) {
        [void]$lines.Add("Current editor readiness")
        [void]$lines.Add("------------------------")
        [void]$lines.Add("Movable runtime markers: $([int](Get-NumberField $readiness 'movableRuntimeMarkers' 0))")
        [void]$lines.Add("Placement-like markers: $([int](Get-NumberField $readiness 'placementRuntimeMarkers' 0))")
        [void]$lines.Add("Scaled source-lead markers: $([int](Get-NumberField $readiness 'scaledSourceLeadMarkers' 0))")
        [void]$lines.Add("Strong identity: $([string](Get-ObjectField $readiness 'strongIdentity' ''))")
        [void]$lines.Add("")
    }

    $routeGraph = @(Get-ObjectFieldArray $Board "publicRouteGraph")
    if ($routeGraph.Count -gt 0) {
        [void]$lines.Add("Public route graph")
        [void]$lines.Add("------------------")
        foreach ($route in $routeGraph) {
            $label = [string](Get-ObjectField $route "label" "Stone Hill route")
            $role = [string](Get-ObjectField $route "routeRole" "")
            $targets = @(Get-ObjectFieldArray $route "expectedTargets" | ForEach-Object { [string]$_ })
            $markers = Format-StoneHillTargetIndexes (Get-ObjectFieldArray $route "currentMarkerIndexes")
            $cue = [string](Get-ObjectField $route "validationCue" "")
            [void]$lines.Add("$label")
            if (-not [string]::IsNullOrWhiteSpace($role)) {
                [void]$lines.Add("  Route: $role")
            }
            if ($targets.Count -gt 0) {
                [void]$lines.Add("  Expected targets: $($targets -join ', ')")
            }
            [void]$lines.Add("  Current marker candidates: $markers")
            if (-not [string]::IsNullOrWhiteSpace($cue)) {
                [void]$lines.Add("  Validation cue: $cue")
            }
        }
        [void]$lines.Add("")
    }

    $locations = @(Get-ObjectFieldArray $Board "locations")
    if ($locations.Count -gt 0) {
        [void]$lines.Add("Location ledger")
        [void]$lines.Add("---------------")
        foreach ($location in $locations) {
            $name = [string](Get-ObjectField $location "locationName" "Stone Hill location")
            $status = [string](Get-ObjectField $location "status" "")
            $confidence = [string](Get-ObjectField $location "confidence" "")
            $targets = @(Get-ObjectFieldArray $location "expectedTargets" | ForEach-Object { [string]$_ })
            $markers = Format-StoneHillTargetIndexes (Get-ObjectFieldArray $location "candidateIndexes")
            $sourceLeads = [string](Get-ObjectField $location "sourceLeads" "none")
            $editorUse = [string](Get-ObjectField $location "editorUse" "")
            $validation = [string](Get-ObjectField $location "validationAction" "")

            [void]$lines.Add("$name")
            if (-not [string]::IsNullOrWhiteSpace($status) -or -not [string]::IsNullOrWhiteSpace($confidence)) {
                [void]$lines.Add("  Status: $status; confidence: $confidence")
            }
            if ($targets.Count -gt 0) {
                [void]$lines.Add("  Expected targets: $($targets -join ', ')")
            }
            [void]$lines.Add("  Candidate mobys: $markers")
            [void]$lines.Add("  Source leads: $sourceLeads")
            if (-not [string]::IsNullOrWhiteSpace($editorUse)) {
                [void]$lines.Add("  Editor use: $editorUse")
            }
            if (-not [string]::IsNullOrWhiteSpace($validation)) {
                [void]$lines.Add("  Validation: $validation")
            }
        }
        [void]$lines.Add("")
    }

    [void]$lines.Add("Targets")
    [void]$lines.Add("-------")
    foreach ($row in @(Get-ObjectFieldArray $Board "targets")) {
        $name = [string](Get-ObjectField $row "targetName" "Stone Hill target")
        $status = [string](Get-ObjectField $row "status" "")
        $confidence = [string](Get-ObjectField $row "confidence" "")
        $candidates = Format-StoneHillTargetIndexes (Get-ObjectFieldArray $row "candidateIndexes")
        $related = Format-StoneHillTargetIndexes (Get-ObjectFieldArray $row "relatedRouteIndexes")
        $sourceLeads = [string](Get-ObjectField $row "sourceLeads" "none")
        $relatedSourceLeads = [string](Get-ObjectField $row "relatedSourceLeads" "none")
        $editorAction = [string](Get-ObjectField $row "editorAction" "")
        $validationAction = [string](Get-ObjectField $row "validationAction" "")

        [void]$lines.Add("$name")
        [void]$lines.Add("  Status: $status; confidence: $confidence")
        [void]$lines.Add("  Candidate mobys: $candidates")
        if ($related -ne "none") {
            [void]$lines.Add("  Related route mobys: $related")
        }
        [void]$lines.Add("  Source leads: $sourceLeads")
        if ($relatedSourceLeads -ne "none") {
            [void]$lines.Add("  Related source leads: $relatedSourceLeads")
        }
        if (-not [string]::IsNullOrWhiteSpace($editorAction)) {
            [void]$lines.Add("  Editor action: $editorAction")
        }
        if (-not [string]::IsNullOrWhiteSpace($validationAction)) {
            [void]$lines.Add("  Validation: $validationAction")
        }
        [void]$lines.Add("")
    }

    $priority = @(Get-ObjectFieldArray $Board "priorityOrder")
    if ($priority.Count -gt 0) {
        [void]$lines.Add("Priority")
        [void]$lines.Add("--------")
        foreach ($item in $priority) {
            [void]$lines.Add("- $([string]$item)")
        }
    }

    return ($lines.ToArray() -join [Environment]::NewLine)
}

function Show-StoneHillTargetBoard {
    $path = Join-Path $PSScriptRoot "stonehill-replica-target-board.json"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nRegenerate it with tools\New-StoneHillReplicaTargetBoard.ps1 after creating stonehill-replica-map-plan.json.", "Stone Hill target board not found", "OK", "Information") | Out-Null
        return
    }

    try {
        $board = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $text = Format-StoneHillTargetBoardText $board
        if ($null -ne $script:DiscInfoBox) {
            $script:DiscInfoBox.Text = $text
        }
        if ($null -ne $script:RightTabs -and $null -ne $script:DiscTab) {
            $script:RightTabs.SelectedTab = $script:DiscTab
        }
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Stone Hill target board loaded into Disc Image panel"
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not load Stone Hill target board.`n$($_.Exception.Message)", "Stone Hill target board failed", "OK", "Error") | Out-Null
    }
}

function Format-StoneHillReplicaReadinessText($Readiness) {
    $lines = New-Object System.Collections.Generic.List[string]
    $generatedAt = [string](Get-ObjectField $Readiness "generatedAt" "")
    $grade = [string](Get-ObjectField $Readiness "readinessGrade" "unknown")
    $meaning = [string](Get-ObjectField $Readiness "readinessMeaning" "")
    [void]$lines.Add("Stone Hill replica readiness")
    if (-not [string]::IsNullOrWhiteSpace($generatedAt)) {
        [void]$lines.Add("Generated: $generatedAt")
    }
    [void]$lines.Add("Grade: $grade")
    if (-not [string]::IsNullOrWhiteSpace($meaning)) {
        [void]$lines.Add($meaning)
    }
    [void]$lines.Add("")

    $coverage = Get-ObjectField $Readiness "coverage" $null
    if ($null -ne $coverage) {
        [void]$lines.Add("Coverage")
        [void]$lines.Add("--------")
        [void]$lines.Add("Decoded runtime mobys: $([int](Get-NumberField $coverage 'decodedRuntimeMobys' 0))")
        [void]$lines.Add("Placement-like mobys: $([int](Get-NumberField $coverage 'placementRuntimeMobys' 0))")
        [void]$lines.Add("Strong identity mobys: $([int](Get-NumberField $coverage 'strongIdentityMobys' 0))")
        [void]$lines.Add("Scaled source-window leads: $([int](Get-NumberField $coverage 'scaledSourceLeadMobys' 0))")
        [void]$lines.Add("Targets: $([int](Get-NumberField $coverage 'usableClassTargets' 0)) usable class, $([int](Get-NumberField $coverage 'patchTestableHypothesisTargets' 0)) patch-testable, $([int](Get-NumberField $coverage 'routeHypothesisTargets' 0)) route-only, $([int](Get-NumberField $coverage 'blockedTargets' 0)) blocked")
        [void]$lines.Add("Focused RAM pairs: $([int](Get-NumberField $coverage 'completedFocusedPairs' 0)) complete, $([int](Get-NumberField $coverage 'missingFocusedPairs' 0)) missing")
        [void]$lines.Add("")
    }

    $blockers = @(Get-ObjectFieldArray $Readiness "blockers")
    if ($blockers.Count -gt 0) {
        [void]$lines.Add("Blockers")
        [void]$lines.Add("--------")
        foreach ($blocker in $blockers) {
            [void]$lines.Add("- $([string]$blocker)")
        }
        [void]$lines.Add("")
    }

    $targets = @(Get-ObjectFieldArray $Readiness "targetReadiness")
    if ($targets.Count -gt 0) {
        [void]$lines.Add("Target readiness")
        [void]$lines.Add("----------------")
        foreach ($target in $targets) {
            $name = [string](Get-ObjectField $target "targetName" "Stone Hill target")
            $targetGrade = [string](Get-ObjectField $target "grade" "")
            $indexes = Format-StoneHillTargetIndexes (Get-ObjectFieldArray $target "candidateIndexes")
            $related = Format-StoneHillTargetIndexes (Get-ObjectFieldArray $target "relatedRouteIndexes")
            $sourceLeads = [string](Get-ObjectField $target "sourceLeadSummary" (Get-ObjectField $target "sourceLeads" "none"))
            $next = [string](Get-ObjectField $target "nextValidation" "")
            [void]$lines.Add("$name")
            [void]$lines.Add("  Grade: $targetGrade")
            [void]$lines.Add("  Candidate mobys: $indexes")
            if ($related -ne "none") {
                [void]$lines.Add("  Related route mobys: $related")
            }
            [void]$lines.Add("  Source leads: $sourceLeads")
            if (-not [string]::IsNullOrWhiteSpace($next)) {
                [void]$lines.Add("  Next validation: $next")
            }
        }
        [void]$lines.Add("")
    }

    $actions = @(Get-ObjectFieldArray $Readiness "nextActions")
    if ($actions.Count -gt 0) {
        [void]$lines.Add("Next actions")
        [void]$lines.Add("------------")
        foreach ($action in $actions) {
            [void]$lines.Add("- $([string]$action)")
        }
    }

    return ($lines.ToArray() -join [Environment]::NewLine)
}

function Show-StoneHillReplicaReadiness {
    $path = Join-Path $PSScriptRoot "stonehill-replica-readiness.json"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nRegenerate it with tools\New-StoneHillReplicaReadinessReport.ps1.", "Stone Hill readiness not found", "OK", "Information") | Out-Null
        return
    }

    try {
        $readiness = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $text = Format-StoneHillReplicaReadinessText $readiness
        if ($null -ne $script:DiscInfoBox) {
            $script:DiscInfoBox.Text = $text
        }
        if ($null -ne $script:RightTabs -and $null -ne $script:DiscTab) {
            $script:RightTabs.SelectedTab = $script:DiscTab
        }
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Stone Hill readiness loaded into Disc Image panel"
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not load Stone Hill readiness.`n$($_.Exception.Message)", "Stone Hill readiness failed", "OK", "Error") | Out-Null
    }
}

function Show-StoneHillValidationPlaybook {
    $path = Join-Path $PSScriptRoot "stonehill-validation-playbook.md"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nRegenerate it with tools\New-StoneHillValidationPlaybook.ps1.", "Stone Hill validation playbook not found", "OK", "Information") | Out-Null
        return
    }

    try {
        $text = Get-Content -Raw -LiteralPath $path
        if ($null -ne $script:DiscInfoBox) {
            $script:DiscInfoBox.Text = $text
        }
        if ($null -ne $script:RightTabs -and $null -ne $script:DiscTab) {
            $script:RightTabs.SelectedTab = $script:DiscTab
        }
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Stone Hill validation playbook loaded into Disc Image panel"
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not load Stone Hill validation playbook.`n$($_.Exception.Message)", "Stone Hill validation playbook failed", "OK", "Error") | Out-Null
    }
}

function Show-StoneHillIdentityMatrix {
    $path = Join-Path $PSScriptRoot "stonehill-identity-matrix.md"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nRegenerate it with tools\New-StoneHillIdentityMatrix.ps1.", "Stone Hill identity matrix not found", "OK", "Information") | Out-Null
        return
    }

    try {
        $text = Get-Content -Raw -LiteralPath $path
        if ($null -ne $script:DiscInfoBox) {
            $script:DiscInfoBox.Text = $text
        }
        if ($null -ne $script:RightTabs -and $null -ne $script:DiscTab) {
            $script:RightTabs.SelectedTab = $script:DiscTab
        }
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Stone Hill identity matrix loaded into Disc Image panel"
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not load Stone Hill identity matrix.`n$($_.Exception.Message)", "Stone Hill identity matrix failed", "OK", "Error") | Out-Null
    }
}

function Show-StoneHillReplicaDossier {
    $path = Join-Path $PSScriptRoot "stonehill-replica-dossier.md"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nRegenerate it with tools\New-StoneHillReplicaDossier.ps1.", "Stone Hill dossier not found", "OK", "Information") | Out-Null
        return
    }

    try {
        $text = Get-Content -Raw -LiteralPath $path
        if ($null -ne $script:DiscInfoBox) {
            $script:DiscInfoBox.Text = $text
        }
        if ($null -ne $script:RightTabs -and $null -ne $script:DiscTab) {
            $script:RightTabs.SelectedTab = $script:DiscTab
        }
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Stone Hill dossier loaded into Disc Image panel"
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not load Stone Hill dossier.`n$($_.Exception.Message)", "Stone Hill dossier failed", "OK", "Error") | Out-Null
    }
}

function Show-StoneHillMobyMap {
    $path = Join-Path $PSScriptRoot "stonehill-moby-map.png"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nRegenerate it with tools\Render-StoneHillMobyMap.ps1.", "Stone Hill moby map not found", "OK", "Information") | Out-Null
        return
    }

    try {
        $sourceImage = [System.Drawing.Image]::FromFile($path)
        try {
            $mapImage = New-Object System.Drawing.Bitmap -ArgumentList $sourceImage
        }
        finally {
            $sourceImage.Dispose()
        }

        $viewer = New-Object System.Windows.Forms.Form
        $viewer.Text = "Stone Hill labelled moby map"
        $viewer.StartPosition = "CenterParent"
        $viewer.Size = New-Object System.Drawing.Size(1180, 820)
        $viewer.MinimumSize = New-Object System.Drawing.Size(760, 520)
        $viewer.BackColor = [System.Drawing.Color]::FromArgb(24, 26, 30)

        $caption = New-Object System.Windows.Forms.Label
        $caption.Dock = "Top"
        $caption.Height = 38
        $caption.Padding = New-Object System.Windows.Forms.Padding(12, 8, 12, 6)
        $caption.ForeColor = [System.Drawing.Color]::White
        $caption.BackColor = [System.Drawing.Color]::FromArgb(34, 37, 42)
        $caption.Text = "Reference only: use Load Stone Hill Workbench and the L# target picker to select and move the matching runtime mobys."

        $picture = New-Object System.Windows.Forms.PictureBox
        $picture.Dock = "Fill"
        $picture.BackColor = [System.Drawing.Color]::FromArgb(24, 26, 30)
        $picture.SizeMode = "Zoom"
        $picture.Image = $mapImage

        $viewer.Controls.Add($picture)
        $viewer.Controls.Add($caption)
        $viewer.Add_FormClosed({
            if ($null -ne $picture.Image) {
                $picture.Image.Dispose()
                $picture.Image = $null
            }
        })
        [void]$viewer.Show($script:Form)

        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Opened Stone Hill labelled moby map"
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not open Stone Hill moby map.`n$($_.Exception.Message)", "Stone Hill moby map failed", "OK", "Error") | Out-Null
    }
}

function Get-EntityByRuntimeMobyIndex([int]$Index) {
    foreach ($entity in $script:Project.entities) {
        if ([int](Get-NumberField $entity "runtimeMobyIndex" -1) -eq $Index) {
            return $entity
        }
    }
    return $null
}

function Get-StoneHillTargetPickerText($Entity) {
    $index = [int](Get-NumberField $Entity "runtimeMobyIndex" -1)
    $typeHex = "0x{0:X2}" -f [int](Get-NumberField $Entity "mobyTypeId" 0)
    $displayLabel = [string](Get-ObjectField $Entity "stoneHillDisplayTargetLabel" "")
    $targetGuess = Get-ObjectField $Entity "stoneHillPublicTargetGuess" $null
    $targetCandidates = @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetCandidates")
    $mapZone = [string](Get-ObjectField $Entity "stoneHillMapZone" "")
    $sourceStatus = [string](Get-ObjectField $Entity "stoneHillSourceStatus" "")
    $targetConfidence = ""
    $patchPriority = Get-StoneHillEntityPatchPriority $Entity

    if ([string]::IsNullOrWhiteSpace($displayLabel) -and $targetCandidates.Count -gt 0) {
        $displayLabel = [string](Get-ObjectField $targetCandidates[0] "name" "")
        $targetConfidence = [string](Get-ObjectField $targetCandidates[0] "confidence" "")
    }
    if ([string]::IsNullOrWhiteSpace($displayLabel) -and $null -ne $targetGuess) {
        $displayLabel = [string](Get-ObjectField $targetGuess "label" "")
        $targetConfidence = [string](Get-ObjectField $targetGuess "confidence" "")
    }
    if ([string]::IsNullOrWhiteSpace($displayLabel)) {
        $displayLabel = [string](Get-ObjectField $Entity "candidateKind" (Get-ObjectField $Entity "name" "Stone Hill moby"))
    }
    if ([string]::IsNullOrWhiteSpace($targetConfidence) -and $null -ne $targetGuess) {
        $targetConfidence = [string](Get-ObjectField $targetGuess "confidence" "")
    }

    $parts = New-Object System.Collections.Generic.List[string]
    if ($index -ge 0) { [void]$parts.Add("L$index") }
    [void]$parts.Add($typeHex)
    [void]$parts.Add($displayLabel)
    if (-not [string]::IsNullOrWhiteSpace($targetConfidence)) { [void]$parts.Add($targetConfidence) }
    if (-not [string]::IsNullOrWhiteSpace($mapZone)) { [void]$parts.Add($mapZone) }
    if ($sourceStatus -eq "scaled-int16 source-window lead") { [void]$parts.Add("source lead") }
    if (-not [string]::IsNullOrWhiteSpace($patchPriority) -and $patchPriority -notin @("none", "unranked")) { [void]$parts.Add("patch $patchPriority") }
    return ($parts.ToArray() -join " | ")
}

function Refresh-StoneHillTargetPicker {
    if ($null -eq $script:StoneHillTargetBox) { return }
    $previousId = ""
    if ($script:StoneHillTargetBox.SelectedIndex -ge 0 -and $script:StoneHillTargetBox.SelectedIndex -lt @($script:StoneHillTargetPickerItems).Count) {
        $previousId = [string](Get-ObjectField $script:StoneHillTargetPickerItems[$script:StoneHillTargetBox.SelectedIndex] "entityId" "")
    }

    $script:Refreshing = $true
    try {
        $script:StoneHillTargetBox.Items.Clear()
        $items = New-Object System.Collections.Generic.List[object]
        $candidateEntities = @($script:Project.entities | Where-Object {
            (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" -and (
                $null -ne (Get-ObjectField $_ "stoneHillReplicaMarker" $null) -or
                -not [string]::IsNullOrWhiteSpace([string](Get-ObjectField $_ "stoneHillDisplayTargetLabel" "")) -or
                @(Get-ObjectFieldArray $_ "stoneHillPublicTargetCandidates").Count -gt 0
            )
        } | Sort-Object { [int](Get-NumberField $_ "runtimeMobyIndex" 999999) })

        foreach ($entity in $candidateEntities) {
            $entityId = [string](Get-ObjectField $entity "id" "")
            if ([string]::IsNullOrWhiteSpace($entityId)) { continue }
            $text = Get-StoneHillTargetPickerText $entity
            [void]$items.Add([ordered]@{
                entityId = $entityId
                text = $text
            })
            [void]$script:StoneHillTargetBox.Items.Add($text)
        }

        $script:StoneHillTargetPickerItems = @($items.ToArray())
        $script:StoneHillTargetBox.Enabled = ($script:StoneHillTargetPickerItems.Count -gt 0)
        if ($script:StoneHillTargetPickerItems.Count -eq 0) {
            $script:StoneHillTargetBox.Text = ""
            return
        }

        $selectedIndex = -1
        if (-not [string]::IsNullOrWhiteSpace($previousId)) {
            for ($i = 0; $i -lt $script:StoneHillTargetPickerItems.Count; $i++) {
                if ([string](Get-ObjectField $script:StoneHillTargetPickerItems[$i] "entityId" "") -eq $previousId) {
                    $selectedIndex = $i
                    break
                }
            }
        }
        if ($selectedIndex -lt 0) { $selectedIndex = 0 }
        $script:StoneHillTargetBox.SelectedIndex = $selectedIndex
    }
    finally {
        $script:Refreshing = $false
    }
}

function Center-CanvasOnEntity($Entity) {
    if ($null -eq $script:Canvas -or $null -eq $Entity) { return }
    $entityX = [double](Get-NumberField $Entity "x" 0)
    $entityY = [double](Get-NumberField $Entity "y" 0)
    if ($script:Zoom -lt 0.045) { $script:Zoom = 0.045 }
    $clientWidth = [Math]::Max(200, [int]$script:Canvas.ClientSize.Width)
    $clientHeight = [Math]::Max(160, [int]$script:Canvas.ClientSize.Height)
    $script:Pan = New-Object System.Drawing.PointF(
        [float](($clientWidth / 2.0) - ($entityX * [double]$script:Zoom)),
        [float](($clientHeight / 2.0) - ($entityY * [double]$script:Zoom))
    )
    $script:Canvas.Invalidate()
}

function Select-StoneHillTargetPickerEntry {
    if ($null -eq $script:StoneHillTargetBox -or $script:StoneHillTargetBox.SelectedIndex -lt 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Load the Stone Hill Workbench first, then choose a target marker.", "No Stone Hill target", "OK", "Information") | Out-Null
        return
    }
    $index = $script:StoneHillTargetBox.SelectedIndex
    if ($index -ge @($script:StoneHillTargetPickerItems).Count) { return }
    $entry = $script:StoneHillTargetPickerItems[$index]
    $entity = Get-EntityById ([string](Get-ObjectField $entry "entityId" ""))
    if ($null -eq $entity) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "That Stone Hill target is not loaded in the current project.", "Target unavailable", "OK", "Information") | Out-Null
        Refresh-StoneHillTargetPicker
        return
    }

    Select-Entity ([string](Get-ObjectField $entity "id" ""))
    if ($null -ne $script:RightTabs -and $null -ne $script:ObjectsTab) {
        $script:RightTabs.SelectedTab = $script:ObjectsTab
    }
    if ($null -ne $script:EntityList) {
        foreach ($item in $script:EntityList.Items) {
            if ([string]$item.Tag -eq [string](Get-ObjectField $entity "id" "")) {
                $item.Selected = $true
                $item.EnsureVisible()
                break
            }
        }
    }
    Center-CanvasOnEntity $entity
    if ($null -ne $script:StatusLabel) {
        $script:StatusLabel.Text = "Selected Stone Hill target: $([string](Get-ObjectField $entry 'text' 'marker'))"
    }
}

function Fit-CanvasToEntities {
    if ($null -eq $script:Canvas -or @($script:Project.entities).Count -eq 0) { return }
    $fitEntities = @($script:Project.entities | Where-Object { Test-PlacementLikeEntity $_ })
    if ($fitEntities.Count -eq 0) { $fitEntities = @($script:Project.entities) }
    $xs = @($fitEntities | ForEach-Object { Get-NumberField $_ "x" 0 })
    $ys = @($fitEntities | ForEach-Object { Get-NumberField $_ "y" 0 })
    if ($xs.Count -eq 0 -or $ys.Count -eq 0) { return }
    $minX = ($xs | Measure-Object -Minimum).Minimum
    $maxX = ($xs | Measure-Object -Maximum).Maximum
    $minY = ($ys | Measure-Object -Minimum).Minimum
    $maxY = ($ys | Measure-Object -Maximum).Maximum
    $spanX = [Math]::Max(100, $maxX - $minX)
    $spanY = [Math]::Max(100, $maxY - $minY)
    $availableWidth = [Math]::Max(200, $script:Canvas.ClientSize.Width - 80)
    $availableHeight = [Math]::Max(160, $script:Canvas.ClientSize.Height - 80)
    $script:Zoom = [Math]::Max(0.02, [Math]::Min(0.7, [Math]::Min(($availableWidth / $spanX), ($availableHeight / $spanY))))
    $script:Pan = New-Object System.Drawing.PointF(([float](60 - ($minX * $script:Zoom))), ([float](60 - ($minY * $script:Zoom))))
    $script:Canvas.Invalidate()
}

function Update-ProjectDimensionsFromEntities {
    $fitEntities = @($script:Project.entities | Where-Object { Test-PlacementLikeEntity $_ })
    if ($fitEntities.Count -eq 0) { return }
    $xs = @($fitEntities | ForEach-Object { [double](Get-NumberField $_ "x" 0) })
    $ys = @($fitEntities | ForEach-Object { [double](Get-NumberField $_ "y" 0) })
    if ($xs.Count -eq 0 -or $ys.Count -eq 0) { return }
    $minX = ($xs | Measure-Object -Minimum).Minimum
    $maxX = ($xs | Measure-Object -Maximum).Maximum
    $minY = ($ys | Measure-Object -Minimum).Minimum
    $maxY = ($ys | Measure-Object -Maximum).Maximum
    $script:Project.dimensions.width = [Math]::Min(99999, [Math]::Max(2400, [int]($maxX - [Math]::Min(0, $minX) + 900)))
    $script:Project.dimensions.height = [Math]::Min(99999, [Math]::Max(1600, [int]($maxY - [Math]::Min(0, $minY) + 900)))
}

function Remap-RuntimeMobysToTopDownXY {
    Ensure-ProjectShape
    $changed = 0
    foreach ($entity in $script:Project.entities) {
        if ((Get-ObjectField $entity "origin" "") -ne "runtime-moby-ram-dump") { continue }
        $raw = Get-ObjectField $entity "rawPosition" $null
        if ($null -eq $raw) { continue }
        $mapX = [int]([double](Get-NumberField $raw "x" 0) / 16.0)
        $mapY = [int]([double](Get-NumberField $raw "y" 0) / 16.0)
        $mapZ = [int]([double](Get-NumberField $raw "z" 0) / 16.0)
        Set-ObjectField $entity "x" $mapX
        Set-ObjectField $entity "y" $mapY
        Set-ObjectField $entity "z" $mapZ
        Set-ObjectField $entity "originalMapX" $mapX
        Set-ObjectField $entity "originalMapY" $mapY
        Set-ObjectField $entity "originalMapZ" $mapZ
        Set-ObjectField $entity "runtimeMapX" $mapX
        Set-ObjectField $entity "runtimeMapY" $mapY
        Set-ObjectField $entity "runtimeMapZ" $mapZ
        Set-ObjectField $entity "mapProjection" "runtime-xy-topdown"
        $changed++
    }
    if ($changed -gt 0) {
        Update-ProjectDimensionsFromEntities
        Set-Dirty $true
        Refresh-All
        Fit-CanvasToEntities
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Remapped $changed runtime mobys to top-down X/Y placement."
        }
    }
    else {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "No runtime mobys with raw positions are loaded.", "No runtime mobys", "OK", "Information") | Out-Null
    }
}

function Get-CurrentGeometryCandidate {
    Ensure-ProjectShape
    $overlay = Get-ObjectField $script:Project "geometryOverlay" $null
    if ($null -eq $overlay) { return $null }
    $candidates = @(Get-ObjectFieldArray $overlay "candidates")
    if ($candidates.Count -eq 0) { return $null }
    $candidateIndex = [int](Get-NumberField $script:Project "geometryOverlayCandidateIndex" 0)
    if ($candidateIndex -lt 0 -or $candidateIndex -ge $candidates.Count) { $candidateIndex = 0 }
    return $candidates[$candidateIndex]
}

function Get-GeometryCandidatePoints($Candidate) {
    $points = @(Get-ObjectFieldArray $Candidate "projectedPoints")
    if ($points.Count -eq 0) {
        $points = @(Get-ObjectFieldArray $Candidate "points")
    }
    return $points
}

function Get-GeometryCandidateBounds($Candidate, $Points = $null) {
    if ($null -eq $Candidate) { return $null }
    $bounds = Get-ObjectField $Candidate "projectedBounds" $null
    if ($null -ne $bounds) {
        $minX = [double](Get-NumberField $bounds "minX" 0)
        $maxX = [double](Get-NumberField $bounds "maxX" 0)
        $minY = [double](Get-NumberField $bounds "minY" 0)
        $maxY = [double](Get-NumberField $bounds "maxY" 0)
        if ($maxX -gt $minX -or $maxY -gt $minY) {
            return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
        }
    }

    if ($null -eq $Points) {
        $Points = @(Get-GeometryCandidatePoints $Candidate)
    }
    else {
        $Points = @($Points)
    }
    if ($Points.Count -eq 0) { return $null }

    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    foreach ($point in $Points) {
        $x = [double](Get-NumberField $point "x" 0)
        $y = [double](Get-NumberField $point "y" 0)
        if ($x -lt $minX) { $minX = $x }
        if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
    }
    if ([double]::IsInfinity($minX) -or [double]::IsInfinity($minY)) { return $null }
    return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
}

function Format-GeometryBoundsText($Bounds) {
    if ($null -eq $Bounds) { return "bounds unknown" }
    return "bounds X {0}..{1} Y {2}..{3}" -f [int][Math]::Round([double]$Bounds.minX), [int][Math]::Round([double]$Bounds.maxX), [int][Math]::Round([double]$Bounds.minY), [int][Math]::Round([double]$Bounds.maxY)
}

function Get-DefaultStoneHillGeometryOverlayPaths {
    $root = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($root)) {
        $root = (Get-Location).Path
    }
    return @(
        (Join-Path $root "stonehill-runtime-scene-editor-overlay.json"),
        (Join-Path $root "stonehill-runtime-scene-overlay.json"),
        (Join-Path $root "stonehill-wad-ground-model-candidate4-oriented-xz-overlay.json"),
        (Join-Path $root "stonehill-wad-ground-model-aligned-robust-overlay.json")
    )
}

function Import-DefaultStoneHillGeometryOverlay([switch]$Quiet) {
    foreach ($path in (Get-DefaultStoneHillGeometryOverlayPaths)) {
        if (-not (Test-Path -LiteralPath $path)) { continue }
        try {
            [void](Import-GeometryOverlayFromPath $path -Quiet)
            if ($null -ne $script:StatusLabel) {
                $script:StatusLabel.Text = "Loaded Stone Hill geometry overlay: $([System.IO.Path]::GetFileName($path))"
            }
            return $true
        }
        catch {
            if (-not $Quiet) {
                [System.Windows.Forms.MessageBox]::Show($script:Form, "Found a Stone Hill geometry file but could not load it.`n`n$path`n`n$($_.Exception.Message)", "Geometry load failed", "OK", "Error") | Out-Null
            }
        }
    }
    if (-not $Quiet) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "No Stone Hill geometry overlay was found next to the editor.`n`nExpected: stonehill-runtime-scene-editor-overlay.json", "No geometry overlay", "OK", "Information") | Out-Null
    }
    return $false
}

function Expand-ProjectDimensionsToGeometry {
    Ensure-ProjectShape
    $candidate = Get-CurrentGeometryCandidate
    if ($null -eq $candidate) { return $false }
    $bounds = Get-GeometryCandidateBounds $candidate
    if ($null -eq $bounds) { return $false }

    $width = [int][Math]::Ceiling([Math]::Max([double](Get-NumberField $script:Project.dimensions "width" 2400), [double]$bounds.maxX + 900.0))
    $height = [int][Math]::Ceiling([Math]::Max([double](Get-NumberField $script:Project.dimensions "height" 1600), [double]$bounds.maxY + 900.0))
    $script:Project.dimensions.width = [Math]::Min(99999, [Math]::Max(2400, $width))
    $script:Project.dimensions.height = [Math]::Min(99999, [Math]::Max(1600, $height))
    return $true
}

function Fit-CanvasToGeometry([switch]$Quiet) {
    if ($null -eq $script:Canvas) { return $false }
    $candidate = Get-CurrentGeometryCandidate
    if ($null -eq $candidate) {
        if (Import-DefaultStoneHillGeometryOverlay -Quiet:$Quiet) {
            $candidate = Get-CurrentGeometryCandidate
        }
    }
    if ($null -eq $candidate) {
        if (-not $Quiet) {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "No geometry overlay is loaded, and the default Stone Hill overlay could not be found.", "No geometry overlay", "OK", "Information") | Out-Null
        }
        return $false
    }
    $points = @(Get-GeometryCandidatePoints $candidate)
    $bounds = Get-GeometryCandidateBounds $candidate $points
    if ($null -eq $bounds) {
        if (-not $Quiet) {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "The current geometry candidate has no usable projected bounds.", "No geometry bounds", "OK", "Information") | Out-Null
        }
        return $false
    }

    [void](Expand-ProjectDimensionsToGeometry)

    $pad = 260.0
    $minX = [double]$bounds.minX - $pad
    $maxX = [double]$bounds.maxX + $pad
    $minY = [double]$bounds.minY - $pad
    $maxY = [double]$bounds.maxY + $pad
    $spanX = [Math]::Max(100, $maxX - $minX)
    $spanY = [Math]::Max(100, $maxY - $minY)
    $availableWidth = [Math]::Max(200, $script:Canvas.ClientSize.Width - 80)
    $availableHeight = [Math]::Max(160, $script:Canvas.ClientSize.Height - 80)
    $script:Zoom = [Math]::Max(0.01, [Math]::Min(0.9, [Math]::Min(($availableWidth / $spanX), ($availableHeight / $spanY))))
    $script:Pan = New-Object System.Drawing.PointF(([float](40 - ($minX * $script:Zoom))), ([float](40 - ($minY * $script:Zoom))))
    $script:Canvas.Invalidate()
    if (-not $Quiet -and $null -ne $script:StatusLabel) {
        $script:StatusLabel.Text = "Fit geometry view: $(Format-GeometryBoundsText $bounds), points $($points.Count)"
    }
    return $true
}

function Normalize-ImportedEntityView {
    if (@($script:Project.entities).Count -eq 0) {
        Reset-CanvasView
        return
    }
    Fit-CanvasToEntities
    if ($script:Zoom -le 0.021) {
        $script:StatusLabel.Text = "Imported objects span a very large area; view was fit as far as possible."
    }
}

function Refresh-Inspector {
    if ($null -eq $script:NameBox) { return }
    $script:Refreshing = $true
    try {
        $entity = Get-EntityById $script:SelectedEntityId
        $enabled = $null -ne $entity
        foreach ($control in @($script:NameBox, $script:TypeBox, $script:XBox, $script:YBox, $script:ZBox, $script:ColorButton, $script:NotesBox, $script:DeleteButton)) {
            $control.Enabled = $enabled
        }
        if (-not $enabled) {
            $script:NameBox.Text = ""
            $script:TypeBox.SelectedIndex = -1
            $script:XBox.Value = 0
            $script:YBox.Value = 0
            $script:ZBox.Value = 0
            $script:NotesBox.Text = ""
            $script:ColorButton.BackColor = [System.Drawing.Color]::Gray
            return
        }

        $script:NameBox.Text = [string](Get-ObjectField $entity "name" "")
        $script:TypeBox.SelectedItem = [string](Get-ObjectField $entity "type" "")
        $script:XBox.Value = [decimal][Math]::Max($script:XBox.Minimum, [Math]::Min($script:XBox.Maximum, (Get-NumberField $entity "x" 0)))
        $script:YBox.Value = [decimal][Math]::Max($script:YBox.Minimum, [Math]::Min($script:YBox.Maximum, (Get-NumberField $entity "y" 0)))
        $script:ZBox.Value = [decimal][Math]::Max($script:ZBox.Minimum, [Math]::Min($script:ZBox.Maximum, (Get-NumberField $entity "z" 0)))
        $script:NotesBox.Text = [string](Get-ObjectField $entity "notes" "")
        $script:ColorButton.BackColor = Convert-HexToColor ([string](Get-ObjectField $entity "color" "#808080"))
    }
    finally {
        $script:Refreshing = $false
    }
}

function Refresh-LevelControls {
    if ($null -eq $script:LevelNameBox) { return }
    Ensure-ProjectShape
    $script:Refreshing = $true
    try {
        if ($null -ne $script:LevelSelectBox) {
            $script:LevelSelectBox.Items.Clear()
            $levels = @()
            if ($null -ne $script:Project.discImage -and $null -ne $script:Project.discImage.spyroWad) {
                $levels = @($script:Project.discImage.spyroWad.levels)
            }
            foreach ($level in $levels) {
                $label = "{0} (WAD {1}/{2})" -f $level.levelName, $level.metadataWadIndex, $level.assetWadIndex
                [void]$script:LevelSelectBox.Items.Add($label)
            }
            $script:LevelSelectBox.Enabled = ($levels.Count -gt 0)
            $selectedIndex = -1
            if ($levels.Count -gt 0 -and $null -ne $script:Project.selectedLevel) {
                for ($i = 0; $i -lt $levels.Count; $i++) {
                    if ($levels[$i].metadataWadIndex -eq $script:Project.selectedLevel.metadataWadIndex) {
                        $selectedIndex = $i
                        break
                    }
                }
            }
            if ($selectedIndex -lt 0 -and $levels.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($script:Project.levelName)) {
                for ($i = 0; $i -lt $levels.Count; $i++) {
                    if ($levels[$i].levelName -eq $script:Project.levelName) {
                        $selectedIndex = $i
                        break
                    }
                }
            }
            if ($selectedIndex -lt 0 -and $levels.Count -gt 0) {
                $selectedIndex = 0
            }
            if ($selectedIndex -ge 0) {
                $script:LevelSelectBox.SelectedIndex = $selectedIndex
            }
        }
        $script:LevelNameBox.Text = $script:Project.levelName
        $script:WidthBox.Value = [decimal](Get-NumberField $script:Project.dimensions "width" 2400)
        $script:HeightBox.Value = [decimal](Get-NumberField $script:Project.dimensions "height" 1600)
        $script:PaletteBox.SelectedItem = $script:Project.palette.name
        $script:MusicBox.SelectedItem = $script:Project.music.name
        $script:PalettePreview.BackColor = Convert-HexToColor $script:Project.palette.background
    }
    finally {
        $script:Refreshing = $false
    }
    Update-LevelSelectionPreview
}

function Get-SelectedWadLevel {
    if ($null -eq $script:LevelSelectBox -or $script:LevelSelectBox.SelectedIndex -lt 0) { return $null }
    if ($null -eq $script:Project.discImage -or $null -eq $script:Project.discImage.spyroWad) { return $null }
    $levels = @($script:Project.discImage.spyroWad.levels)
    if ($script:LevelSelectBox.SelectedIndex -ge $levels.Count) { return $null }
    return $levels[$script:LevelSelectBox.SelectedIndex]
}

function Update-LevelSelectionPreview {
    if ($null -eq $script:LevelSelectionInfoBox) { return }
    $level = Get-SelectedWadLevel
    if ($null -eq $level) {
        $script:LevelSelectionInfoBox.Text = "Import a Spyro disc image to populate detected WAD levels."
        return
    }

    $loaded = "No"
    if ($null -ne $script:Project.selectedLevel -and $script:Project.selectedLevel.metadataWadIndex -eq $level.metadataWadIndex) {
        $loaded = "Yes"
    }
    $script:LevelSelectionInfoBox.Text = @(
        "Selected: $($level.levelName)",
        "Metadata WAD: $($level.metadataWadIndex) at offset $($level.metadataOffset) ($($level.metadataSize) bytes)",
        "Asset WAD: $($level.assetWadIndex) at offset $($level.assetOffset) ($($level.assetSize) bytes)",
        "Loaded into project: $loaded"
    ) -join [Environment]::NewLine
}

function Refresh-DiscControls {
    if ($null -eq $script:DiscInfoBox) { return }
    Ensure-ProjectShape
    $script:Refreshing = $true
    try {
        $disc = $script:Project.discImage
        if ([string]::IsNullOrWhiteSpace($disc.path)) {
            $script:DiscInfoBox.Text = "No disc image imported yet."
        }
        else {
            $sizeMb = [Math]::Round(([double]$disc.sizeBytes / 1MB), 2)
            $script:DiscInfoBox.Text = @(
                "File: $($disc.fileName)",
                "Size: $sizeMb MB",
                "Format: $($disc.format)",
                "Serial: $($disc.serial)",
                "Volume: $($disc.volumeId)",
                "Files found: $(@($disc.files).Count)",
                "WAD levels: $(if ($null -ne $disc.spyroWad) { $disc.spyroWad.levelCount } else { 0 })",
                "SHA1: $($disc.sha1)",
                "Path: $($disc.path)"
            ) -join [Environment]::NewLine
        }

        $script:DiscFileList.BeginUpdate()
        $script:DiscFileList.Items.Clear()
        foreach ($file in @($disc.files)) {
            $item = New-Object System.Windows.Forms.ListViewItem($file.path)
            [void]$item.SubItems.Add($(if ($file.isDirectory) { "Dir" } else { "File" }))
            [void]$item.SubItems.Add([string]$file.lba)
            [void]$item.SubItems.Add([string]$file.size)
            [void]$script:DiscFileList.Items.Add($item)
        }
        $script:DiscFileList.EndUpdate()
        $analysis = @($disc.analysis)
        if ($null -ne $script:Project.selectedLevel) {
            $analysis += ""
            $analysis += "Selected WAD level: $($script:Project.selectedLevel.levelName) (metadata WAD $($script:Project.selectedLevel.metadataWadIndex), asset WAD $($script:Project.selectedLevel.assetWadIndex))"
            $analysis += "Object decode status: $($script:Project.selectedLevel.objectDecodeStatus)"
        }
        if ($null -ne $disc.stoneHill) {
            $analysis += ""
            $analysis += Get-StoneHillAnalysisSummary $disc.stoneHill
        }
        if ($analysis.Count -eq 0) {
            $analysis = @("Import a Spyro disc image, then use Analyze Loader Status.")
        }
        $script:PatchSummaryBox.Text = ($analysis -join [Environment]::NewLine)
    }
    finally {
        $script:Refreshing = $false
    }
}

function Get-StoneHillAnalysisSummary($StoneHill) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("Stone Hill reverse-engineering pass:")
    if ($null -ne $StoneHill.stoneHill) {
        [void]$lines.Add("  Metadata WAD: $($StoneHill.stoneHill.metadataWadIndex), offset $($StoneHill.stoneHill.metadataOffset), size $($StoneHill.stoneHill.metadataSize)")
        [void]$lines.Add("  Asset WAD: $($StoneHill.stoneHill.assetWadIndex), offset $($StoneHill.stoneHill.assetOffset), size $($StoneHill.stoneHill.assetSize)")
    }
    if ($null -ne $StoneHill.mobyRuntime) {
        [void]$lines.Add("  Runtime moby stride: $($StoneHill.mobyRuntime.structStrideBytes) bytes")
        [void]$lines.Add("  Type/state byte offsets: $($StoneHill.mobyRuntime.typeOffsetHex), $($StoneHill.mobyRuntime.stateOffsetHex)")
    }
    $hitCount = 0
    if ($null -ne $StoneHill.metadata -and $null -ne $StoneHill.metadata.storesOrLoadsToKnownGlobals) {
        $hitCount = @($StoneHill.metadata.storesOrLoadsToKnownGlobals).Count
    }
    [void]$lines.Add("  Overlay references to level moby pointer: $hitCount")
    [void]$lines.Add("  Patch status: research data attached; object source records are not linked to disc bytes yet.")
    return $lines.ToArray()
}

function Refresh-All {
    Ensure-ProjectShape
    Refresh-LevelControls
    Refresh-DiscControls
    Refresh-EntityList
    Refresh-StoneHillTargetPicker
    Refresh-Inspector
    if ($null -ne $script:Canvas) { $script:Canvas.Invalidate() }
    Update-Title
}

function Select-DiscLevel {
    Ensure-ProjectShape
    if ($null -ne $script:LevelSelectBox -and $script:LevelSelectBox.SelectedIndex -lt 0 -and $script:LevelSelectBox.Items.Count -gt 0) {
        $script:LevelSelectBox.SelectedIndex = 0
    }
    if ($null -eq $script:LevelSelectBox -or $script:LevelSelectBox.SelectedIndex -lt 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import a Spyro disc image and choose a detected WAD level first.", "No level selected", "OK", "Information") | Out-Null
        return
    }
    if ($null -eq $script:Project.discImage -or $null -eq $script:Project.discImage.spyroWad) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "No WAD level list is available yet.", "No levels detected", "OK", "Information") | Out-Null
        return
    }

    $level = Get-SelectedWadLevel
    if ($null -eq $level) { return }
    $script:Project.selectedLevel = [ordered]@{
        levelId = $level.levelId
        levelName = $level.levelName
        metadataWadIndex = $level.metadataWadIndex
        metadataOffset = $level.metadataOffset
        metadataSize = $level.metadataSize
        assetWadIndex = $level.assetWadIndex
        assetOffset = $level.assetOffset
        assetSize = $level.assetSize
        objectDecodeStatus = "pending-source-record-map"
    }
    $script:Project.levelName = $level.levelName
    $matchingTrack = $script:MusicTracks | Where-Object { $_.Name -eq $level.levelName } | Select-Object -First 1
    if ($null -ne $matchingTrack) {
        $script:Project.music.id = $matchingTrack.Id
        $script:Project.music.name = $matchingTrack.Name
    }
    $script:Project.source = "Disc-linked WAD level selection. Object records are not decoded yet."
    Set-Dirty $true
    Refresh-All
    $script:StatusLabel.Text = "Selected $($level.levelName); WAD $($level.metadataWadIndex)/$($level.assetWadIndex) linked, object decode pending."
}

function Select-Entity([string]$Id) {
    $script:SelectedEntityId = $Id
    Refresh-EntityList
    Refresh-Inspector
    Refresh-ObjectSummary
    Refresh-WadCandidateView
    if ($null -ne $script:Canvas) { $script:Canvas.Invalidate() }
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Get-MovedRawPositionForEntity($Entity) {
    $raw = Get-ObjectField $Entity "rawPosition" $null
    if ($null -eq $raw) { return $null }

    $originalMapX = Get-NumberField $Entity "originalMapX" ([double](Get-NumberField $Entity "x" 0))
    $originalMapY = Get-NumberField $Entity "originalMapY" ([double](Get-NumberField $Entity "y" 0))
    $originalMapZ = Get-NumberField $Entity "originalMapZ" ([double](Get-NumberField $Entity "z" 0))
    $rawX = [int](Get-NumberField $raw "x" 0)
    $rawY = [int](Get-NumberField $raw "y" 0)
    $rawZ = [int](Get-NumberField $raw "z" 0)
    $deltaX = [int](([double](Get-NumberField $Entity "x" 0) - $originalMapX) * 16.0)
    $deltaY = [int](([double](Get-NumberField $Entity "y" 0) - $originalMapY) * 16.0)
    $deltaZ = [int](([double](Get-NumberField $Entity "z" 0) - $originalMapZ) * 16.0)

    $newRawX = $rawX + $deltaX
    $projection = [string](Get-ObjectField $Entity "mapProjection" "")
    if ($projection -eq "runtime-xz-topdown") {
        $newRawY = $rawY + $deltaZ
        $newRawZ = $rawZ + $deltaY
    }
    elseif ($projection -eq "runtime-xy-topdown") {
        $newRawY = $rawY + $deltaY
        $newRawZ = $rawZ + $deltaZ
    }
    else {
        $newRawY = $rawY + $deltaY
        $newRawZ = [int]((Get-NumberField $Entity "z" 0) * 16.0)
        if ($newRawZ -eq 0) { $newRawZ = $rawZ }
    }

    return [ordered]@{ x = $newRawX; y = $newRawY; z = $newRawZ }
}

function Add-BinaryPatch($Patch) {
    Ensure-ProjectShape
    $items = New-Object System.Collections.ArrayList
    foreach ($existing in @($script:Project.binaryPatches)) {
        [void]$items.Add($existing)
    }
    [void]$items.Add($Patch)
    $script:Project.binaryPatches = @($items.ToArray())
}

function Get-SelectedLinkedMoby {
    $entity = Get-EntityById $script:SelectedEntityId
    if ($null -eq $entity) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Select a linked moby first.", "No object selected", "OK", "Information") | Out-Null
        return $null
    }
    $link = Get-ObjectField $entity "wadLink" $null
    if ($null -eq $link) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "The selected moby does not have a WAD link yet. Run Link WAD, then choose a green or teal moby.", "No WAD link", "OK", "Information") | Out-Null
        return $null
    }
    return $entity
}

function New-PositionPatchForEntity($Entity, [int64]$ImageOffset, [string]$Confidence, [string]$Notes) {
    $raw = Get-ObjectField $Entity "rawPosition" $null
    if ($null -eq $raw) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "The selected moby has no original raw runtime position.", "No raw position", "OK", "Information") | Out-Null
        return $null
    }

    $newRaw = Get-MovedRawPositionForEntity $Entity
    $newRawX = [int](Get-NumberField $newRaw "x" 0)
    $newRawY = [int](Get-NumberField $newRaw "y" 0)
    $newRawZ = [int](Get-NumberField $newRaw "z" 0)

    $patchBytes = Join-ByteArrays @((Convert-Int32ToBytes $newRawX), (Convert-Int32ToBytes $newRawY), (Convert-Int32ToBytes $newRawZ))
    if ($ImageOffset -lt 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "The selected WAD link has no image offset.", "Invalid WAD link", "OK", "Error") | Out-Null
        return $null
    }

    return [ordered]@{
        id = [guid]::NewGuid().ToString()
        kind = "runtime-moby-position"
        entityId = Get-ObjectField $Entity "id" ""
        entityName = Get-ObjectField $Entity "name" "Object"
        mobyType = Get-MobyTypeKey $Entity
        imageOffset = $ImageOffset
        dataHex = Convert-BytesToHex $patchBytes
        originalRawPosition = $raw
        newRawPosition = [ordered]@{ x = $newRawX; y = $newRawY; z = $newRawZ }
        confidence = $Confidence
        notes = $Notes
    }
}

function Queue-SelectedWadPositionPatch {
    Ensure-ProjectShape
    $entity = Get-SelectedLinkedMoby
    if ($null -eq $entity) { return }
    $link = Get-ObjectField $entity "wadLink" $null
    $imageOffset = [int64](Get-NumberField $link "imageOffset" -1)
    $patch = New-PositionPatchForEntity $entity $imageOffset (Get-ObjectField $link "confidence" "unknown") "Queued from selected WAD-linked moby. Test on a copied image only."
    if ($null -eq $patch) { return }

    Add-BinaryPatch $patch
    Set-ObjectField $entity "patchStatus" "position-patch-queued"
    $raw = Get-ObjectField $entity "rawPosition" $null
    $rawX = [int](Get-NumberField $raw "x" 0)
    $rawY = [int](Get-NumberField $raw "y" 0)
    $rawZ = [int](Get-NumberField $raw "z" 0)
    $newRawX = [int](Get-NumberField $patch.newRawPosition "x" 0)
    $newRawY = [int](Get-NumberField $patch.newRawPosition "y" 0)
    $newRawZ = [int](Get-NumberField $patch.newRawPosition "z" 0)
    Set-ObjectField $entity "notes" ([string](Get-ObjectField $entity "notes" "") + "`r`nQueued position patch at image offset 0x$('{0:X}' -f $imageOffset): raw ($rawX,$rawY,$rawZ) -> ($newRawX,$newRawY,$newRawZ).")
    Set-Dirty $true
    Refresh-All
    [System.Windows.Forms.MessageBox]::Show($script:Form, "Queued one position patch for $($patch.entityName).`n`nImage offset: 0x$('{0:X}' -f $imageOffset)`nRaw position: ($rawX, $rawY, $rawZ) -> ($newRawX, $newRawY, $newRawZ)`n`nUse Create Patched Copy... to write it to a copied BIN.", "Patch queued", "OK", "Information") | Out-Null
}

function Queue-SelectedAllCandidatePositionPatches {
    Ensure-ProjectShape
    $entity = Get-SelectedLinkedMoby
    if ($null -eq $entity) { return }
    if ($null -eq $script:Project.selectedLevel -or $null -eq $script:Project.selectedLevel.assetOffset) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Select and load a detected WAD level first.", "No WAD level selected", "OK", "Information") | Out-Null
        return
    }
    if ([string]::IsNullOrWhiteSpace($script:Project.discImage.path) -or -not (Test-Path -LiteralPath $script:Project.discImage.path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import the Spyro disc image first.", "No disc image", "OK", "Information") | Out-Null
        return
    }

    $link = Get-ObjectField $entity "wadLink" $null
    $candidateOffsets = New-Object System.Collections.ArrayList
    foreach ($candidate in @(Get-ObjectFieldArray $entity "wadLinkCandidates")) {
        [void]$candidateOffsets.Add([int64]$candidate)
    }
    foreach ($candidate in @(Get-ObjectFieldArray $link "allCandidateOffsets")) {
        [void]$candidateOffsets.Add([int64]$candidate)
    }
    $linkedOffset = [int64](Get-NumberField $link "assetRelativeOffset" -1)
    if ($linkedOffset -ge 0) { [void]$candidateOffsets.Add($linkedOffset) }

    $unique = @($candidateOffsets.ToArray() | Sort-Object -Unique)
    if ($unique.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "No candidate offsets are stored for this moby.", "No candidates", "OK", "Information") | Out-Null
        return
    }

    try {
        $layout = Detect-DiscLayout $script:Project.discImage.path
        $wadEntry = @($script:Project.discImage.files | Where-Object { $_.path -eq "WAD.WAD" } | Select-Object -First 1)
        if ($wadEntry.Count -eq 0) { throw "WAD.WAD was not found in the imported disc file table." }
        $wadEntry = $wadEntry[0]
        $assetOffset = [int64]$script:Project.selectedLevel.assetOffset
        $queued = 0
        foreach ($assetRelative in $unique) {
            $wadRelativeOffset = $assetOffset + [int64]$assetRelative
            $imageOffset = Convert-DiscFileOffsetToImageOffset $layout ([int]$wadEntry.lba) $wadRelativeOffset
            $confidence = "diagnostic-all-candidates asset+0x$('{0:X}' -f [int64]$assetRelative)"
            $patch = New-PositionPatchForEntity $entity $imageOffset $confidence "Queued diagnostic patch for every stored WAD coordinate candidate. Test on a copied image only."
            if ($null -ne $patch) {
                Add-BinaryPatch $patch
                $queued++
            }
        }
        Set-ObjectField $entity "patchStatus" "candidate-position-patches-queued"
        Set-ObjectField $entity "notes" ([string](Get-ObjectField $entity "notes" "") + "`r`nQueued diagnostic position patches for $queued WAD candidate offsets. This is for testing ambiguous mappings on a copied BIN.")
        Set-Dirty $true
        Refresh-All
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Queued $queued diagnostic candidate patch(es) for $([string](Get-ObjectField $entity 'name' 'Object')).`n`nThis writes the same moved position to every stored WAD candidate for that moby. Use this only on a copied BIN while we identify the true source record.", "Candidate patches queued", "OK", "Information") | Out-Null
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not queue candidate patches.`n$($_.Exception.Message)", "Patch queue failed", "OK", "Error") | Out-Null
    }
}

function Parse-StoneHillSourceWindowValue([string]$ValueText) {
    if ([string]::IsNullOrWhiteSpace($ValueText)) { return $null }
    if ($ValueText -notmatch '^([xyz])=(-?\d+)@\+(\d+)$') { return $null }
    return [ordered]@{
        axis = $matches[1]
        value = [int]$matches[2]
        inWindow = [int]$matches[3]
    }
}

function Get-StoneHillSourcePatchImagePath {
    if ($null -ne $script:Project.discImage -and
        -not [string]::IsNullOrWhiteSpace([string]$script:Project.discImage.path) -and
        (Test-Path -LiteralPath $script:Project.discImage.path)) {
        return [string]$script:Project.discImage.path
    }

    $localImage = Join-Path $PSScriptRoot "Spyro the Dragon (USA).bin"
    if (Test-Path -LiteralPath $localImage) {
        Ensure-ProjectShape
        $item = Get-Item -LiteralPath $localImage
        $script:Project.discImage.path = $item.FullName
        $script:Project.discImage.fileName = $item.Name
        $script:Project.discImage.sizeBytes = $item.Length
        $script:Project.discImage.format = "raw-bin-local-fallback"
        $script:Project.discImage.importedAt = (Get-Date).ToString("s")
        $script:Project.discImage.files = @([ordered]@{ path = "WAD.WAD"; lba = 37; size = 0; isDirectory = $false })
        return $item.FullName
    }

    return ""
}

function Get-StoneHillSourcePatchLevelContext {
    $assetOffset = 8390656L
    $assetSize = 3682304L
    $wadLba = 37
    if ($null -ne $script:Project.selectedLevel) {
        $selectedAssetOffset = [int64](Get-NumberField $script:Project.selectedLevel "assetOffset" -1)
        if ($selectedAssetOffset -ge 0) { $assetOffset = $selectedAssetOffset }
        $selectedAssetSize = [int64](Get-NumberField $script:Project.selectedLevel "assetSize" -1)
        if ($selectedAssetSize -gt 0) { $assetSize = $selectedAssetSize }
    }
    return [ordered]@{
        wadLba = $wadLba
        assetOffset = $assetOffset
        assetSize = $assetSize
    }
}

function Queue-SelectedStoneHillSourceWindowPatches {
    Ensure-ProjectShape
    $entity = Get-EntityById $script:SelectedEntityId
    if ($null -eq $entity) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Select a Stone Hill moby first.", "No object selected", "OK", "Information") | Out-Null
        return
    }
    $sourceLead = Get-ObjectField $entity "stoneHillSourceLead" $null
    if ($null -eq $sourceLead) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "The selected moby has no Stone Hill source-window leads. Load the Stone Hill workbench or regenerate stonehill-moby-source-links.json.", "No source leads", "OK", "Information") | Out-Null
        return
    }
    $windows = @(Get-ObjectFieldArray $sourceLead "scaledAxisWindows")
    if ($windows.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "No scaled-int16 source windows are attached to this moby.", "No source windows", "OK", "Information") | Out-Null
        return
    }

    $raw = Get-ObjectField $entity "rawPosition" $null
    $newRaw = Get-MovedRawPositionForEntity $entity
    if ($null -eq $raw -or $null -eq $newRaw) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "The selected moby has no original raw runtime position.", "No raw position", "OK", "Information") | Out-Null
        return
    }

    $imagePath = Get-StoneHillSourcePatchImagePath
    if ([string]::IsNullOrWhiteSpace($imagePath)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import the Spyro disc image first, or place Spyro the Dragon (USA).bin next to the editor.", "No disc image", "OK", "Information") | Out-Null
        return
    }

    try {
        $layout = Detect-DiscLayout $imagePath
        $context = Get-StoneHillSourcePatchLevelContext
        $queued = 0
        $seenOffsets = @{}
        foreach ($window in ($windows | Select-Object -First 12)) {
            $assetWindow = [int64](Convert-HexTextToInt64 ([string](Get-ObjectField $window "assetRelativeWindow" "")) -1)
            if ($assetWindow -lt 0) { continue }
            foreach ($valueText in @(Get-ObjectFieldArray $window "values")) {
                $parsed = Parse-StoneHillSourceWindowValue ([string]$valueText)
                if ($null -eq $parsed) { continue }
                $axis = [string](Get-ObjectField $parsed "axis" "")
                $oldRawAxis = [int](Get-NumberField $raw $axis 0)
                $newRawAxis = [int](Get-NumberField $newRaw $axis 0)
                $sourceDelta = [int][Math]::Round(($newRawAxis - $oldRawAxis) / 16.0)
                if ($sourceDelta -eq 0) { continue }
                $newValue = [int](Get-NumberField $parsed "value" 0) + $sourceDelta
                $patchBytes = Convert-Int16ToBytes $newValue
                if ($null -eq $patchBytes) { continue }

                $assetRelative = $assetWindow + [int](Get-NumberField $parsed "inWindow" 0)
                $dedupeKey = "$assetRelative"
                if ($seenOffsets.ContainsKey($dedupeKey)) { continue }
                $seenOffsets[$dedupeKey] = $true
                $wadRelativeOffset = [int64](Get-NumberField $context "assetOffset" 8390656) + $assetRelative
                $imageOffset = Convert-DiscFileOffsetToImageOffset $layout ([int](Get-NumberField $context "wadLba" 37)) $wadRelativeOffset
                Add-BinaryPatch ([ordered]@{
                    id = [guid]::NewGuid().ToString()
                    kind = "stone-hill-scaled-int16-source-window"
                    entityId = Get-ObjectField $entity "id" ""
                    entityName = Get-ObjectField $entity "name" "Object"
                    mobyType = Get-MobyTypeKey $entity
                    imageOffset = $imageOffset
                    dataHex = Convert-BytesToHex $patchBytes
                    assetRelativeOffset = ("0x{0:X}" -f $assetRelative)
                    wadRelativeOffset = ("0x{0:X}" -f $wadRelativeOffset)
                    axis = $axis
                    oldSourceValue = [int](Get-NumberField $parsed "value" 0)
                    newSourceValue = $newValue
                    confidence = "diagnostic-scaled-int16-source-window"
                    notes = "Queued from Stone Hill scaled-int16 source-window lead. Test only on a copied BIN; this is not a verified object source record."
                })
                $queued++
            }
        }

        if ($queued -eq 0) {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "No Stone Hill source-window patches were queued. Move the selected moby first so at least one source axis changes.", "No movement delta", "OK", "Information") | Out-Null
            return
        }

        Set-ObjectField $entity "patchStatus" "stone-hill-source-window-patches-queued"
        Set-ObjectField $entity "notes" ([string](Get-ObjectField $entity "notes" "") + "`r`nQueued $queued Stone Hill scaled-int16 source-window diagnostic patch(es). Test on a copied BIN only.")
        Set-Dirty $true
        Refresh-All
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Queued $queued Stone Hill source-window patch(es) for $([string](Get-ObjectField $entity 'name' 'Object')).`n`nThese write moved scaled-int16 coordinates to the current source-window leads. Use Create Patched Copy... and test only on a disposable BIN.", "Stone Hill source patches queued", "OK", "Information") | Out-Null
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not queue Stone Hill source-window patches.`n$($_.Exception.Message)", "Source patch queue failed", "OK", "Error") | Out-Null
    }
}

function Add-Entity([string]$Type) {
    $color = "#D93C36"
    if ($Type -eq "Enemy") { $color = "#7D4F2A" }
    if ($Type -eq "Dragon") { $color = "#E9D14C" }
    $entity = [ordered]@{
        id = [guid]::NewGuid().ToString()
        type = $Type
        name = "$Type $($script:Project.entities.Count + 1)"
        x = [int]((Get-NumberField $script:Project.dimensions "width" 2400) / 2)
        y = [int]((Get-NumberField $script:Project.dimensions "height" 1600) / 2)
        z = 0
        color = $color
        origin = "manual-project-marker"
        patchStatus = "not-linked-to-disc"
        notes = "Manual editor marker. This will not patch the imported disc until the Spyro object decoder links it to a real moby record."
    }
    $script:Project.entities += $entity
    Select-Entity $entity.id
    Set-Dirty $true
    if ($null -ne $script:StatusLabel) {
        $script:StatusLabel.Text = "Added manual $Type marker; not linked to imported disc data yet."
    }
}

function Save-ProjectAs {
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Title = "Save Spyro editor project"
    $dialog.Filter = "Spyro level project (*.splevel.json)|*.splevel.json|JSON (*.json)|*.json|All files (*.*)|*.*"
    $dialog.FileName = (($script:Project.levelName -replace '[\\/:*?"<>|]', '_') + ".splevel.json")
    if ($dialog.ShowDialog($script:Form) -eq [System.Windows.Forms.DialogResult]::OK) {
        $script:ProjectPath = $dialog.FileName
        Save-Project
    }
}

function Save-Project {
    if ([string]::IsNullOrWhiteSpace($script:ProjectPath)) {
        Save-ProjectAs
        return
    }
    Ensure-ProjectShape
    $script:Project | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $script:ProjectPath -Encoding UTF8
    Set-Dirty $false
    $script:StatusLabel.Text = "Saved $script:ProjectPath"
}

function Open-Project {
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = "Open Spyro editor project"
    $dialog.Filter = "Spyro level project (*.splevel.json)|*.splevel.json|JSON (*.json)|*.json|All files (*.*)|*.*"
    if ($dialog.ShowDialog($script:Form) -eq [System.Windows.Forms.DialogResult]::OK) {
        try {
            $json = Get-Content -LiteralPath $dialog.FileName -Raw
            $script:Project = $json | ConvertFrom-Json
            $script:ProjectPath = $dialog.FileName
            $script:SelectedEntityId = $null
            Set-Dirty $false
            Refresh-All
            $script:StatusLabel.Text = "Opened $script:ProjectPath"
        }
        catch {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not open project.`n$($_.Exception.Message)", "Open failed", "OK", "Error") | Out-Null
        }
    }
}

function Export-PatchPlan {
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Title = "Export patch plan"
    $dialog.Filter = "Patch plan JSON (*.patchplan.json)|*.patchplan.json|JSON (*.json)|*.json"
    $dialog.FileName = (($script:Project.levelName -replace '[\\/:*?"<>|]', '_') + ".patchplan.json")
    if ($dialog.ShowDialog($script:Form) -eq [System.Windows.Forms.DialogResult]::OK) {
        $plan = [ordered]@{
            target = "Spyro the Dragon PS1 extracted level data"
            generatedBy = "Spyro PS1 Level Editor prototype"
            warning = "Semantic level edits require Spyro-specific binary mappings before they can be written directly into the image."
            levelName = $script:Project.levelName
            discImage = $script:Project.discImage
            paletteSwap = $script:Project.palette
            musicSwap = $script:Project.music
            geometryOverlay = $script:Project.geometryOverlay
            objectPlacements = $script:Project.entities
            binaryPatches = $script:Project.binaryPatches
        }
        $plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $dialog.FileName -Encoding UTF8
        $script:StatusLabel.Text = "Exported patch plan $($dialog.FileName)"
    }
}

function Import-GeometryOverlay {
    Ensure-ProjectShape
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = "Import experimental geometry overlay"
    $dialog.Filter = "Geometry overlay JSON (*.json)|*.json|All files (*.*)|*.*"
    if ($dialog.ShowDialog($script:Form) -ne [System.Windows.Forms.DialogResult]::OK) { return }

    try {
        $overlay = Get-Content -Raw -LiteralPath $dialog.FileName | ConvertFrom-Json
        $candidates = @(Get-ObjectFieldArray $overlay "candidates")
        if ($candidates.Count -eq 0) {
            throw "The selected JSON does not contain geometry candidates."
        }
        Set-ObjectField $overlay "path" $dialog.FileName
        Set-ObjectField $script:Project "geometryOverlay" $overlay
        Set-ObjectField $script:Project "geometryOverlayCandidateIndex" 0
        Set-Dirty $true
        Refresh-All
        if (-not (Fit-CanvasToGeometry -Quiet)) {
            Fit-CanvasToEntities
        }
        $best = $candidates[0]
        $bounds = Get-GeometryCandidateBounds $best
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Geometry overlay imported: $($candidates.Count) candidates, $(Format-GeometryBoundsText $bounds)"
        }
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Imported geometry overlay.`n`nCandidates: $($candidates.Count)`nBest: $([string](Get-ObjectField $best 'runtimeAddress' 'unknown')) stride $([int](Get-NumberField $best 'stride' 0)), vertices $([int](Get-NumberField $best 'validVertices' 0))`n`nThis is experimental RAM geometry, not verified WAD mesh data yet.", "Geometry overlay imported", "OK", "Information") | Out-Null
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not import geometry overlay.`n$($_.Exception.Message)", "Geometry import failed", "OK", "Error") | Out-Null
    }
}

function Import-GeometryOverlayFromPath([string]$Path, [switch]$Quiet) {
    Ensure-ProjectShape
    try {
        if ([string]::IsNullOrWhiteSpace($Path)) { throw "No geometry overlay path was provided." }
        $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
        $overlay = Get-Content -Raw -LiteralPath $resolved.Path | ConvertFrom-Json
        $candidates = @(Get-ObjectFieldArray $overlay "candidates")
        if ($candidates.Count -eq 0) {
            throw "The selected JSON does not contain geometry candidates."
        }
        Set-ObjectField $overlay "path" $resolved.Path
        Set-ObjectField $script:Project "geometryOverlay" $overlay
        Set-ObjectField $script:Project "geometryOverlayCandidateIndex" 0
        Set-Dirty $true
        Refresh-All
        if (-not (Fit-CanvasToGeometry -Quiet)) {
            Fit-CanvasToEntities
        }
        $best = $candidates[0]
        $bounds = Get-GeometryCandidateBounds $best
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Geometry overlay imported: $($candidates.Count) candidates, $(Format-GeometryBoundsText $bounds)"
        }
        if (-not $Quiet) {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "Imported geometry overlay.`n`n$($resolved.Path)`n`nCandidates: $($candidates.Count)", "Geometry overlay imported", "OK", "Information") | Out-Null
        }
        return $candidates.Count
    }
    catch {
        if (-not $Quiet) {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not import geometry overlay.`n$($_.Exception.Message)", "Geometry import failed", "OK", "Error") | Out-Null
        }
        else {
            throw
        }
    }
}

function Import-Candidate4XZOverlay {
    $path = Join-Path (Get-Location).Path "stonehill-wad-ground-model-candidate4-oriented-xz-overlay.json"
    if (-not (Test-Path -LiteralPath $path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not find:`n$path`n`nUse Import Geometry... and select the overlay JSON manually, or regenerate the candidate 4 X/Z overlay.", "Overlay not found", "OK", "Information") | Out-Null
        return
    }
    Import-GeometryOverlayFromPath $path
}

function Get-StoneHillCatalogEntityType([string]$CandidateKind, [int]$TypeId) {
    if ($TypeId -eq 0x20 -or $CandidateKind -like "*gem*" -or $CandidateKind -like "*collectible*") { return "Gem" }
    if ($TypeId -eq 0x30 -or $CandidateKind -like "*dragon*") { return "Dragon" }
    if ($TypeId -eq 0x18 -or $CandidateKind -like "*enemy*") { return "Enemy" }
    return "Moby"
}

function Get-StoneHillCatalogDisplayName($Moby, [string]$TablePrefix = "L") {
    $index = [int](Get-NumberField $Moby "index" -1)
    $typeHex = [string](Get-ObjectField $Moby "typeHex" ("0x{0:X2}" -f [int](Get-NumberField $Moby "typeId" 0)))
    $kind = [string](Get-ObjectField $Moby "candidateKind" "unidentified moby")
    $shortKind = switch -Wildcard ($kind) {
        "*gem*" { "Gem candidate" }
        "*collectible*" { "Gem candidate" }
        "*dragon*" { "Dragon/NPC candidate" }
        "*enemy*" { "Enemy candidate" }
        "*helper*" { "Helper/system candidate" }
        "*sparse*" { "Sparse candidate" }
        default { "Unknown moby" }
    }
    if ($index -ge 0) {
        return "$shortKind $TablePrefix#$index $typeHex"
    }
    return "$shortKind $typeHex"
}

function Add-StoneHillCatalogLabelsToEntities($Entities, $Catalog) {
    if ($null -eq $Catalog) { return @($Entities) }
    $catalogMobys = @(Get-ObjectFieldArray $Catalog "mobys")
    if ($catalogMobys.Count -eq 0) { return @($Entities) }

    $catalogByAddress = @{}
    foreach ($moby in $catalogMobys) {
        $address = [string](Get-ObjectField $moby "runtimeAddress" "")
        if (-not [string]::IsNullOrWhiteSpace($address) -and -not $catalogByAddress.ContainsKey($address)) {
            $catalogByAddress[$address] = $moby
        }
    }

    foreach ($entity in $Entities) {
        $address = [string](Get-ObjectField $entity "runtimeAddress" "")
        if ([string]::IsNullOrWhiteSpace($address) -or -not $catalogByAddress.ContainsKey($address)) { continue }
        $moby = $catalogByAddress[$address]
        $candidateKind = [string](Get-ObjectField $moby "candidateKind" "unidentified moby")
        $confidence = [string](Get-ObjectField $moby "confidence" "unknown")
        $evidence = [string](Get-ObjectField $moby "evidence" "")
        $diffMeaning = [string](Get-ObjectField $moby "diffMeaning" "")
        $changedInDiff = [bool](Get-ObjectField $moby "changedInDiff" $false)
        $typeId = [int](Get-NumberField $moby "typeId" (Get-NumberField $entity "mobyTypeId" 0))

        Set-ObjectField $entity "name" (Get-StoneHillCatalogDisplayName $moby "L")
        Set-ObjectField $entity "type" (Get-StoneHillCatalogEntityType $candidateKind $typeId)
        Set-ObjectField $entity "candidateKind" $candidateKind
        Set-ObjectField $entity "catalogConfidence" $confidence
        Set-ObjectField $entity "catalogEvidence" $evidence
        Set-ObjectField $entity "changedInDiff" $changedInDiff
        Set-ObjectField $entity "diffMeaning" $diffMeaning
        if (-not [string]::IsNullOrWhiteSpace([string](Get-ObjectField $moby "color" ""))) {
            Set-ObjectField $entity "color" ([string](Get-ObjectField $moby "color" "#5DADE2"))
        }
        $notes = [string](Get-ObjectField $entity "notes" "")
        $catalogNote = "Stone Hill catalog label: $candidateKind; confidence $confidence."
        if (-not [string]::IsNullOrWhiteSpace($evidence)) { $catalogNote += " Evidence: $evidence" }
        if ($changedInDiff) { $catalogNote += " Changed in gem diff: $diffMeaning." }
        if ($notes -notlike "*Stone Hill catalog label:*") {
            Set-ObjectField $entity "notes" ($catalogNote + "`r`n" + $notes)
        }
    }
    return @($Entities)
}

function Add-StoneHillRouteAnchorsToEntities($Entities, $Catalog) {
    if ($null -eq $Catalog) { return @($Entities) }
    $anchors = @(Get-ObjectFieldArray $Catalog "publicRouteAnchors")
    if ($anchors.Count -eq 0) { return @($Entities) }

    $result = New-Object System.Collections.Generic.List[object]
    foreach ($entity in @($Entities)) { [void]$result.Add($entity) }

    foreach ($anchor in $anchors) {
        $name = [string](Get-ObjectField $anchor "name" "Stone Hill route")
        $target = [string](Get-ObjectField $anchor "target" "public route anchor")
        $evidence = [string](Get-ObjectField $anchor "evidence" "")
        $related = @(Get-ObjectFieldArray $anchor "relatedMobys" | ForEach-Object { "L$([int]$_)" })
        $notes = "Stone Hill public route anchor: $target."
        if (-not [string]::IsNullOrWhiteSpace($evidence)) { $notes += " Evidence: $evidence" }
        if ($related.Count -gt 0) { $notes += " Related runtime mobys: $($related -join ', ')." }
        $notes += " This is a map landmark, not a runtime moby or verified patch target."

        [void]$result.Add([ordered]@{
            id = [guid]::NewGuid().ToString()
            type = "Landmark"
            name = $name
            x = [double](Get-NumberField $anchor "x" 0)
            y = [double](Get-NumberField $anchor "y" 0)
            z = 0
            color = "#FFD260"
            notes = $notes
            origin = "stone-hill-public-route-anchor"
            stoneHillRouteAnchor = $anchor
        })
    }

    return @($result.ToArray())
}

function New-StoneHillSourceLeadFromRecord($Record) {
    $windows = @()
    foreach ($window in @(Get-ObjectFieldArray $Record "scaledAxisWindows" | Select-Object -First 12)) {
            $windows += [ordered]@{
                assetRelativeWindow = [string](Get-ObjectField $window "assetRelativeWindow" "")
                wadRelativeWindow = [string](Get-ObjectField $window "wadRelativeWindow" "")
                assetSubfileIndex = [int](Get-NumberField $window "assetSubfileIndex" -1)
                axisCount = [int](Get-NumberField $window "axisCount" 0)
                spread = [int](Get-NumberField $window "spread" 0)
                values = @(Get-ObjectFieldArray $window "values")
                typeByteOffsets = @(Get-ObjectFieldArray $window "typeByteOffsets")
            stateByteOffsets = @(Get-ObjectFieldArray $window "stateByteOffsets")
        }
    }

    $exactMatches = @()
    foreach ($match in @(Get-ObjectFieldArray $Record "exactMatches" | Where-Object { [int](Get-NumberField $_ "count" 0) -gt 0 } | Select-Object -First 12)) {
        $exactMatches += [ordered]@{
            kind = [string](Get-ObjectField $match "kind" "")
            count = [int](Get-NumberField $match "count" 0)
            offsets = @(Get-ObjectFieldArray $match "offsets" | Select-Object -First 16)
        }
    }

    return [ordered]@{
        index = [int](Get-NumberField $Record "index" -1)
        runtimeAddress = [string](Get-ObjectField $Record "runtimeAddress" "")
        typeHex = [string](Get-ObjectField $Record "typeHex" "")
        stateHex = [string](Get-ObjectField $Record "stateHex" "")
        candidateKind = [string](Get-ObjectField $Record "candidateKind" "unidentified moby")
        confidence = [string](Get-ObjectField $Record "confidence" "unknown")
        placeholderPosition = [bool](Get-ObjectField $Record "placeholderPosition" $false)
        placementLike = [bool](Get-ObjectField $Record "placementLike" $false)
        exactMatches = $exactMatches
        scaledAxisWindows = $windows
    }
}

function Add-StoneHillSourceLinksToEntities($Entities, $SourceLinks) {
    if ($null -eq $SourceLinks) { return @($Entities) }
    $records = @(Get-ObjectFieldArray $SourceLinks "records")
    if ($records.Count -eq 0) { return @($Entities) }

    $recordsByIndex = @{}
    foreach ($record in $records) {
        $index = [int](Get-NumberField $record "index" -1)
        if ($index -ge 0 -and -not $recordsByIndex.ContainsKey($index)) {
            $recordsByIndex[$index] = $record
        }
    }

    foreach ($entity in $Entities) {
        $index = [int](Get-NumberField $entity "runtimeMobyIndex" -1)
        if ($index -lt 0 -or -not $recordsByIndex.ContainsKey($index)) { continue }

        $lead = New-StoneHillSourceLeadFromRecord $recordsByIndex[$index]
        $windows = @(Get-ObjectFieldArray $lead "scaledAxisWindows")
        $exactMatches = @(Get-ObjectFieldArray $lead "exactMatches")
        Set-ObjectField $entity "stoneHillSourceLead" $lead
        Set-ObjectField $entity "sourceRecordStatus" "stone-hill-lead-only"

        $notes = [string](Get-ObjectField $entity "notes" "")
        if ($notes -like "*Stone Hill source leads:*") { continue }

        $sourceNote = "Stone Hill source leads: "
        if ($windows.Count -gt 0) {
            $best = $windows[0]
            $sourceNote += "$($windows.Count) scaled-int16 window(s); best $([string](Get-ObjectField $best 'assetRelativeWindow' '')) subfile $([int](Get-NumberField $best 'assetSubfileIndex' -1)) with values $((@(Get-ObjectFieldArray $best 'values') | Select-Object -First 4) -join ', ')."
        }
        else {
            $sourceNote += "no scaled-int16 placement window found."
        }
        if ($exactMatches.Count -gt 0) {
            $exactSummary = @($exactMatches | Select-Object -First 4 | ForEach-Object { ([string](Get-ObjectField $_ 'kind' '') + '=' + [int](Get-NumberField $_ 'count' 0)) })
            $sourceNote += " Exact coordinate probes: $($exactSummary -join ', ')."
        }
        $sourceNote += " These are research leads, not verified disc patch offsets."
        Set-ObjectField $entity "notes" ($sourceNote + "`r`n" + $notes)
    }
    return @($Entities)
}

function Add-StoneHillSpecialDataToEntities($Entities, $SpecialDataReport) {
    if ($null -eq $SpecialDataReport) { return @($Entities) }
    $records = @(Get-ObjectFieldArray $SpecialDataReport "records")
    if ($records.Count -eq 0) { return @($Entities) }

    $recordsByIndex = @{}
    foreach ($record in $records) {
        $index = [int](Get-NumberField $record "index" -1)
        if ($index -ge 0 -and -not $recordsByIndex.ContainsKey($index)) {
            $recordsByIndex[$index] = $record
        }
    }

    foreach ($entity in $Entities) {
        $index = [int](Get-NumberField $entity "runtimeMobyIndex" -1)
        if ($index -lt 0 -or -not $recordsByIndex.ContainsKey($index)) { continue }

        $record = $recordsByIndex[$index]
        Set-ObjectField $entity "stoneHillSpecialData" $record

        $valid = [bool](Get-ObjectField $record "validMainRamPointer" $false)
        $pointer = [string](Get-ObjectField $record "specialDataPointer" "")
        $matches = @(Get-ObjectFieldArray $record "sourceMatches" | Where-Object { [int](Get-NumberField $_ "count" 0) -gt 0 })
        Set-ObjectField $entity "stoneHillSpecialDataStatus" $(if ($valid) { "valid special-data pointer" } else { "no valid special-data pointer" })

        $notes = [string](Get-ObjectField $entity "notes" "")
        if ($notes -like "*Stone Hill special data:*") { continue }
        if ($valid) {
            $matchSummary = @($matches | Select-Object -First 3 | ForEach-Object { ([string](Get-ObjectField $_ "kind" "") + "=" + [int](Get-NumberField $_ "count" 0)) })
            $specialNote = "Stone Hill special data: $pointer; WAD byte matches: $(if ($matchSummary.Count -gt 0) { $matchSummary -join ', ' } else { 'none' })."
            Set-ObjectField $entity "notes" ($specialNote + "`r`n" + $notes)
        }
    }
    return @($Entities)
}

function Add-StoneHillReplicaPlanToEntities($Entities, $ReplicaPlan) {
    if ($null -eq $ReplicaPlan) { return @($Entities) }
    $markers = @(Get-ObjectFieldArray $ReplicaPlan "mobyMarkers")
    if ($markers.Count -eq 0) { return @($Entities) }

    $markersByIndex = @{}
    $markersByAddress = @{}
    foreach ($marker in $markers) {
        $index = [int](Get-NumberField $marker "index" -1)
        if ($index -ge 0 -and -not $markersByIndex.ContainsKey($index)) {
            $markersByIndex[$index] = $marker
        }
        $address = [string](Get-ObjectField $marker "runtimeAddress" "")
        if (-not [string]::IsNullOrWhiteSpace($address) -and -not $markersByAddress.ContainsKey($address)) {
            $markersByAddress[$address] = $marker
        }
    }

    foreach ($entity in $Entities) {
        $marker = $null
        $index = [int](Get-NumberField $entity "runtimeMobyIndex" -1)
        $address = [string](Get-ObjectField $entity "runtimeAddress" "")
        if ($index -ge 0 -and $markersByIndex.ContainsKey($index)) {
            $marker = $markersByIndex[$index]
        }
        elseif (-not [string]::IsNullOrWhiteSpace($address) -and $markersByAddress.ContainsKey($address)) {
            $marker = $markersByAddress[$address]
        }
        if ($null -eq $marker) { continue }

        $zone = [string](Get-ObjectField $marker "zoneLabel" "")
        $role = [string](Get-ObjectField $marker "workingRole" "")
        $targetGuess = Get-ObjectField $marker "publicTargetGuess" $null
        $targetCandidates = @(Get-ObjectFieldArray $marker "publicTargetCandidates")
        $displayTargetLabel = [string](Get-ObjectField $marker "displayTargetLabel" "")
        $publicHints = @(Get-ObjectFieldArray $marker "publicTargetHints")
        $sourceLead = Get-ObjectField $marker "sourceLead" $null
        $sourceStatus = $(if ($null -ne $sourceLead) { [string](Get-ObjectField $sourceLead "status" "no source lead") } else { "no source lead" })
        Set-ObjectField $entity "stoneHillReplicaMarker" $marker
        Set-ObjectField $entity "stoneHillMapZone" $zone
        Set-ObjectField $entity "stoneHillWorkingRole" $role
        Set-ObjectField $entity "stoneHillPublicTargetGuess" $targetGuess
        Set-ObjectField $entity "stoneHillPublicTargetCandidates" $targetCandidates
        Set-ObjectField $entity "stoneHillDisplayTargetLabel" $displayTargetLabel
        Set-ObjectField $entity "stoneHillPublicTargetHints" $publicHints
        Set-ObjectField $entity "stoneHillSourceStatus" $sourceStatus
        if (-not [string]::IsNullOrWhiteSpace($displayTargetLabel)) {
            $typeHex = [string](Get-ObjectField $marker "typeHex" ("0x{0:X2}" -f [int](Get-NumberField $entity "mobyTypeId" 0)))
            if ($index -ge 0) {
                Set-ObjectField $entity "name" "$displayTargetLabel L#$index $typeHex"
            }
        }

        $notes = [string](Get-ObjectField $entity "notes" "")
        if ($notes -like "*Stone Hill map plan:*") { continue }
        $planNote = "Stone Hill map plan: $zone; $role; source status: $sourceStatus."
        if (-not [string]::IsNullOrWhiteSpace($displayTargetLabel)) {
            $planNote += " Display label: $displayTargetLabel."
        }
        if ($targetCandidates.Count -gt 0) {
            $candidateNames = @($targetCandidates | Select-Object -First 4 | ForEach-Object { [string](Get-ObjectField $_ "name" "") } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            if ($candidateNames.Count -gt 0) {
                $planNote += " Target candidates: $($candidateNames -join ' | ')."
            }
        }
        if ($null -ne $targetGuess) {
            $guessLabel = [string](Get-ObjectField $targetGuess "label" "")
            $guessConfidence = [string](Get-ObjectField $targetGuess "confidence" "")
            if (-not [string]::IsNullOrWhiteSpace($guessLabel)) {
                $planNote += " Best public target guess: $guessLabel ($guessConfidence)."
            }
        }
        if ($publicHints.Count -gt 0) {
            $planNote += " Public hints: $((@($publicHints | Select-Object -First 3)) -join ' | ')."
        }
        Set-ObjectField $entity "notes" ($planNote + "`r`n" + $notes)
    }
    return @($Entities)
}

function Import-StoneHillReplica([switch]$Quiet) {
    Ensure-ProjectShape
    $previousProject = $script:Project
    $previousSelectedEntityId = $script:SelectedEntityId
    $previousDirty = $script:Dirty
    $loadCommitted = $false
    try {
        $preservedDiscImage = Get-ObjectField $script:Project "discImage" $null
        $ramPath = Join-Path $PSScriptRoot "stonehill-before-gem-clean.bin"
        $catalogPath = Join-Path $PSScriptRoot "stonehill-moby-catalog.json"
        $sourceLinksPath = Join-Path $PSScriptRoot "stonehill-moby-source-links.json"
        $specialDataPath = Join-Path $PSScriptRoot "stonehill-moby-special-data.json"
        $replicaPlanPath = Join-Path $PSScriptRoot "stonehill-replica-map-plan.json"
        $geometryPaths = @(
            (Join-Path $PSScriptRoot "stonehill-runtime-scene-editor-overlay.json"),
            (Join-Path $PSScriptRoot "stonehill-runtime-scene-overlay.json"),
            (Join-Path $PSScriptRoot "stonehill-wad-ground-model-candidate4-oriented-xz-overlay.json"),
            (Join-Path $PSScriptRoot "stonehill-wad-ground-model-aligned-robust-overlay.json")
        )
        if (-not (Test-Path -LiteralPath $ramPath)) {
            throw "Missing Stone Hill RAM dump: $ramPath"
        }

        $dumpBytes = [System.IO.File]::ReadAllBytes($ramPath)
        $ramWindow = Find-PsxRamWindow $dumpBytes
        $entities = @(Convert-RuntimeMobysToEntities ([byte[]]$ramWindow.ram) ([uint32]$ramWindow.pointer) "level")
        if ($entities.Count -eq 0) {
            throw ("No plausible moby records were found at runtime pointer 0x{0:X8}." -f ([uint32]$ramWindow.pointer))
        }

        $catalog = $null
        if (Test-Path -LiteralPath $catalogPath) {
            $catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
            $entities = @(Add-StoneHillCatalogLabelsToEntities $entities $catalog)
            $entities = @(Add-StoneHillRouteAnchorsToEntities $entities $catalog)
        }
        if (Test-Path -LiteralPath $sourceLinksPath) {
            $sourceLinks = Get-Content -Raw -LiteralPath $sourceLinksPath | ConvertFrom-Json
            $entities = @(Add-StoneHillSourceLinksToEntities $entities $sourceLinks)
        }
        if (Test-Path -LiteralPath $specialDataPath) {
            $specialDataReport = Get-Content -Raw -LiteralPath $specialDataPath | ConvertFrom-Json
            $entities = @(Add-StoneHillSpecialDataToEntities $entities $specialDataReport)
        }
        if (Test-Path -LiteralPath $replicaPlanPath) {
            $replicaPlan = Get-Content -Raw -LiteralPath $replicaPlanPath | ConvertFrom-Json
            $entities = @(Add-StoneHillReplicaPlanToEntities $entities $replicaPlan)
        }

        $script:Project = New-EditorProject
        Ensure-ProjectShape
        Set-ObjectField $script:Project "levelName" "Stone Hill Replica Workbench"
        Set-ObjectField $script:Project "selectedLevel" ([pscustomobject]@{ id = "stone_hill"; name = "Stone Hill"; wadId = "0x0B" })
        Set-ObjectField $script:Project "source" "Current Stone Hill workbench generated from the local RAM dump, moby catalog, and best-known runtime scene geometry overlay."
        Set-ObjectField $script:Project "music" ([pscustomobject]@{ id = "stone_hill"; name = "Stone Hill" })
        Set-ObjectField $script:Project "palette" ([pscustomobject]@{ name = "Artisans Day"; background = "#7AB5FF"; terrain = "#6CC46F"; accent = "#E8CE6D" })
        if ($null -ne $preservedDiscImage) {
            Set-ObjectField $script:Project "discImage" $preservedDiscImage
        }
        if ($null -ne $catalog) {
            Set-ObjectField $script:Project "stoneHillPublicRouteEdges" @(Get-ObjectFieldArray $catalog "publicRouteEdges")
        }
        Set-ObjectField $script:Project "entities" @($entities | ForEach-Object { [pscustomobject]$_ })
        $projectEntities = @(Get-ObjectFieldArray $script:Project "entities")
        $placementEntities = @($projectEntities | Where-Object { Test-PlacementLikeEntity $_ })
        if ($placementEntities.Count -eq 0) { $placementEntities = @($projectEntities) }
        if ($placementEntities.Count -gt 0) {
            $maxEntityX = ($placementEntities | ForEach-Object { [double](Get-NumberField $_ "x" 0) } | Measure-Object -Maximum).Maximum
            $maxEntityY = ($placementEntities | ForEach-Object { [double](Get-NumberField $_ "y" 0) } | Measure-Object -Maximum).Maximum
            $dimensions = Get-ObjectField $script:Project "dimensions" $null
            if ($null -ne $dimensions) {
                Set-ObjectField $dimensions "width" ([Math]::Min(99999, [Math]::Max(2400, [int]($maxEntityX + 900))))
                Set-ObjectField $dimensions "height" ([Math]::Min(99999, [Math]::Max(1600, [int]($maxEntityY + 900))))
            }
        }
        $projectEntities = @(Get-ObjectFieldArray $script:Project "entities")
        if ($projectEntities.Count -gt 0) {
            $script:SelectedEntityId = [string](Get-ObjectField $projectEntities[0] "id" "")
        }

        $geometryCandidateCount = 0
        $geometryPath = ""
        foreach ($candidatePath in $geometryPaths) {
            if (Test-Path -LiteralPath $candidatePath) {
                $geometryPath = $candidatePath
                $geometryCandidateCount = [int](Import-GeometryOverlayFromPath $candidatePath -Quiet)
                break
            }
        }

        Set-Dirty $true
        Refresh-All
        if ($geometryCandidateCount -gt 0) {
            [void](Expand-ProjectDimensionsToGeometry)
            Refresh-All
            [void](Fit-CanvasToGeometry -Quiet)
        }
        else {
            Normalize-ImportedEntityView
        }

        $projectEntities = @(Get-ObjectFieldArray $script:Project "entities")
        $strongGemCount = @($projectEntities | Where-Object { [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x20 }).Count
        $dragonCandidateCount = @($projectEntities | Where-Object { [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x30 }).Count
        $enemyCandidateCount = @($projectEntities | Where-Object { [int](Get-NumberField $_ "mobyTypeId" 0) -eq 0x18 }).Count
        $sourceLeadCount = @($projectEntities | Where-Object { $null -ne (Get-ObjectField $_ "stoneHillSourceLead" $null) }).Count
        $specialDataCount = @($projectEntities | Where-Object { [bool](Get-ObjectField (Get-ObjectField $_ "stoneHillSpecialData" $null) "validMainRamPointer" $false) }).Count
        $mapPlanCount = @($projectEntities | Where-Object { $null -ne (Get-ObjectField $_ "stoneHillReplicaMarker" $null) }).Count
        $routeAnchorCount = @($projectEntities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "stone-hill-public-route-anchor" }).Count
        $routeEdgeCount = @(Get-ObjectFieldArray $script:Project "stoneHillPublicRouteEdges").Count
        $geometryNote = if ($geometryCandidateCount -gt 0) { "Runtime scene geometry is loaded. Look for cyan terrain lines behind the colored moby dots; use Fit Geometry if the view looks empty." } else { "No geometry overlay was found. The colored moby dots should still be visible with Fit View." }
        $script:StatusLabel.Text = "Loaded Stone Hill: $($projectEntities.Count - $routeAnchorCount) mobys, $geometryCandidateCount geometry candidates. Cyan lines are terrain."
        $loadCommitted = $true
        if (-not $Quiet) {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "Loaded the current Stone Hill workbench.`n`nMobys: $($projectEntities.Count - $routeAnchorCount)`nRoute anchors: $routeAnchorCount`nRoute edges: $routeEdgeCount`nStrong gem/collectible labels: $strongGemCount`nWeak dragon/NPC candidates: $dragonCandidateCount`nWeak enemy/object candidates: $enemyCandidateCount`nSource-window leads attached: $sourceLeadCount`nSpecial-data records attached: $specialDataCount`nMap-plan markers attached: $mapPlanCount`nGeometry overlay candidates: $geometryCandidateCount`n`n$geometryNote`n`nThe labels remain conservative: only type 0x20 has direct gem-collection evidence. Move runtime mobys freely in the editor, but disc patches still require verified WAD source-record links.", "Stone Hill workbench loaded", "OK", "Information") | Out-Null
        }
    }
    catch {
        if (-not $loadCommitted) {
            $script:Project = $previousProject
            $script:SelectedEntityId = $previousSelectedEntityId
            Set-Dirty $previousDirty
            Refresh-All
        }
        if ($Quiet) {
            throw
        }
        else {
            [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not load the Stone Hill workbench.`n$($_.Exception.Message)", "Stone Hill workbench failed", "OK", "Error") | Out-Null
        }
    }
}

function Cycle-GeometryOverlayCandidate([int]$Direction = 1) {
    Ensure-ProjectShape
    $overlay = Get-ObjectField $script:Project "geometryOverlay" $null
    if ($null -eq $overlay) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import a geometry overlay first.", "No geometry overlay", "OK", "Information") | Out-Null
        return
    }
    $candidates = @(Get-ObjectFieldArray $overlay "candidates")
    if ($candidates.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "The imported geometry overlay has no candidates.", "No geometry candidates", "OK", "Information") | Out-Null
        return
    }
    $current = [int](Get-NumberField $script:Project "geometryOverlayCandidateIndex" 0)
    $next = ($current + $Direction) % $candidates.Count
    if ($next -lt 0) { $next += $candidates.Count }
    Set-ObjectField $script:Project "geometryOverlayCandidateIndex" $next
    $candidate = $candidates[$next]
    Set-Dirty $true
    if (-not (Fit-CanvasToGeometry -Quiet) -and $null -ne $script:Canvas) {
        $script:Canvas.Invalidate()
    }
    if ($null -ne $script:StatusLabel) {
        $bounds = Get-GeometryCandidateBounds $candidate
        $script:StatusLabel.Text = "Geometry candidate $($next + 1)/$($candidates.Count): $([string](Get-ObjectField $candidate 'runtimeAddress' 'candidate')), $(Format-GeometryBoundsText $bounds)"
    }
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Get-ImageSha1([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha1 = [System.Security.Cryptography.SHA1]::Create()
        $hash = $sha1.ComputeHash($stream)
        return -join ($hash | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $stream.Dispose()
    }
}

function Test-PvdAt([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 6)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [System.Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    $volumeId = [System.Text.Encoding]::ASCII.GetString($buffer, 40, 32).Trim()
    $root = New-Object byte[] 34
    [Array]::Copy($buffer, 156, $root, 0, 34)
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        userSize = 2048
        description = $(if ($SectorSize -eq 2352) { "Raw PS1 BIN/CUE track (2352-byte sectors)" } else { "ISO image (2048-byte sectors)" })
        volumeId = $volumeId
        rootExtent = [int](Get-UInt32LE $root 2)
        rootLength = [int](Get-UInt32LE $root 10)
    }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        foreach ($candidate in @(
            @{ SectorSize = 2048; UserOffset = 0 },
            @{ SectorSize = 2352; UserOffset = 24 },
            @{ SectorSize = 2336; UserOffset = 8 }
        )) {
            $layout = Test-PvdAt $stream $candidate.SectorSize $candidate.UserOffset
            if ($null -ne $layout) { return $layout }
        }
    }
    finally {
        $stream.Dispose()
    }
    return [ordered]@{
        sectorSize = 2048
        userOffset = 0
        userSize = 2048
        description = "Unknown image layout"
        volumeId = ""
        rootExtent = 0
        rootLength = 0
    }
}

function Read-DiscUserData([System.IO.FileStream]$Stream, $Layout, [int]$Lba, [int]$Length) {
    if ($Length -le 0) { return @() }
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $sector = $Lba
    while ($remaining -gt 0) {
        $toRead = [Math]::Min([int]$Layout.userSize, $remaining)
        $Stream.Position = ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $sector++
    }
    return $result
}

function Parse-IsoDirectory([System.IO.FileStream]$Stream, $Layout, [int]$Extent, [int]$Length, [string]$Path, [int]$Depth) {
    if ($Extent -le 0 -or $Length -le 0 -or $Depth -gt 5) { return @() }
    $data = Read-DiscUserData $Stream $Layout $Extent $Length
    $entries = @()
    $offset = 0
    while ($offset -lt $data.Length) {
        $recordLength = [int]$data[$offset]
        if ($recordLength -eq 0) {
            $offset = ([Math]::Floor($offset / 2048) + 1) * 2048
            continue
        }
        if (($offset + $recordLength) -gt $data.Length -or $recordLength -lt 34) { break }
        $lba = [int](Get-UInt32LE $data ($offset + 2))
        $size = [int](Get-UInt32LE $data ($offset + 10))
        $flags = [int]$data[$offset + 25]
        $nameLength = [int]$data[$offset + 32]
        $name = [System.Text.Encoding]::ASCII.GetString($data, ($offset + 33), $nameLength)
        $name = ($name -replace ';1$', '')
        if ($name -ne [char]0 -and $name -ne [char]1) {
            $fullPath = $(if ([string]::IsNullOrWhiteSpace($Path)) { $name } else { "$Path/$name" })
            $isDirectory = (($flags -band 2) -ne 0)
            $entry = [ordered]@{
                path = $fullPath
                name = $name
                lba = $lba
                size = $size
                isDirectory = $isDirectory
            }
            $entries += $entry
            if ($isDirectory) {
                $entries += Parse-IsoDirectory $Stream $Layout $lba $size $fullPath ($Depth + 1)
            }
        }
        $offset += $recordLength
    }
    return $entries
}

function Get-DiscFileEntries([string]$Path, $Layout) {
    if ($Layout.rootExtent -le 0 -or $Layout.rootLength -le 0) { return @() }
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return Parse-IsoDirectory $stream $Layout $Layout.rootExtent $Layout.rootLength "" 0
    }
    finally {
        $stream.Dispose()
    }
}

function Find-SpyroSerial($Entries) {
    foreach ($entry in $Entries) {
        if ($entry.name -match '^(SCUS|SCES|SCPS|SLUS|SLES|SLPS)[_.-]?\d{3}[_.-]?\d{2}$') {
            return $entry.name
        }
        if ($entry.path -match '(SCUS|SCES|SCPS|SLUS|SLES|SLPS)[_.-]?\d{3}[_.-]?\d{2}') {
            return $Matches[0]
        }
    }
    return ""
}

function Get-SpyroLevelName([int]$LevelId) {
    $names = @{
        0x0A = "Artisans"; 0x0B = "Stone Hill"; 0x0C = "Dark Hollow"; 0x0D = "Town Square";
        0x0E = "Toasty"; 0x0F = "Sunny Flight"; 0x14 = "Peace Keepers"; 0x15 = "Dry Canyon";
        0x16 = "Cliff Town"; 0x17 = "Ice Cavern"; 0x18 = "Doctor Shemp"; 0x19 = "Night Flight";
        0x1E = "Magic Crafters"; 0x1F = "Alpine Ridge"; 0x20 = "High Caves"; 0x21 = "Wizard Peak";
        0x22 = "Blowhard"; 0x23 = "Crystal Flight"; 0x28 = "Beast Makers"; 0x29 = "Terrace Village";
        0x2A = "Misty Bog"; 0x2B = "Tree Tops"; 0x2C = "Metalhead"; 0x2D = "Wild Flight";
        0x32 = "Dream Weavers"; 0x33 = "Dark Passage"; 0x34 = "Lofty Castle"; 0x35 = "Haunted Towers";
        0x36 = "Jacques"; 0x37 = "Icy Flight"; 0x3C = "Gnasty's World"; 0x3D = "Gnorc Cove";
        0x3E = "Twilight Harbor"; 0x3F = "Gnasty Gnorc"; 0x40 = "Gnasty's Loot"
    }
    if ($names.ContainsKey($LevelId)) { return $names[$LevelId] }
    return ""
}

function Read-DiscFileBytes([System.IO.FileStream]$Stream, $Layout, [int]$FileLba, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($FileLba + [Math]::Floor($absolute / 2048))
        $toCopy = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + $sectorOffset
        [void]$Stream.Read($result, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $absolute += $toCopy
    }
    return $result
}

function Convert-DiscFileOffsetToImageOffset($Layout, [int]$FileLba, [int64]$FileOffset) {
    $sectorOffset = [int]($FileOffset % 2048)
    $sector = [int64]$FileLba + [int64][Math]::Floor($FileOffset / 2048)
    return ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + $sectorOffset
}

function Convert-Int32ToBytes([int]$Value) {
    return [BitConverter]::GetBytes([int32]$Value)
}

function Convert-Int16ToBytes([int]$Value) {
    if ($Value -lt -32768 -or $Value -gt 32767) { return $null }
    return [BitConverter]::GetBytes([int16]$Value)
}

function Join-ByteArrays([byte[][]]$Arrays) {
    $length = 0
    foreach ($array in $Arrays) { $length += $array.Length }
    $result = New-Object byte[] $length
    $offset = 0
    foreach ($array in $Arrays) {
        [Array]::Copy($array, 0, $result, $offset, $array.Length)
        $offset += $array.Length
    }
    return $result
}

function Find-PatternOffsets([byte[]]$Bytes, [byte[]]$Pattern, [int]$MaxMatches = 4) {
    $matches = @()
    if ($Pattern.Length -eq 0 -or $Bytes.Length -lt $Pattern.Length) { return $matches }
    $first = $Pattern[0]
    for ($i = 0; $i -le ($Bytes.Length - $Pattern.Length); $i++) {
        if ($Bytes[$i] -ne $first) { continue }
        $ok = $true
        for ($j = 1; $j -lt $Pattern.Length; $j++) {
            if ($Bytes[$i + $j] -ne $Pattern[$j]) {
                $ok = $false
                break
            }
        }
        if ($ok) {
            $matches += $i
            if ($matches.Count -ge $MaxMatches) { return $matches }
        }
    }
    return $matches
}

function New-WadMatchResult([string]$Kind, [int64[]]$Matches, [string]$Description) {
    return [ordered]@{
        kind = $Kind
        matches = @($Matches)
        count = @($Matches).Count
        description = $Description
    }
}

function Find-WadCoordinateMatches([byte[]]$AssetBytes, [int]$RawX, [int]$RawY, [int]$RawZ) {
    $results = @()
    $xyz32 = Join-ByteArrays @((Convert-Int32ToBytes $RawX), (Convert-Int32ToBytes $RawY), (Convert-Int32ToBytes $RawZ))
    $results += New-WadMatchResult "int32-xyz" @(Find-PatternOffsets $AssetBytes $xyz32 12) "Exact raw int32 X/Y/Z"

    $xy32 = Join-ByteArrays @((Convert-Int32ToBytes $RawX), (Convert-Int32ToBytes $RawY))
    $xz32 = Join-ByteArrays @((Convert-Int32ToBytes $RawX), (Convert-Int32ToBytes $RawZ))
    $yz32 = Join-ByteArrays @((Convert-Int32ToBytes $RawY), (Convert-Int32ToBytes $RawZ))
    $results += New-WadMatchResult "int32-xy" @(Find-PatternOffsets $AssetBytes $xy32 12) "Raw int32 X/Y pair"
    $results += New-WadMatchResult "int32-xz" @(Find-PatternOffsets $AssetBytes $xz32 12) "Raw int32 X/Z pair"
    $results += New-WadMatchResult "int32-yz" @(Find-PatternOffsets $AssetBytes $yz32 12) "Raw int32 Y/Z pair"

    foreach ($scale in @(16, 256, 4096)) {
        if (($RawX % $scale) -eq 0 -and ($RawY % $scale) -eq 0 -and ($RawZ % $scale) -eq 0) {
            $sx = Convert-Int16ToBytes ([int]($RawX / $scale))
            $sy = Convert-Int16ToBytes ([int]($RawY / $scale))
            $sz = Convert-Int16ToBytes ([int]($RawZ / $scale))
            if ($null -ne $sx -and $null -ne $sy -and $null -ne $sz) {
                $pattern = Join-ByteArrays @($sx, $sy, $sz)
                $results += New-WadMatchResult "int16-xyz-div$scale" @(Find-PatternOffsets $AssetBytes $pattern 12) "Scaled int16 X/Y/Z divided by $scale"
            }
        }
    }

    return $results
}

function Get-BestWadCandidate([byte[]]$AssetBytes, [int64[]]$Candidates) {
    $ranked = @()
    foreach ($candidate in $Candidates) {
        $offset = [int]$candidate
        $windowStart = [Math]::Max(0, $offset - 32)
        $stats = Get-ByteWindowStats $AssetBytes $windowStart 96
        $ranked += [ordered]@{
            assetRelativeOffset = [int64]$candidate
            windowStart = $windowStart
            profile = $stats.profile
            shape = $stats.shape
            score = [double]$stats.score
            uniqueCount = [int]$stats.uniqueCount
            zeroPct = [double]$stats.zeroPct
        }
    }
    if ($ranked.Count -eq 0) { return $null }
    return @($ranked | Sort-Object -Property score, uniqueCount | Select-Object -First 1)[0]
}

function Link-RuntimeMobysToWad {
    Ensure-ProjectShape
    if ([string]::IsNullOrWhiteSpace($script:Project.discImage.path) -or -not (Test-Path -LiteralPath $script:Project.discImage.path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import the Spyro disc image first.", "No disc image", "OK", "Information") | Out-Null
        return
    }
    if ($null -eq $script:Project.selectedLevel -or $null -eq $script:Project.selectedLevel.assetOffset) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Select and load a detected WAD level first.", "No WAD level selected", "OK", "Information") | Out-Null
        return
    }

    $runtimeMobys = @($script:Project.entities | Where-Object { (Get-ObjectField $_ "origin" "") -eq "runtime-moby-ram-dump" })
    if ($runtimeMobys.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import runtime mobys from a RAM dump first.", "No runtime mobys", "OK", "Information") | Out-Null
        return
    }

    try {
        $layout = Detect-DiscLayout $script:Project.discImage.path
        $wadEntry = @($script:Project.discImage.files | Where-Object { $_.path -eq "WAD.WAD" } | Select-Object -First 1)
        if ($wadEntry.Count -eq 0) { throw "WAD.WAD was not found in the imported disc file table." }
        $wadEntry = $wadEntry[0]
        $assetOffset = [int64]$script:Project.selectedLevel.assetOffset
        $assetSize = [int]$script:Project.selectedLevel.assetSize
        if ($assetSize -le 0) { throw "Selected level has no asset package size." }

        $stream = [System.IO.File]::OpenRead($script:Project.discImage.path)
        try {
            $assetBytes = Read-DiscFileBytes $stream $layout ([int]$wadEntry.lba) $assetOffset $assetSize
        }
        finally {
            $stream.Dispose()
        }

        $linked = 0
        $ambiguous = 0
        $notFound = 0
        foreach ($entity in $runtimeMobys) {
            $raw = Get-ObjectField $entity "rawPosition" $null
            if ($null -eq $raw) { $notFound++; continue }
            $rawX = [int](Get-NumberField $raw "x" 0)
            $rawY = [int](Get-NumberField $raw "y" 0)
            $rawZ = [int](Get-NumberField $raw "z" 0)
            $baseNotes = [string](Get-ObjectField $entity "notes" "")
            $matchResults = @(Find-WadCoordinateMatches $assetBytes $rawX $rawY $rawZ)
            $primary = @($matchResults | Where-Object { $_.kind -eq "int32-xyz" } | Select-Object -First 1)[0]
            $matches = @($primary.matches)
            $probeSummary = @($matchResults | Where-Object { $_.count -gt 0 } | ForEach-Object { "$($_.kind)=$($_.count)" })
            if ($matches.Count -eq 1) {
                $assetRelativeOffset = [int64]$matches[0]
                $wadRelativeOffset = $assetOffset + $assetRelativeOffset
                $imageOffset = Convert-DiscFileOffsetToImageOffset $layout ([int]$wadEntry.lba) $wadRelativeOffset
                Set-ObjectField $entity "wadLink" ([ordered]@{
                    levelName = $script:Project.selectedLevel.levelName
                    assetWadIndex = $script:Project.selectedLevel.assetWadIndex
                    assetRelativeOffset = $assetRelativeOffset
                    wadRelativeOffset = $wadRelativeOffset
                    imageOffset = $imageOffset
                    coordinateOrder = "x,y,z"
                    coordinateFieldOffsets = @(0, 4, 8)
                    confidence = "exact-runtime-int32-triple"
                })
                Set-ObjectField $entity "patchStatus" "disc-linked-candidate"
                Set-ObjectField $entity "color" "#58D68D"
                Set-ObjectField $entity "notes" ($baseNotes + "`r`nWAD link: exact coordinate triple at asset offset 0x$('{0:X}' -f $assetRelativeOffset), image offset 0x$('{0:X}' -f $imageOffset).")
                $linked++
            }
            elseif ($matches.Count -gt 1) {
                $bestCandidate = Get-BestWadCandidate $assetBytes @($matches | ForEach-Object { [int64]$_ })
                $bestOffset = [int64]$bestCandidate.assetRelativeOffset
                $bestWadOffset = $assetOffset + $bestOffset
                $bestImageOffset = Convert-DiscFileOffsetToImageOffset $layout ([int]$wadEntry.lba) $bestWadOffset
                Set-ObjectField $entity "patchStatus" "disc-linked-ranked-candidate"
                Set-ObjectField $entity "wadLinkCandidates" @($matches | ForEach-Object { [int64]$_ })
                Set-ObjectField $entity "wadLink" ([ordered]@{
                    levelName = $script:Project.selectedLevel.levelName
                    assetWadIndex = $script:Project.selectedLevel.assetWadIndex
                    assetRelativeOffset = $bestOffset
                    wadRelativeOffset = $bestWadOffset
                    imageOffset = $bestImageOffset
                    coordinateOrder = "x,y,z"
                    coordinateFieldOffsets = @(0, 4, 8)
                    confidence = "ranked-table-like-ambiguous"
                    profile = $bestCandidate.profile
                    allCandidateOffsets = @($matches | ForEach-Object { [int64]$_ })
                })
                Set-ObjectField $entity "wadMatchProbe" $matchResults
                Set-ObjectField $entity "color" "#48C9B0"
                $candidateText = (($matches | Select-Object -First 6 | ForEach-Object { "0x$('{0:X}' -f $_)" }) -join ", ")
                Set-ObjectField $entity "notes" ($baseNotes + "`r`nWAD link: ambiguous coordinate triple; $($matches.Count) candidate offsets found: $candidateText. Ranked best: asset+0x$('{0:X}' -f $bestOffset), image+0x$('{0:X}' -f $bestImageOffset), $($bestCandidate.profile). Probe: $($probeSummary -join ', ').")
                $ambiguous++
            }
            else {
                Set-ObjectField $entity "wadMatchProbe" $matchResults
                $bestProbe = @($matchResults | Where-Object { $_.count -gt 0 } | Sort-Object -Property count | Select-Object -First 1)
                if ($bestProbe.Count -gt 0) {
                    Set-ObjectField $entity "patchStatus" "probe-match-no-exact-triple"
                    Set-ObjectField $entity "color" "#AF7AC5"
                    Set-ObjectField $entity "notes" ($baseNotes + "`r`nWAD link: no exact int32 X/Y/Z triple, but alternate coordinate probes matched: $($probeSummary -join ', '). Best probe: $($bestProbe[0].kind).")
                    $notFound++
                }
                else {
                    Set-ObjectField $entity "patchStatus" "no-wad-coordinate-match"
                    Set-ObjectField $entity "notes" ($baseNotes + "`r`nWAD link: no exact int32 X/Y/Z coordinate triple or alternate coordinate probe found in selected asset package.")
                    $notFound++
                }
            }
        }

        Set-Dirty $true
        Refresh-All
        Fit-CanvasToEntities
        Refresh-WadCandidateView
        [System.Windows.Forms.MessageBox]::Show($script:Form, "WAD link pass complete.`n`nExact links: $linked`nRanked ambiguous candidates: $ambiguous`nNot exact/probe-only or missing: $notFound`n`nGreen mobys have one exact source offset. Teal mobys had repeated coordinate triples and the table-like candidate was ranked best. Purple mobys have alternate coordinate probe hits but no exact triple.", "WAD link pass", "OK", "Information") | Out-Null
        $script:StatusLabel.Text = "WAD link pass: $linked exact, $ambiguous ambiguous, $notFound not found"
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not link runtime mobys to WAD data.`n$($_.Exception.Message)", "WAD link failed", "OK", "Error") | Out-Null
    }
}

function Get-SpyroWadSummary([string]$ImagePath, $Layout, $Entries) {
    $wad = @($Entries | Where-Object { $_.path -eq "WAD.WAD" } | Select-Object -First 1)
    if ($wad.Count -eq 0) { return $null }
    $wad = $wad[0]
    $stream = [System.IO.File]::OpenRead($ImagePath)
    try {
        $header = Read-DiscFileBytes $stream $Layout $wad.lba 0 2048
        $firstDataOffset = [int64](Get-UInt32LE $header 0)
        if ($firstDataOffset -le 0 -or $firstDataOffset -gt 2048) { $firstDataOffset = 2048 }
        $archiveEntries = @()
        for ($offset = 0; $offset -le ($firstDataOffset - 8); $offset += 8) {
            $fileOffset = [int64](Get-UInt32LE $header $offset)
            $fileSize = [int64](Get-UInt32LE $header ($offset + 4))
            if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
            if ($fileOffset -le 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $wad.size) { continue }
            $entryHead = Read-DiscFileBytes $stream $Layout $wad.lba $fileOffset 16
            $firstWord = [int](Get-UInt32LE $entryHead 0)
            $secondWord = [uint32](Get-UInt32LE $entryHead 4)
            $kind = "unknown"
            if ($firstWord -eq 0x800 -and $fileSize -gt 2048) { $kind = "asset-package" }
            $levelName = Get-SpyroLevelName $firstWord
            if (-not [string]::IsNullOrWhiteSpace($levelName)) { $kind = "level-metadata" }
            $archiveEntries += [ordered]@{
                index = [int]($offset / 8)
                offset = $fileOffset
                size = $fileSize
                kind = $kind
                firstWord = ("0x{0:X8}" -f $firstWord)
                levelId = $(if (-not [string]::IsNullOrWhiteSpace($levelName)) { $firstWord } else { $null })
                levelName = $levelName
                hasPointerTableHint = (([uint64]$secondWord) -ge 0x80000000L -and ([uint64]$secondWord) -lt 0x80200000L)
            }
        }
        $levels = @($archiveEntries | Where-Object { $_.kind -eq "level-metadata" } | ForEach-Object {
            $metadataIndex = $_.index
            $assetEntry = $archiveEntries | Where-Object { $_.index -eq ($metadataIndex + 1) -and $_.kind -eq "asset-package" } | Select-Object -First 1
            [ordered]@{
                levelId = $_.levelId
                levelName = $_.levelName
                metadataWadIndex = $_.index
                metadataOffset = $_.offset
                metadataSize = $_.size
                assetWadIndex = $(if ($null -ne $assetEntry) { $assetEntry.index } else { $null })
                assetOffset = $(if ($null -ne $assetEntry) { $assetEntry.offset } else { $null })
                assetSize = $(if ($null -ne $assetEntry) { $assetEntry.size } else { $null })
                hasPointerTableHint = $_.hasPointerTableHint
            }
        })
        return [ordered]@{
            wadLba = $wad.lba
            wadSize = $wad.size
            entryCount = @($archiveEntries).Count
            levelCount = @($levels).Count
            levels = $levels
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Find-AsciiMarkersInFile([string]$Path) {
    $markers = [ordered]@{
        cd001 = $false
        systemCnf = $false
        serials = @()
        likelyArchives = @()
    }
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $bufferSize = 1048576
        $buffer = New-Object byte[] $bufferSize
        $overlap = ""
        while ($true) {
            $read = $stream.Read($buffer, 0, $bufferSize)
            if ($read -le 0) { break }
            $text = $overlap + [System.Text.Encoding]::ASCII.GetString($buffer, 0, $read)
            if ($text.Contains("CD001")) { $markers.cd001 = $true }
            if ($text.Contains("SYSTEM.CNF")) { $markers.systemCnf = $true }
            foreach ($match in [regex]::Matches($text, '(SCUS|SCES|SCPS|SLUS|SLES|SLPS)[_.-]?\d{3}[_.-]?\d{2}')) {
                if ($markers.serials -notcontains $match.Value) { $markers.serials += $match.Value }
            }
            foreach ($match in [regex]::Matches($text, '[A-Z0-9_]{1,12}\.(WAD|DAT|BIN|XA|STR)')) {
                if ($markers.likelyArchives.Count -lt 25 -and $markers.likelyArchives -notcontains $match.Value) {
                    $markers.likelyArchives += $match.Value
                }
            }
            $overlap = $text.Substring([Math]::Max(0, $text.Length - 64))
        }
    }
    finally {
        $stream.Dispose()
    }
    return $markers
}

function Get-DiscAnalysis([string]$ImagePath, $Disc, $Entries) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("Container import: ready")
    if (@($Entries).Count -gt 0) {
        [void]$lines.Add("ISO9660 file table: $(@($Entries).Count) entries found")
    }
    else {
        [void]$lines.Add("ISO9660 file table: no entries found")
    }

    if (-not [string]::IsNullOrWhiteSpace($Disc.serial)) {
        [void]$lines.Add("Detected executable serial: $($Disc.serial)")
    }
    elseif (-not [string]::IsNullOrWhiteSpace($ImagePath) -and (Test-Path -LiteralPath $ImagePath)) {
        $markers = Find-AsciiMarkersInFile $ImagePath
        if ($markers.serials.Count -gt 0) {
            [void]$lines.Add("Raw scan serial candidates: $($markers.serials -join ', ')")
        }
        if ($markers.systemCnf) {
            [void]$lines.Add("Raw scan: SYSTEM.CNF marker found")
        }
        if ($markers.cd001) {
            [void]$lines.Add("Raw scan: CD001 marker found")
        }
        if ($markers.likelyArchives.Count -gt 0) {
            [void]$lines.Add("Raw scan archive/file candidates: $($markers.likelyArchives -join ', ')")
        }
    }

    $spyro1Known = $false
    if ($Disc.serial -match 'SCUS[_.-]?942[_.-]?28|SCES[_.-]?014[_.-]?38|SCPS[_.-]?100[_.-]?83') {
        $spyro1Known = $true
    }
    if ($spyro1Known) {
        [void]$lines.Add("Spyro 1 disc identity: recognized")
    }
    else {
        [void]$lines.Add("Spyro 1 disc identity: not confirmed yet")
    }

    if ($null -ne $Disc.spyroWad) {
        [void]$lines.Add("WAD.WAD archive: $($Disc.spyroWad.entryCount) entries found")
        [void]$lines.Add("Spyro level package index: $($Disc.spyroWad.levelCount) level metadata entries found")
        $preview = @($Disc.spyroWad.levels | Select-Object -First 8 | ForEach-Object { "$($_.levelName) (WAD $($_.metadataWadIndex)/$($_.assetWadIndex))" })
        if ($preview.Count -gt 0) {
            [void]$lines.Add("Detected levels: $($preview -join ', ')")
        }
    }
    else {
        [void]$lines.Add("WAD.WAD archive: not found or not parsed")
    }

    [void]$lines.Add("Level object decoder: not mapped yet")
    [void]$lines.Add("Palette/music patcher: waiting for verified Spyro 1 offsets")
    [void]$lines.Add("Direct binary patches queued: $($script:Project.binaryPatches.Count)")
    [void]$lines.Add("Next step: map the loaded executable/data files to Spyro 1 object tables, then connect those records to the editor objects.")
    return $lines.ToArray()
}

function Analyze-ImportedDisc {
    Ensure-ProjectShape
    if ([string]::IsNullOrWhiteSpace($script:Project.discImage.path) -or -not (Test-Path -LiteralPath $script:Project.discImage.path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import a disc image first.", "No image imported", "OK", "Information") | Out-Null
        return
    }
    try {
        $script:StatusLabel.Text = "Analyzing imported image..."
        $script:Project.discImage.analysis = Get-DiscAnalysis $script:Project.discImage.path $script:Project.discImage $script:Project.discImage.files
        Set-Dirty $true
        Refresh-All
        $script:StatusLabel.Text = "Analysis complete"
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not analyze image.`n$($_.Exception.Message)", "Analysis failed", "OK", "Error") | Out-Null
    }
}

function Analyze-StoneHillLevel {
    Ensure-ProjectShape
    if ([string]::IsNullOrWhiteSpace($script:Project.discImage.path) -or -not (Test-Path -LiteralPath $script:Project.discImage.path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import a disc image first.", "No image imported", "OK", "Information") | Out-Null
        return
    }

    $toolPath = Join-Path $PSScriptRoot "tools\Analyze-StoneHillObjects.ps1"
    if (-not (Test-Path -LiteralPath $toolPath)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Stone Hill analyzer was not found at:`n$toolPath", "Analyzer missing", "OK", "Error") | Out-Null
        return
    }

    try {
        $script:StatusLabel.Text = "Analyzing Stone Hill WAD data..."
        $outPath = Join-Path $PSScriptRoot "stonehill-object-analysis.json"
        $quotedTool = '"' + $toolPath + '"'
        $quotedImage = '"' + $script:Project.discImage.path + '"'
        $quotedOut = '"' + $outPath + '"'
        $args = "-NoProfile -ExecutionPolicy Bypass -File $quotedTool -ImagePath $quotedImage -OutPath $quotedOut"
        $process = Start-Process -FilePath "powershell.exe" -ArgumentList $args -NoNewWindow -PassThru -Wait
        if ($process.ExitCode -ne 0) {
            throw "Stone Hill analyzer exited with code $($process.ExitCode)."
        }

        $stoneHill = Get-Content -LiteralPath $outPath -Raw | ConvertFrom-Json
        $script:Project.discImage.stoneHill = $stoneHill
        $script:Project.levelName = "Stone Hill"
        $script:Project.music = [pscustomobject]@{ id = "stone_hill"; name = "Stone Hill" }
        Set-Dirty $true
        Refresh-All
        $script:StatusLabel.Text = "Stone Hill analysis attached to project"
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Stone Hill analysis is attached to this project.`n`nConfirmed: WAD 9/10 package offsets and the runtime moby stride/type-state fields.`n`nNext: map the on-disc object source records so moved entities can generate binary patches.", "Stone Hill analysis complete", "OK", "Information") | Out-Null
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not analyze Stone Hill.`n$($_.Exception.Message)", "Stone Hill analysis failed", "OK", "Error") | Out-Null
        $script:StatusLabel.Text = "Stone Hill analysis failed"
    }
}

function Import-DiscImage {
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = "Import Spyro PS1 disc image"
    $dialog.Filter = "Disc images (*.bin;*.iso;*.img)|*.bin;*.iso;*.img|All files (*.*)|*.*"
    if ($dialog.ShowDialog($script:Form) -ne [System.Windows.Forms.DialogResult]::OK) { return }

    try {
        $info = Get-Item -LiteralPath $dialog.FileName
        $layout = Detect-DiscLayout $dialog.FileName
        $entries = Get-DiscFileEntries $dialog.FileName $layout
        $serial = Find-SpyroSerial $entries
        $spyroWad = Get-SpyroWadSummary $dialog.FileName $layout $entries
        $disc = [ordered]@{
            path = $dialog.FileName
            fileName = $info.Name
            sizeBytes = [int64]$info.Length
            sha1 = Get-ImageSha1 $dialog.FileName
            format = $layout.description
            serial = $serial
            volumeId = $layout.volumeId
            importedAt = (Get-Date).ToString("s")
            analysis = @()
            spyroWad = $spyroWad
            stoneHill = $null
            files = @($entries | Select-Object -First 500)
        }
        $disc.analysis = Get-DiscAnalysis $dialog.FileName $disc $entries
        $script:Project.discImage = $disc
        $script:Project.selectedLevel = $null
        Set-Dirty $true
        Refresh-All
        $script:StatusLabel.Text = "Imported disc image $($info.Name)"
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not import disc image.`n$($_.Exception.Message)", "Import failed", "OK", "Error") | Out-Null
    }
}

function Convert-PsxAddressToRamOffset([uint32]$Address, [int]$RamLength) {
    $offset = [int]($Address -band 0x001FFFFF)
    if ($offset -lt 0 -or ($offset + 4) -gt $RamLength) { return -1 }
    return $offset
}

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $copy = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $copy, 0, $Length)
    return $copy
}

function Test-PlausibleRuntimeMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 80) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + 0x48]
    $state = [int]$Ram[$Offset + 0x49]
    if ($type -le 0 -or $type -gt 0x7F) { return $false }
    if ($state -gt 0x7F) { return $false }
    $x = Get-Int32LE $Ram ($Offset + 4)
    $y = Get-Int32LE $Ram ($Offset + 8)
    $z = Get-Int32LE $Ram ($Offset + 12)
    if ([Math]::Abs([int64]$x) -gt 4000000 -or [Math]::Abs([int64]$y) -gt 4000000 -or [Math]::Abs([int64]$z) -gt 4000000) { return $false }
    if ([Math]::Abs([int64]$x) -lt 16 -and [Math]::Abs([int64]$y) -lt 16 -and [Math]::Abs([int64]$z) -lt 16) { return $false }
    return $true
}

function Test-PsxPointerInMainRam([uint32]$Pointer) {
    $address = [uint64]$Pointer
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Count-PlausibleMobysAtPointer([byte[]]$Bytes, [int]$WindowBase, [uint32]$Pointer) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) { return 0 }
    $tableOffset = [int]($Pointer -band 0x001FFFFF)
    $count = 0
    for ($i = 0; $i -lt 128; $i++) {
        $recordOffset = $WindowBase + $tableOffset + ($i * 80)
        if (($recordOffset + 80) -gt $Bytes.Length) { break }
        if (Test-PlausibleRuntimeMoby $Bytes $recordOffset) { $count++ }
    }
    return $count
}

function Find-PsxRamWindow([byte[]]$Bytes) {
    $windowSize = 0x200000
    if ($Bytes.Length -lt $windowSize) {
        throw "This importer expects a raw 2 MB PS1 RAM dump, or a file containing at least the PS1 RAM region."
    }

    $ptrOffset = [int](([Convert]::ToUInt32("80075828", 16)) -band 0x001FFFFF)
    $levelIdOffset = [int](([Convert]::ToUInt32("800758B4", 16)) -band 0x001FFFFF)
    $candidateBases = New-Object System.Collections.Generic.List[int]
    foreach ($base in @(0, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000, ($Bytes.Length - $windowSize))) {
        if ($base -ge 0 -and ($base + $windowSize) -le $Bytes.Length -and -not $candidateBases.Contains($base)) {
            [void]$candidateBases.Add($base)
        }
    }
    for ($base = 0; $base -le ($Bytes.Length - $windowSize); $base += 0x10000) {
        if (-not $candidateBases.Contains($base)) { [void]$candidateBases.Add($base) }
    }

    $best = $null
    foreach ($base in $candidateBases) {
        $pointer = Get-UInt32LE $Bytes ($base + $ptrOffset)
        $mobyCount = Count-PlausibleMobysAtPointer $Bytes $base $pointer
        $levelId = Get-UInt32LE $Bytes ($base + $levelIdOffset)
        $score = $mobyCount
        if ($levelId -gt 0 -and $levelId -lt 0x80) { $score += 2 }
        if (Test-PsxPointerInMainRam $pointer) { $score += 4 }
        if ($null -eq $best -or $score -gt $best.score) {
            $best = [ordered]@{
                baseOffset = $base
                pointer = $pointer
                levelId = $levelId
                mobyCount = $mobyCount
                score = $score
            }
        }
    }

    if ($null -eq $best -or -not (Test-PsxPointerInMainRam ([uint32]$best.pointer)) -or $best.mobyCount -eq 0) {
        $rawPointer = Get-UInt32LE $Bytes $ptrOffset
        throw ("Could not find a PS1 RAM window with a live moby table. At raw offset 0x75828 the pointer is 0x{0:X8}. Make sure the dump is main RAM, not VRAM/scratchpad/save-state metadata, and dump after Stone Hill finishes loading." -f $rawPointer)
    }

    return [ordered]@{
        ram = Copy-ByteRange $Bytes $best.baseOffset $windowSize
        baseOffset = $best.baseOffset
        pointer = [uint32]$best.pointer
        levelId = [uint32]$best.levelId
        mobyCount = [int]$best.mobyCount
    }
}

function Convert-RuntimeMobysToEntities([byte[]]$Ram, [uint32]$MobyPointer, [string]$MobyTableName = "level") {
    $start = Convert-PsxAddressToRamOffset $MobyPointer $Ram.Length
    if ($start -lt 0) { throw ("Runtime moby pointer 0x{0:X8} is outside this RAM dump." -f $MobyPointer) }

    $records = @()
    $runtimeBase = [Convert]::ToUInt64("80000000", 16)
    $badRun = 0
    $afterLongGap = $false
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        $gapBeforeRecord = $badRun
        if (-not (Test-PlausibleRuntimeMoby $Ram $offset)) {
            $badRun++
            if ($badRun -ge 24) { $afterLongGap = $true }
            continue
        }
        $sparseAfterGap = ($afterLongGap -or $gapBeforeRecord -ge 24)
        $badRun = 0
        $records += [ordered]@{
            index = $i
            ramOffset = $offset
            sparseAfterGap = $sparseAfterGap
            gapBeforeRecord = $gapBeforeRecord
            runtimeAddress = ("0x{0:X8}" -f ($runtimeBase + [uint64]$offset))
            specialDataPointer = ("0x{0:X8}" -f (Get-UInt32LE $Ram $offset))
            rawX = Get-Int32LE $Ram ($offset + 4)
            rawY = Get-Int32LE $Ram ($offset + 8)
            rawZ = Get-Int32LE $Ram ($offset + 12)
            typeId = [int]$Ram[$offset + 0x48]
            state = [int]$Ram[$offset + 0x49]
        }
    }
    if ($records.Count -eq 0) { return @() }

    $placementRecords = @($records | Where-Object { -not ($_.rawX -eq 0 -and $_.rawY -eq 4096 -and $_.rawZ -eq 0) })
    if ($placementRecords.Count -eq 0) { $placementRecords = @($records) }
    $scaledX = @($placementRecords | ForEach-Object { [double]$_.rawX / 16.0 })
    $scaledY = @($placementRecords | ForEach-Object { [double]$_.rawY / 16.0 })
    $maxX = ($scaledX | Measure-Object -Maximum).Maximum
    $maxY = ($scaledY | Measure-Object -Maximum).Maximum
    $width = [Math]::Max(2400, [int]($maxX + 900))
    $height = [Math]::Max(1600, [int]($maxY + 900))
    $script:Project.dimensions.width = [Math]::Min(99999, $width)
    $script:Project.dimensions.height = [Math]::Min(99999, $height)

    $entities = @()
    foreach ($record in $records) {
        $mapX = [int]([double]$record.rawX / 16.0)
        $mapY = [int]([double]$record.rawY / 16.0)
        $mapZ = [int]([double]$record.rawZ / 16.0)
        $mobyTypeName = Get-MobyTypeNameById ([int]$record.typeId)
        $displayName = if ($mobyTypeName -ne "Unknown moby") {
            "$mobyTypeName type $($('{0:X2}' -f $record.typeId)) #$($record.index)"
        }
        else {
            "Moby type $($('{0:X2}' -f $record.typeId)) #$($record.index)"
        }
        $notePrefix = Get-MobyTypeNoteById ([int]$record.typeId)
        $runtimeConfidence = if ($record.sparseAfterGap) { "sparse-table-candidate" } else { "contiguous-table-record" }
        if ($record.sparseAfterGap) {
            $notePrefix += "Sparse table candidate: this plausible record appears after a gap of $($record.gapBeforeRecord) invalid records, so verify visually before patching. "
        }
        $entities += [ordered]@{
            id = [guid]::NewGuid().ToString()
            type = "Moby"
            name = $displayName
            x = $mapX
            y = $mapY
            z = $mapZ
            color = Get-MobyTypeColorById ([int]$record.typeId)
            origin = "runtime-moby-ram-dump"
            patchStatus = "runtime-only-not-disc-linked"
            originalMapX = $mapX
            originalMapY = $mapY
            originalMapZ = $mapZ
            runtimeMapX = $mapX
            runtimeMapY = $mapY
            runtimeMapZ = $mapZ
            mapProjection = "runtime-xy-topdown"
            runtimeAddress = $record.runtimeAddress
            runtimeMobyTable = $MobyTableName
            runtimeMobyIndex = $record.index
            runtimeConfidence = $runtimeConfidence
            gapBeforeRecord = $record.gapBeforeRecord
            mobyTypeId = $record.typeId
            mobyState = $record.state
            rawPosition = [ordered]@{ x = $record.rawX; y = $record.rawY; z = $record.rawZ }
            notes = "$notePrefix`Imported from a live PS1 RAM dump. Canvas uses top-down PS1 X/Y; inspector Z stores the remaining PS1 coordinate. Runtime address $($record.runtimeAddress), type 0x$('{0:X2}' -f $record.typeId), state 0x$('{0:X2}' -f $record.state). This is editable in the GUI, but not disc-patchable until the matching WAD source record is mapped."
        }
    }
    return $entities
}

function Import-RuntimeMobyDump {
    Ensure-ProjectShape
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = "Import Spyro runtime RAM dump"
    $dialog.Filter = "Raw PS1 RAM dump (*.ram;*.dmp;*.bin)|*.ram;*.dmp;*.bin|All files (*.*)|*.*"
    if ($dialog.ShowDialog($script:Form) -ne [System.Windows.Forms.DialogResult]::OK) { return }

    try {
        $dumpInfo = Get-Item -LiteralPath $dialog.FileName
        if ($dumpInfo.Length -gt 67108864) {
            throw "That file is $([Math]::Round($dumpInfo.Length / 1MB, 1)) MB, which looks like a disc image or save-state container, not a raw PS1 RAM dump. Use Import Image... for Spyro .bin/.cue game images. Import RAM Mobys needs a raw main-RAM dump from the emulator, usually 2 MB."
        }
        $dumpBytes = [System.IO.File]::ReadAllBytes($dialog.FileName)
        $ramWindow = Find-PsxRamWindow $dumpBytes
        $ram = $ramWindow.ram
        $mobyPointer = [uint32]$ramWindow.pointer
        $entities = @(Convert-RuntimeMobysToEntities $ram $mobyPointer)
        if ($entities.Count -eq 0) {
            throw ("No plausible moby records were found at runtime pointer 0x{0:X8}." -f $mobyPointer)
        }

        $script:Project.entities = @($entities | ForEach-Object { [pscustomobject]$_ })
        $script:SelectedEntityId = [string](Get-ObjectField $script:Project.entities[0] "id" "")
        Set-Dirty $true
        Refresh-All
        Normalize-ImportedEntityView
        $script:StatusLabel.Text = "Imported $($entities.Count) runtime mobys from RAM dump"
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Imported $($entities.Count) runtime mobys into the editor.`n`nRAM window offset: 0x$('{0:X}' -f $ramWindow.baseOffset)`nRuntime moby pointer: 0x$('{0:X8}' -f $mobyPointer)`nDetected level id: 0x$('{0:X}' -f $ramWindow.levelId)`n`nThese are live runtime objects, so you can click and move them now. They are not disc-patchable until we map each moby back to its WAD source record.", "Runtime mobys imported", "OK", "Information") | Out-Null
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not import runtime mobys.`n$($_.Exception.Message)", "RAM import failed", "OK", "Error") | Out-Null
    }
}

function Convert-HexStringToBytes([string]$Hex) {
    $clean = ($Hex -replace '[^0-9A-Fa-f]', '')
    if ($clean.Length % 2 -ne 0) {
        throw "Patch byte string has an odd number of hex digits."
    }
    $bytes = New-Object byte[] ([int]($clean.Length / 2))
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($clean.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function New-PatchPlanObject {
    Ensure-ProjectShape
    return [ordered]@{
        target = "Spyro the Dragon PS1 disc image"
        generatedBy = "Spyro PS1 Level Editor prototype"
        warning = "Object, palette, and music changes are semantic until mapped to verified Spyro binary offsets."
        levelName = $script:Project.levelName
        selectedLevel = $script:Project.selectedLevel
        discImage = $script:Project.discImage
        paletteSwap = $script:Project.palette
        musicSwap = $script:Project.music
        geometryOverlay = $script:Project.geometryOverlay
        objectPlacements = $script:Project.entities
        binaryPatches = $script:Project.binaryPatches
        pendingMappings = @("objectPlacements", "paletteSwap", "musicSwap")
    }
}

function Create-PatchedImage {
    Ensure-ProjectShape
    if ([string]::IsNullOrWhiteSpace($script:Project.discImage.path) -or -not (Test-Path -LiteralPath $script:Project.discImage.path)) {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Import a disc image first.", "No image imported", "OK", "Information") | Out-Null
        return
    }

    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Title = "Create patched image copy"
    $dialog.Filter = "Disc image copy (*.bin;*.iso;*.img)|*.bin;*.iso;*.img|All files (*.*)|*.*"
    $sourceName = [System.IO.Path]::GetFileNameWithoutExtension($script:Project.discImage.fileName)
    $sourceExt = [System.IO.Path]::GetExtension($script:Project.discImage.fileName)
    $dialog.FileName = "$sourceName-editorpatched$sourceExt"
    if ($dialog.ShowDialog($script:Form) -ne [System.Windows.Forms.DialogResult]::OK) { return }

    try {
        [System.IO.File]::Copy($script:Project.discImage.path, $dialog.FileName, $true)
        $patchCount = 0
        if ($script:Project.binaryPatches.Count -gt 0) {
            $stream = [System.IO.File]::Open($dialog.FileName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite)
            try {
                foreach ($patch in $script:Project.binaryPatches) {
                    if ($null -eq $patch.imageOffset -or [string]::IsNullOrWhiteSpace($patch.dataHex)) { continue }
                    $bytes = Convert-HexStringToBytes $patch.dataHex
                    $stream.Position = [int64]$patch.imageOffset
                    $stream.Write($bytes, 0, $bytes.Length)
                    $patchCount++
                }
            }
            finally {
                $stream.Dispose()
            }
        }

        $planPath = "$($dialog.FileName).patchplan.json"
        New-PatchPlanObject | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8
        $message = "Created patched copy.`n`nBinary patches written: $patchCount`nPatch plan: $planPath"
        if ($patchCount -eq 0) {
            $message += "`n`nNo direct game-data bytes were changed yet because the semantic edits still need verified Spyro 1 offset mappings."
        }
        [System.Windows.Forms.MessageBox]::Show($script:Form, $message, "Patched image copy", "OK", "Information") | Out-Null
        $script:StatusLabel.Text = "Created patched image copy $($dialog.FileName)"
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Could not create patched image.`n$($_.Exception.Message)", "Patch failed", "OK", "Error") | Out-Null
    }
}

function Prompt-Unsaved {
    if (-not $script:Dirty) { return $true }
    $result = [System.Windows.Forms.MessageBox]::Show($script:Form, "Save changes to this level project?", "Unsaved changes", "YesNoCancel", "Question")
    if ($result -eq [System.Windows.Forms.DialogResult]::Cancel) { return $false }
    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) { Save-Project }
    return $true
}

function Draw-Diamond([System.Drawing.Graphics]$G, [System.Drawing.Brush]$Brush, [float]$X, [float]$Y, [float]$Size) {
    $points = @(
        (New-Object System.Drawing.PointF($X, $Y - $Size)),
        (New-Object System.Drawing.PointF($X + $Size, $Y)),
        (New-Object System.Drawing.PointF($X, $Y + $Size)),
        (New-Object System.Drawing.PointF($X - $Size, $Y))
    )
    $G.FillPolygon($Brush, $points)
}

function Draw-Star([System.Drawing.Graphics]$G, [System.Drawing.Brush]$Brush, [float]$X, [float]$Y, [float]$R1, [float]$R2) {
    $points = New-Object 'System.Drawing.PointF[]' 10
    for ($i = 0; $i -lt 10; $i++) {
        $angle = (-90 + ($i * 36)) * [Math]::PI / 180
        $r = $(if ($i % 2 -eq 0) { $R1 } else { $R2 })
        $points[$i] = New-Object System.Drawing.PointF(($X + [Math]::Cos($angle) * $r), ($Y + [Math]::Sin($angle) * $r))
    }
    $G.FillPolygon($Brush, $points)
}

function Draw-WorldGrid([System.Drawing.Graphics]$G, [float]$LevelWidth, [float]$LevelHeight, [float]$Zoom, [float]$PanX, [float]$PanY) {
    $gridStep = 500
    if ($Zoom -lt 0.04) { $gridStep = 2000 }
    elseif ($Zoom -lt 0.08) { $gridStep = 1000 }

    $minorPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(42, 255, 255, 255), 1)
    $majorPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(78, 255, 255, 255), 1)
    $axisPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 255, 255, 255), 2)
    $labelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 255, 255, 255))
    $labelBack = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(130, 20, 24, 30))

    for ($x = 0; $x -le $LevelWidth; $x += $gridStep) {
        $sx = [float](($x * $Zoom) + $PanX)
        $pen = $(if (($x % ($gridStep * 2)) -eq 0) { $majorPen } else { $minorPen })
        if ($x -eq 0) { $pen = $axisPen }
        $G.DrawLine($pen, $sx, $PanY, $sx, ($PanY + ($LevelHeight * $Zoom)))
        if ($Zoom -ge 0.035) {
            $rect = New-Object System.Drawing.RectangleF(($sx + 3), ($PanY + 3), 74, 18)
            $G.FillRectangle($labelBack, $rect)
            $G.DrawString("X $x", $script:SmallFont, $labelBrush, ($rect.X + 3), ($rect.Y + 2))
        }
    }
    for ($y = 0; $y -le $LevelHeight; $y += $gridStep) {
        $sy = [float](($y * $Zoom) + $PanY)
        $pen = $(if (($y % ($gridStep * 2)) -eq 0) { $majorPen } else { $minorPen })
        if ($y -eq 0) { $pen = $axisPen }
        $G.DrawLine($pen, $PanX, $sy, ($PanX + ($LevelWidth * $Zoom)), $sy)
        if ($Zoom -ge 0.035) {
            $rect = New-Object System.Drawing.RectangleF(($PanX + 3), ($sy + 3), 74, 18)
            $G.FillRectangle($labelBack, $rect)
            $G.DrawString("Y $y", $script:SmallFont, $labelBrush, ($rect.X + 3), ($rect.Y + 2))
        }
    }
}

function Draw-PlacementBounds([System.Drawing.Graphics]$G, [float]$Zoom, [float]$PanX, [float]$PanY) {
    $placementEntities = @($script:Project.entities | Where-Object { Test-PlacementLikeEntity $_ })
    if ($placementEntities.Count -lt 2) { return }

    $xs = @($placementEntities | ForEach-Object { Get-NumberField $_ "x" 0 })
    $ys = @($placementEntities | ForEach-Object { Get-NumberField $_ "y" 0 })
    $minX = [double](($xs | Measure-Object -Minimum).Minimum)
    $maxX = [double](($xs | Measure-Object -Maximum).Maximum)
    $minY = [double](($ys | Measure-Object -Minimum).Minimum)
    $maxY = [double](($ys | Measure-Object -Maximum).Maximum)
    $pad = 350.0
    $rect = New-Object System.Drawing.RectangleF(
        [float]((($minX - $pad) * $Zoom) + $PanX),
        [float]((($minY - $pad) * $Zoom) + $PanY),
        [float](($maxX - $minX + ($pad * 2)) * $Zoom),
        [float](($maxY - $minY + ($pad * 2)) * $Zoom)
    )
    $fill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(46, 245, 232, 150))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 245, 232, 150), 2)
    $G.FillRectangle($fill, $rect)
    $G.DrawRectangle($pen, [int]$rect.X, [int]$rect.Y, [int]$rect.Width, [int]$rect.Height)
    $G.DrawString("Runtime placement area", $script:SmallFont, [System.Drawing.Brushes]::White, ($rect.X + 8), ($rect.Y + 7))
}

function Get-InterpolatedByte([double]$A, [double]$B, [double]$T) {
    return [int][Math]::Max(0, [Math]::Min(255, [Math]::Round($A + (($B - $A) * $T))))
}

function Get-HeightTintColor([double]$Value, $HeightBounds, [int]$Alpha = 82) {
    $minZ = [double](Get-NumberField $HeightBounds "minZ" 0)
    $maxZ = [double](Get-NumberField $HeightBounds "maxZ" 1)
    $span = [Math]::Max(1.0, $maxZ - $minZ)
    $t = [Math]::Max(0.0, [Math]::Min(1.0, (($Value - $minZ) / $span)))

    if ($t -lt 0.5) {
        $local = $t / 0.5
        $r = Get-InterpolatedByte 44 52 $local
        $g = Get-InterpolatedByte 124 158 $local
        $b = Get-InterpolatedByte 88 158 $local
    }
    else {
        $local = ($t - 0.5) / 0.5
        $r = Get-InterpolatedByte 52 190 $local
        $g = Get-InterpolatedByte 158 172 $local
        $b = Get-InterpolatedByte 158 112 $local
    }
    return [System.Drawing.Color]::FromArgb($Alpha, $r, $g, $b)
}

function Get-MaterialTintColor([int]$TextureId, [int]$Alpha = 92) {
    $palette = @(
        @(83, 164, 96), @(202, 180, 82), @(96, 150, 196), @(180, 128, 198),
        @(210, 112, 86), @(104, 184, 170), @(195, 146, 72), @(142, 166, 96),
        @(170, 112, 154), @(92, 138, 118), @(208, 96, 118), @(112, 178, 210),
        @(156, 138, 82), @(116, 158, 132), @(188, 132, 104), @(126, 146, 188)
    )
    $id = [Math]::Max(0, $TextureId)
    $base = $palette[$id % $palette.Count]
    $band = [int][Math]::Floor($id / [double]$palette.Count)
    $factor = 0.88 + (($band % 4) * 0.08)
    $r = [int][Math]::Max(0, [Math]::Min(255, [Math]::Round($base[0] * $factor)))
    $g = [int][Math]::Max(0, [Math]::Min(255, [Math]::Round($base[1] * $factor)))
    $b = [int][Math]::Max(0, [Math]::Min(255, [Math]::Round($base[2] * $factor)))
    return [System.Drawing.Color]::FromArgb($Alpha, $r, $g, $b)
}

function Get-PolygonHeightBounds($Candidate, $Polygons) {
    $heightBounds = Get-ObjectField $Candidate "heightBounds" $null
    if ($null -ne $heightBounds) { return $heightBounds }

    $minZ = [double]::PositiveInfinity
    $maxZ = [double]::NegativeInfinity
    foreach ($polygon in $Polygons) {
        $z = [double](Get-NumberField $polygon "avgZ" 0)
        if ($z -lt $minZ) { $minZ = $z }
        if ($z -gt $maxZ) { $maxZ = $z }
    }
    if ([double]::IsInfinity($minZ) -or [double]::IsInfinity($maxZ)) {
        return [pscustomobject]@{ minZ = 0; maxZ = 1 }
    }
    return [pscustomobject]@{ minZ = $minZ; maxZ = $maxZ }
}

function Draw-GeometryPolygons([System.Drawing.Graphics]$G, $Candidate, [float]$Zoom, [float]$PanX, [float]$PanY) {
    $polygons = @(Get-ObjectFieldArray $Candidate "polygons")
    if ($polygons.Count -eq 0) { return 0 }

    $heightBounds = Get-PolygonHeightBounds $Candidate $polygons
    $brushes = @{}
    $drawn = 0
    $maxDraw = if ($Zoom -lt 0.035) { 1200 } elseif ($Zoom -lt 0.08) { 1700 } else { 2200 }
    $step = [Math]::Max(1, [int][Math]::Ceiling($polygons.Count / [double]$maxDraw))
    try {
        for ($polygonIndex = 0; $polygonIndex -lt $polygons.Count; $polygonIndex += $step) {
            $polygon = $polygons[$polygonIndex]
            $sourcePoints = @(Get-ObjectFieldArray $polygon "points")
            if ($sourcePoints.Count -lt 3) { continue }

            $screenPoints = New-Object System.Drawing.PointF[] $sourcePoints.Count
            for ($i = 0; $i -lt $sourcePoints.Count; $i++) {
                $point = $sourcePoints[$i]
                $screenPoints[$i] = [System.Drawing.PointF]::new(
                    [float](([double](Get-NumberField $point "x" 0) * [double]$Zoom) + [double]$PanX),
                    [float](([double](Get-NumberField $point "y" 0) * [double]$Zoom) + [double]$PanY)
                )
            }

            $useMaterialColor = ([string]$script:GeometryFaceColorMode -eq "material" -and ($polygon.PSObject.Properties.Name -contains "textureId"))
            if ($useMaterialColor) {
                $textureId = [int](Get-NumberField $polygon "textureId" 0)
                $bucket = "tex:$textureId"
                if (-not $brushes.ContainsKey($bucket)) {
                    $brushes[$bucket] = New-Object System.Drawing.SolidBrush((Get-MaterialTintColor $textureId 92))
                }
            }
            else {
                $avgZ = [double](Get-NumberField $polygon "avgZ" 0)
                $minZ = [double](Get-NumberField $heightBounds "minZ" 0)
                $maxZ = [double](Get-NumberField $heightBounds "maxZ" 1)
                $heightBucket = [int][Math]::Max(0, [Math]::Min(23, [Math]::Floor((($avgZ - $minZ) / [Math]::Max(1.0, $maxZ - $minZ)) * 23.0)))
                $bucket = "height:$heightBucket"
                if (-not $brushes.ContainsKey($bucket)) {
                    $bucketValue = $minZ + ((([double]$heightBucket + 0.5) / 24.0) * [Math]::Max(1.0, $maxZ - $minZ))
                    $brushes[$bucket] = New-Object System.Drawing.SolidBrush((Get-HeightTintColor $bucketValue $heightBounds 88))
                }
            }
            $G.FillPolygon($brushes[$bucket], $screenPoints)
            $drawn++
        }
    }
    finally {
        foreach ($brush in $brushes.Values) {
            $brush.Dispose()
        }
    }
    return $drawn
}

function Draw-GeometryOverlay([System.Drawing.Graphics]$G, [float]$Zoom, [float]$PanX, [float]$PanY) {
    $overlay = Get-ObjectField $script:Project "geometryOverlay" $null
    if ($null -eq $overlay) { return }

    $candidates = @(Get-ObjectFieldArray $overlay "candidates")
    if ($candidates.Count -eq 0) { return }
    $candidateIndex = [int](Get-NumberField $script:Project "geometryOverlayCandidateIndex" 0)
    if ($candidateIndex -lt 0 -or $candidateIndex -ge $candidates.Count) { $candidateIndex = 0 }
    $candidate = $candidates[$candidateIndex]
    $points = @(Get-ObjectFieldArray $candidate "projectedPoints")
    if ($points.Count -eq 0) {
        $points = @(Get-ObjectFieldArray $candidate "points")
    }
    if ($points.Count -eq 0) { return }

    $fastViewport = Test-FastViewportMode
    $drawnPolygons = 0
    if ($script:ShowGeometryFaces -and -not $fastViewport) {
        $drawnPolygons = Draw-GeometryPolygons $G $candidate $Zoom $PanX $PanY
    }

    $pointBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 48, 236, 255))
    $edgePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(215, 28, 220, 255), 2)
    $boundsPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 255, 212, 96), 2)
    $labelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 220, 245, 255))
    $edges = @(Get-ObjectFieldArray $candidate "edges")
    $drawnEdges = 0
    if ($script:ShowGeometryEdges) {
        $maxEdgeDraw = if ($fastViewport) { 900 } elseif ($Zoom -lt 0.035) { 1400 } elseif ($Zoom -lt 0.08) { 2200 } else { 2800 }
        $edgeStep = [Math]::Max(1, [int][Math]::Ceiling($edges.Count / [double]$maxEdgeDraw))
        for ($edgeIndex = 0; $edgeIndex -lt $edges.Count; $edgeIndex += $edgeStep) {
            $edge = $edges[$edgeIndex]
            $x1 = [double](Get-NumberField $edge "x1" 0)
            $y1 = [double](Get-NumberField $edge "y1" 0)
            $x2 = [double](Get-NumberField $edge "x2" 0)
            $y2 = [double](Get-NumberField $edge "y2" 0)
            $G.DrawLine(
                $edgePen,
                [float](($x1 * $Zoom) + $PanX),
                [float](($y1 * $Zoom) + $PanY),
                [float](($x2 * $Zoom) + $PanX),
                [float](($y2 * $Zoom) + $PanY)
            )
            $drawnEdges++
        }
    }

    $bounds = Get-GeometryCandidateBounds $candidate $points
    if ($null -ne $bounds) {
        $rect = New-Object System.Drawing.RectangleF(
            [float](([double]$bounds.minX * $Zoom) + $PanX),
            [float](([double]$bounds.minY * $Zoom) + $PanY),
            [float](([double]$bounds.maxX - [double]$bounds.minX) * $Zoom),
            [float](([double]$bounds.maxY - [double]$bounds.minY) * $Zoom)
        )
        if ($rect.Width -gt 1 -and $rect.Height -gt 1) {
            $G.DrawRectangle($boundsPen, [int]$rect.X, [int]$rect.Y, [int]$rect.Width, [int]$rect.Height)
        }
    }

    $drawn = 0
    if ($script:ShowGeometryPoints -and -not $fastViewport) {
        $maxPointDraw = if ($Zoom -lt 0.08) { 800 } else { 1400 }
        $pointStep = [Math]::Max(1, [int][Math]::Ceiling($points.Count / [double]$maxPointDraw))
        for ($pointIndex = 0; $pointIndex -lt $points.Count; $pointIndex += $pointStep) {
            $point = $points[$pointIndex]
            $x = [double](Get-NumberField $point "x" 0)
            $y = [double](Get-NumberField $point "y" 0)
            $sx = [float](($x * $Zoom) + $PanX)
            $sy = [float](($y * $Zoom) + $PanY)

            $size = $(if ($Zoom -lt 0.08) { 3.0 } else { 4.5 })
            $G.FillEllipse($pointBrush, ($sx - $size), ($sy - $size), ($size * 2), ($size * 2))
            $drawn++
        }
    }

    $projection = [string](Get-ObjectField $candidate "projection" "xy")
    $fitScore = [double](Get-NumberField $candidate "fitScore" 0)
    $inside = [int](Get-NumberField $candidate "mobysInsideProjection" 0)
    $edgeSource = [string](Get-ObjectField $candidate "edgeSource" "memory-order")
    $polygonTotal = [int](Get-NumberField $candidate "totalPolygons" (Get-ObjectFieldArray $candidate "polygons").Count)
    $pointStatus = if ($script:ShowGeometryPoints) { "$drawn/$($points.Count) points" } else { "points hidden" }
    $fastText = if ($fastViewport) { " fast nav" } else { "" }
    $faceMode = if ([string]$script:GeometryFaceColorMode -eq "material") { "materials" } else { "height" }
    $label = "Stone Hill terrain${fastText}: geometry $($candidateIndex + 1)/$($candidates.Count), $([string](Get-ObjectField $candidate 'runtimeAddress' 'candidate')), $drawnPolygons/$polygonTotal faces ($faceMode), $drawnEdges/$($edges.Count) lines, $pointStatus"
    $labelRect = New-Object System.Drawing.RectangleF(10, 44, ([Math]::Min(760, [Math]::Max(260, $script:Canvas.ClientSize.Width - 20))), 28)
    $labelBack = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, 7, 22, 30))
    $G.FillRectangle($labelBack, $labelRect)
    $format = New-Object System.Drawing.StringFormat
    $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
    $G.DrawString($label, $script:SmallFont, $labelBrush, ($labelRect.X + 8), ($labelRect.Y + 7))
}

function Draw-StoneHillRouteEdges([System.Drawing.Graphics]$G, [float]$Zoom, [float]$PanX, [float]$PanY) {
    $edges = @(Get-ObjectFieldArray $script:Project "stoneHillPublicRouteEdges")
    if ($edges.Count -eq 0) { return }

    $routePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(155, 255, 210, 96), 2)
    $routePen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $labelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(215, 255, 230, 145))
    $labelBack = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(145, 20, 24, 30))
    try {
        foreach ($edge in $edges) {
            $x1 = [double](Get-NumberField $edge "x1" 0)
            $y1 = [double](Get-NumberField $edge "y1" 0)
            $x2 = [double](Get-NumberField $edge "x2" 0)
            $y2 = [double](Get-NumberField $edge "y2" 0)
            $sx1 = [float](($x1 * $Zoom) + $PanX)
            $sy1 = [float](($y1 * $Zoom) + $PanY)
            $sx2 = [float](($x2 * $Zoom) + $PanX)
            $sy2 = [float](($y2 * $Zoom) + $PanY)
            $G.DrawLine($routePen, $sx1, $sy1, $sx2, $sy2)

            if ($Zoom -ge 0.08) {
                $role = [string](Get-ObjectField $edge "routeRole" "")
                if (-not [string]::IsNullOrWhiteSpace($role)) {
                    $mx = [float](($sx1 + $sx2) / 2.0)
                    $my = [float](($sy1 + $sy2) / 2.0)
                    $rect = New-Object System.Drawing.RectangleF(($mx + 5), ($my - 10), 156, 18)
                    $G.FillRectangle($labelBack, $rect)
                    $format = New-Object System.Drawing.StringFormat
                    $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
                    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
                    $G.DrawString($role, $script:SmallFont, $labelBrush, $rect, $format)
                }
            }
        }
    }
    finally {
        $routePen.Dispose()
        $labelBrush.Dispose()
        $labelBack.Dispose()
    }
}

function Get-CanvasEntityLabel($Entity) {
    $index = [int](Get-NumberField $Entity "runtimeMobyIndex" -1)
    $displayLabel = [string](Get-ObjectField $Entity "stoneHillDisplayTargetLabel" "")
    $targetGuess = Get-ObjectField $Entity "stoneHillPublicTargetGuess" $null
    $targetCandidates = @(Get-ObjectFieldArray $Entity "stoneHillPublicTargetCandidates")
    $candidateKind = [string](Get-ObjectField $Entity "candidateKind" "")
    $entityName = [string](Get-ObjectField $Entity "name" "Object")

    if ([string]::IsNullOrWhiteSpace($displayLabel) -and $targetCandidates.Count -gt 0) {
        $displayLabel = [string](Get-ObjectField $targetCandidates[0] "name" "")
    }
    if ([string]::IsNullOrWhiteSpace($displayLabel) -and $null -ne $targetGuess) {
        $displayLabel = [string](Get-ObjectField $targetGuess "label" "")
    }
    if ([string]::IsNullOrWhiteSpace($displayLabel) -and -not [string]::IsNullOrWhiteSpace($candidateKind)) {
        $displayLabel = $candidateKind
    }
    if ([string]::IsNullOrWhiteSpace($displayLabel)) {
        $displayLabel = $entityName
    }

    $displayLabel = $displayLabel `
        -replace "Stone Hill ", "" `
        -replace "\(Stone Hill treasure/gem\)", "" `
        -replace "Internal helper/state record", "helper/state" `
        -replace "unverified late-table record", "late-table"
    $displayLabel = $displayLabel.Trim()

    if ($index -ge 0) {
        $label = "L$index $displayLabel"
        $sourceStatus = [string](Get-ObjectField $Entity "stoneHillSourceStatus" "")
        if ($sourceStatus -eq "scaled-int16 source-window lead") {
            $label += " lead"
        }
        return $label
    }

    return $displayLabel
}

function Draw-Canvas([object]$Sender, [System.Windows.Forms.PaintEventArgs]$Event) {
    $g = $Event.Graphics
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $canvasBack = [System.Drawing.Color]::FromArgb(24, 26, 30)
    $g.Clear($canvasBack)

    $bg = Convert-HexToColor ([string](Get-ObjectField $script:Project.palette "background" "#7AB5FF"))
    $terrain = Convert-HexToColor ([string](Get-ObjectField $script:Project.palette "terrain" "#6CC46F"))
    $accent = Convert-HexToColor ([string](Get-ObjectField $script:Project.palette "accent" "#E8CE6D"))
    $levelWidth = [float](Get-NumberField $script:Project.dimensions "width" 2400)
    $levelHeight = [float](Get-NumberField $script:Project.dimensions "height" 1600)
    if ($levelWidth -lt 200) { $levelWidth = 200 }
    if ($levelHeight -lt 200) { $levelHeight = 200 }
    $panX = [float](Convert-ToNumber $script:Pan.X 40)
    $panY = [float](Convert-ToNumber $script:Pan.Y 40)
    $zoom = [float][Math]::Max(0.02, [double]$script:Zoom)
    $levelRect = New-Object System.Drawing.RectangleF($panX, $panY, ($levelWidth * $zoom), ($levelHeight * $zoom))
    $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, $bg))), $levelRect)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(95, $terrain))), ($levelRect.X + 24), ($levelRect.Y + 28), ($levelRect.Width - 48), ($levelRect.Height - 56))
    Draw-WorldGrid $g $levelWidth $levelHeight $zoom $panX $panY
    Draw-PlacementBounds $g $zoom $panX $panY
    Draw-GeometryOverlay $g $zoom $panX $panY
    Draw-StoneHillRouteEdges $g $zoom $panX $panY
    $g.DrawRectangle((New-Object System.Drawing.Pen($accent, 3)), [int]$levelRect.X, [int]$levelRect.Y, [int]$levelRect.Width, [int]$levelRect.Height)

    $entityCount = @($script:Project.entities).Count
    foreach ($entity in $script:Project.entities) {
        try {
            $entityX = [double](Get-NumberField $entity "x" 0)
            $entityY = [double](Get-NumberField $entity "y" 0)
            $entityType = [string](Get-ObjectField $entity "type" "Object")
            $entityName = Get-CanvasEntityLabel $entity
            $entityId = [string](Get-ObjectField $entity "id" "")
            $isPlacement = Test-PlacementLikeEntity $entity
            $screenX = [float](($entityX * [double]$script:Zoom) + [double](Convert-ToNumber $script:Pan.X 0))
            $screenY = [float](($entityY * [double]$script:Zoom) + [double](Convert-ToNumber $script:Pan.Y 0))
            $color = Convert-HexToColor ([string](Get-ObjectField $entity "color" "#808080"))
            if (-not $isPlacement -and $entityId -ne $script:SelectedEntityId) {
                $color = [System.Drawing.Color]::FromArgb(120, $color)
            }
            $brush = New-Object System.Drawing.SolidBrush($color)
            $outline = $(if ($entityId -eq $script:SelectedEntityId) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(50, 50, 50) })
            if ($entityType -eq "Gem") {
                Draw-Diamond $g $brush $screenX $screenY 9
            }
            elseif ($entityType -eq "Dragon") {
                Draw-Star $g $brush $screenX $screenY 13 6
            }
            elseif ($entityType -eq "Landmark") {
                $landmarkPen = New-Object System.Drawing.Pen($color, 2)
                $g.DrawEllipse($landmarkPen, ($screenX - 14), ($screenY - 14), 28, 28)
                $g.DrawLine($landmarkPen, ($screenX - 19), $screenY, ($screenX + 19), $screenY)
                $g.DrawLine($landmarkPen, $screenX, ($screenY - 19), $screenX, ($screenY + 19))
                $landmarkPen.Dispose()
            }
            else {
                $g.FillEllipse($brush, ($screenX - 10), ($screenY - 10), 20, 20)
            }
            $g.DrawEllipse((New-Object System.Drawing.Pen($outline, 2)), ($screenX - 13), ($screenY - 13), 26, 26)
            $clientWidth = [double](Convert-ToNumber $Sender.ClientSize.Width 0)
            $labelWidth = [float]([Math]::Min(170.0, [Math]::Max(60.0, ($clientWidth - ($screenX + 22.0)))))
            if ($labelWidth -gt 35 -and ($isPlacement -or $entityId -eq $script:SelectedEntityId)) {
                $labelRect = New-Object System.Drawing.RectangleF(($screenX + 14), ($screenY - 12), $labelWidth, 18)
                $labelBack = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 20, 24, 30))
                $g.FillRectangle($labelBack, $labelRect)
                $format = New-Object System.Drawing.StringFormat
                $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
                $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
                $g.DrawString($entityName, $script:SmallFont, [System.Drawing.Brushes]::White, $labelRect, $format)
            }
        }
        catch {
            continue
        }
    }

    $overlayText = "Objects: $entityCount   Zoom: {0:P0}   Runtime XY grid   Pan: {1},{2}" -f $script:Zoom, [int]$panX, [int]$panY
    if ($entityCount -eq 0) {
        $overlayText += "   No objects loaded"
    }
    $overlayText += "   Wheel zoom, right-drag pan"
    $overlayRect = New-Object System.Drawing.RectangleF(10, 10, ([Math]::Min(520, $Sender.ClientSize.Width - 20)), 28)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(185, 10, 12, 16))), $overlayRect)
    $g.DrawString($overlayText, $script:SmallFont, [System.Drawing.Brushes]::White, ($overlayRect.X + 8), ($overlayRect.Y + 7))
}

function Build-Menu {
    $menu = New-Object System.Windows.Forms.MenuStrip
    $file = New-Object System.Windows.Forms.ToolStripMenuItem("File")
    $new = New-Object System.Windows.Forms.ToolStripMenuItem("New")
    $open = New-Object System.Windows.Forms.ToolStripMenuItem("Open...")
    $save = New-Object System.Windows.Forms.ToolStripMenuItem("Save")
    $saveAs = New-Object System.Windows.Forms.ToolStripMenuItem("Save As...")
    $importImage = New-Object System.Windows.Forms.ToolStripMenuItem("Import Disc Image...")
    $patchImage = New-Object System.Windows.Forms.ToolStripMenuItem("Create Patched Image Copy...")
    $importRam = New-Object System.Windows.Forms.ToolStripMenuItem("Import Runtime Moby RAM Dump...")
    $importStoneHillReplica = New-Object System.Windows.Forms.ToolStripMenuItem("Load Stone Hill Workbench")
    $showStoneHillTargets = New-Object System.Windows.Forms.ToolStripMenuItem("Show Stone Hill Target Board")
    $showStoneHillReadiness = New-Object System.Windows.Forms.ToolStripMenuItem("Show Stone Hill Readiness")
    $showStoneHillValidation = New-Object System.Windows.Forms.ToolStripMenuItem("Show Stone Hill Validation Playbook")
    $showStoneHillIdentity = New-Object System.Windows.Forms.ToolStripMenuItem("Show Stone Hill Identity Matrix")
    $showStoneHillDossier = New-Object System.Windows.Forms.ToolStripMenuItem("Show Stone Hill Replica Dossier")
    $showStoneHillMobyMap = New-Object System.Windows.Forms.ToolStripMenuItem("Show Stone Hill Moby Map")
    $remapRuntime = New-Object System.Windows.Forms.ToolStripMenuItem("Use X/Y Top-Down for Runtime Mobys")
    $importGeometry = New-Object System.Windows.Forms.ToolStripMenuItem("Import Geometry Overlay...")
    $importCandidate4Xz = New-Object System.Windows.Forms.ToolStripMenuItem("Import Candidate 4 X/Z Overlay")
    $nextGeometry = New-Object System.Windows.Forms.ToolStripMenuItem("Next Geometry Candidate")
    $fitGeometry = New-Object System.Windows.Forms.ToolStripMenuItem("Fit Geometry View")
    $linkWad = New-Object System.Windows.Forms.ToolStripMenuItem("Link Runtime Mobys to WAD...")
    $queuePatch = New-Object System.Windows.Forms.ToolStripMenuItem("Queue Selected WAD Position Patch...")
    $queueCandidatePatches = New-Object System.Windows.Forms.ToolStripMenuItem("Queue Selected All Candidate Position Patches...")
    $queueStoneHillSourcePatches = New-Object System.Windows.Forms.ToolStripMenuItem("Queue Selected Stone Hill Source-Window Patches...")
    $export = New-Object System.Windows.Forms.ToolStripMenuItem("Export Patch Plan...")
    $exit = New-Object System.Windows.Forms.ToolStripMenuItem("Exit")
    $new.Add_Click({ if (Prompt-Unsaved) { $script:Project = New-EditorProject; $script:ProjectPath = $null; $script:SelectedEntityId = $null; Set-Dirty $false; Refresh-All } })
    $open.Add_Click({ if (Prompt-Unsaved) { Open-Project } })
    $save.Add_Click({ Save-Project })
    $saveAs.Add_Click({ Save-ProjectAs })
    $importImage.Add_Click({ Import-DiscImage })
    $importRam.Add_Click({ Import-RuntimeMobyDump })
    $importStoneHillReplica.Add_Click({ if (Prompt-Unsaved) { Import-StoneHillReplica } })
    $showStoneHillTargets.Add_Click({ Show-StoneHillTargetBoard })
    $showStoneHillReadiness.Add_Click({ Show-StoneHillReplicaReadiness })
    $showStoneHillValidation.Add_Click({ Show-StoneHillValidationPlaybook })
    $showStoneHillIdentity.Add_Click({ Show-StoneHillIdentityMatrix })
    $showStoneHillDossier.Add_Click({ Show-StoneHillReplicaDossier })
    $showStoneHillMobyMap.Add_Click({ Show-StoneHillMobyMap })
    $remapRuntime.Add_Click({ Remap-RuntimeMobysToTopDownXY })
    $importGeometry.Add_Click({ Import-GeometryOverlay })
    $importCandidate4Xz.Add_Click({ Import-Candidate4XZOverlay })
    $nextGeometry.Add_Click({ Cycle-GeometryOverlayCandidate })
    $fitGeometry.Add_Click({ Fit-CanvasToGeometry })
    $linkWad.Add_Click({ Link-RuntimeMobysToWad })
    $queuePatch.Add_Click({ Queue-SelectedWadPositionPatch })
    $queueCandidatePatches.Add_Click({ Queue-SelectedAllCandidatePositionPatches })
    $queueStoneHillSourcePatches.Add_Click({ Queue-SelectedStoneHillSourceWindowPatches })
    $patchImage.Add_Click({ Create-PatchedImage })
    $export.Add_Click({ Export-PatchPlan })
    $exit.Add_Click({ $script:Form.Close() })
    [void]$file.DropDownItems.AddRange(@($new, $open, $save, $saveAs, (New-Object System.Windows.Forms.ToolStripSeparator), $importImage, $importRam, $importStoneHillReplica, $showStoneHillTargets, $showStoneHillReadiness, $showStoneHillValidation, $showStoneHillIdentity, $showStoneHillDossier, $showStoneHillMobyMap, $remapRuntime, $importGeometry, $importCandidate4Xz, $nextGeometry, $fitGeometry, $linkWad, $queuePatch, $queueCandidatePatches, $queueStoneHillSourcePatches, $patchImage, $export, (New-Object System.Windows.Forms.ToolStripSeparator), $exit))

    $edit = New-Object System.Windows.Forms.ToolStripMenuItem("Edit")
    $addGem = New-Object System.Windows.Forms.ToolStripMenuItem("Add Gem")
    $addEnemy = New-Object System.Windows.Forms.ToolStripMenuItem("Add Enemy")
    $addDragon = New-Object System.Windows.Forms.ToolStripMenuItem("Add Dragon")
    $addGem.Add_Click({ Add-Entity "Gem" })
    $addEnemy.Add_Click({ Add-Entity "Enemy" })
    $addDragon.Add_Click({ Add-Entity "Dragon" })
    [void]$edit.DropDownItems.AddRange(@($addGem, $addEnemy, $addDragon))

    $help = New-Object System.Windows.Forms.ToolStripMenuItem("Help")
    $about = New-Object System.Windows.Forms.ToolStripMenuItem("About")
    $about.Add_Click({
        [System.Windows.Forms.MessageBox]::Show($script:Form, "Spyro PS1 Level Editor prototype`n`nMove gems, enemies, and dragon locations; palette swap level colors; change level music; import PS1 images; save/load project files; export patch plans.", "About", "OK", "Information") | Out-Null
    })
    [void]$help.DropDownItems.Add($about)

    [void]$menu.Items.AddRange(@($file, $edit, $help))
    return $menu
}

function Build-App {
    $script:SmallFont = New-Object System.Drawing.Font("Segoe UI", 8)
    $script:UiFont = New-Object System.Drawing.Font("Segoe UI", 9)
    $script:HeaderFont = New-Object System.Drawing.Font("Segoe UI Semibold", 10)

    $script:Form = New-Object System.Windows.Forms.Form
    $script:Form.StartPosition = "CenterScreen"
    $script:Form.MinimumSize = New-Object System.Drawing.Size(980, 680)
    $script:Form.Size = New-Object System.Drawing.Size(1400, 860)
    $script:Form.Font = $script:UiFont
    $script:Form.BackColor = [System.Drawing.Color]::FromArgb(244, 246, 248)
    $script:Form.KeyPreview = $true
    $script:Form.Add_KeyDown({
        if ($_.KeyCode -eq [System.Windows.Forms.Keys]::G) {
            if ($_.Shift) {
                Cycle-GeometryOverlayCandidate -1
            }
            else {
                Cycle-GeometryOverlayCandidate 1
            }
            $_.Handled = $true
        }
        elseif ($_.KeyCode -eq [System.Windows.Forms.Keys]::F) {
            Fit-CanvasToEntities
            $_.Handled = $true
        }
        elseif ($_.KeyCode -eq [System.Windows.Forms.Keys]::H) {
            Fit-CanvasToGeometry
            $_.Handled = $true
        }
    })
    $script:Form.MainMenuStrip = Build-Menu
    $script:Form.Controls.Add($script:Form.MainMenuStrip)

    $script:ViewportSettleTimer = New-Object System.Windows.Forms.Timer
    $script:ViewportSettleTimer.Interval = 320
    $script:ViewportSettleTimer.Add_Tick({
        if ((Get-Date) -ge $script:FastViewportUntil) {
            $script:ViewportSettleTimer.Stop()
            if ($null -ne $script:Canvas) {
                $script:Canvas.Invalidate()
            }
        }
    })

    $main = New-Object System.Windows.Forms.SplitContainer
    $main.Dock = "Fill"
    $main.Orientation = "Vertical"
    $main.SplitterDistance = 820
    $main.Panel1.Padding = New-Object System.Windows.Forms.Padding(14)
    $main.Panel2.Padding = New-Object System.Windows.Forms.Padding(12)
    $main.BackColor = New-UiColor 216 222 230
    $script:Form.Controls.Add($main)
    $main.BringToFront()

    $toolbar = New-Object System.Windows.Forms.FlowLayoutPanel
    $toolbar.Dock = "Top"
    $toolbar.Height = 44
    $toolbar.FlowDirection = "LeftToRight"
    $toolbar.WrapContents = $true
    $toolbar.AutoScroll = $true
    $toolbar.Padding = New-Object System.Windows.Forms.Padding(0, 6, 0, 4)
    $toolbar.BackColor = New-UiColor 244 246 248
    $main.Panel1.Controls.Add($toolbar)

    foreach ($pair in @(
        @("Add Gem", { Add-Entity "Gem" }),
        @("Add Enemy", { Add-Entity "Enemy" }),
        @("Add Dragon", { Add-Entity "Dragon" }),
        @("Load Stone Hill", { if (Prompt-Unsaved) { Import-StoneHillReplica } }),
        @("Fit View", { Fit-CanvasToEntities }),
        @("Fit Geometry", { Fit-CanvasToGeometry }),
        @("Reset View", { Reset-CanvasView }),
        @("Next Geometry", { Cycle-GeometryOverlayCandidate }),
        @("Faces", { Toggle-GeometryLayer "faces" }),
        @("Materials", { Toggle-GeometryFaceColorMode }),
        @("Lines", { Toggle-GeometryLayer "edges" }),
        @("Points", { Toggle-GeometryLayer "points" }),
        @("Zoom In", { Set-CanvasZoom ([double]$script:Zoom * 1.25) }),
        @("Zoom Out", { Set-CanvasZoom ([double]$script:Zoom / 1.25) })
    )) {
        $button = New-ActionButton $pair[0] 112 $pair[1]
        [void]$toolbar.Controls.Add($button)
    }

    $script:Canvas = New-Object System.Windows.Forms.Panel
    $script:Canvas.Dock = "Fill"
    $script:Canvas.BackColor = [System.Drawing.Color]::FromArgb(30, 32, 36)
    $script:Canvas.BorderStyle = "FixedSingle"
    try {
        $doubleBufferedProperty = $script:Canvas.GetType().GetProperty("DoubleBuffered", [System.Reflection.BindingFlags]"Instance, NonPublic")
        $doubleBufferedProperty.SetValue($script:Canvas, $true, $null)
    }
    catch { }
    $script:Canvas.Add_Paint({
        param($sender, $event)
        try {
            Draw-Canvas $sender $event
        }
        catch {
            $errorRect = New-Object System.Drawing.RectangleF(10, 46, ([Math]::Max(240, $sender.ClientSize.Width - 20)), 48)
            $event.Graphics.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 80, 20, 24))), $errorRect)
            $event.Graphics.DrawString("Canvas draw error: $($_.Exception.Message)", $script:UiFont, [System.Drawing.Brushes]::White, ($errorRect.X + 8), ($errorRect.Y + 8))
            if ($null -ne $script:StatusLabel) {
                $script:StatusLabel.Text = "Canvas draw skipped bad object data: $($_.Exception.Message)"
            }
        }
    })
    $script:Canvas.TabStop = $true
    $script:Canvas.Add_MouseEnter({ $script:Canvas.Focus() })
    $script:Canvas.Add_MouseWheel({
        param($sender, $event)
        $factor = if ($event.Delta -gt 0) { 1.18 } else { 1.0 / 1.18 }
        Start-FastViewportMode 300
        Set-CanvasZoom ([double]$script:Zoom * $factor) $event.X $event.Y
        if ($null -ne $script:StatusLabel) {
            $script:StatusLabel.Text = "Zoom {0:P0}; right-drag or middle-drag to pan" -f [double]$script:Zoom
        }
    })
    $script:Canvas.Add_MouseDown({
        param($sender, $event)
        $script:Canvas.Focus()
        if ($event.Button -eq [System.Windows.Forms.MouseButtons]::Right -or $event.Button -eq [System.Windows.Forms.MouseButtons]::Middle) {
            Start-FastViewportMode 320
            $script:PanningCanvas = $true
            $script:PanDragLast = New-Object System.Drawing.Point($event.X, $event.Y)
            $script:Canvas.Cursor = [System.Windows.Forms.Cursors]::SizeAll
            return
        }
        if ($event.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
            $entity = Hit-TestEntity $event.X $event.Y
            if ($null -ne $entity) {
                $entityId = [string](Get-ObjectField $entity "id" "")
                Select-Entity $entityId
                $world = Screen-ToWorld $event.X $event.Y
                $script:DragEntityId = $entityId
                $script:DragOffset = New-Object System.Drawing.PointF(((Get-NumberField $entity "x" 0) - $world.X), ((Get-NumberField $entity "y" 0) - $world.Y))
            }
        }
    })
    $script:Canvas.Add_MouseMove({
        param($sender, $event)
        if ($script:PanningCanvas) {
            Start-FastViewportMode 320
            $dx = $event.X - $script:PanDragLast.X
            $dy = $event.Y - $script:PanDragLast.Y
            $script:Pan = New-Object System.Drawing.PointF(([float]([double]$script:Pan.X + $dx)), ([float]([double]$script:Pan.Y + $dy)))
            $script:PanDragLast = New-Object System.Drawing.Point($event.X, $event.Y)
            $script:Canvas.Invalidate()
        }
        elseif (-not [string]::IsNullOrWhiteSpace($script:DragEntityId)) {
            $entity = Get-EntityById $script:DragEntityId
            if ($null -ne $entity) {
                $world = Screen-ToWorld $event.X $event.Y
                Set-ObjectField $entity "x" ([int][Math]::Max(0, [Math]::Min((Get-NumberField $script:Project.dimensions "width" 2400), $world.X + $script:DragOffset.X)))
                Set-ObjectField $entity "y" ([int][Math]::Max(0, [Math]::Min((Get-NumberField $script:Project.dimensions "height" 1600), $world.Y + $script:DragOffset.Y)))
                Refresh-Inspector
                Refresh-EntityList
                $script:Canvas.Invalidate()
                Set-Dirty $true
            }
        }
    })
    $script:Canvas.Add_MouseUp({
        $script:DragEntityId = $null
        $script:PanningCanvas = $false
        if ($null -ne $script:Canvas) {
            $script:Canvas.Cursor = [System.Windows.Forms.Cursors]::Default
        }
    })
    $script:Canvas.Add_Resize({ $script:Canvas.Invalidate() })
    $main.Panel1.Controls.Add($script:Canvas)

    $rightTabs = New-Object System.Windows.Forms.TabControl
    $rightTabs.Dock = "Fill"
    $rightTabs.Padding = New-Object System.Drawing.Point(12, 5)
    $script:RightTabs = $rightTabs
    $main.Panel2.Controls.Add($rightTabs)

    $levelTab = New-Object System.Windows.Forms.TabPage("Level")
    $objectsTab = New-Object System.Windows.Forms.TabPage("Objects")
    $script:ObjectsTab = $objectsTab
    $discTab = New-Object System.Windows.Forms.TabPage("Disc Image")
    $script:DiscTab = $discTab
    [void]$rightTabs.TabPages.AddRange(@($levelTab, $objectsTab, $discTab))

    $levelPanel = New-Object System.Windows.Forms.TableLayoutPanel
    $levelPanel.Dock = "Top"
    $levelPanel.AutoSize = $true
    $levelPanel.ColumnCount = 2
    $levelPanel.RowCount = 9
    $levelPanel.Padding = New-Object System.Windows.Forms.Padding(12)
    [void]$levelPanel.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Absolute", 126)))
    [void]$levelPanel.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Percent", 100)))
    [void]$levelPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 38)))
    [void]$levelPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 92)))
    for ($rowIndex = 2; $rowIndex -lt 8; $rowIndex++) {
        [void]$levelPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 38)))
    }
    [void]$levelPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 44)))
    $levelTab.Controls.Add($levelPanel)

    $levelPickerPanel = New-Object System.Windows.Forms.TableLayoutPanel
    $levelPickerPanel.Dock = "Fill"
    $levelPickerPanel.ColumnCount = 2
    $levelPickerPanel.RowCount = 1
    $levelPickerPanel.Margin = New-Object System.Windows.Forms.Padding(0)
    [void]$levelPickerPanel.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Percent", 100)))
    [void]$levelPickerPanel.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Absolute", 76)))

    $script:LevelSelectBox = New-Object System.Windows.Forms.ComboBox
    $script:LevelSelectBox.DropDownStyle = "DropDownList"
    $script:LevelSelectBox.Dock = "Fill"
    $script:LevelSelectBox.Enabled = $false
    $script:LevelSelectBox.DropDownWidth = 360
    $script:LevelSelectBox.MaxDropDownItems = 18
    $script:LevelSelectBox.Margin = New-Object System.Windows.Forms.Padding(0, 4, 8, 4)
    $script:LevelSelectBox.Add_SelectedIndexChanged({
        if ($script:Refreshing) { return }
        Update-LevelSelectionPreview
    })
    $loadLevelButton = New-ActionButton "Load" 68 { Select-DiscLevel }
    $loadLevelButton.Dock = "Fill"
    $loadLevelButton.Margin = New-Object System.Windows.Forms.Padding(0, 4, 0, 4)
    [void]$levelPickerPanel.Controls.Add($script:LevelSelectBox, 0, 0)
    [void]$levelPickerPanel.Controls.Add($loadLevelButton, 1, 0)

    $script:LevelSelectionInfoBox = New-Object System.Windows.Forms.TextBox
    $script:LevelSelectionInfoBox.Dock = "Fill"
    $script:LevelSelectionInfoBox.Multiline = $true
    $script:LevelSelectionInfoBox.ReadOnly = $true
    $script:LevelSelectionInfoBox.ScrollBars = "Vertical"
    $script:LevelSelectionInfoBox.WordWrap = $false
    $script:LevelSelectionInfoBox.Text = "Import a Spyro disc image to populate detected WAD levels."

    $script:LevelNameBox = New-Object System.Windows.Forms.TextBox
    $script:LevelNameBox.Dock = "Fill"
    $script:LevelNameBox.Add_TextChanged({ if ($script:Refreshing) { return }; $script:Project.levelName = $script:LevelNameBox.Text; Set-Dirty $true })
    $script:WidthBox = New-Object System.Windows.Forms.NumericUpDown
    $script:WidthBox.Maximum = 99999
    $script:WidthBox.Minimum = 200
    $script:WidthBox.Dock = "Fill"
    $script:WidthBox.Add_ValueChanged({ if ($script:Refreshing) { return }; $script:Project.dimensions.width = [int]$script:WidthBox.Value; Set-Dirty $true; $script:Canvas.Invalidate() })
    $script:HeightBox = New-Object System.Windows.Forms.NumericUpDown
    $script:HeightBox.Maximum = 99999
    $script:HeightBox.Minimum = 200
    $script:HeightBox.Dock = "Fill"
    $script:HeightBox.Add_ValueChanged({ if ($script:Refreshing) { return }; $script:Project.dimensions.height = [int]$script:HeightBox.Value; Set-Dirty $true; $script:Canvas.Invalidate() })
    $script:PaletteBox = New-Object System.Windows.Forms.ComboBox
    $script:PaletteBox.DropDownStyle = "DropDownList"
    $script:PaletteBox.Dock = "Fill"
    foreach ($palette in $script:Palettes) { [void]$script:PaletteBox.Items.Add($palette.Name) }
    $script:PaletteBox.Add_SelectedIndexChanged({
        if ($script:Refreshing) { return }
        $selected = $script:Palettes | Where-Object { $_.Name -eq $script:PaletteBox.SelectedItem } | Select-Object -First 1
        if ($null -ne $selected) {
            $script:Project.palette.name = $selected.Name
            $script:Project.palette.background = $selected.Background
            $script:Project.palette.terrain = $selected.Terrain
            $script:Project.palette.accent = $selected.Accent
            $script:PalettePreview.BackColor = Convert-HexToColor $selected.Background
            Set-Dirty $true
            $script:Canvas.Invalidate()
        }
    })
    $script:MusicBox = New-Object System.Windows.Forms.ComboBox
    $script:MusicBox.DropDownStyle = "DropDownList"
    $script:MusicBox.Dock = "Fill"
    foreach ($track in $script:MusicTracks) { [void]$script:MusicBox.Items.Add($track.Name) }
    $script:MusicBox.Add_SelectedIndexChanged({
        if ($script:Refreshing) { return }
        $selected = $script:MusicTracks | Where-Object { $_.Name -eq $script:MusicBox.SelectedItem } | Select-Object -First 1
        if ($null -ne $selected) {
            $script:Project.music.id = $selected.Id
            $script:Project.music.name = $selected.Name
            Set-Dirty $true
        }
    })
    $script:PalettePreview = New-Object System.Windows.Forms.Panel
    $script:PalettePreview.Dock = "Fill"
    $script:PalettePreview.Height = 28
    $script:PalettePreview.BorderStyle = "FixedSingle"

    $rows = @(
        @("Disc level", $levelPickerPanel),
        @("WAD details", $script:LevelSelectionInfoBox),
        @("Level name", $script:LevelNameBox),
        @("Width", $script:WidthBox),
        @("Height", $script:HeightBox),
        @("Palette swap", $script:PaletteBox),
        @("Preview", $script:PalettePreview),
        @("Music", $script:MusicBox)
    )
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $label = New-FieldLabel $rows[$i][0]
        [void]$levelPanel.Controls.Add($label, 0, $i)
        [void]$levelPanel.Controls.Add($rows[$i][1], 1, $i)
    }

    $exportButton = New-ActionButton "Export Patch Plan" 180 { Export-PatchPlan }
    $exportButton.Height = 34
    $exportButton.Dock = "Fill"
    [void]$levelPanel.Controls.Add($exportButton, 1, 8)

    $discLayout = New-Object System.Windows.Forms.TableLayoutPanel
    $discLayout.Dock = "Fill"
    $discLayout.ColumnCount = 1
    $discLayout.RowCount = 4
    $discLayout.Padding = New-Object System.Windows.Forms.Padding(10)
    [void]$discLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 82)))
    [void]$discLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 132)))
    [void]$discLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 100)))
    [void]$discLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 86)))
    $discTab.Controls.Add($discLayout)

    $discButtons = New-Object System.Windows.Forms.FlowLayoutPanel
    $discButtons.Dock = "Fill"
    $discButtons.FlowDirection = "LeftToRight"
    $discButtons.WrapContents = $true
    $discButtons.AutoScroll = $true
    $discButtons.Padding = New-Object System.Windows.Forms.Padding(0, 4, 0, 0)
    $importDiscButton = New-ActionButton "Import Image..." 128 { Import-DiscImage }
    $analyzeDiscButton = New-ActionButton "Analyze Loader Status" 168 { Analyze-ImportedDisc }
    $stoneHillButton = New-ActionButton "Stone Hill Pass" 142 { Analyze-StoneHillLevel }
    $stoneHillWorkbenchButton = New-ActionButton "Load Stone Hill" 138 { if (Prompt-Unsaved) { Import-StoneHillReplica } }
    $stoneHillTargetsButton = New-ActionButton "Show SH Targets" 148 { Show-StoneHillTargetBoard }
    $stoneHillReadinessButton = New-ActionButton "SH Readiness" 132 { Show-StoneHillReplicaReadiness }
    $stoneHillValidationButton = New-ActionButton "SH Playbook" 124 { Show-StoneHillValidationPlaybook }
    $stoneHillIdentityButton = New-ActionButton "SH Identity" 118 { Show-StoneHillIdentityMatrix }
    $stoneHillDossierButton = New-ActionButton "SH Dossier" 118 { Show-StoneHillReplicaDossier }
    $stoneHillMapButton = New-ActionButton "Show SH Map" 122 { Show-StoneHillMobyMap }
    $importRamButton = New-ActionButton "Import RAM Mobys..." 156 { Import-RuntimeMobyDump }
    $importGeometryButton = New-ActionButton "Import Geometry..." 152 { Import-GeometryOverlay }
    $importCandidate4XzButton = New-ActionButton "Import C4 X/Z" 128 { Import-Candidate4XZOverlay }
    $nextGeometryButton = New-ActionButton "Next Geometry" 132 { Cycle-GeometryOverlayCandidate }
    $fitGeometryButton = New-ActionButton "Fit Geometry" 122 { Fit-CanvasToGeometry }
    $remapRuntimeButton = New-ActionButton "Use Moby X/Y" 128 { Remap-RuntimeMobysToTopDownXY }
    $linkWadButton = New-ActionButton "Link WAD..." 112 { Link-RuntimeMobysToWad }
    $queuePatchButton = New-ActionButton "Queue Pos Patch" 138 { Queue-SelectedWadPositionPatch }
    $queueAllCandidatesButton = New-ActionButton "Queue All Candidates" 164 { Queue-SelectedAllCandidatePositionPatches }
    $queueStoneHillSourceButton = New-ActionButton "Queue SH Leads" 134 { Queue-SelectedStoneHillSourceWindowPatches }
    $patchImageButton = New-ActionButton "Create Patched Copy..." 172 { Create-PatchedImage }
    [void]$discButtons.Controls.AddRange(@($importDiscButton, $analyzeDiscButton, $stoneHillButton, $stoneHillWorkbenchButton, $stoneHillTargetsButton, $stoneHillReadinessButton, $stoneHillValidationButton, $stoneHillIdentityButton, $stoneHillDossierButton, $stoneHillMapButton, $importRamButton, $remapRuntimeButton, $importGeometryButton, $importCandidate4XzButton, $nextGeometryButton, $fitGeometryButton, $linkWadButton, $queuePatchButton, $queueAllCandidatesButton, $queueStoneHillSourceButton, $patchImageButton))
    [void]$discLayout.Controls.Add($discButtons, 0, 0)

    $script:DiscInfoBox = New-Object System.Windows.Forms.TextBox
    $script:DiscInfoBox.Dock = "Fill"
    $script:DiscInfoBox.Multiline = $true
    $script:DiscInfoBox.ReadOnly = $true
    $script:DiscInfoBox.ScrollBars = "Vertical"
    $script:DiscInfoBox.WordWrap = $false
    [void]$discLayout.Controls.Add($script:DiscInfoBox, 0, 1)

    $script:DiscFileList = New-Object System.Windows.Forms.ListView
    $script:DiscFileList.Dock = "Fill"
    $script:DiscFileList.View = "Details"
    $script:DiscFileList.FullRowSelect = $true
    [void]$script:DiscFileList.Columns.Add("Path", 300)
    [void]$script:DiscFileList.Columns.Add("Type", 54)
    [void]$script:DiscFileList.Columns.Add("LBA", 70)
    [void]$script:DiscFileList.Columns.Add("Size", 96)
    [void]$discLayout.Controls.Add($script:DiscFileList, 0, 2)

    $script:PatchSummaryBox = New-Object System.Windows.Forms.TextBox
    $script:PatchSummaryBox.Dock = "Fill"
    $script:PatchSummaryBox.Multiline = $true
    $script:PatchSummaryBox.ReadOnly = $true
    $script:PatchSummaryBox.ScrollBars = "Vertical"
    [void]$discLayout.Controls.Add($script:PatchSummaryBox, 0, 3)

    $objectsSplit = New-Object System.Windows.Forms.SplitContainer
    $objectsSplit.Dock = "Fill"
    $objectsSplit.Orientation = "Horizontal"
    $objectsSplit.SplitterDistance = 330
    $objectsTab.Controls.Add($objectsSplit)

    $objectsTop = New-Object System.Windows.Forms.TableLayoutPanel
    $objectsTop.Dock = "Fill"
    $objectsTop.ColumnCount = 1
    $objectsTop.RowCount = 3
    $objectsTop.Padding = New-Object System.Windows.Forms.Padding(8)
    [void]$objectsTop.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 124)))
    [void]$objectsTop.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 38)))
    [void]$objectsTop.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 100)))
    $objectsSplit.Panel1.Controls.Add($objectsTop)

    $script:ObjectSummaryBox = New-Object System.Windows.Forms.TextBox
    $script:ObjectSummaryBox.Dock = "Fill"
    $script:ObjectSummaryBox.Multiline = $true
    $script:ObjectSummaryBox.ReadOnly = $true
    $script:ObjectSummaryBox.ScrollBars = "Vertical"
    $script:ObjectSummaryBox.Text = "No objects loaded."
    [void]$objectsTop.Controls.Add($script:ObjectSummaryBox, 0, 0)

    $targetPickerPanel = New-Object System.Windows.Forms.TableLayoutPanel
    $targetPickerPanel.Dock = "Fill"
    $targetPickerPanel.ColumnCount = 2
    $targetPickerPanel.RowCount = 1
    $targetPickerPanel.Margin = New-Object System.Windows.Forms.Padding(0)
    [void]$targetPickerPanel.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Percent", 100)))
    [void]$targetPickerPanel.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Absolute", 96)))
    $script:StoneHillTargetBox = New-Object System.Windows.Forms.ComboBox
    $script:StoneHillTargetBox.DropDownStyle = "DropDownList"
    $script:StoneHillTargetBox.Dock = "Fill"
    $script:StoneHillTargetBox.Enabled = $false
    $script:StoneHillTargetBox.DropDownWidth = 620
    $script:StoneHillTargetBox.MaxDropDownItems = 20
    $script:StoneHillTargetBox.Margin = New-Object System.Windows.Forms.Padding(0, 3, 8, 3)
    $stoneHillPickButton = New-ActionButton "Go" 82 { Select-StoneHillTargetPickerEntry }
    $stoneHillPickButton.Dock = "Fill"
    $stoneHillPickButton.Margin = New-Object System.Windows.Forms.Padding(0, 3, 0, 3)
    [void]$targetPickerPanel.Controls.Add($script:StoneHillTargetBox, 0, 0)
    [void]$targetPickerPanel.Controls.Add($stoneHillPickButton, 1, 0)
    [void]$objectsTop.Controls.Add($targetPickerPanel, 0, 1)

    $script:EntityList = New-Object System.Windows.Forms.ListView
    $script:EntityList.Dock = "Fill"
    $script:EntityList.View = "Details"
    $script:EntityList.FullRowSelect = $true
    $script:EntityList.MultiSelect = $false
    [void]$script:EntityList.Columns.Add("Name", 180)
    [void]$script:EntityList.Columns.Add("Type", 78)
    [void]$script:EntityList.Columns.Add("Runtime", 122)
    [void]$script:EntityList.Columns.Add("Position", 136)
    [void]$script:EntityList.Columns.Add("Link", 170)
    $script:EntityList.Add_SelectedIndexChanged({
        if ($script:EntityList.SelectedItems.Count -gt 0) {
            $script:SelectedEntityId = [string]$script:EntityList.SelectedItems[0].Tag
            Refresh-Inspector
            Refresh-ObjectSummary
            Refresh-WadCandidateView
            $script:Canvas.Invalidate()
        }
    })
    $script:EntityList.Add_Resize({ Resize-EntityListColumns })
    $script:EntityList.Add_MouseDoubleClick({
        if ($script:EntityList.SelectedItems.Count -gt 0) {
            $script:SelectedEntityId = [string]$script:EntityList.SelectedItems[0].Tag
            Fit-CanvasToEntities
            Refresh-Inspector
            Refresh-ObjectSummary
            Refresh-WadCandidateView
        }
    })
    [void]$objectsTop.Controls.Add($script:EntityList, 0, 2)

    $inspectorArea = New-Object System.Windows.Forms.TableLayoutPanel
    $inspectorArea.Dock = "Fill"
    $inspectorArea.ColumnCount = 1
    $inspectorArea.RowCount = 2
    $inspectorArea.Padding = New-Object System.Windows.Forms.Padding(0)
    [void]$inspectorArea.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 44)))
    [void]$inspectorArea.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 56)))
    $objectsSplit.Panel2.Controls.Add($inspectorArea)

    $inspector = New-Object System.Windows.Forms.TableLayoutPanel
    $inspector.Dock = "Fill"
    $inspector.AutoScroll = $true
    $inspector.ColumnCount = 2
    $inspector.RowCount = 8
    $inspector.Padding = New-Object System.Windows.Forms.Padding(8)
    [void]$inspector.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Absolute", 92)))
    [void]$inspector.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Percent", 100)))
    for ($rowIndex = 0; $rowIndex -lt 6; $rowIndex++) {
        [void]$inspector.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 36)))
    }
    [void]$inspector.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 100)))
    [void]$inspector.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 36)))
    [void]$inspectorArea.Controls.Add($inspector, 0, 0)

    $wadPanel = New-Object System.Windows.Forms.TableLayoutPanel
    $wadPanel.Dock = "Fill"
    $wadPanel.ColumnCount = 1
    $wadPanel.RowCount = 2
    $wadPanel.Padding = New-Object System.Windows.Forms.Padding(8)
    [void]$wadPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Absolute", 24)))
    [void]$wadPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 100)))
    [void]$inspectorArea.Controls.Add($wadPanel, 0, 1)

    $wadLabel = New-FieldLabel "WAD/source candidates"
    [void]$wadPanel.Controls.Add($wadLabel, 0, 0)

    $script:NameBox = New-Object System.Windows.Forms.TextBox
    $script:NameBox.Dock = "Fill"
    $script:NameBox.Add_TextChanged({ if ($script:Refreshing) { return }; $entity = Get-EntityById $script:SelectedEntityId; if ($null -ne $entity) { Set-ObjectField $entity "name" $script:NameBox.Text; Refresh-EntityList; Set-Dirty $true; $script:Canvas.Invalidate() } })
    $script:TypeBox = New-Object System.Windows.Forms.ComboBox
    $script:TypeBox.DropDownStyle = "DropDownList"
    $script:TypeBox.Dock = "Fill"
    [void]$script:TypeBox.Items.AddRange(@("Gem", "Enemy", "Dragon", "Moby"))
    $script:TypeBox.Add_SelectedIndexChanged({ if ($script:Refreshing) { return }; $entity = Get-EntityById $script:SelectedEntityId; if ($null -ne $entity -and $null -ne $script:TypeBox.SelectedItem) { Set-ObjectField $entity "type" $script:TypeBox.SelectedItem.ToString(); Refresh-EntityList; Set-Dirty $true; $script:Canvas.Invalidate() } })
    $script:XBox = New-Object System.Windows.Forms.NumericUpDown
    $script:XBox.Maximum = 99999
    $script:XBox.Minimum = -99999
    $script:XBox.Dock = "Fill"
    $script:XBox.Add_ValueChanged({ if ($script:Refreshing) { return }; $entity = Get-EntityById $script:SelectedEntityId; if ($null -ne $entity) { Set-ObjectField $entity "x" ([int]$script:XBox.Value); Refresh-EntityList; Set-Dirty $true; $script:Canvas.Invalidate() } })
    $script:YBox = New-Object System.Windows.Forms.NumericUpDown
    $script:YBox.Maximum = 99999
    $script:YBox.Minimum = -99999
    $script:YBox.Dock = "Fill"
    $script:YBox.Add_ValueChanged({ if ($script:Refreshing) { return }; $entity = Get-EntityById $script:SelectedEntityId; if ($null -ne $entity) { Set-ObjectField $entity "y" ([int]$script:YBox.Value); Refresh-EntityList; Set-Dirty $true; $script:Canvas.Invalidate() } })
    $script:ZBox = New-Object System.Windows.Forms.NumericUpDown
    $script:ZBox.Maximum = 99999
    $script:ZBox.Minimum = -99999
    $script:ZBox.Dock = "Fill"
    $script:ZBox.Add_ValueChanged({ if ($script:Refreshing) { return }; $entity = Get-EntityById $script:SelectedEntityId; if ($null -ne $entity) { Set-ObjectField $entity "z" ([int]$script:ZBox.Value); Refresh-EntityList; Set-Dirty $true } })
    $script:ColorButton = New-Object System.Windows.Forms.Button
    $script:ColorButton.Text = "Choose"
    $script:ColorButton.Dock = "Fill"
    $script:ColorButton.Add_Click({
        $entity = Get-EntityById $script:SelectedEntityId
        if ($null -ne $entity) {
            $dialog = New-Object System.Windows.Forms.ColorDialog
            $dialog.Color = Convert-HexToColor ([string](Get-ObjectField $entity "color" "#808080"))
            if ($dialog.ShowDialog($script:Form) -eq [System.Windows.Forms.DialogResult]::OK) {
                Set-ObjectField $entity "color" (Convert-ColorToHex $dialog.Color)
                $script:ColorButton.BackColor = $dialog.Color
                Set-Dirty $true
                $script:Canvas.Invalidate()
            }
        }
    })
    $script:NotesBox = New-Object System.Windows.Forms.TextBox
    $script:NotesBox.Multiline = $true
    $script:NotesBox.Dock = "Fill"
    $script:NotesBox.ScrollBars = "Vertical"
    $script:NotesBox.Add_TextChanged({ if ($script:Refreshing) { return }; $entity = Get-EntityById $script:SelectedEntityId; if ($null -ne $entity) { Set-ObjectField $entity "notes" $script:NotesBox.Text; Set-Dirty $true } })
    $script:WadCandidateBox = New-Object System.Windows.Forms.TextBox
    $script:WadCandidateBox.Multiline = $true
    $script:WadCandidateBox.ReadOnly = $true
    $script:WadCandidateBox.ScrollBars = "Both"
    $script:WadCandidateBox.WordWrap = $false
    $script:WadCandidateBox.Dock = "Fill"
    $script:WadCandidateBox.Font = New-Object System.Drawing.Font("Consolas", 8)
    $script:WadCandidateBox.Text = "Select a runtime moby to inspect WAD/source candidates."
    [void]$wadPanel.Controls.Add($script:WadCandidateBox, 0, 1)
    $script:DeleteButton = New-Object System.Windows.Forms.Button
    $script:DeleteButton.Text = "Delete Object"
    $script:DeleteButton.Dock = "Fill"
    $script:DeleteButton.Add_Click({
        $entity = Get-EntityById $script:SelectedEntityId
        if ($null -ne $entity) {
            $script:Project.entities = @($script:Project.entities | Where-Object { $_.id -ne $script:SelectedEntityId })
            $script:SelectedEntityId = $null
            Set-Dirty $true
            Refresh-All
        }
    })

    $fields = @(
        @("Name", $script:NameBox),
        @("Type", $script:TypeBox),
        @("X", $script:XBox),
        @("Y", $script:YBox),
        @("Z", $script:ZBox),
        @("Color", $script:ColorButton),
        @("Notes", $script:NotesBox),
        @("", $script:DeleteButton)
    )
    for ($i = 0; $i -lt $fields.Count; $i++) {
        $label = New-FieldLabel $fields[$i][0]
        [void]$inspector.Controls.Add($label, 0, $i)
        [void]$inspector.Controls.Add($fields[$i][1], 1, $i)
    }

    $script:StatusLabel = New-Object System.Windows.Forms.ToolStripStatusLabel("Ready")
    $status = New-Object System.Windows.Forms.StatusStrip
    [void]$status.Items.Add($script:StatusLabel)
    $script:Form.Controls.Add($status)

    $script:Form.Add_FormClosing({
        param($sender, $event)
        if (-not (Prompt-Unsaved)) {
            $event.Cancel = $true
        }
    })

    Refresh-All
}

Build-App

if ($SmokeTest) {
    Write-Host "Smoke test passed: form, controls, and default project initialized."
    exit 0
}

if ($SmokeTestStoneHillWorkbench) {
    Import-StoneHillReplica -Quiet
    $entities = @(Get-ObjectFieldArray $script:Project "entities")
    $overlay = Get-ObjectField $script:Project "geometryOverlay" $null
    $candidates = @(Get-ObjectFieldArray $overlay "candidates")
    Write-Host "Stone Hill workbench smoke test passed: entities=$($entities.Count), geometryCandidates=$($candidates.Count), level=$(Get-ObjectField $script:Project 'levelName' '')"
    exit 0
}

[void][System.Windows.Forms.Application]::EnableVisualStyles()
[System.Windows.Forms.Application]::Run($script:Form)
