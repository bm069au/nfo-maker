$ErrorActionPreference = 'Stop'
$nfoRoot = $PSScriptRoot
& (Join-Path $nfoRoot 'build-icon.ps1') -Kind Nfo
$nfoCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$nfoDist = Join-Path $nfoRoot '.build'
New-Item -ItemType Directory -Force -Path $nfoDist | Out-Null
& $nfoCompiler /nologo /target:winexe /platform:anycpu /optimize+ "/win32icon:$nfoRoot\assets\app.ico" "/out:$nfoDist\NfoMaker.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "$nfoRoot\NfoMaker.cs" "$nfoRoot\Branding.cs" "$nfoRoot\Batch.cs"
if ($LASTEXITCODE -ne 0) { throw 'NFO Maker compilation failed' }
Write-Output (Join-Path $nfoDist 'NfoMaker.exe')
