# Clickto - Windows packaging
$ErrorActionPreference = "Stop"

Write-Host "==> Publishing self-contained win-x64 build"
dotnet publish Clickto/Clickto.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist-win

Write-Host ""
Write-Host "Done -> dist-win\Clickto.exe"
