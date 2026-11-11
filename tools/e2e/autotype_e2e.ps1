<#
  Özbeça — "yozish paytida o'girish" uchun real ilovalardagi avtomatik sinov.

  Ishga tushirish: run_autotype_tests.bat (yoki: powershell -ExecutionPolicy Bypass -File autotype_e2e.ps1)
  Sinov paytida klaviatura va sichqonchaga TEGMANG. To'xtatish: Esc.

  Har bir ilovada har bir jumla odamga o'xshab (harfma-harf, Shift bilan, tasodifiy kechikish) yoziladi,
  keyin Ctrl+A, Ctrl+C bilan o'qilib, kutilgan natija bilan solishtiriladi. Kutilgan natija dasturning o'z
  kodi (Ozbeca.Core.TokenBuffer + Transliterator) bilan hisoblanadi — qoidalar unit testlarda tekshiriladi,
  bu yerda esa hook, tugma yuborish va ilovalar bilan ishlash tekshiriladi.

  Natija: tools\e2e\results\autotype_<vaqt>.md  (oxirgisining yo'li: results\LATEST.txt)
#>
param(
    [int]$Delay = 90,          # oddiy tezlik (ms, harflar orasida)
    [int]$FastDelay = 35,      # tez yozuvchi
    [string]$Exe = '',
    [switch]$DryRun,           # ilovalarni ochmasdan: jumlalar va kutilgan natijalarni ko'rsatadi
    [int]$Limit = 0,           # 0 — hammasi; aks holda faqat birinchi N jumla (tez tekshiruv uchun)
    [string]$Only = ''         # faqat shu ilova(lar), masalan: -Only VS   yoki   -Only Notepad
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $Here '..\..')
if (-not $Exe) { $Exe = Join-Path $Root 'src\Ozbeca.App\bin\Release\net48\Ozbeca.exe' }
$Work = Join-Path $Here 'work'; $Results = Join-Path $Here 'results'
New-Item -ItemType Directory -Force $Work, $Results | Out-Null
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Report = Join-Path $Results "autotype_$Stamp.md"

Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text; using System.Threading;
public static class E2E {
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
  [DllImport("user32.dll")] static extern short VkKeyScan(char ch);
  [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  static void K(byte vk, bool up) {
    uint f = up ? 2u : 0u;
    if (vk == 0x2E || (vk >= 0x21 && vk <= 0x28)) f |= 1;
    keybd_event(vk, (byte)MapVirtualKey(vk, 0), f, UIntPtr.Zero);
  }
  static readonly Random R = new Random(2026);
  public static bool Aborted;
  public static bool EscDown() { if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) Aborted = true; return Aborted; }
  public static string FgTitle() { var sb = new StringBuilder(512); GetWindowText(GetForegroundWindow(), sb, 512); return sb.ToString(); }
  // mods: 1=Ctrl 2=Alt 4=Shift
  public static void Chord(int mods, byte key) {
    if ((mods & 1) != 0) K(0x11, false); if ((mods & 2) != 0) K(0x12, false); if ((mods & 4) != 0) K(0x10, false);
    K(key, false); K(key, true);
    if ((mods & 4) != 0) K(0x10, true); if ((mods & 2) != 0) K(0x12, true); if ((mods & 1) != 0) K(0x11, true);
  }
  // Odamga o'xshash yozish. '\b' = Backspace, '\n' = Enter, '\u2190' = chap strelka, '\u2192' = o'ng strelka.
  public static bool Type(string text, int delayMs, int jitterMs) {
    foreach (char c in text) {
      if (EscDown()) return false;
      short s;
      if (c == '\n') s = 0x0D; else if (c == '\b') s = 0x08; else if (c == '\u2190') s = 0x25; else if (c == '\u2192') s = 0x27;
      else s = VkKeyScan(c);
      if (s == -1) throw new ArgumentException("Yozib bo'lmaydigan belgi: " + c);
      byte vk = (byte)(s & 0xFF); bool shift = (s & 0x100) != 0;
      if (shift) K(0x10, false);
      K(vk, false); Thread.Sleep(12); K(vk, true);
      if (shift) K(0x10, true);
      Thread.Sleep(Math.Max(5, delayMs + R.Next(-jitterMs, jitterMs + 1)));
    }
    return true;
  }
}
"@
$ws = New-Object -ComObject WScript.Shell
function Wait-Ms([int]$ms) { Start-Sleep -Milliseconds $ms }
function Read-All {
    Set-Clipboard -Value '__E2E__'
    [E2E]::Chord(1, 0x41); Wait-Ms 300      # Ctrl+A
    [E2E]::Chord(1, 0x43); Wait-Ms 500      # Ctrl+C
    $t = Get-Clipboard -Raw
    if ($t -eq '__E2E__') { return $null }
    return $t
}
function Clear-Field { [E2E]::Chord(1, 0x41); Wait-Ms 150; [E2E]::Chord(0, 0x2E); Wait-Ms 250 } # Ctrl+A, Delete

# ---------------- Tekshiruvlar ----------------
if (-not (Test-Path $Exe)) { Write-Host "XATO: $Exe topilmadi. Avval: dotnet build src\Ozbeca.App -c Release" -ForegroundColor Red; exit 1 }

# ---------------- Kutilgan natija: dasturning o'z kodi ----------------
[void][Reflection.Assembly]::LoadFrom($Exe)
function Get-ExpectedCode([string]$typed) {
    $tb = New-Object Ozbeca.Core.TokenBuffer
    $tb.CodeMode = $true                       # oyna almashganda — holat noma'lum (Enter'gacha hech narsa o'girilmaydi)
    $out = New-Object System.Text.StringBuilder
    foreach ($c in $typed.ToCharArray()) {
        $del = 0; $rep = ''
        if ($c -eq ' ') {
            if ($tb.TryComplete([ref]$del, [ref]$rep)) { [void]$out.Remove($out.Length - $del, $del); [void]$out.Append($rep) }
            [void]$out.Append(' ')
        }
        elseif ($c -eq "`n") { $tb.ResetNewLine(); [void]$out.Append("`n") }
        elseif ($c -eq "`b") { $tb.Backspace(); if ($out.Length) { [void]$out.Remove($out.Length - 1, 1) } }
        elseif ($c -eq '"' -and $tb.TryCompleteBeforeQuote([ref]$del, [ref]$rep)) {
            [void]$out.Remove($out.Length - $del, $del); [void]$out.Append($rep).Append('"')
        }
        else { $tb.Add($c); [void]$out.Append($c) }
    }
    return $out.ToString()
}

function Get-Expected([string]$typed) {
    $tb = New-Object Ozbeca.Core.TokenBuffer
    $out = New-Object System.Text.StringBuilder
    foreach ($c in $typed.ToCharArray()) {
        switch ($c) {
            ' ' {
                $del = 0; $rep = ''
                if ($tb.TryComplete([ref]$del, [ref]$rep)) { [void]$out.Remove($out.Length - $del, $del); [void]$out.Append($rep) }
                [void]$out.Append(' ')
            }
            "`b" { $tb.Backspace(); if ($out.Length) { [void]$out.Remove($out.Length - 1, 1) } }
            "`n" {
                $del = 0; $rep = ''
                if ($tb.TryCompleteAtEnter([ref]$del, [ref]$rep)) { [void]$out.Remove($out.Length - $del, $del); [void]$out.Append($rep) }
                [void]$out.Append("`n")
            }
            ([char]0x2190) { $tb.Reset() }   # strelka: so'z unutiladi, matn o'zgarmaydi (kursor qaytadi)
            ([char]0x2192) { $tb.Reset() }
            default { $tb.Add($c); [void]$out.Append($c) }
        }
    }
    return $out.ToString()
}

# ---------------- Test jumlalari ----------------
$L = [char]0x2190; $R = [char]0x2192
$Cases = [System.Collections.Generic.List[string]]::new()
@(
    "choy ", "shahar ", "O'zbekiston ", "g'isht ", "qo'shiq ",
    "O'zbekiston Respublikasi poytaxti Toshkent shahri ",
    "Bugun choy ichdik va shirin qo'shiq kuyladik ",
    "Chorshanba kuni bog'da shaftoli terdik ",
    "SHAHAR CHOY O'ZBEK G'ALABA ",
    "Choy Shahar O'zbek G'isht Shoir ",
    "Is'hoq va Ishoq as'hob ashob ",
    "ma'no san'at she'r e'lon ta'lim jur'at ",
    "tong keng dengiz singil rang ko'ngil ",
    "Bu 'kino' edi 'tong' bo'ldi ",
    "https://shop.uz/choy saytini och ",
    "www.shahar.uz/choy manzili ",
    "info@shahar.uz ga yoz ",
    "@shohruh #choyxona kanali ",
    "2026-yil 21-oktabr choyxonada ",
    "Narxi 15000 so'm, chegirma 10% ",
    "Salom! Qalaysan? Yaxshimisan? ",
    "(qavs ichida shahar) [kvadrat choy] ",
    "o``rta g``alaba ",
    "choy, shahar. o'zbek; bog': ",
    "choy  shahar  ikki probel ",
    "shop check short which ",
    "choyx`b shaharr`b qo'shiqq`b ",
    "choy`nshahar ",
    "Yaxshimisan?`n",
    "salom choy`nqalaysan shirin`n",
    "Bugun shahar bo'ylab sayr qildik`n",
    ("sh$L$R" + "ahar choy "),
    "maktab kitob daftar olma non ",
    "To'g'ri yo'l, to'g'ri so'z, to'g'ri ish ",
    "Farg'ona Qo'qon Chirchiq Shahrisabz Qashqadaryo ",
    "cho'l sho'rva cho'chqa sho'x ",
    "O'ZBEKISTON CHO'L SHO'RVA ",
    "a b c d e f g h ",
    "s h c h o ' g ' "
) | ForEach-Object { $Cases.Add($_) }   # `b (Backspace), `n (Enter), `` (backtick) PowerShell qatorlarida allaqachon belgi

# HANDOFF/Core testlaridagi so'zlar — 5 tadan jumla qilib
$words = "o'g'il qo'l ko'z yo'l to'qson bo'lim o'qituvchi o'quvchi ko'cha go'sht do'st to'y so'z o'rik o'n ko'p yo'q bo'sh qo'shni o'zbekcha to'g'ri o'g'ri bo'g'iz g'oz g'alaba g'arb g'ildirak tog' bog' sog' yog' bag'ir tog'lar bog'bon sog'lik yog'och chiroq choynak chaqaloq chumoli chiroyli kuch qilich bolacha ich chol chap uch sichqon qarshi shamol shaftoli shirin osh ish qush tosh yosh bosh kishi Toshkent Samarqand Buxoro Andijon Namangan e'tibor qal'a ra'no sur'at ming bilan Shoshilinch shanba chorva o'shanda g'ishtchi qo'shiqchi o'quvchilar".Split(' ')
for ($i = 0; $i -lt $words.Count; $i += 5) { $Cases.Add((($words[$i..([Math]::Min($i + 4, $words.Count - 1))]) -join ' ') + ' ') }

if ($Limit -gt 0) { $Cases = [System.Collections.Generic.List[string]]($Cases | Select-Object -First $Limit) }

if ($DryRun) {
    $show = { param($x) $x -replace "`n", '⏎' -replace "`b", '⌫' -replace [char]0x2190, '←' -replace [char]0x2192, '→' }
    foreach ($c in $Cases) { "{0,-60} => {1}" -f (& $show $c), (& $show (Get-Expected $c)) }
    "JAMI jumla: $($Cases.Count)"
    exit 0
}

# Ctrl+V bilan qo'yiladigan matnlar: ko'p so'zli — o'girilishi kerak; bitta so'z — o'zgarmasligi kerak (parol himoyasi)
$long = (1..6 | ForEach-Object { "Bugun shahar bo'ylab sayr qildik, choyxonada osh yedik va qo'shiq tingladik." }) -join ' '
$PasteCases = @(
    "Bugun shahar bo'ylab sayr qildik. Choyxonada osh yedik.",
    "O'zbekiston Respublikasi`nchoy va shirin qo'shiq`nhttps://shop.uz/choy sayti",
    $long,
    "Is'hoq, ma'no va g'isht: SHAHAR CHOY",
    "shahar123",
    "Özbekiston allaqachon yangi alifboda"
)
function Get-PasteExpected([string]$t) {
    if ($t -notmatch '\s') { return $t }
    return [Ozbeca.Core.Transliterator]::ToNewAlphabet($t)
}

# ---------------- Ilovalar ----------------
function Start-Notepad { $f = Join-Path $Work "e2e_$Stamp.txt"; [IO.File]::WriteAllText($f, ''); Start-Process notepad.exe $f; Wait-Ms 2500; [void]$ws.AppActivate("e2e_$Stamp"); Wait-Ms 600 }
function Stop-Notepad { [E2E]::Chord(1, 0x53); Wait-Ms 400; [E2E]::Chord(1, 0x57); Wait-Ms 600 }   # Ctrl+S, Ctrl+W
$ChromeExe = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$ChromeProfile = Join-Path $Work 'chrome-profile'
function Start-Chrome([string]$kind) {
    $html = Join-Path $Work "e2e_$kind.html"
    $tag = if ($kind -eq 'input') { "<input id=f style='width:900px;font-size:18px'>" } else { "<textarea id=f cols=90 rows=8 style='font-size:18px'></textarea>" }
    [IO.File]::WriteAllText($html, "<!doctype html><meta charset=utf-8><title>e2e-$kind</title>$tag<script>f.focus()</script>", [Text.UTF8Encoding]::new($true))
    Start-Process $ChromeExe -ArgumentList "--user-data-dir=`"$ChromeProfile`"", '--no-first-run', '--new-window', "file:///$($html -replace '\\','/')"
    Wait-Ms 4000; [void]$ws.AppActivate("e2e-$kind"); Wait-Ms 700
}
function Stop-Chrome { Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" | Where-Object { $_.CommandLine -like '*chrome-profile*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }; Wait-Ms 800 }
function Start-Word {
    $doc = Join-Path $Work "e2e_$Stamp.docx"; python -I (Join-Path $Here 'mkdocx.py') $doc ' '
    Start-Process $doc; Wait-Ms 9000; [void]$ws.AppActivate("e2e_$Stamp"); Wait-Ms 800
}
function Stop-Word { Get-Process WINWORD -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*e2e_$Stamp*" } | Stop-Process; Wait-Ms 1000 }

$Apps = @(
    @{ Name = 'Notepad';         Title = "e2e_$Stamp";   Start = { Start-Notepad };           Stop = { Stop-Notepad }; Enter = $true;  Relaxed = $false },
    @{ Name = 'Chrome textarea'; Title = 'e2e-textarea'; Start = { Start-Chrome 'textarea' }; Stop = { Stop-Chrome };  Enter = $true;  Relaxed = $false },
    @{ Name = 'Chrome input';    Title = 'e2e-input';    Start = { Start-Chrome 'input' };    Stop = { Stop-Chrome };  Enter = $false; Relaxed = $false },
    @{ Name = 'Word';            Title = "e2e_$Stamp";   Start = { Start-Word };              Stop = { Stop-Word };    Enter = $true;  Relaxed = $true }
)

function Normalize([string]$s, [bool]$relaxed) {
    if ($null -eq $s) { return $null }
    $s = $s -replace "`r`n", "`n" -replace "`r", "`n"
    $s = $s.TrimEnd("`n")
    if ($relaxed) {   # Word: aqlli tirnoqlar va gap boshidagi katta harf (Word'ning o'z avto-tuzatishi)
        $s = $s -replace '[\u2018\u2019]', "'" -replace '[\u201C\u201D]', '"'
        $s = $s.ToLowerInvariant()
        # Word nusxa olganda oxiriga bo'sh paragraf qo'shadi va "aqlli probel" bilan probelni ikkilantiradi
        $s = ($s -replace '[ \t]+', ' ' -replace ' ?\n ?', "`n").Trim()
    }
    return $s
}

# ---------------- Ishga tushirish ----------------
$session = (query session 2>$null) -match '^>' | Out-String
if ($session -notmatch 'Active') { Write-Host "XATO: Windows sessiyasi faol emas (masofadan uzilgan). Kompyuter oldida ishga tushiring." -ForegroundColor Red; exit 1 }
Write-Host ""
Write-Host "  Ozbeca avto-yozish sinovi: $($Cases.Count) jumla x $($Apps.Count) ilova (+ tez yozish)" -ForegroundColor Cyan
Write-Host "  Klaviatura va sichqonchaga TEGMANG. To'xtatish: Esc." -ForegroundColor Yellow
Write-Host ""
Start-Sleep 3

# Sozlamalarni vaqtincha: avto-yozish yoqilgan holda
$SettingsDir = Join-Path $env:APPDATA 'Ozbeca'; $SettingsFile = Join-Path $SettingsDir 'settings.json'; $Backup = Join-Path $Work 'settings.backup.json'
$hadSettings = Test-Path $SettingsFile
if ($hadSettings) { Copy-Item $SettingsFile $Backup -Force }
New-Item -ItemType Directory -Force $SettingsDir | Out-Null
[IO.File]::WriteAllText($SettingsFile, '{"AutoType":true,"Enabled":true,"Hotkey":"Ctrl+Alt+O","RestoreDelayMs":400,"ShowToast":true}', [Text.UTF8Encoding]::new($false))
$wasRunning = Get-Process Ozbeca -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Path
Get-Process Ozbeca -ErrorAction SilentlyContinue | Stop-Process; Wait-Ms 800
Remove-Item (Join-Path $SettingsDir 'ozbeca.log') -ErrorAction SilentlyContinue
Start-Process $Exe -ArgumentList 'Ctrl+Alt+O'; Wait-Ms 2500

$rows = [System.Collections.Generic.List[object]]::new()
$sw = [Diagnostics.Stopwatch]::StartNew()
try {
    foreach ($app in $Apps) {
        if ($Only -and $app.Name -notlike "*$Only*") { continue }
        foreach ($speed in @(@{ N = 'oddiy'; D = $Delay }, @{ N = 'tez'; D = $FastDelay })) {
            if ($speed.N -eq 'tez' -and $app.Relaxed) { continue }   # Word'da faqat oddiy tezlik
            if ([E2E]::Aborted) { break }
            Write-Host ("== {0} ({1}, {2} ms)" -f $app.Name, $speed.N, $speed.D) -ForegroundColor Cyan
            & $app.Start
            $pass = 0; $n = 0
            foreach ($typed in $Cases) {
                if ([E2E]::EscDown()) { break }
                if (-not $app.Enter -and $typed.Contains("`n")) { continue }
                $n++
                [void]$ws.AppActivate($app.Title); Wait-Ms 250
                Clear-Field
                $ok = [E2E]::Type($typed, $speed.D, [int]($speed.D * 0.4)); if (-not $ok) { break }
                Wait-Ms 700
                $window = [E2E]::FgTitle()
                $got = Read-All
                $exp = Get-Expected $typed
                $good = (Normalize $got $app.Relaxed) -eq (Normalize $exp $app.Relaxed)
                # Yozish paytida boshqa oyna oldinga chiqdi (user aralashdi / bildirishnoma) — xato emas, alohida
                $interfered = $window -notlike "*$($app.Title)*"
                if ($good) { $pass++ }
                $rows.Add([pscustomobject]@{ App = $app.Name; Speed = $speed.N; Kind = 'yozish'; Ok = $good; Interfered = $interfered; Typed = $typed; Expected = $exp; Got = $got; Window = $window })
                Write-Host -NoNewline $(if ($good) { '.' } elseif ($interfered) { '?' } else { 'X' }) -ForegroundColor $(if ($good) { 'Green' } elseif ($interfered) { 'Yellow' } else { 'Red' })
            }

            # Ctrl+V (faqat oddiy tezlikda; ko'p qatorli matn input'ga emas)
            if ($speed.N -eq 'oddiy') {
                foreach ($paste in $PasteCases) {
                    if ([E2E]::EscDown()) { break }
                    if (-not $app.Enter -and $paste.Contains("`n")) { continue }
                    $n++
                    [void]$ws.AppActivate($app.Title); Wait-Ms 250
                    Clear-Field
                    Set-Clipboard -Value $paste; Wait-Ms 300
                    [E2E]::Chord(1, 0x56); Wait-Ms 1200           # Ctrl+V; clipboard ~500 ms da tiklanadi
                    $restored = (Get-Clipboard -Raw) -eq $paste
                    $window = [E2E]::FgTitle()
                    $got = Read-All
                    $exp = Get-PasteExpected $paste
                    $good = ((Normalize $got $app.Relaxed) -eq (Normalize $exp $app.Relaxed)) -and $restored
                    $interfered = $window -notlike "*$($app.Title)*"
                    if ($good) { $pass++ }
                    $note = if (-not $restored) { '   [clipboard TIKLANMADI]' } else { '' }
                    $rows.Add([pscustomobject]@{ App = $app.Name; Speed = 'Ctrl+V'; Kind = 'paste'; Ok = $good; Interfered = $interfered; Typed = $paste + $note; Expected = $exp; Got = $got; Window = $window })
                    Write-Host -NoNewline $(if ($good) { '.' } elseif ($interfered) { '?' } else { 'X' }) -ForegroundColor $(if ($good) { 'Green' } elseif ($interfered) { 'Yellow' } else { 'Red' })
                }
            }
            Write-Host ("  {0}/{1}" -f $pass, $n)
            & $app.Stop
        }
    }

    # Parol maydoni: choy1990 + Enter o'zgarmasligi kerak (UIA IsPassword). Nusxa olib bo'lmaydi —
    # sahifa Enter'da qiymatni oyna sarlavhasiga chiqaradi.
    if (-not [E2E]::Aborted -and (-not $Only -or 'Chrome' -like "*$Only*")) {
        Write-Host "== Chrome parol maydoni (o'girmasligi kerak)" -ForegroundColor Cyan
        $html = Join-Path $Work 'e2e_pw.html'
        [IO.File]::WriteAllText($html, "<!doctype html><meta charset=utf-8><title>e2e-pw</title><input id=f type=password style='font-size:18px'><script>f.focus();f.addEventListener('keydown',e=>{if(e.key==='Enter'){document.title='pw:'+f.value;e.preventDefault();}})</script>", [Text.UTF8Encoding]::new($true))
        Start-Process $ChromeExe -ArgumentList "--user-data-dir=`"$ChromeProfile`"", '--no-first-run', '--new-window', "file:///$($html -replace '\\','/')"
        Wait-Ms 4000; [void]$ws.AppActivate('e2e-pw'); Wait-Ms 700
        foreach ($pw in @("choy1990`n", "shahar choy`n")) {
            Clear-Field
            [void][E2E]::Type($pw, $Delay, 30); Wait-Ms 1200
            $window = [E2E]::FgTitle()
            $exp = 'pw:' + $pw.TrimEnd("`n")
            $good = $window -like "$exp*"
            $rows.Add([pscustomobject]@{ App = 'Chrome parol maydoni'; Speed = 'oddiy'; Kind = 'parol'; Ok = $good; Interfered = ($window -notlike 'pw:*' -and $window -notlike 'e2e-pw*'); Typed = $pw; Expected = $exp; Got = $window; Window = $window })
            Write-Host -NoNewline $(if ($good) { '.' } else { 'X' }) -ForegroundColor $(if ($good) { 'Green' } else { 'Red' })
        }
        Write-Host ''
        Stop-Chrome
    }

    # VS Code: kod rejimi — faqat "..." ichi o'giriladi; kod, izoh va kalit so'zlar o'zgarmaydi.
    # .js fayl: VS Code qo'shtirnoq/qavslarni o'zi yopadi va yopuvchi belgi yozilganda ustidan yozadi.
    $code = Join-Path $env:LOCALAPPDATA 'Programs\Microsoft VS Code\Code.exe'
    $CodeCases = @(
        "`nconsole.log(`"Bugun choy ichdik`");`n",
        "`nlet msg = `"Toshkent shahri, O'zbekiston`";`n",
        "`nconst shahar_id = 5; // choy izoh`n",
        "`ntry { push(); } catch (e) { match(); }`n",
        "`nalert(`"shirin choy`" + name);`n",
        "x = `"choy shahar`";`n"            # Enter'siz boshlanadi: holat noma'lum — o'girilmasligi kerak
    )
    if ((Test-Path $code) -and -not [E2E]::Aborted -and (-not $Only -or 'VS Code' -like "*$Only*")) {
        Write-Host "== VS Code (kod rejimi: faqat qo'shtirnoq ichi)" -ForegroundColor Cyan
        $f = Join-Path $Work "e2e_vscode_$Stamp.js"; [IO.File]::WriteAllText($f, '')
        Start-Process $code -ArgumentList '-n', "`"$f`""; Wait-Ms 8000
        $title = "e2e_vscode_$Stamp"
        $pass = 0
        foreach ($typed in $CodeCases) {
            if ([E2E]::EscDown()) { break }
            [void]$ws.AppActivate($title); Wait-Ms 300
            Clear-Field
            # Oyna faollashganda holat noma'lum bo'lishi uchun: sichqonchasiz — boshqa oynaga o'tib-qaytish o'rniga
            # har keys "`n" bilan boshlanadi (oxirgisidan tashqari)
            [void][E2E]::Type($typed, $Delay, 30); Wait-Ms 800
            $window = [E2E]::FgTitle()
            $got = Read-All
            $exp = Get-ExpectedCode $typed
            $good = (Normalize $got $false).Trim() -eq (Normalize $exp $false).Trim()
            $interfered = $window -notlike "*$title*"
            if ($good) { $pass++ }
            $rows.Add([pscustomobject]@{ App = 'VS Code (kod rejimi)'; Speed = 'oddiy'; Kind = 'yozish'; Interfered = $interfered; Ok = $good; Typed = $typed; Expected = $exp; Got = $got; Window = $window })
            Write-Host -NoNewline $(if ($good) { '.' } elseif ($interfered) { '?' } else { 'X' }) -ForegroundColor $(if ($good) { 'Green' } elseif ($interfered) { 'Yellow' } else { 'Red' })
        }
        Write-Host ("  {0}/{1}" -f $pass, $CodeCases.Count)
        [E2E]::Chord(1, 0x53); Wait-Ms 400; [E2E]::Chord(5, 0x57); Wait-Ms 1000   # Ctrl+S, Ctrl+Shift+W
    }
}
finally {
    Get-Process Ozbeca -ErrorAction SilentlyContinue | Stop-Process
    Stop-Chrome
    if ($hadSettings) { Copy-Item $Backup $SettingsFile -Force } else { Remove-Item $SettingsFile -ErrorAction SilentlyContinue }
    if ($wasRunning) { Start-Process $wasRunning }
}

# ---------------- Hisobot ----------------
$total = $rows.Count; $okCount = @($rows | Where-Object Ok).Count
$interCount = @($rows | Where-Object { -not $_.Ok -and $_.Interfered }).Count
$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# Avto-yozish sinovi — $Stamp")
[void]$md.AppendLine("")
[void]$md.AppendLine("- Exe: ``$Exe`` ($((Get-Item $Exe).VersionInfo.ProductVersion))")
[void]$md.AppendLine("- Tezlik: oddiy $Delay ms, tez $FastDelay ms · Davomiyligi: $([int]$sw.Elapsed.TotalMinutes) daqiqa" + $(if ([E2E]::Aborted) { ' · **Esc bilan to''xtatildi**' } else { '' }))
[void]$md.AppendLine("- **JAMI: $okCount / $total o'tdi**" + $(if ($interCount) { " · $interCount tasi aralashuv sababli (boshqa oyna oldinga chiqqan) — xato emas" } else { '' }))
[void]$md.AppendLine("")
[void]$md.AppendLine("| Ilova | Tezlik | O'tdi |"); [void]$md.AppendLine("|---|---|---|")
$rows | Group-Object App, Speed | ForEach-Object { [void]$md.AppendLine("| $($_.Group[0].App) | $($_.Group[0].Speed) | $(@($_.Group | Where-Object Ok).Count) / $($_.Count) |") }
$fails = @($rows | Where-Object { -not $_.Ok } | Sort-Object Interfered)
if ($fails.Count) {
    [void]$md.AppendLine(""); [void]$md.AppendLine("## O'tmaganlar"); [void]$md.AppendLine("")
    foreach ($f in $fails) {
        $show = { param($s) if ($null -eq $s) { '(o''qilmadi)' } else { ($s -replace "`r", '' -replace "`n", '⏎' -replace "`b", '⌫') } }
        $warn = if ($f.Interfered) { ' · ⚠️ ARALASHUV' } else { '' }
        [void]$md.AppendLine("- **$($f.App)** ($($f.Speed))$warn · oyna: $($f.Window)")
        [void]$md.AppendLine("  - yozildi:  ``$(& $show $f.Typed)``")
        [void]$md.AppendLine("  - kutildi:  ``$(& $show $f.Expected)``")
        [void]$md.AppendLine("  - chiqdi:   ``$(& $show $f.Got)``")
    }
}
[void]$md.AppendLine(""); [void]$md.AppendLine("## Dastur logi"); [void]$md.AppendLine(""); [void]$md.AppendLine('```')
$log = Join-Path $SettingsDir 'ozbeca.log'
if (Test-Path $log) { Get-Content $log -Encoding UTF8 | Select-Object -Last 60 | ForEach-Object { [void]$md.AppendLine($_) } }
[void]$md.AppendLine('```')
[IO.File]::WriteAllText($Report, $md.ToString(), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Results 'LATEST.txt'), $Report, [Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "  JAMI: $okCount / $total o'tdi. Hisobot: $Report" -ForegroundColor $(if ($okCount -eq $total) { 'Green' } else { 'Yellow' })
