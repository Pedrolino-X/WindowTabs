param([switch]$Test, [string]$PythonPath, [string]$MSBuildPath)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
 $compiler = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '.tools\fsharp\tools\Microsoft.FSharp.Targets'))
 if (!$PythonPath) { $PythonPath = (Get-Command python -ErrorAction Stop).Source }
 if (!$MSBuildPath) {
  $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
  if (Test-Path $vswhere) {
   $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
  }
  if (!$MSBuildPath) { $MSBuildPath = (Get-Command MSBuild -ErrorAction Stop).Source }
 }
 $python = $PythonPath
 $msbuild = $MSBuildPath
 [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
 function Restore-Package([string]$Id, [string]$Version, [string]$Destination) {
  if (Test-Path (Join-Path $Destination "$Id.nuspec")) { return }
  New-Item -ItemType Directory -Force -Path $Destination | Out-Null
  $archive = Join-Path $Destination 'package.zip'
  $lowerId = $Id.ToLowerInvariant()
  $normalizedVersion = ([version]$Version).ToString()
  # NuGet normalizes a fourth zero component (3.0.0.0 becomes 3.0.0).
  if (([version]$Version).Revision -eq 0) { $normalizedVersion = ([version]$Version).ToString(3) }
  Write-Host "Restoring $Id $Version"
  Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$lowerId/$normalizedVersion/$lowerId.$normalizedVersion.nupkg" -OutFile $archive -UseBasicParsing
  Expand-Archive -LiteralPath $archive -DestinationPath $Destination -Force
  Remove-Item -LiteralPath $archive
 }
 Restore-Package 'FSharp.Compiler.Tools' '10.2.3' (Join-Path $PSScriptRoot '.tools\fsharp')
 [xml]$packageList = Get-Content WtProgram\packages.config
 foreach ($package in $packageList.packages.package) {
  Restore-Package $package.id $package.version (Join-Path $PSScriptRoot "packages\$($package.id).$($package.version)")
 }
 & $python .tools\prepare-build.py
 if ($LASTEXITCODE -ne 0) {throw 'Build preparation failed'}
 & $msbuild WtProgram\WtProgram.compat.fsproj /t:Build /p:Configuration=Release /p:PostBuildEvent= "/p:FSharpTargetsPath=$compiler" /v:minimal /nologo
 if ($LASTEXITCODE -ne 0) {throw 'Original repair build failed'}
 Copy-Item WtProgram\Language WtProgram\bin\Release -Recurse -Force
 Copy-Item WtProgram\Settings WtProgram\bin\Release -Recurse -Force
 if ($Test) {
  & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:Tests\GroupFixture.exe Tests\GroupFixture.cs
  if ($LASTEXITCODE -ne 0) {throw 'Group fixture build failed'}
  & $msbuild Tests\ResourceTests.fsproj /t:Build /v:minimal /nologo
  if ($LASTEXITCODE -ne 0) {throw 'Test build failed'}
  Copy-Item WtProgram\App.config Tests\bin\ResourceTests.exe.config -Force
  Copy-Item WtProgram\bin\Release\FSharp.PowerPack*.dll Tests\bin -Force
  & .\Tests\bin\ResourceTests.exe
  if ($LASTEXITCODE -ne 0) {throw 'Resource tests failed'}
  & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /r:System.Drawing.dll /out:Tests\IconRaster.exe Tests\IconRaster.cs
  if ($LASTEXITCODE -ne 0) {throw 'Icon test build failed'}
  & .\Tests\IconRaster.exe
  if ($LASTEXITCODE -ne 0) {throw 'Icon tests failed'}
 }
} finally {Pop-Location}

