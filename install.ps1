# Markdown Reading Desk, install from source (per-user, no admin rights needed)
#
#   powershell -ExecutionPolicy Bypass -File install.ps1
#   powershell -ExecutionPolicy Bypass -File install.ps1 -Dest "G:\Programs\ReadingDesk"
#
# Builds dist\ReadingDesk.exe and runs its installer. If you just want the app,
# grab ReadingDesk.exe from the Releases page and double-click it instead.

param(
  [string]$Dest = (Join-Path $env:LOCALAPPDATA 'ReadingDesk')
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $here 'build.ps1')

$exe = Join-Path $here 'dist\ReadingDesk.exe'
& $exe --install --dest $Dest --silent

Write-Host ""
Write-Host "Installed to $Dest" -ForegroundColor Green
Write-Host "Start Menu:  Markdown Reading Desk"
Write-Host ""
Write-Host "One step left. Windows will not let a script set the default app:" -ForegroundColor Yellow
Write-Host "  right-click any .md  ->  Open with  ->  Choose another app"
Write-Host "  ->  Markdown Reading Desk  ->  tick 'Always use this app'"
Write-Host ""
Write-Host "To undo:  `"$Dest\ReadingDesk.exe`" --uninstall"
