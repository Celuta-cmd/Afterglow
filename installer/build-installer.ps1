param([switch]$SkipPublish,[string]$Version='2.1.1',[string]$Python='python',[string]$ToolPath)
$ErrorActionPreference='Stop'
$sourceRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(!$SkipPublish){& (Join-Path $sourceRoot 'build.ps1');if($LASTEXITCODE -ne 0){throw 'App build failed.'}}
if(!$ToolPath){$ToolPath=Join-Path $sourceRoot '.installer-tools'}
$wix=Join-Path $ToolPath 'wix.exe'
if(!(Test-Path $wix)){dotnet tool install wix --version 5.0.2 --tool-path $ToolPath;if($LASTEXITCODE -ne 0){throw 'WiX installation failed.'}}
& $wix extension add -g WixToolset.UI.wixext/5.0.2
if($LASTEXITCODE -ne 0){throw 'WiX UI extension installation failed.'}
& $Python (Join-Path $PSScriptRoot 'generate.py')
if($LASTEXITCODE -ne 0){throw 'MSI authoring failed.'}
$output=Join-Path $sourceRoot ('Afterglow-'+$Version+'-x64.msi')
& $wix build (Join-Path $PSScriptRoot 'Afterglow.wxs') -arch x64 -ext WixToolset.UI.wixext -d "PayloadDir=$(Join-Path $sourceRoot 'portable')" -d "ProjectDir=$sourceRoot" -d "ProductVersion=$Version" -o $output
if($LASTEXITCODE -ne 0){throw 'MSI build failed.'}
Write-Output $output
