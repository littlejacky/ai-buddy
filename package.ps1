Write-Host "=== 1. Copying Dist to wwwroot ==="
$wwwroot = "d:/Lab/test/buddy/server/Buddy.Api/wwwroot"
if (Test-Path $wwwroot) { Remove-Item -Recurse -Force $wwwroot }
New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null
Copy-Item -Path "d:/Lab/test/buddy/client/dist/*" -Destination $wwwroot -Recurse -Force

Write-Host "=== 2. Publishing Backend (Release) ==="
$publishDir = "d:/Lab/test/buddy/publish"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish d:/Lab/test/buddy/server/Buddy.Api/Buddy.Api.csproj -c Release -o $publishDir

Write-Host "=== 3. Creating Deployment Zip ==="
$zipPath = "d:/Lab/test/buddy/buddy-deploy.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$publishDir/*" -DestinationPath $zipPath -Force

$zipItem = Get-Item $zipPath
Write-Host "=== Success! buddy-deploy.zip created. Size: $([math]::Round($zipItem.Length / 1MB, 2)) MB ==="
