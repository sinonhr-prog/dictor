. "$PSScriptRoot/env.ps1"
New-Item -ItemType Directory -Force (Join-Path $DictorRoot 'runtime/models') | Out-Null
$metadata = Invoke-RestMethod 'https://huggingface.co/api/models/ggerganov/whisper.cpp'
$revision = $metadata.sha
$tree = Invoke-RestMethod "https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/$revision"
$manifestPath = Join-Path $DictorRoot 'runtime/models/manifest.json'
$manifest = if (Test-Path $manifestPath) { @(Get-Content $manifestPath -Raw | ConvertFrom-Json) } else { @() }
foreach ($name in @('ggml-small-q5_1.bin', 'ggml-large-v3-turbo-q5_0.bin', 'ggml-large-v3-turbo.bin')) {
    $entry = $tree | Where-Object path -eq $name
    if (-not $entry.lfs.oid) { throw 'MODEL_EXPECTED_HASH_MISSING' }
    $target = Join-Path $DictorRoot "runtime/models/$name"
    $url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/$revision/$name"
    if (-not (Test-Path -LiteralPath $target)) {
        Invoke-WebRequest $url -OutFile "$target.partial"
        if ((Get-FileHash "$target.partial" -Algorithm SHA256).Hash -ne $entry.lfs.oid) { throw 'MODEL_HASH_MISMATCH' }
        Move-Item -LiteralPath "$target.partial" -Destination $target
    }
    if ((Get-FileHash $target -Algorithm SHA256).Hash -ne $entry.lfs.oid) { throw 'MODEL_HASH_MISMATCH' }
    $manifest = @($manifest | Where-Object file -ne $name)
    $manifest += [pscustomobject]@{ file=$name; source=$url; revision=$revision; size=$entry.size; sha256=$entry.lfs.oid; format='ggml'; license='MIT' }
    $manifest | ConvertTo-Json -Depth 4 -AsArray | Set-Content (Join-Path $DictorRoot 'runtime/models/manifest.json') -Encoding utf8
    Write-Output "Verified $name"
}

$metadata = Invoke-RestMethod 'https://huggingface.co/api/models/ggml-org/whisper-vad'
$revision = $metadata.sha
$tree = Invoke-RestMethod "https://huggingface.co/api/models/ggml-org/whisper-vad/tree/$revision"
$name = 'ggml-silero-v6.2.0.bin'
$entry = $tree | Where-Object path -eq $name
if (-not $entry.lfs.oid) { throw 'VAD_EXPECTED_HASH_MISSING' }
$target = Join-Path $DictorRoot "runtime/models/$name"
$url = "https://huggingface.co/ggml-org/whisper-vad/resolve/$revision/$name"
if (-not (Test-Path $target)) {
    Invoke-WebRequest $url -OutFile "$target.partial"
    if ((Get-FileHash "$target.partial" -Algorithm SHA256).Hash -ne $entry.lfs.oid) { throw 'VAD_HASH_MISMATCH' }
    Move-Item -LiteralPath "$target.partial" -Destination $target
}
if ((Get-FileHash $target -Algorithm SHA256).Hash -ne $entry.lfs.oid) { throw 'VAD_HASH_MISMATCH' }
$manifest = @($manifest | Where-Object file -ne $name)
$manifest += [pscustomobject]@{ file=$name; source=$url; revision=$revision; size=$entry.size; sha256=$entry.lfs.oid; format='ggml'; license='MIT' }
$manifest | ConvertTo-Json -Depth 4 -AsArray | Set-Content $manifestPath -Encoding utf8
Write-Output 'Verified VAD'
