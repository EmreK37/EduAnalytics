param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "EduAnalytics\src\EduAnalytics.UI\EduAnalytics.UI.csproj"
$artifacts = Join-Path $root "artifacts"
$publishDir = Join-Path $artifacts "publish\EduAnalytics"
$packageDir = Join-Path $artifacts "package"
$installerDir = Join-Path $artifacts "installer"
$installerTemplate = Join-Path $root "installer\install.cmd"
$setupSed = Join-Path $installerDir "EduAnalytics-Setup.sed"
$setupExe = Join-Path $installerDir "EduAnalytics-Setup-$Version.exe"
$zipPath = Join-Path $installerDir "EduAnalytics-Portable-$Version.zip"
$iexpressWorkDir = Join-Path "C:\tmp" "EduAnalyticsSetupBuild"
$iexpressSetup = Join-Path $iexpressWorkDir "EduAnalytics-Setup-$Version.exe"
$iexpressSed = Join-Path $iexpressWorkDir "setup.sed"

New-Item -ItemType Directory -Force -Path $publishDir, $packageDir, $installerDir | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Get-ChildItem -LiteralPath $packageDir -Force | Remove-Item -Recurse -Force
Copy-Item -LiteralPath (Join-Path $publishDir "EduAnalytics.UI.exe") -Destination (Join-Path $packageDir "EduAnalytics.UI.exe") -Force
Copy-Item -LiteralPath $installerTemplate -Destination (Join-Path $packageDir "install.cmd") -Force

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

if (Test-Path -LiteralPath $iexpressWorkDir) {
    Remove-Item -LiteralPath $iexpressWorkDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $iexpressWorkDir | Out-Null
Copy-Item -LiteralPath (Join-Path $packageDir "EduAnalytics.UI.exe") -Destination (Join-Path $iexpressWorkDir "EduAnalytics.UI.exe") -Force
Copy-Item -LiteralPath (Join-Path $packageDir "install.cmd") -Destination (Join-Path $iexpressWorkDir "install.cmd") -Force

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=1
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=EduAnalytics kurulumu baslatilsin mi?
DisplayLicense=
FinishMessage=EduAnalytics kurulumu tamamlandi.
TargetName=$iexpressSetup
FriendlyName=EduAnalytics Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=install.cmd
UserQuietInstCmd=install.cmd
SourceFiles=SourceFiles

[Strings]
FILE0=install.cmd
FILE1=EduAnalytics.UI.exe

[SourceFiles]
SourceFiles0=$iexpressWorkDir\

[SourceFiles0]
%FILE0%=
%FILE1%=
"@

Set-Content -LiteralPath $setupSed -Value $sed -Encoding ASCII
Set-Content -LiteralPath $iexpressSed -Value $sed -Encoding ASCII

$iexpress = Get-Command iexpress.exe -ErrorAction SilentlyContinue
if ($iexpress) {
    if (Test-Path -LiteralPath $setupExe) {
        Remove-Item -LiteralPath $setupExe -Force
    }

    & $iexpress.Source /N $iexpressSed
    Start-Sleep -Seconds 2
    Get-Process iexpress, makecab -ErrorAction SilentlyContinue | Wait-Process -Timeout 360

    if (Test-Path -LiteralPath $iexpressSetup) {
        Copy-Item -LiteralPath $iexpressSetup -Destination $setupExe -Force
    }
    else {
        Write-Warning "IExpress setup exe uretmedi. Portable zip hazir."
    }
}
else {
    Write-Warning "iexpress.exe bulunamadi. Setup EXE olusturulamadi; portable zip hazir."
}

Write-Host ""
Write-Host "Release paketleri hazir:"
Write-Host "  Publish: $publishDir"
Write-Host "  Portable ZIP: $zipPath"
if (Test-Path -LiteralPath $setupExe) {
    Write-Host "  Setup EXE: $setupExe"
}
