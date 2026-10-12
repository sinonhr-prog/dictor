param([switch]$Publish)
. "$PSScriptRoot/env.ps1"
Push-Location $DictorRoot
try {
    & $Dotnet restore Dictor.sln --locked-mode
    if ($LASTEXITCODE) { throw 'RESTORE_FAILED' }
    & $Dotnet build Dictor.sln -c Release --no-restore -warnaserror
    if ($LASTEXITCODE) { throw 'BUILD_FAILED' }
    & $Dotnet format Dictor.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE) { throw 'FORMAT_FAILED' }
    & $Dotnet test tests/Dictor.Tests/Dictor.Tests.csproj -c Release --no-build --no-restore
    if ($LASTEXITCODE) { throw 'TEST_FAILED' }
    if ($Publish) {
        & $Dotnet publish src/Dictor/Dictor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o runtime
        if ($LASTEXITCODE) { throw 'PUBLISH_FAILED' }
    }
} finally { Pop-Location }
