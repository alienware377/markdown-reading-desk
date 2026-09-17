# Markdown Reading Desk — uninstaller
#
#   powershell -ExecutionPolicy Bypass -File uninstall.ps1
#
# Unregisters the .md handler, restores whatever .md pointed at before, and
# removes the Start Menu entry. The install folder is left in place so nothing
# is deleted behind your back — the path is printed at the end.

param(
  [string]$Dest
)

# Find the install folder from the registration, so this works wherever it went.
$app = $Dest
if (-not $app) {
  $reg = (Get-ItemProperty 'HKCU:\Software\Classes\Applications\ReadingDesk.exe\shell\open\command' `
          -ErrorAction SilentlyContinue).'(default)'
  if ($reg -match '^"([^"]+)"') { $app = Split-Path -Parent $matches[1] }
}
if (-not $app) { $app = Join-Path $env:LOCALAPPDATA 'ReadingDesk' }

Remove-Item 'HKCU:\Software\Classes\ReadingDesk.md' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Classes\Applications\ReadingDesk.exe' -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty 'HKCU:\Software\Classes\.md\OpenWithProgids' -Name 'ReadingDesk.md' -Force -ErrorAction SilentlyContinue

$json = Join-Path $app 'previous-md-association.json'
if (Test-Path $json) {
  $prev = (Get-Content -Raw $json | ConvertFrom-Json).previous_md_progid
  if ($prev) { Set-ItemProperty 'HKCU:\Software\Classes\.md' '(default)' $prev }
}

$bak = Join-Path $app 'backup-FileExts-md.reg'
if (Test-Path $bak) { reg import $bak | Out-Null }

Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Markdown Reading Desk.lnk') `
  -Force -ErrorAction SilentlyContinue

Write-Host "Unregistered." -ForegroundColor Green
Write-Host "Delete the folder yourself when you're ready: $app"
Write-Host "You may need to pick your preferred .md app once via right-click > Open with."
