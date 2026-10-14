param([switch]$Test)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\Afterglow\Afterglow.csproj'
$destination = Join-Path $PSScriptRoot 'portable'
Push-Location $PSScriptRoot
try { dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $destination }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'extension') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'RESEARCH.md'), (Join-Path $PSScriptRoot 'VERIFICATION.md'), (Join-Path $PSScriptRoot 'DEMO.txt'), (Join-Path $PSScriptRoot 'LYRICS.md'), (Join-Path $PSScriptRoot 'INSTALL.md'), (Join-Path $PSScriptRoot 'REDESIGN.md'), (Join-Path $PSScriptRoot 'LICENSE') -Destination $destination -Force
if (Test-Path (Join-Path $PSScriptRoot 'third-party')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'third-party') -Destination $destination -Recurse -Force }
if ($Test) {
    $testDirectory = Join-Path $PSScriptRoot 'test-results'
    New-Item -ItemType Directory -Force -Path $testDirectory | Out-Null
    $report = Join-Path $testDirectory 'self-test.txt'
    $testProcess = Start-Process -FilePath (Join-Path $destination 'Afterglow.exe') -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
    Get-Content -LiteralPath $report
    if ($testProcess.ExitCode -ne 0) { throw 'Native app tests failed.' }
    node --test (Join-Path $PSScriptRoot 'src\extension.test.cjs') (Join-Path $PSScriptRoot 'src\setup.test.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'Extension tests failed.' }
}
