# Windows Sandbox ICHIDA ishlaydi (LogonCommand). run_sandbox_e2e.ps1 tayyorlagan C:\e2e-in dan nusxa olib,
# sinovni yurgizadi va natijani C:\e2e-out ga (xost papkasi) yozadi. Faqat ASCII - BOM'siz ham to'g'ri o'qilsin.
$ErrorActionPreference = 'Continue'
$In = 'C:\e2e-in'; $Out = 'C:\e2e-out'; $Local = 'C:\e2e'
Start-Transcript -Path (Join-Path $Out 'boot.log') -Force | Out-Null
try {
    . (Join-Path $In 'params.ps1')   # $E2EArgs (Limit, Only, ...), $KeepOpen

    # Klaviatura tartibi: sinov inglizcha tugmalar bilan yozadi (VkKeyScan)
    try { Set-WinUserLanguageList -LanguageList en-US -Force -ErrorAction Stop } catch { "Til o'rnatilmadi: $_" }

    # Edge birinchi ishga tushirish oynalari sinovga xalaqit bermasin (faqat shu Sandbox ichida)
    $pol = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
    New-Item -Path $pol -Force | Out-Null
    foreach ($kv in @{ HideFirstRunExperience = 1; DefaultBrowserSettingEnabled = 0; StartupBoostEnabled = 0; BackgroundModeEnabled = 0 }.GetEnumerator()) {
        New-ItemProperty -Path $pol -Name $kv.Key -Value $kv.Value -PropertyType DWord -Force | Out-Null
    }

    # Skript va exe yoziladigan joyga: C:\e2e\tools\e2e (skript work\ va results\ ni o'z yonida yaratadi)
    $dst = Join-Path $Local 'tools\e2e'
    New-Item -ItemType Directory -Force $dst | Out-Null
    Copy-Item (Join-Path $In 'autotype_e2e.ps1') $dst -Force
    Copy-Item (Join-Path $In 'Ozbeca.exe') $Local -Force

    Start-Sleep -Seconds 10   # ish stoli to'liq yuklansin
    "Sinov boshlandi: $(Get-Date -Format s); parametrlar: $($E2EArgs | Out-String)"
    $script = Join-Path $dst 'autotype_e2e.ps1'
    & $script -Exe (Join-Path $Local 'Ozbeca.exe') @E2EArgs *>&1 | Tee-Object -FilePath (Join-Path $Out 'console.log')
    "Sinov tugadi: $(Get-Date -Format s)"

    # UI skrinshoti: Sozlamalar oynasi (ikkinchi ishga tushirish ishlab turgan nusxaga "sozlamalarni och" deydi)
    try {
        Start-Process (Join-Path $Local 'Ozbeca.exe'); Start-Sleep -Seconds 6
        Start-Process (Join-Path $Local 'Ozbeca.exe'); Start-Sleep -Seconds 5
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $b = [Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height
        [Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Location, [Drawing.Point]::Empty, $b.Size)
        $bmp.Save((Join-Path $Out 'settings.png'))
    } catch { "Skrinshot olinmadi: $_" }

    $res = Join-Path $dst 'results'
    if (Test-Path $res) { Copy-Item (Join-Path $res '*') $Out -Force -ErrorAction SilentlyContinue }
    $log = Join-Path $env:APPDATA 'Ozbeca\ozbeca.log'
    if (Test-Path $log) { Copy-Item $log $Out -Force }
}
catch { "XATO: $_`n$($_.ScriptStackTrace)" }
finally {
    Stop-Transcript | Out-Null
    Set-Content -Path (Join-Path $Out 'DONE.txt') -Value (Get-Date -Format s)
    if (-not $KeepOpen) { Start-Sleep -Seconds 3; Stop-Computer -Force }
}
