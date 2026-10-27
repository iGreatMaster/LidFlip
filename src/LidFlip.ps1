Add-Type -AssemblyName System.Windows.Forms, System.Drawing

# ---- single instance: a new launch tells the running copy to quit, then takes over ----
# (so re-running after an update always loads the new version, and you never get two icons)
$script:quitEvt = New-Object System.Threading.EventWaitHandle($false, [System.Threading.EventResetMode]::AutoReset, 'Local\LidFlip_Quit')
$script:mutex   = New-Object System.Threading.Mutex($false, 'Local\LidFlip_Single')
function Acquire-Mutex([int]$ms) {
    try { $script:mutex.WaitOne($ms) }
    catch [System.Threading.AbandonedMutexException] { $true }   # previous copy was killed
}
if (-not (Acquire-Mutex 0)) {
    [void]$script:quitEvt.Set()                                  # ask the running copy to exit
    if (-not (Acquire-Mutex 8000)) { exit }                      # it didn't let go: leave it running
}

$script:LogFile = Join-Path $PSScriptRoot 'LidFlip.log'

function Show-Error([string]$msg) {
    Add-Content -Path $script:LogFile -Value ("{0}  {1}" -f (Get-Date -Format s), $msg)
    [System.Windows.Forms.MessageBox]::Show($msg, 'LidFlip - error') | Out-Null
}

function Get-Lid {
    $out  = & powercfg.exe /qh SCHEME_CURRENT SUB_BUTTONS LIDACTION 2>&1 | Out-String
    $hex  = [regex]::Matches($out, '0x[0-9a-fA-F]{8}')
    if ($hex.Count -lt 1) { throw "Could not read lid setting. powercfg said:`n$out" }
    [Convert]::ToInt32($hex[0].Value, 16)   # first 0x value = AC (plugged in)
}

function Set-Lid([int]$n) {
    foreach ($a in @(
        @('/setacvalueindex', 'SCHEME_CURRENT', 'SUB_BUTTONS', 'LIDACTION', "$n"),
        @('/setdcvalueindex', 'SCHEME_CURRENT', 'SUB_BUTTONS', 'LIDACTION', "$n"),
        @('/setactive',       'SCHEME_CURRENT'))) {
        $o = & powercfg.exe @a 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "powercfg $($a -join ' ') failed:`n$o" }
    }
    if ((Get-Lid) -ne $n) { throw "Setting did not stick (still $(Get-Lid)). Power plan may be locked by policy." }
}

function Toggle-Lid {
    $ok = $false
    try {
        if ((Get-Lid) -eq 0) { Set-Lid 1 } else { Set-Lid 0 }
        $ok = $true
    } catch { Show-Error $_.Exception.Message }
    if ($ok) { Update-Tray -Animate } else { Update-Tray }
}

# ================= ICON RENDERING =================
# Everything is drawn at 256px and downscaled to 32px for crisp, smooth edges.
$script:S = 256
$script:LidRest = 24   # resting lid angle: half-shut, so it reads as "closing the lid"

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

# Awake = emerald tile + lightning bolt.  Sleep = indigo "night" tile + crescent moon.
$script:Pal = @{
    awake = @{ top = (C '#34D399'); bot = (C '#047857') }
    sleep = @{ top = (C '#818CF8'); bot = (C '#3730A3') }
}

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function New-Gfx($bmp) {
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = 'AntiAlias'
    $g.InterpolationMode  = 'HighQualityBicubic'
    $g.PixelOffsetMode    = 'HighQuality'
    $g.CompositingQuality = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g
}

# Glyph = laptop seen from the side (base + hinged lid) and a badge saying what
# happens when the lid shuts:  moon = sleeps,  lightning bolt = stays awake.
#   $lid    lid angle in degrees (0 = shut, 90 = upright)
#   $badge  'sleep' | 'awake';  $bs / $br = badge scale / rotation (for the animation)
function Draw-Glyph($g, [double]$lid, [string]$badge, [double]$bs, [double]$br) {
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

    # base: solid slab with a little front notch (reads as a laptop even when shut)
    $g.FillPath($white, (New-RoundRect 36 178 184 26 12))
    $notch = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 0, 0, 0))
    $g.FillPath($notch, (New-RoundRect 108 192 40 8 4))

    # lid: thinner, hinged at the back-left, rotates up
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 18
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $a = $lid * [math]::PI / 180
    $hx = 50; $hy = 166; $len = 116
    $g.DrawLine($pen, $hx, $hy, [float]($hx + $len * [math]::Cos($a)), [float]($hy - $len * [math]::Sin($a)))

    if ($bs -gt 0.02) {
        $state = $g.Save()
        $g.TranslateTransform(182, 68)
        $g.RotateTransform([float]$br)
        $g.ScaleTransform([float]($bs * 0.85), [float]($bs * 0.85))
        if ($badge -eq 'sleep') {
            $outer = New-Object System.Drawing.Drawing2D.GraphicsPath
            $outer.AddEllipse(-42, -42, 84, 84)
            $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
            $inner.AddEllipse(-20, -60, 80, 80)
            $moon = New-Object System.Drawing.Region $outer
            $moon.Exclude($inner)
            $g.FillRegion($white, $moon)                       # crescent moon = sleeps
        } else {
            $pts = [System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 10, -48), (New-Object System.Drawing.PointF -28, 8),
                (New-Object System.Drawing.PointF -4, 8),   (New-Object System.Drawing.PointF -12, 48),
                (New-Object System.Drawing.PointF 28, -10), (New-Object System.Drawing.PointF 4, -10))
            $g.FillPolygon($white, $pts)                       # lightning bolt = stays awake
        }
        $g.Restore($state)
    }
}

function Mix([System.Drawing.Color]$a, [System.Drawing.Color]$b, [double]$t) {
    [System.Drawing.Color]::FromArgb(255,
        [int]($a.R + ($b.R - $a.R) * $t),
        [int]($a.G + ($b.G - $a.G) * $t),
        [int]($a.B + ($b.B - $a.B) * $t))
}

function Ease-InOut([double]$t) { if ($t -lt 0.5) { 4 * $t * $t * $t } else { 1 - [math]::Pow(-2 * $t + 2, 3) / 2 } }
function Ease-Back([double]$t)  { $c1 = 1.9; $c3 = $c1 + 1; 1 + $c3 * [math]::Pow($t - 1, 3) + $c1 * [math]::Pow($t - 1, 2) }

# One frame: gradient tile + glossy highlight + glyph (scaled/rotated) + optional ripple
function New-Frame($top, $bot, [double]$tileScale, [double]$lid, [string]$badge, [double]$bs, [double]$br, [double]$ripple) {
    $S = $script:S
    $b = New-Object System.Drawing.Bitmap $S, $S
    $g = New-Gfx $b

    $size = 236 * $tileScale
    $off  = ($S - $size) / 2
    $tile = New-RoundRect $off $off $size $size (70 * $tileScale)
    $rect = New-Object System.Drawing.RectangleF $off, $off, $size, $size
    $g.FillPath((New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bot, 90.0), $tile)

    # glass highlight on the upper half
    $hiRect = New-Object System.Drawing.RectangleF $off, $off, $size, ($size / 2)
    $hi = New-Object System.Drawing.Drawing2D.LinearGradientBrush $hiRect,
            ([System.Drawing.Color]::FromArgb(70, 255, 255, 255)),
            ([System.Drawing.Color]::FromArgb(0, 255, 255, 255)), 90.0
    $g.SetClip($tile)
    $g.FillRectangle($hi, $hiRect)

    # expanding light ripple inside the tile
    if ($ripple -gt 0 -and $ripple -lt 1) {
        $rr = 20 + 200 * $ripple
        $alpha = [int](150 * (1 - $ripple))
        $rp = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255)), (26 * (1 - $ripple) + 4)
        $g.DrawEllipse($rp, 128 - $rr, 128 - $rr, 2 * $rr, 2 * $rr)
    }
    $g.ResetClip()

    Draw-Glyph $g $lid $badge $bs $br
    $g.Dispose()

    # downscale to tray size
    $out = New-Object System.Drawing.Bitmap 32, 32
    $g2 = New-Gfx $out
    $g2.DrawImage($b, 0, 0, 32, 32)
    $g2.Dispose(); $b.Dispose()
    [System.Drawing.Icon]::FromHandle($out.GetHicon())
}

# Transition: the lid swings open and slams shut, the tile squishes and recolours,
# the old badge spins away, the new one pops in with an overshoot, and a light ripple fires.
function New-Transition([string]$from, [string]$to) {
    $N = 22
    $frames = New-Object System.Collections.Generic.List[System.Drawing.Icon]
    $dir = if ($to -eq 'awake') { 1 } else { -1 }
    for ($i = 1; $i -le $N; $i++) {
        $t  = $i / $N
        $c  = Ease-InOut $t
        $top = Mix $script:Pal[$from].top $script:Pal[$to].top $c
        $bot = Mix $script:Pal[$from].bot $script:Pal[$to].bot $c
        $tileScale = 1 - 0.10 * [math]::Sin([math]::PI * $t)
        # lid: swings from its resting angle up to ~70 deg and back, with a small bounce at the end
        $lid = if ($t -lt 0.8) { $script:LidRest + 46 * [math]::Sin([math]::PI * $t / 0.8) } else { $script:LidRest + 6 * [math]::Sin([math]::PI * ($t - 0.8) / 0.2) }
        if ($t -lt 0.4) {
            $p = $t / 0.4
            $badge = $from; $bs = 1 - (Ease-InOut $p); $br = -160 * $p * $dir
        } else {
            $p = ($t - 0.4) / 0.6
            $badge = $to;   $bs = Ease-Back $p;        $br = 160 * (1 - $p) * $dir
        }
        $ripple = if ($t -gt 0.7) { ($t - 0.7) / 0.3 } else { 0 }
        $frames.Add((New-Frame $top $bot $tileScale $lid $badge $bs $br $ripple))
    }
    , $frames.ToArray()
}

$script:iconAwake = New-Frame $script:Pal.awake.top $script:Pal.awake.bot 1 $script:LidRest 'awake' 1 0 0
$script:iconSleep = New-Frame $script:Pal.sleep.top $script:Pal.sleep.bot 1 $script:LidRest 'sleep' 1 0 0
$script:toAwake   = New-Transition 'sleep' 'awake'
$script:toSleep   = New-Transition 'awake' 'sleep'

$script:tray = New-Object System.Windows.Forms.NotifyIcon

# frame player
$script:anim = @(); $script:animIdx = 0
$script:animTimer = New-Object System.Windows.Forms.Timer
$script:animTimer.Interval = 22
$script:animTimer.add_Tick({
    if ($script:animIdx -lt $script:anim.Count) {
        $script:tray.Icon = $script:anim[$script:animIdx]
        $script:animIdx++
    } else { $script:animTimer.Stop() }
})

function Play-Anim($frames) {
    $script:animTimer.Stop()
    $script:anim = $frames; $script:animIdx = 0
    $script:animTimer.Start()
}

function Update-Tray([switch]$Animate) {
    try { $v = Get-Lid } catch { $v = -1 }
    if ($v -eq 0) {
        $script:tray.Text = 'Lid close: STAY AWAKE (click to switch)'
        if ($Animate) { Play-Anim $script:toAwake } else { $script:tray.Icon = $script:iconAwake }
    } else {
        $script:tray.Text = 'Lid close: SLEEP (click to switch)'
        if ($Animate) { Play-Anim $script:toSleep } else { $script:tray.Icon = $script:iconSleep }
    }
}

$script:startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'LidFlip.lnk'
$script:vbsPath    = Join-Path $PSScriptRoot 'LidFlip.vbs'

function Toggle-Startup {
    try {
        if (Test-Path $script:startupLnk) {
            Remove-Item $script:startupLnk
        } else {
            $ws  = New-Object -ComObject WScript.Shell
            $lnk = $ws.CreateShortcut($script:startupLnk)
            $lnk.TargetPath       = 'wscript.exe'
            $lnk.Arguments        = '"' + $script:vbsPath + '"'
            $lnk.WorkingDirectory = $PSScriptRoot
            $lnk.Save()
        }
    } catch { Show-Error $_.Exception.Message }
}

function Get-TrayKeys {
    $root = 'HKCU:\Control Panel\NotifyIconSettings'
    if (-not (Test-Path $root)) { return @() }
    @(Get-ChildItem $root | Where-Object {
        "$((Get-ItemProperty $_.PSPath).ExecutablePath)" -match 'powershell\.exe$'
    })
}

function Get-Pinned {
    foreach ($k in Get-TrayKeys) {
        if ((Get-ItemProperty $k.PSPath).IsPromoted -eq 1) { return $true }
    }
    $false
}

# Remembered choice (survives restarts even if Windows re-creates the tray entry)
$script:PrefKey = 'HKCU:\Software\LidFlip'

function Get-PinPref {
    try { (Get-ItemProperty $script:PrefKey -Name Pinned -ErrorAction Stop).Pinned -eq 1 }
    catch { Get-Pinned }   # first run: fall back to current Windows state
}

function Set-PinPref([int]$v) {
    if (-not (Test-Path $script:PrefKey)) { New-Item $script:PrefKey -Force | Out-Null }
    Set-ItemProperty $script:PrefKey -Name Pinned -Value $v -Type DWord
}

# Apply remembered choice to every PowerShell tray entry; returns $true if anything changed
function Apply-Pin([int]$v) {
    $changed = $false
    foreach ($k in Get-TrayKeys) {
        if ((Get-ItemProperty $k.PSPath).IsPromoted -ne $v) {
            Set-ItemProperty $k.PSPath -Name IsPromoted -Value $v -Type DWord
            $changed = $true
        }
    }
    if ($changed) {
        # re-register the icon so Explorer picks up the change
        $script:tray.Visible = $false
        Start-Sleep -Milliseconds 300
        $script:tray.Visible = $true
    }
    $changed
}

function Pin-Tray {
    try {
        if ((Get-TrayKeys).Count -eq 0) { throw 'No PowerShell tray entry found in the registry. Drag the icon from the ^ arrow onto the taskbar.' }
        $new = if (Get-PinPref) { 0 } else { 1 }
        Set-PinPref $new
        [void](Apply-Pin $new)
    } catch { Show-Error $_.Exception.Message }
}

# ---- events ----
$script:tray.add_MouseClick({
    param($s, $e)
    if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Left) { Toggle-Lid }
})

$menu      = New-Object System.Windows.Forms.ContextMenuStrip
$miAwake   = New-Object System.Windows.Forms.ToolStripMenuItem 'Stay awake when lid is closed'
$miStartup = New-Object System.Windows.Forms.ToolStripMenuItem 'Start with Windows'
$miPin     = New-Object System.Windows.Forms.ToolStripMenuItem 'Pin next to clock'
$miExit    = New-Object System.Windows.Forms.ToolStripMenuItem 'Exit'

$miAwake.add_Click({ Toggle-Lid })
$miStartup.add_Click({ Toggle-Startup })
$miPin.add_Click({ Pin-Tray })
$miExit.add_Click({
    $script:tray.Visible = $false
    [System.Windows.Forms.Application]::Exit()
})

[void]$menu.Items.Add($miAwake)
[void]$menu.Items.Add($miStartup)
[void]$menu.Items.Add($miPin)
[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
[void]$menu.Items.Add($miExit)

$menu.add_Opening({
    try { $miAwake.Checked = ((Get-Lid) -eq 0) } catch { $miAwake.Checked = $false }
    $miStartup.Checked = (Test-Path $script:startupLnk)
    try { $miPin.Checked = (Get-PinPref) } catch { $miPin.Checked = $false }
})

$script:tray.ContextMenuStrip = $menu

Update-Tray
$script:tray.Visible = $true

# On launch (e.g. after a restart) re-apply the remembered pin choice.
# Explorer may register the icon a few seconds late at login, so retry for ~1 minute.
$script:pinTries = 0
$script:pinTimer = New-Object System.Windows.Forms.Timer
$script:pinTimer.Interval = 3000
$script:pinTimer.add_Tick({
    $script:pinTries++
    try {
        if (Test-Path $script:PrefKey) {
            $want = if (Get-PinPref) { 1 } else { 0 }
            if ((Get-TrayKeys).Count -gt 0) {
                [void](Apply-Pin $want)
                $script:pinTimer.Stop()
            }
        } else { $script:pinTimer.Stop() }
    } catch { }
    if ($script:pinTries -ge 20) { $script:pinTimer.Stop() }
})
$script:pinTimer.Start()

# Listen for a newer launch asking this copy to quit
$script:quitTimer = New-Object System.Windows.Forms.Timer
$script:quitTimer.Interval = 400
$script:quitTimer.add_Tick({
    if ($script:quitEvt.WaitOne(0)) {
        $script:quitTimer.Stop()
        $script:tray.Visible = $false
        [System.Windows.Forms.Application]::Exit()
    }
})
$script:quitTimer.Start()

[System.Windows.Forms.Application]::Run()

# clean up so the icon disappears immediately and the next launch can take over
$script:tray.Visible = $false
$script:tray.Dispose()
try { $script:mutex.ReleaseMutex() } catch { }
