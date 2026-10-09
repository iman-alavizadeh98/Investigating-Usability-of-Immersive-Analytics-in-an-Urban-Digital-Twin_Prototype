# Draws the pictures of the VR Help brochure (XRHelpPanel) and the tutorial
# cards (XRTutorial): six schematic 800 x 500 px images, few words each.
#
#   help_controllers.png  what each controller input does
#   help_toolbar.png      the table toolbar (DATA / CLICK / TABLE rows)
#   help_tools.png        click tools: Select, Copy, Compare
#   help_panels.png       top view: table, toolbar, Info, Legend, Compare
#   help_wrist.png        look at the left wrist: Views / Task / Panels
#   help_table.png        move, turn and resize the table
#
# Output: Assets/Textures/UrbanAnalytics/VR/help_*.png (opaque, background =
# RuntimeUi.PanelColor). Re-run after changing the VR controls or the toolbar
# and rebuild the VR scene (UrbanAnalytics > VR > Build VR Scene), which
# assigns the pictures by file name (XRHelpPanel.DefaultCards).
#
# Run (Windows PowerShell 5.1, uses System.Drawing):
#   powershell -ExecutionPolicy Bypass -File Unity/City_Digital_Twin/Tools/make_vr_help_images.ps1

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $projectRoot "Assets\Textures\UrbanAnalytics\VR"
New-Item -ItemType Directory -Force $outDir | Out-Null

$W = 800
$H = 500

function Rgb([int]$r, [int]$gr, [int]$b) { [System.Drawing.Color]::FromArgb(255, $r, $gr, $b) }

# Colours (RuntimeUi): panel, text, muted, accent, button; plus a few for drawings.
$cPanel  = Rgb 18 20 26
$cText   = Rgb 242 245 247
$cMuted  = Rgb 168 176 189
$cAccent = Rgb 255 212 0
$cButton = Rgb 54 59 69
$cActive = Rgb 148 120 13
$cTable  = Rgb 92 74 58
$cCity   = Rgb 150 160 175
$cBlue   = Rgb 64 160 255
$cPink   = Rgb 255 115 204
$cGreen  = Rgb 120 220 120

# Arrows built from code points (Windows PowerShell reads BOM-less scripts as ANSI).
$ArrowL = [string][char]0x2190
$ArrowR = [string][char]0x2192
$ArrowU = [string][char]0x2191
$ArrowD = [string][char]0x2193

function NewFont([float]$size, [string]$style = "Regular") {
    New-Object System.Drawing.Font("Segoe UI", $size, [System.Drawing.FontStyle]::$style, [System.Drawing.GraphicsUnit]::Pixel)
}

$fTitle = NewFont 34 "Bold"
$fBig   = NewFont 30 "Bold"
$fBody  = NewFont 26
$fBold  = NewFont 26 "Bold"
$fSmall = NewFont 21
$fSmallB = NewFont 21 "Bold"

function Brush($c) { New-Object System.Drawing.SolidBrush($c) }
function Pen($c, [float]$w) { New-Object System.Drawing.Pen($c, $w) }

$center = New-Object System.Drawing.StringFormat
$center.Alignment = [System.Drawing.StringAlignment]::Center
$center.LineAlignment = [System.Drawing.StringAlignment]::Center

$left = New-Object System.Drawing.StringFormat
$left.Alignment = [System.Drawing.StringAlignment]::Near
$left.LineAlignment = [System.Drawing.StringAlignment]::Center

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2 * $r, 2 * $r, 180, 90)
    $p.AddArc($x + $w - 2 * $r, $y, 2 * $r, 2 * $r, 270, 90)
    $p.AddArc($x + $w - 2 * $r, $y + $h - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $p.AddArc($x, $y + $h - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $p.CloseFigure()
    return $p
}

function Begin() {
    $script:bmp = New-Object System.Drawing.Bitmap($W, $H)
    $script:g = [System.Drawing.Graphics]::FromImage($script:bmp)
    $script:g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $script:g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $script:g.Clear($cPanel)
}

function Finish([string]$name) {
    $path = Join-Path $outDir ($name + ".png")
    $script:bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $script:g.Dispose()
    $script:bmp.Dispose()
    Write-Output "Wrote $path"
}

function Text([string]$s, $font, $color, [float]$x, [float]$y, [float]$w, [float]$h, $fmt = $left) {
    $g.DrawString($s, $font, (Brush $color), (New-Object System.Drawing.RectangleF($x, $y, $w, $h)), $fmt)
}

function Box([float]$x, [float]$y, [float]$w, [float]$h, $fill, [string]$label = "", $font = $fSmallB, $textColor = $cText, [float]$r = 10) {
    $p = RoundRect $x $y $w $h $r
    $g.FillPath((Brush $fill), $p)
    if ($label -ne "") { Text $label $font $textColor $x $y $w $h $center }
}

function Arrow([float]$x1, [float]$y1, [float]$x2, [float]$y2, $color, [float]$width = 5) {
    $pen = Pen $color $width
    $pen.CustomEndCap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap(4, 4)
    $g.DrawLine($pen, $x1, $y1, $x2, $y2)
}

# A building block (isometric-ish box) at x, y (bottom centre).
function Building([float]$x, [float]$y, [float]$w, [float]$h, $color) {
    $g.FillRectangle((Brush $color), ($x - $w / 2), ($y - $h), $w, $h)
    $top = New-Object 'System.Drawing.PointF[]' 4
    $top[0] = New-Object System.Drawing.PointF(($x - $w / 2), ($y - $h))
    $top[1] = New-Object System.Drawing.PointF(($x - $w / 2 + 14), ($y - $h - 10))
    $top[2] = New-Object System.Drawing.PointF(($x + $w / 2 + 14), ($y - $h - 10))
    $top[3] = New-Object System.Drawing.PointF(($x + $w / 2), ($y - $h))
    $lighter = [System.Drawing.Color]::FromArgb(255, [Math]::Min(255, $color.R + 40), [Math]::Min(255, $color.G + 40), [Math]::Min(255, $color.B + 40))
    $g.FillPolygon((Brush $lighter), $top)
}

# A controller seen from above: ring + handle.
function Controller([float]$cx, [float]$cy, [string]$name) {
    $g.FillRectangle((Brush (Rgb 200 204 212)), ($cx - 22), ($cy + 30), 44, 80)
    $g.FillEllipse((Brush (Rgb 200 204 212)), ($cx - 60), ($cy - 60), 120, 120)
    $g.FillEllipse((Brush $cPanel), ($cx - 34), ($cy - 34), 68, 68)
    Text $name $fBold $cText ($cx - 80) ($cy + 118) 160 34 $center
}

# ---------------------------------------------------------------- controllers
Begin
Text "Controllers" $fTitle $cAccent 30 18 740 46
Controller 200 150 "Left"
Controller 600 150 "Right"
$rows = @(
    @(40, 316, "Grip: move the table"),
    @(40, 352, "Stick: turn the table"),
    @(40, 388, "Y: Info panel  X: pin menu"),
    @(440, 316, "Trigger: click"),
    @(440, 352, "Grip: grab a copy / panel"),
    @(440, 388, "A: copy   B: unselect"),
    @(440, 424, ("Stick " + $ArrowU + $ArrowD + ": table size"))
)
foreach ($r in $rows) { Text $r[2] $fSmall $cText $r[0] $r[1] 360 34 }
Finish "help_controllers"

# ---------------------------------------------------------------- toolbar
Begin
Text "Table toolbar" $fTitle $cAccent 30 18 740 46
Box 30 80 740 340 (Rgb 30 33 40) "" $fSmallB $cText 14
$rowY = @(104, 214, 324)
$labels = @("DATA", "CLICK", "TABLE")
$buttons = @(
    @(@("< View", 95), @("view name", 190), @("View >", 95), @("Clear table", 150)),
    @(@("Select", 95), @("Copy", 90), @("Compare", 115), @("Remove copies", 160)),
    @(@("Smaller", 105), @("Bigger", 95), @("Turn", 85), @("Bring here", 140))
)
for ($i = 0; $i -lt 3; $i++) {
    Text $labels[$i] $fSmallB $cAccent 46 $rowY[$i] 100 72
    $x = 150
    foreach ($b in $buttons[$i]) {
        $fill = $cButton
        if ($b[0] -eq "view name") { $fill = (Rgb 30 33 40) }
        if ($b[0] -eq "Select") { $fill = $cActive }
        Box $x $rowY[$i] $b[1] 72 $fill $b[0]
        $x += $b[1] + 10
    }
}
Text "Every button changes the table." $fBody $cMuted 30 440 740 40 $center
Finish "help_toolbar"

# ---------------------------------------------------------------- click tools
Begin
Text "Click tools" $fTitle $cAccent 30 18 740 46
$cols = @(140, 400, 660)
# Select: building with highlight + pointer ray.
Building 140 330 70 120 $cCity
$g.DrawRectangle((Pen $cAccent 5), 101, 196, 78, 138)
Arrow 40 120 120 230 $cAccent 4
# Copy: building with a copy above it.
Building 400 360 70 90 $cCity
$g.DrawLine((Pen $cBlue 3), 400, 270, 400, 230)
Building 400 230 70 90 $cBlue
# Compare: two areas -> A | B
Box 590 270 60 60 $cPink "A" $fBig
Box 680 270 60 60 $cGreen "B" $fBig $cPanel
Box 600 150 130 80 (Rgb 30 33 40) "A | B" $fBold
Arrow 665 268 665 236 $cText 3
$names = @("Select", "Copy", "Compare")
$texts = @("click: see its info", "click: it pops up", "click two: side by side")
for ($i = 0; $i -lt 3; $i++) {
    Text $names[$i] $fBig $cText ($cols[$i] - 130) 380 260 40 $center
    Text $texts[$i] $fSmall $cMuted ($cols[$i] - 130) 420 260 34 $center
}
Finish "help_tools"

# ---------------------------------------------------------------- panels (top view)
Begin
Text "Panels" $fTitle $cAccent 30 18 740 46
# Table.
Box 230 110 340 220 $cTable "" $fSmallB $cText 8
Box 250 128 300 184 (Rgb 120 104 88) "city" $fBold (Rgb 230 225 215) 4
# Legend at the far edge.
Box 300 74 200 28 $cAccent "Legend" $fSmallB $cPanel 6
# Toolbar at the near edge.
Box 270 340 260 28 $cButton "Toolbar" $fSmallB
# User.
$g.FillEllipse((Brush $cText), 384, 388, 32, 32)
Text "you" $fSmall $cMuted 360 422 80 30 $center
# Info right, Compare left (angled toward the user).
Box 560 340 150 70 $cBlue "Info" $fBold $cPanel
Box 90 340 150 70 $cPink "Compare" $fBold $cPanel
Text "Grab bar: move any panel" $fSmall $cMuted 30 450 740 34 $center
Finish "help_panels"

# ---------------------------------------------------------------- wrist menu
Begin
Text "Wrist menu" $fTitle $cAccent 30 18 740 46
# Forearm and hand.
$arm = RoundRect 80 300 360 70 30
$g.FillPath((Brush (Rgb 200 170 150)), $arm)
$g.FillEllipse((Brush (Rgb 200 170 150)), 410, 285, 110, 100)
# Menu above the wrist.
Box 200 90 300 190 (Rgb 30 33 40) "" $fSmallB $cText 12
$tabs = @("Views", "Task", "Panels")
for ($i = 0; $i -lt 3; $i++) { Box (214 + $i * 96) 106 88 44 $cButton $tabs[$i] $fSmallB }
Box 214 164 272 30 $cButton ""
Box 214 204 272 30 $cButton ""
Box 214 244 272 26 $cButton ""
# Eye looking at it.
$g.FillEllipse((Brush $cText), 620, 120, 90, 54)
$g.FillEllipse((Brush $cPanel), 648, 130, 34, 34)
Arrow 610 150 510 170 $cAccent 4
Text "Look at your left wrist" $fBody $cText 30 410 740 40 $center
Finish "help_wrist"

# ---------------------------------------------------------------- table
Begin
Text "Move the table" $fTitle $cAccent 30 18 740 46
Box 220 160 360 200 $cTable "" $fSmallB $cText 8
Box 240 178 320 164 (Rgb 120 104 88) "" $fSmallB $cText 4
Arrow 400 260 560 260 $cAccent 6
Arrow 400 260 240 260 $cAccent 6
Arrow 400 260 400 130 $cAccent 6
# Turn arc.
$g.DrawArc((Pen $cAccent 5), 600, 150, 120, 120, 200, 250)
Text "Left grip: move + turn" $fBody $cText 30 400 740 36 $center
Text ("Right stick " + $ArrowU + " " + $ArrowD + ": bigger / smaller") $fBody $cMuted 30 440 740 36 $center
Finish "help_table"
