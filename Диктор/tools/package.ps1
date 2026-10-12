param([string]$Name = 'Dictor-Portable-win-x64')
. "$PSScriptRoot/env.ps1"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$publish = Join-Path $DictorRoot '.build/portable-publish'
$dist = Join-Path $DictorRoot 'dist'
if ($Name -notmatch '^[A-Za-z0-9_-]+$') { throw 'PACKAGE_NAME_INVALID' }
$package = Join-Path $dist $Name
$archive = "$package.zip"
if ((Test-Path $package) -or (Test-Path $archive)) { throw 'PACKAGE_ALREADY_EXISTS' }
if (-not (Test-Path "$publish/Dictor.exe")) { throw 'PUBLISH_REQUIRED' }
New-Item -ItemType Directory -Path $package -Force | Out-Null
Get-ChildItem $publish | Where-Object Extension -ne '.pdb' | Copy-Item -Destination $package -Recurse
Rename-Item -LiteralPath "$package/Dictor.exe" -NewName 'Диктор.exe'
Copy-Item "$DictorRoot/runtime/licenses" $package -Recurse
Copy-Item "$DictorRoot/src/Dictor/Assets/Dictor.ico" $package
New-Item -ItemType Directory "$package/models" | Out-Null
$required = @('ggml-large-v3-turbo.bin', 'ggml-small-q5_1.bin', 'ggml-silero-v6.2.0.bin')
$models = @(Get-Content "$DictorRoot/runtime/models/manifest.json" -Raw | ConvertFrom-Json | Where-Object file -in $required)
if ($models.Count -ne 3) { throw 'MODEL_MANIFEST_INCOMPLETE' }
foreach ($model in $models) {
    $source = Join-Path "$DictorRoot/runtime/models" $model.file
    if ((Get-Item $source).Length -ne $model.size -or (Get-FileHash $source -Algorithm SHA256).Hash -ne $model.sha256) { throw 'MODEL_INTEGRITY_FAILED' }
    Copy-Item $source "$package/models"
}
ConvertTo-Json -InputObject $models -Depth 5 | Set-Content "$package/models/manifest.json" -Encoding utf8
$checksums = @(Get-ChildItem $package -File -Recurse | ForEach-Object {
    [ordered]@{ file = [IO.Path]::GetRelativePath($package, $_.FullName).Replace('\', '/'); size = $_.Length; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
ConvertTo-Json -InputObject $checksums -Depth 4 | Set-Content "$package/checksums.json" -Encoding utf8
[IO.Compression.ZipFile]::CreateFromDirectory($package, $archive, [IO.Compression.CompressionLevel]::Fastest, $true)
(Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($archive) | Set-Content "$archive.sha256" -Encoding ascii
Get-Item $archive | Select-Object FullName, Length
