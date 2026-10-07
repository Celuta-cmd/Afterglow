param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'Install the Visual Studio C++ build tools and Windows SDK to build the composition helper.'}
$environment=Join-Path $installation 'VC\Auxiliary\Build\vcvars64.bat'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$nativeOutput=(Resolve-Path -LiteralPath $OutputDirectory).Path
$cpp=Join-Path $PSScriptRoot 'Composition.cpp'
Push-Location -LiteralPath $nativeOutput
try {
    $command='call "'+$environment+'" >nul && cl.exe /nologo /LD /MT /O2 /W4 /WX /EHsc /std:c++17 /DUNICODE /D_UNICODE "'+$cpp+'" /Fe:"'+(Join-Path $nativeOutput 'Afterglow.Composition.dll')+'" /link d3d11.lib dxgi.lib dcomp.lib d2d1.lib user32.lib'
    & cmd.exe /d /c $command
    if($LASTEXITCODE -ne 0){throw 'Native composition build failed.'}
} finally {Pop-Location}
