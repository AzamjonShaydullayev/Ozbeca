# Ozbeca.exe'ni kod imzolash sertifikati bilan imzolaydi — Windows SmartScreen "noma'lum dastur" deb ogohlantirmasin.
# Sertifikat (Authenticode, .pfx) muallifda bo'lishi kerak; u bu repoga HECH QACHON qo'shilmaydi.
#
#   pwsh tools/sign/Sign-Exe.ps1 -Pfx C:\keys\ozbeca.pfx
#   (parol so'raladi; yoki $env:OZBECA_PFX_PASSWORD)
#
# signtool — Windows SDK tarkibida (winget install Microsoft.WindowsSDK.10.0.22621).
param(
    [Parameter(Mandatory)] [string] $Pfx,
    [string] $Exe = (Join-Path $PSScriptRoot "../../src/Ozbeca.App/bin/Release/net48/Ozbeca.exe"),
    [string] $TimestampUrl = "http://timestamp.digicert.com"
)
$ErrorActionPreference = "Stop"

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw "signtool.exe topilmadi — Windows SDK o'rnating" }

$password = $env:OZBECA_PFX_PASSWORD
if (-not $password) {
    $secure = Read-Host "PFX paroli" -AsSecureString
    $password = [Runtime.InteropServices.Marshal]::PtrToStringUni([Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($secure))
}

# Vaqt tamg'asi (timestamp) — sertifikat muddati tugagandan keyin ham imzo haqiqiy qoladi.
& $signtool.FullName sign /fd SHA256 /f $Pfx /p $password /tr $TimestampUrl /td SHA256 /d "Ozbeca" $Exe
if ($LASTEXITCODE -ne 0) { throw "Imzolash muvaffaqiyatsiz ($LASTEXITCODE)" }
& $signtool.FullName verify /pa $Exe
if ($LASTEXITCODE -ne 0) { throw "Imzo tekshiruvi o'tmadi" }
Write-Host "Imzolandi: $Exe"
(Get-FileHash $Exe -Algorithm SHA256).Hash
