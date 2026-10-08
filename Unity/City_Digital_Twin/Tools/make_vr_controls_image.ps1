# Draws the VR controls diagram shown on the wrist menu's Help tab
# (Meta Quest 3 Touch Plus controllers, schematic, labelled with what each
# input does in the UrbanAnalytics VR tabletop mode).
#
# Output: Assets/Textures/UrbanAnalytics/VR/vr_controls.png (1000 x 1240 px,
# opaque, background = RuntimeUi.PanelColor). Re-run after changing the VR
# controls (XRControllerShortcuts, XRTableMover, XRHandMenu, XRControllerPointer)
# and keep it in sync with XRHandMenu.ControlsText.
#
# Run (Windows PowerShell 5.1, uses System.Drawing):
#   powershell -ExecutionPolicy Bypass -File Unity/City_Digital_Twin/Tools/make_vr_controls_image.ps1

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $projectRoot "Assets\Textures\UrbanAnalytics\VR"
$outPath = Join-Path $outDir "vr_controls.png"
New-Item -ItemType Directory -Force $outDir | Out-Null

$W = 1000
$H = 1240

$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

function Rgb([int]$r, [int]$gr, [int]$b) { [System.Drawing.Color]::FromArgb(255, $r, $gr, $b) }

# Colours (RuntimeUi): panel, text, muted, accent.
$cPanel  = Rgb 18 20 26
$cText   = Rgb 242 245 247
$cMuted  = Rgb 168 176 189
$cAccent = Rgb 255 212 0
$cBody   = Rgb 200 204 212
$cBodyDk = Rgb 120 126 138
$cButton = Rgb 64 70 82

$g.Clear($cPanel)

# Arrows built from code points (Windows PowerShell reads BOM-less scripts as ANSI).
$LR = "$([char]0x2190) $([char]0x2192)"
$UD = "$([char]0x2191) $([char]0x2193)"

$fSection = New-Object System.Drawing.Font("Segoe UI", 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fTitle   = New-Object System.Drawing.Font("Segoe UI", 27, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fAction  = New-Object System.Drawing.Font("Segoe UI", 25, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fButton  = New-Object System.Drawing.Font("Segoe UI", 24, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fNote    = New-Object System.Drawing.Font("Segoe UI", 24, [System.Drawing.FontStyle]::Italic, [System.Drawing.GraphicsUnit]::Pixel)

$bText   = New-Object System.Drawing.SolidBrush($cText)
$bMuted  = New-Object System.Drawing.SolidBrush($cMuted)
$bAccent = New-Object System.Drawing.SolidBrush($cAccent)
$bBody   = New-Object System.Drawing.SolidBrush($cBody)
$bButton = New-Object System.Drawing.SolidBrush($cButton)
$bPanel  = New-Object System.Drawing.SolidBrush($cPanel)

$pOutline = New-Object System.Drawing.Pen($cBodyDk, 3)
$pLeader  = New-Object System.Drawing.Pen($cAccent, 2.5)
$pDashed  = New-Object System.Drawing.Pen($cAccent, 4)
$pDashed.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash

$center = New-Object System.Drawing.StringFormat
$center.Alignment = [System.Drawing.StringAlignment]::Center
$center.LineAlignment = [System.Drawing.StringAlignment]::Center

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2 * $r, 2 * $r, 180, 90)
    $p.AddArc($x + $w - 2 * $r, $y, 2 * $r, 2 * $r, 270, 90)
    $p.AddArc($x + $w - 2 * $r, $y + $h - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $p.AddArc($x, $y + $h - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $p.CloseFigure()
    return $p
}

function Circle($brush, $pen, [float]$cx, [float]$cy, [float]$r) {
    if ($brush) { $g.FillEllipse($brush, $cx - $r, $cy - $r, 2 * $r, 2 * $r) }
    if ($pen) { $g.DrawEllipse($pen, $cx - $r, $cy - $r, 2 * $r, 2 * $r) }
}

# A label: bold title + action text, left or right column, with a leader
# line from the control point (px, py) to the label.
function Label([string]$side, [float]$y, [string]$title, [string]$action, [float]$px, [float]$py) {
    $colW = 330
    if ($side -eq "left") { $x = 24; $anchorX = $x + $colW + 6 } else { $x = $W - 24 - $colW; $anchorX = $x - 6 }
    $titleRect = New-Object System.Drawing.RectangleF($x, $y, $colW, 34)
    $actionRect = New-Object System.Drawing.RectangleF($x, ($y + 34), $colW, 70)
    $fmt = New-Object System.Drawing.StringFormat
    if ($side -eq "left") { $fmt.Alignment = [System.Drawing.StringAlignment]::Far } else { $fmt.Alignment = [System.Drawing.StringAlignment]::Near }
    $g.DrawString($title, $fTitle, $bAccent, $titleRect, $fmt)
    $g.DrawString($action, $fAction, $bText, $actionRect, $fmt)
    $g.DrawLine($pLeader, $anchorX, ($y + 18), $px, $py)
    Circle $bAccent $null $px $py 5
}

# One controller. $mirror = 1 for the left controller, -1 for the right
# (the inner side, with the face buttons and the grip, faces the other hand).
function Controller([float]$y0, [int]$mirror, [string]$name, [string]$b1, [string]$b2, [string]$small) {
    $cx = 500

    $g.DrawString($name, $fSection, $bText, 24, $y0)

    # Handle (below the face, drawn first), face plate.
    $handle = RoundRect ($cx - 52) ($y0 + 290) 104 230 46
    $g.FillPath($bBody, $handle)
    $g.DrawPath($pOutline, $handle)
    Circle $bBody $pOutline $cx ($y0 + 230) 120

    # Trigger: on the front, under the index finger (drawn above the face).
    $g.DrawArc($pDashed, ($cx - 70), ($y0 + 88), 140, 60, 200, 140)

    # Grip: on the inner side of the handle, under the middle finger.
    $gx = if ($mirror -eq 1) { $cx + 56 } else { $cx - 56 - 26 }
    $grip = RoundRect $gx ($y0 + 340) 26 120 12
    $g.DrawPath($pDashed, $grip)

    # Thumbstick (outer side), two face buttons and the small button (inner side).
    $sx = $cx - 46 * $mirror
    Circle $bButton $pOutline $sx ($y0 + 218) 40
    Circle $bBodyDk $null $sx ($y0 + 218) 24

    $b1x = $cx + 50 * $mirror; $b1y = $y0 + 178
    $b2x = $cx + 62 * $mirror; $b2y = $y0 + 240
    Circle $bButton $pOutline $b1x $b1y 24
    Circle $bButton $pOutline $b2x $b2y 24
    $g.DrawString($b1, $fButton, $bText, $b1x, ($b1y + 1), $center)
    $g.DrawString($b2, $fButton, $bText, $b2x, ($b2y + 1), $center)

    $mx = $cx + 30 * $mirror; $my = $y0 + 294
    Circle $bButton $pOutline $mx $my 12
    $g.DrawString($small, $fAction, $bMuted, ($mx + 26 * $mirror - 8), ($my - 16))

    return @{ Stick = @($sx, ($y0 + 218)); B1 = @($b1x, $b1y); B2 = @($b2x, $b2y); Small = @($mx, $my); Trigger = @($cx, ($y0 + 92)); Grip = @(($gx + 13), ($y0 + 400)) }
}

# ----- left controller -----
$y0 = 16
$c = Controller $y0 1 "LEFT CONTROLLER" "Y" "X" ""
Label "left"  ($y0 + 60)  "TRIGGER"              "Point with the left hand"           $c.Trigger[0] $c.Trigger[1]
Label "left"  ($y0 + 180) "THUMBSTICK  $LR"     "Turn the table"                     ($c.Stick[0] - 40) $c.Stick[1]
Label "left"  ($y0 + 290) "THUMBSTICK  press"    "Bring the table to you"             ($c.Stick[0] - 20) ($c.Stick[1] + 30)
Label "right" ($y0 + 110) "Y"                    "Show / hide the board"              ($c.B1[0] + 24) $c.B1[1]
Label "right" ($y0 + 220) "X"                    "Pin the menu / back to the wrist"   ($c.B2[0] + 24) $c.B2[1]
Label "right" ($y0 + 370) "GRIP  (hold)"         "Grab: move and turn the table"      ($c.Grip[0] + 13) $c.Grip[1]
$g.DrawString("Look at your left wrist to open the menu", $fNote, $bMuted, (New-Object System.Drawing.RectangleF(24, ($y0 + 540), ($W - 48), 40)), $center)

# Divider
$g.DrawLine((New-Object System.Drawing.Pen($cButton, 2)), 24, 612, ($W - 24), 612)

# ----- right controller -----
$y0 = 628
$c = Controller $y0 (-1) "RIGHT CONTROLLER" "B" "A" ""
Label "right" ($y0 + 60)  "TRIGGER"              "Select  (empty table: clear)"       $c.Trigger[0] $c.Trigger[1]
Label "right" ($y0 + 180) "THUMBSTICK  $LR"     "Previous / next view"               ($c.Stick[0] + 40) $c.Stick[1]
Label "right" ($y0 + 290) "THUMBSTICK  $UD" "Bigger / smaller table"            ($c.Stick[0] + 20) ($c.Stick[1] + 30)
Label "left"  ($y0 + 110) "B"                    "Clear the selection"                ($c.B1[0] - 24) $c.B1[1]
Label "left"  ($y0 + 220) "A"                    "Copy to compare (A, then B)"        ($c.B2[0] - 24) $c.B2[1]
Label "left"  ($y0 + 370) "GRIP + TRIGGER"       "Select the area of a building"      ($c.Grip[0] - 13) $c.Grip[1]
$g.DrawString("Meta button (hold): recentre the view", $fNote, $bMuted, (New-Object System.Drawing.RectangleF(24, ($y0 + 540), ($W - 48), 40)), $center)

$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Output "Wrote $outPath"
