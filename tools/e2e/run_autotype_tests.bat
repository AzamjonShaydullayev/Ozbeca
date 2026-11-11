@echo off
chcp 65001 >nul
title Ozbeca — avto-yozish sinovi
cd /d "%~dp0..\.."

echo.
echo  ============================================================
echo   Ozbeca — "yozish paytida o'girish" sinovi
echo  ============================================================
echo   * Notepad, Chrome va Word o'zi ochiladi, matnlar o'zi yoziladi.
echo   * Sinov ~25-35 daqiqa davom etadi.
echo   * Bu vaqtda klaviatura va sichqonchaga TEGMANG.
echo   * Klaviatura tartibi ENG (inglizcha) bo'lsin.
echo   * To'xtatish uchun: Esc
echo  ============================================================
echo.
pause

echo  [1/2] Dastur yig'ilmoqda (Release)...
dotnet build src\Ozbeca.App -c Release -v q -nologo
if errorlevel 1 (
  echo  XATO: build muvaffaqiyatsiz.
  pause
  exit /b 1
)

echo  [2/2] Sinov boshlandi...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0autotype_e2e.ps1"

echo.
echo  Tugadi. Natija: tools\e2e\results\ papkasida (oxirgisi LATEST.txt da).
pause
