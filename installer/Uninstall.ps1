$ErrorActionPreference = 'SilentlyContinue'
$dst = Join-Path $env:LOCALAPPDATA 'Programs\UberOverlay'
Get-Process UberOverlay | Stop-Process -Force
foreach ($dir in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop'))) {
    Remove-Item (Join-Path $dir 'Uber Eats Overlay.lnk') -Force
}
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UberOverlay' -Recurse -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name UberOverlay -Force
# Se supprime après la fermeture de ce script (il tourne depuis le dossier à effacer)
Start-Process cmd.exe -WindowStyle Hidden -ArgumentList "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q `"$dst`""
# Les réglages et la session Uber restent dans %AppData%\UberOverlay (supprime ce dossier pour tout effacer)
