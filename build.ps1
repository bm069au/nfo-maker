$ErrorActionPreference = 'Stop'
$nfoRoot = $PSScriptRoot
$nfoCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$nfoDist = Join-Path $nfoRoot 'dist'
New-Item -ItemType Directory -Force -Path $nfoDist | Out-Null
& $nfoCompiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$nfoDist\NfoMaker.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "$nfoRoot\NfoMaker.cs" "$nfoRoot\Batch.cs"
if ($LASTEXITCODE -ne 0) { throw 'NFO Maker compilation failed' }
Write-Output (Join-Path $nfoDist 'NfoMaker.exe')
