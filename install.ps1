# Installs LidToggle for the current user (no admin needed).
#   - copies the app to %LOCALAPPDATA%\LidToggle
#   - adds it to Windows startup
#   - starts it

$ErrorActionPreference = 'Stop'

$src  = Join-Path $PSScriptRoot 'src'
$dest = Join-Path $env:LOCALAPPDATA 'LidToggle'

Write-Host "Installing LidToggle to $dest ..."
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path (Join-Path $src '*') -Destination $dest -Force

# Files downloaded from the internet carry a "Mark of the Web" that makes Windows
# show a security prompt every time the launcher runs. Remove it from our own files.
Get-ChildItem $dest -File | Unblock-File

# Start with Windows
$vbs = Join-Path $dest 'LidToggle.vbs'
$lnkPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'LidToggle.lnk'
$ws  = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkPath)
$lnk.TargetPath       = 'wscript.exe'
$lnk.Arguments        = '"' + $vbs + '"'
$lnk.WorkingDirectory = $dest
$lnk.Description      = 'LidToggle - control what happens when the laptop lid closes'
$lnk.Save()

# Launch (a running older copy is replaced automatically)
Start-Process -FilePath 'wscript.exe' -ArgumentList ('"' + $vbs + '"')

Write-Host ''
Write-Host 'Done. LidToggle is running in the system tray (next to the clock).'
Write-Host 'If you cannot see it, look under the ^ arrow, then right-click it > "Pin next to clock".'
