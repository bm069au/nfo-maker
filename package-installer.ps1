$ErrorActionPreference = 'Stop'
$nfoRoot = $PSScriptRoot
& (Join-Path $nfoRoot 'build-icon.ps1') -Kind Nfo
$nfoCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$nfoDist = Join-Path $nfoRoot 'dist'
& (Join-Path $nfoRoot 'build.ps1')
$nfoPayload = Join-Path $nfoDist 'installer-payload.zip'
Compress-Archive -LiteralPath @((Join-Path $nfoDist 'NfoMaker.exe'),(Join-Path $nfoRoot 'README.md'),(Join-Path $nfoRoot 'LICENSE')) -DestinationPath $nfoPayload -Force
& $nfoCompiler /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$nfoRoot\assets\app.ico" "/win32manifest:$nfoRoot\installer.manifest" "/resource:$nfoPayload,payload.zip" "/out:$nfoDist\NfoMaker-Setup.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$nfoRoot\Installer.cs" "$nfoRoot\Branding.cs"
if ($LASTEXITCODE -ne 0) { throw 'NFO installer compilation failed' }
$nfoSetup = Join-Path $nfoDist 'NfoMaker-Setup.exe'
((Get-FileHash -LiteralPath $nfoSetup).Hash.ToLower()+'  NfoMaker-Setup.exe') | Set-Content -LiteralPath (Join-Path $nfoDist 'SETUP-SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath $nfoSetup | Select-Object Name,Length
