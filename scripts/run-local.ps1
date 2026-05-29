$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$env:ASPNETCORE_ENVIRONMENT = "Development"

dotnet run --project .\src\FlipShop.Api\FlipShop.Api.csproj --urls http://127.0.0.1:5000
