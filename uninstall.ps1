# Markdown Reading Desk, uninstall
#
#   powershell -ExecutionPolicy Bypass -File uninstall.ps1
#
# Unregisters the .md handler, puts back whatever .md pointed at before, and
# removes the Start Menu entry. The install folder is left alone so nothing is
# deleted behind your back. The path is printed at the end.
#
# The installed app can do this itself: ReadingDesk.exe --uninstall

$ErrorActionPreference = 'Stop'

$cmd = (Get-ItemProperty 'HKCU:\Software\Classes\Applications\ReadingDesk.exe\shell\open\command' `
        -ErrorAction SilentlyContinue).'(default)'
$exe = $null
if ($cmd -match '^"([^"]+)"') { $exe = $matches[1] }
if (-not $exe -or -not (Test-Path $exe)) {
  $local = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'dist\ReadingDesk.exe'
  if (Test-Path $local) { $exe = $local }
}
if (-not $exe) { throw 'Could not find ReadingDesk.exe. Run build.ps1 first, or pass the path yourself.' }

& $exe --uninstall --silent
Write-Host "Unregistered. The files in $(Split-Path -Parent $exe) are still there; delete them when ready."
