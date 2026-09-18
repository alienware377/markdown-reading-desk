# Markdown Reading Desk installer (per-user, no admin rights needed)
#
#   powershell -ExecutionPolicy Bypass -File install.ps1
#   powershell -ExecutionPolicy Bypass -File install.ps1 -Dest "G:\Programs\ReadingDesk"
#
# Installs to %LOCALAPPDATA%\ReadingDesk unless -Dest says otherwise, adds a
# Start Menu entry, and registers itself as a handler for .md files. Windows 11
# hash-protects the *default* app choice, so the final "always use this app"
# pick has to be made by hand. The script prints the steps when it finishes.

param(
  [string]$Dest = (Join-Path $env:LOCALAPPDATA 'ReadingDesk')
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$app  = $Dest
New-Item -ItemType Directory -Force -Path (Join-Path $app 'open') | Out-Null
Add-Type -AssemblyName System.Drawing

# ── icon: a teal page with rules ──────────────────────────────────────────────
function New-RoundRect([int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc($x, $y, $r, $r, 180, 90)
  $p.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
  $p.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90)
  $p.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
  $p.CloseFigure()
  $p
}

$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)

$teal  = [System.Drawing.Color]::FromArgb(255, 47, 111, 102)
$deep  = [System.Drawing.Color]::FromArgb(255, 32, 78, 72)
$cream = [System.Drawing.Color]::FromArgb(255, 247, 249, 247)
$soft  = [System.Drawing.Color]::FromArgb(255, 168, 200, 192)

$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush `
  (New-Object System.Drawing.Point 12, 12), (New-Object System.Drawing.Point 244, 244), $teal, $deep
$g.FillPath($grad, (New-RoundRect 12 12 232 232 56))
$g.FillPath((New-Object System.Drawing.SolidBrush $cream), (New-RoundRect 62 46 132 164 18))

foreach ($r in @(@(82,82,92,14), @(82,110,92,9), @(82,130,70,9), @(82,150,92,9), @(82,170,52,9))) {
  $col = if ($r[3] -gt 10) { $teal } else { $soft }
  $br = New-Object System.Drawing.SolidBrush $col
  $g.FillPath($br, (New-RoundRect $r[0] $r[1] $r[2] $r[3] $r[3]))
  $br.Dispose()
}
$g.Dispose()

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray(); $ms.Dispose(); $bmp.Dispose()

# single-frame PNG icon
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ico
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]1)
$bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0)
$bw.Write([UInt16]1); $bw.Write([UInt16]32)
$bw.Write([UInt32]$png.Length); $bw.Write([UInt32]22)
$bw.Write($png); $bw.Flush()
$icoPath = Join-Path $app 'ReadingDesk.ico'
[System.IO.File]::WriteAllBytes($icoPath, $ico.ToArray())
$bw.Dispose()

# ── viewer.html: standalone wrapper around the reader, with an embed slot ─────
# The reader runs in a browser app window, so the taskbar and title bar take
# their icon from the page's favicon, so we inline the same artwork.
$slot = '<script type="text/markdown" id="embedded" data-name="<!--NAME-->"><!--EMBED--></script>' + "`r`n"
$body = Get-Content -Raw -Encoding UTF8 (Join-Path $here 'reading-desk.html')
if ($body -notmatch '<div class="app" id="app">') { throw 'reading-desk.html is missing its app anchor' }
$body = $body.Replace('<div class="app" id="app">', $slot + '<div class="app" id="app">')

$favicon = '<link rel="icon" type="image/png" href="data:image/png;base64,{0}">' -f [Convert]::ToBase64String($png)
if ($body -match '</title>') {
  $body = $body -replace '</title>', ("</title>`r`n" + $favicon)
} else {
  $body = $favicon + "`r`n" + $body
}

$head = @'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>html{color-scheme:light dark}body{margin:0;font:14px system-ui,sans-serif}img{max-width:100%}[hidden]{display:none!important}</style>
'@
[System.IO.File]::WriteAllText((Join-Path $app 'viewer.html'), ($head + "`r`n" + $body + "`r`n</html>`r`n"),
  (New-Object System.Text.UTF8Encoding($false)))

# ── compile the launcher ──────────────────────────────────────────────────────
$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc (.NET Framework 4.x required)" }
$exe = Join-Path $app 'ReadingDesk.exe'
& $csc /nologo /target:winexe /optimize+ /win32icon:"$icoPath" /out:"$exe" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $here 'src\ReadingDesk.cs')
if (-not (Test-Path $exe)) { throw 'compile failed' }

# ── remember what .md pointed at, so uninstall.ps1 can put it back ────────────
$prev = (Get-ItemProperty 'HKCU:\Software\Classes\.md' -ErrorAction SilentlyContinue).'(default)'
@{ 'previous_md_progid' = $prev; 'saved' = (Get-Date).ToString('s') } | ConvertTo-Json |
  Set-Content -Encoding UTF8 (Join-Path $app 'previous-md-association.json')
$feMd = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.md'
if (Test-Path "Registry::HKEY_CURRENT_USER\$($feMd.Substring(5))") {
  reg export $feMd (Join-Path $app 'backup-FileExts-md.reg') /y | Out-Null
}

# ── register ──────────────────────────────────────────────────────────────────
$cmd = '"{0}" "%1"' -f $exe

$prog = 'HKCU:\Software\Classes\ReadingDesk.md'
New-Item -Force -Path "$prog\shell\open\command" | Out-Null
New-Item -Force -Path "$prog\DefaultIcon" | Out-Null
Set-ItemProperty $prog '(default)' 'Markdown Document'
Set-ItemProperty $prog 'FriendlyTypeName' 'Markdown Document'
Set-ItemProperty "$prog\DefaultIcon" '(default)' "$icoPath,0"
Set-ItemProperty "$prog\shell\open" 'FriendlyAppName' 'Markdown Reading Desk'
Set-ItemProperty "$prog\shell\open\command" '(default)' $cmd

# so it shows up by name in "Open with"
$ak = 'HKCU:\Software\Classes\Applications\ReadingDesk.exe'
New-Item -Force -Path "$ak\shell\open\command" | Out-Null
New-Item -Force -Path "$ak\SupportedTypes" | Out-Null
New-Item -Force -Path "$ak\DefaultIcon" | Out-Null
Set-ItemProperty $ak 'FriendlyAppName' 'Markdown Reading Desk'
Set-ItemProperty "$ak\DefaultIcon" '(default)' "$icoPath,0"
Set-ItemProperty "$ak\shell\open\command" '(default)' $cmd
Set-ItemProperty "$ak\SupportedTypes" '.md' ''

# Register as a real application, so Windows Settings lists it under
# Default apps instead of only offering it in the Open-with picker.
$cap = 'HKCU:\Software\ReadingDesk\Capabilities'
New-Item -Force -Path "$cap\FileAssociations" | Out-Null
Set-ItemProperty $cap 'ApplicationName' 'Markdown Reading Desk'
Set-ItemProperty $cap 'ApplicationDescription' 'Read markdown files with reading-mode controls'
Set-ItemProperty $cap 'ApplicationIcon' "$icoPath,0"
Set-ItemProperty "$cap\FileAssociations" '.md' 'ReadingDesk.md'
New-Item -Force -Path 'HKCU:\Software\RegisteredApplications' | Out-Null
Set-ItemProperty 'HKCU:\Software\RegisteredApplications' 'Markdown Reading Desk' 'Software\ReadingDesk\Capabilities'

New-Item -Force -Path 'HKCU:\Software\Classes\.md\OpenWithProgids' | Out-Null
Set-ItemProperty 'HKCU:\Software\Classes\.md' '(default)' 'ReadingDesk.md'
Set-ItemProperty 'HKCU:\Software\Classes\.md' 'PerceivedType' 'text'
Set-ItemProperty 'HKCU:\Software\Classes\.md\OpenWithProgids' 'ReadingDesk.md' ([byte[]]@()) -Type None -ErrorAction SilentlyContinue

# ── Start Menu ────────────────────────────────────────────────────────────────
$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Markdown Reading Desk.lnk'
$ws = New-Object -ComObject WScript.Shell
$s = $ws.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = $app
$s.IconLocation = "$icoPath,0"
$s.Description = 'Read markdown files with reading-mode controls'
$s.Save()

Add-Type -Namespace RD -Name Shell -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, System.IntPtr a, System.IntPtr b);'
[RD.Shell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host ""
Write-Host "Installed to $app" -ForegroundColor Green
Write-Host "Start Menu:  Markdown Reading Desk"
Write-Host ""
Write-Host "One step left. Windows won't let a script set the default app:" -ForegroundColor Yellow
Write-Host "  right-click any .md  ->  Open with  ->  Choose another app"
Write-Host "  ->  Markdown Reading Desk  ->  tick 'Always use this app'"
Write-Host ""
Write-Host "To undo:  powershell -ExecutionPolicy Bypass -File `"$app\uninstall.ps1`""
Copy-Item (Join-Path $here 'uninstall.ps1') (Join-Path $app 'uninstall.ps1') -Force
