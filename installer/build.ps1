# Construit installer\out\UberOverlay-Setup.exe : un installeur autonome (IExpress, intégré à Windows).
# L'app est publiée "self-contained" : aucun runtime .NET requis sur le PC cible.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $PSScriptRoot 'work'
$out = Join-Path $PSScriptRoot 'out'
$app = Join-Path $work 'app'
$pkg = Join-Path $work 'pkg'

foreach ($d in $work, $out) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } ; New-Item -ItemType Directory -Force $d | Out-Null }
New-Item -ItemType Directory -Force $app, $pkg | Out-Null

dotnet publish "$root\UberOverlay.Net\UberOverlay.csproj" -c Release -r win-x64 --self-contained true -o $app
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish a échoué' }
Get-ChildItem $app -Include *.pdb, *.xml -Recurse | Remove-Item -Force

Compress-Archive -Path "$app\*" -DestinationPath "$pkg\app.zip" -CompressionLevel Optimal
Copy-Item "$PSScriptRoot\Install.cmd" $pkg
# Scripts .ps1 en UTF-8 avec BOM : sinon PowerShell 5.1 lit les accents en ANSI et les casse
foreach ($f in 'Install.ps1', 'Uninstall.ps1') {
    $text = [IO.File]::ReadAllText("$PSScriptRoot\$f", [Text.Encoding]::UTF8)
    [IO.File]::WriteAllText("$pkg\$f", $text, (New-Object Text.UTF8Encoding $true))
}

$target = Join-Path $out 'UberOverlay-Setup.exe'
$sed = Join-Path $work 'setup.sed'
@"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=<None>
AdminQuietInstCmd=%AppLaunched%
UserQuietInstCmd=%AppLaunched%
SourceFiles=SourceFiles
[Strings]
TargetName=$target
FriendlyName=Uber Eats Overlay
AppLaunched=cmd.exe /c Install.cmd
FILE0="Install.cmd"
FILE1="Install.ps1"
FILE2="Uninstall.ps1"
FILE3="app.zip"
[SourceFiles]
SourceFiles0=$pkg\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
"@ | Set-Content -Path $sed -Encoding ASCII

& "$env:SystemRoot\System32\iexpress.exe" /N /Q $sed
# iexpress rend la main avant la fin : on attend l'apparition du fichier
$t = 0; while (-not (Test-Path $target) -and $t -lt 120) { Start-Sleep 1; $t++ }
Start-Sleep 3
if (-not (Test-Path $target)) { throw 'Installeur non généré' }
"{0} ({1:N1} Mo)" -f $target, ((Get-Item $target).Length / 1MB)
