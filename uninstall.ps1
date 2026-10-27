# Removes LidFlip for the current user.
# By default the lid action is set back to "Sleep" so the laptop doesn't stay on
# in a bag after the tool is gone. Use -KeepLidSetting to leave it as it is.

param([switch]$KeepLidSetting)

$dest    = Join-Path $env:LOCALAPPDATA 'LidFlip'
$lnkPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'LidFlip.lnk'

# Ask a running copy to quit
try {
    $evt = [System.Threading.EventWaitHandle]::OpenExisting('Local\LidFlip_Quit')
    [void]$evt.Set()
    Start-Sleep -Seconds 1
} catch { }   # not running

if (Test-Path $lnkPath) { Remove-Item $lnkPath -Force }
if (Test-Path $dest)    { Remove-Item $dest -Recurse -Force }
if (Test-Path 'HKCU:\Software\LidFlip') { Remove-Item 'HKCU:\Software\LidFlip' -Recurse -Force }

if (-not $KeepLidSetting) {
    powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 1
    powercfg /setdcvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 1
    powercfg /setactive SCHEME_CURRENT
    Write-Host 'Lid close action restored to: Sleep'
}

Write-Host 'LidFlip removed.'
