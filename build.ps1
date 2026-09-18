# Builds dist\ReadingDesk.exe: the reader, the setup window and the .md
# registration, all in one self-contained executable with the reader page
# embedded as a resource.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
# Two compile passes, because the exe draws its own icon: the first pass makes
# a staging build, that build emits the .ico, and the second pass embeds it.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$obj  = Join-Path $here 'obj'
$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Force -Path $obj, $dist | Out-Null

$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc (.NET Framework 4.x required)" }

$src  = Join-Path $here 'src\ReadingDesk.cs'
$page = Join-Path $here 'reading-desk.html'
$refs = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')

# pass 1: staging build, used only to draw the icon
$stage = Join-Path $obj 'stage.exe'
& $csc /nologo /target:winexe /optimize+ "/resource:$page,reader.html" "/out:$stage" $refs $src
if ($LASTEXITCODE -ne 0) { throw 'stage compile failed' }

$ico = Join-Path $obj 'ReadingDesk.ico'
& $stage --emit-icon $ico | Out-Null
if (-not (Test-Path $ico)) { throw 'icon was not produced' }

# pass 2: the real thing
$exe = Join-Path $dist 'ReadingDesk.exe'
& $csc /nologo /target:winexe /optimize+ "/win32icon:$ico" "/resource:$page,reader.html" "/out:$exe" $refs $src
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }

$kb = [math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host "built $exe ($kb KB)" -ForegroundColor Green
