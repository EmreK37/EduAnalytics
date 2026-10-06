@echo off
setlocal

set "APP_NAME=EduAnalytics"
set "EXE_NAME=EduAnalytics.UI.exe"
set "INSTALL_DIR=%LOCALAPPDATA%\Programs\EduAnalytics"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\EduAnalytics"
set "DESKTOP_LINK=%USERPROFILE%\Desktop\EduAnalytics.lnk"
set "START_LINK=%START_MENU_DIR%\EduAnalytics.lnk"
set "UNINSTALL_CMD=%INSTALL_DIR%\uninstall.cmd"

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%START_MENU_DIR%" mkdir "%START_MENU_DIR%"

copy /Y "%~dp0%EXE_NAME%" "%INSTALL_DIR%\%EXE_NAME%" >nul

(
  echo @echo off
  echo taskkill /IM "%EXE_NAME%" /F ^>nul 2^>nul
  echo del "%DESKTOP_LINK%" ^>nul 2^>nul
  echo del "%START_LINK%" ^>nul 2^>nul
  echo reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /f ^>nul 2^>nul
  echo rmdir /S /Q "%INSTALL_DIR%" ^>nul 2^>nul
) > "%UNINSTALL_CMD%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws=New-Object -ComObject WScript.Shell; $s=$ws.CreateShortcut('%DESKTOP_LINK%'); $s.TargetPath='%INSTALL_DIR%\%EXE_NAME%'; $s.WorkingDirectory='%INSTALL_DIR%'; $s.IconLocation='%INSTALL_DIR%\%EXE_NAME%'; $s.Save(); $s=$ws.CreateShortcut('%START_LINK%'); $s.TargetPath='%INSTALL_DIR%\%EXE_NAME%'; $s.WorkingDirectory='%INSTALL_DIR%'; $s.IconLocation='%INSTALL_DIR%\%EXE_NAME%'; $s.Save()"

reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /v "DisplayName" /d "EduAnalytics" /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /v "DisplayVersion" /d "1.0.0" /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /v "Publisher" /d "EduAnalytics" /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /v "InstallLocation" /d "%INSTALL_DIR%" /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /v "DisplayIcon" /d "%INSTALL_DIR%\%EXE_NAME%" /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EduAnalytics" /v "UninstallString" /d "\"%UNINSTALL_CMD%\"" /f >nul

sqllocaldb info MSSQLLocalDB >nul 2>nul
if errorlevel 1 (
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('EduAnalytics kuruldu, ancak bu bilgisayarda SQL Server LocalDB bulunamadi veya MSSQLLocalDB instance calismiyor. Uygulamayi acmadan once SQL Server Express LocalDB kurun.', 'EduAnalytics - LocalDB Gerekli', 'OK', 'Warning')"
)

start "" "%INSTALL_DIR%\%EXE_NAME%"
endlocal
