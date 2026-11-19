# Builds SokolChess.exe (WinForms, .NET Framework 4.8 - already built into Windows 10/11).
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1 [-Tests] [-Out path	o\SokolChess.exe]
param([switch]$Tests, [string]$Out = '')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$csc = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw "Roslyn csc.exe (Visual Studio 2022) not found" }

$refDir = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
if (-not (Test-Path $refDir)) { throw ".NET Framework 4.8 reference assemblies not found: $refDir" }
$refs = 'mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll' |
    ForEach-Object { "-r:" + (Join-Path $refDir $_) }

$engine = Get-ChildItem (Join-Path $root 'src\Engine') -Filter *.cs | ForEach-Object { $_.FullName }
$common = @('-nologo','-noconfig','-nostdlib+','-langversion:latest','-optimize+','-platform:x64','-unsafe+','-deterministic','-warn:3','-nowarn:1591')

if ($Tests) {
    $testSrc = Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object { $_.FullName }
    $out = Join-Path $root 'tests\bin\EngineTests.exe'
    New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
    & $csc @common @refs '-target:exe' "-out:$out" @engine @testSrc
    if ($LASTEXITCODE -ne 0) { throw "Test build failed" }
    Write-Host "Built $out"
    exit 0
}

$app = Get-ChildItem (Join-Path $root 'src\App'), (Join-Path $root 'src\Shared') -Filter *.cs | ForEach-Object { $_.FullName }
$out = if ($Out) { $Out } else { Join-Path $root 'SokolChess.exe' }
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$extra = @('-target:winexe', "-out:$out", "-win32manifest:$(Join-Path $root 'src\app.manifest')")
$icon = Join-Path $root 'src\App\app.ico'
if (Test-Path $icon) { $extra += "-win32icon:$icon" }
& $csc @common @refs @extra @engine @app
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $out"
