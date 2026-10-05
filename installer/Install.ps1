# Installe Uber Eats Overlay pour l'utilisateur courant (pas besoin d'admin) :
# extrait app.zip dans %LocalAppData%\Programs\UberOverlay, crée les raccourcis menu Démarrer + bureau,
# l'entrée "Paramètres > Applications" et le démarrage automatique avec Windows.
# Messages en français, anglais ou espagnol selon la langue de Windows.
# NB : ce fichier doit rester en UTF-8 avec BOM (build.ps1 s'en charge) pour que PowerShell 5.1 lise les accents.
Add-Type -AssemblyName System.Windows.Forms

$lang = [System.Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName
if ($lang -notin 'fr', 'es') { $lang = 'en' }
$T = @{
    fr = @{ title = 'Installation de Uber Eats Overlay'; ask = 'Installer Uber Eats Overlay sur cet ordinateur ?'
            done = "Uber Eats Overlay est installé et lancé.`nIl apparaît dans le menu Démarrer et dans Paramètres > Applications."
            doneTitle = 'Installation terminée'; fail = "L'installation a échoué :" }
    en = @{ title = 'Uber Eats Overlay setup'; ask = 'Install Uber Eats Overlay on this computer?'
            done = "Uber Eats Overlay is installed and running.`nIt is in the Start menu and in Settings > Apps."
            doneTitle = 'Installation complete'; fail = 'Installation failed:' }
    es = @{ title = 'Instalación de Uber Eats Overlay'; ask = '¿Instalar Uber Eats Overlay en este equipo?'
            done = "Uber Eats Overlay está instalado y en ejecución.`nAparece en el menú Inicio y en Configuración > Aplicaciones."
            doneTitle = 'Instalación completada'; fail = 'La instalación ha fallado:' }
}[$lang]

$answer = [System.Windows.Forms.MessageBox]::Show($T.ask, $T.title, 'YesNo', 'Question')
if ($answer -ne 'Yes') { return }

try {
    $ErrorActionPreference = 'Stop'
    $src = $PSScriptRoot
    $dst = Join-Path $env:LOCALAPPDATA 'Programs\UberOverlay'

    Get-Process UberOverlay -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    New-Item -ItemType Directory -Force $dst | Out-Null
    Expand-Archive -LiteralPath "$src\app.zip" -DestinationPath $dst -Force
    Copy-Item "$src\Uninstall.ps1" $dst -Force

    $exe = Join-Path $dst 'UberOverlay.exe'
    $shell = New-Object -ComObject WScript.Shell
    foreach ($dir in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop'))) {
        $lnk = $shell.CreateShortcut((Join-Path $dir 'Uber Eats Overlay.lnk'))
        $lnk.TargetPath = $exe
        $lnk.WorkingDirectory = $dst
        $lnk.Save()
    }

    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UberOverlay'
    New-Item -Force $key | Out-Null
    Set-ItemProperty $key DisplayName 'Uber Eats Overlay'
    Set-ItemProperty $key DisplayVersion '1.0.0'
    Set-ItemProperty $key Publisher 'Paul'
    Set-ItemProperty $key DisplayIcon $exe
    Set-ItemProperty $key InstallLocation $dst
    Set-ItemProperty $key UninstallString "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$dst\Uninstall.ps1`""
    Set-ItemProperty $key NoModify 1 -Type DWord
    Set-ItemProperty $key NoRepair 1 -Type DWord

    # Démarrage automatique avec Windows (désactivable depuis le menu de l'icône)
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' UberOverlay "`"$exe`""

    Start-Process $exe
    [void][System.Windows.Forms.MessageBox]::Show($T.done, $T.doneTitle, 'OK', 'Information')
}
catch {
    [void][System.Windows.Forms.MessageBox]::Show("$($T.fail)`n$($_.Exception.Message)", $T.title, 'OK', 'Error')
}
