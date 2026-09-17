param (
    [string]$Version = "1.0.1",
    [string]$PublishDir = "$PSScriptRoot\..\..\..\publish\win-x64",
    [string]$OutputDir = "$PSScriptRoot\..",
    [string]$Publisher = "CN=7E83DE15-E15F-41B6-B068-989D9548D0BF"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building FrySharp MSIX Package ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host "PublishDir: $PublishDir"
Write-Host "OutputDir: $OutputDir"
Write-Host "Publisher: $Publisher"

# 1. Format 4-part version for AppxManifest (Major.Minor.Build.Revision)
$cleanVer = ($Version -split '-')[0]
$verParts = $cleanVer -split '\.'
while ($verParts.Count -lt 4) {
    $verParts += "0"
}
$fourPartVersion = ($verParts[0..3] -join '.')
Write-Host "AppxManifest 4-Part Version: $fourPartVersion" -ForegroundColor Green

# 2. Locate MakeAppx.exe and signtool.exe from Windows SDK
$makeAppxList = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "MakeAppx.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*x64*" }
$makeAppx = ($makeAppxList | Sort-Object FullName -Descending | Select-Object -First 1).FullName

if (-not $makeAppx) {
    $makeAppx = (Get-Command "MakeAppx.exe" -ErrorAction SilentlyContinue).Source
}

if (-not $makeAppx) {
    throw "MakeAppx.exe could not be found. Please ensure Windows 10/11 SDK is installed."
}

Write-Host "Found MakeAppx: $makeAppx"

$signtoolList = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*x64*" }
$signtool = ($signtoolList | Sort-Object FullName -Descending | Select-Object -First 1).FullName

if (-not $signtool) {
    $signtool = (Get-Command "signtool.exe" -ErrorAction SilentlyContinue).Source
}

# 3. Assemble staging directory
$stageDir = Join-Path $env:TEMP "FrySharp_MSIX_Stage"
if (Test-Path $stageDir) {
    Remove-Item -Recurse -Force $stageDir
}
New-Item -ItemType Directory -Path $stageDir | Out-Null

Write-Host "Copying binaries from $PublishDir to staging..."
Copy-Item -Path "$PublishDir\*" -Destination $stageDir -Recurse -Force

Write-Host "Copying MSIX Assets..."
$assetsDest = Join-Path $stageDir "Assets"
Copy-Item -Path "$PSScriptRoot\Assets" -Destination $assetsDest -Recurse -Force

# 4. Generate AppxManifest.xml from template
$templatePath = Join-Path $PSScriptRoot "AppxManifest.xml.template"
$manifestContent = Get-Content -Path $templatePath -Raw
$manifestContent = $manifestContent -replace '__PUBLISHER__', $Publisher
$manifestContent = $manifestContent -replace '__FOUR_PART_VERSION__', $fourPartVersion
$manifestContent = $manifestContent -replace '__VERSION__', $cleanVer

$manifestDest = Join-Path $stageDir "AppxManifest.xml"
Set-Content -Path $manifestDest -Value $manifestContent -Encoding UTF8
Write-Host "Generated AppxManifest.xml at $manifestDest"

# 5. Pack MSIX
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$msixOutputFile = Join-Path $OutputDir "FrySharp-$Version-x64.msix"
Write-Host "Packing MSIX: $msixOutputFile..." -ForegroundColor Cyan

& "$makeAppx" pack /d "$stageDir" /p "$msixOutputFile" /nv /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE"
}

# 6. Create Certificate & Sign MSIX Package
Write-Host "Creating certificate and signing package..." -ForegroundColor Cyan
$cert = New-SelfSignedCertificate -Type Custom `
    -Subject $Publisher `
    -KeyUsage DigitalSignature `
    -FriendlyName "FrySharp MSIX Signing Certificate" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$password = ConvertTo-SecureString -String "FrySharpSecretPassword123!" -Force -AsPlainText
$pfxPath = Join-Path $env:TEMP "FrySharp_SignCert.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password | Out-Null

if ($signtool) {
    Write-Host "Signing MSIX with signtool..."
    & "$signtool" sign /fd SHA256 /a /f "$pfxPath" /p "FrySharpSecretPassword123!" /v "$msixOutputFile"
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool failed with exit code $LASTEXITCODE"
    }
} else {
    Write-Warning "SignTool not found; MSIX was built unsigned."
}

# Export public certificate (.cer) for sideloading convenience
$cerOutputFile = Join-Path $OutputDir "FrySharp-$Version-PublicCert.cer"
Export-Certificate -Cert $cert -FilePath $cerOutputFile | Out-Null

Write-Host "=== MSIX Build & Signing Completed Successfully! ===" -ForegroundColor Green
Write-Host "MSIX File: $msixOutputFile"
Write-Host "Public Cert: $cerOutputFile"
