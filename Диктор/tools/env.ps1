$ErrorActionPreference = 'Stop'
$script:DictorRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT = Join-Path $DictorRoot '.sdk'
$env:DOTNET_CLI_HOME = Join-Path $DictorRoot '.build/home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:NUGET_PACKAGES = Join-Path $DictorRoot '.packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $DictorRoot '.build/nuget-http'
$env:TEMP = Join-Path $DictorRoot '.build/temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP | Out-Null
$script:Dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
