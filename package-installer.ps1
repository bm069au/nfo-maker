$ErrorActionPreference = 'Stop'
$nfoRoot = $PSScriptRoot
& (Join-Path $nfoRoot 'build-icon.ps1') -Kind Nfo
$nfoCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$nfoDist = Join-Path $nfoRoot 'dist'
$nfoBuild = Join-Path $nfoRoot '.build'
New-Item -ItemType Directory -Force -Path $nfoDist | Out-Null
& (Join-Path $nfoRoot 'build.ps1')
Copy-Item -LiteralPath (Join-Path $nfoRoot 'assets\app.ico') -Destination (Join-Path $nfoBuild 'NfoMaker.ico') -Force
$nfoPayload = Join-Path $nfoBuild 'installer-payload.zip'
Compress-Archive -LiteralPath @((Join-Path $nfoBuild 'NfoMaker.exe'),(Join-Path $nfoRoot 'README.md'),(Join-Path $nfoRoot 'LICENSE'),(Join-Path $nfoBuild 'NfoMaker.ico')) -DestinationPath $nfoPayload -Force
& $nfoCompiler /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$nfoRoot\assets\app.ico" "/win32manifest:$nfoRoot\installer.manifest" "/resource:$nfoPayload,payload.zip" "/out:$nfoDist\NfoMaker-Setup.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$nfoRoot\Installer.cs" "$nfoRoot\Branding.cs"
if ($LASTEXITCODE -ne 0) { throw 'NFO installer compilation failed' }
$nfoSetup = Join-Path $nfoDist 'NfoMaker-Setup.exe'
((Get-FileHash -LiteralPath $nfoSetup).Hash.ToLower()+'  NfoMaker-Setup.exe') | Set-Content -LiteralPath (Join-Path $nfoDist 'SETUP-SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath $nfoSetup | Select-Object Name,Length

# Keep only the ready-to-install release and its checksum in the public output folder.
foreach ($nfoObsoleteName in @('installer-payload.zip','NfoMaker-Windows.zip','NfoMaker.exe','SHA256SUMS.txt')) {
    $nfoObsoletePath = Join-Path $nfoDist $nfoObsoleteName
    if (Test-Path -LiteralPath $nfoObsoletePath) { Remove-Item -LiteralPath $nfoObsoletePath }
}
