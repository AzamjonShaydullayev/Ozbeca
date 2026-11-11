<#
  Avto-yozish E2E sinovini Windows Sandbox ichida yurgizadi - xost klaviaturasi, sichqonchasi va clipboard'iga tegmaydi.

  Talab: Windows Sandbox yoqilgan (Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All).
  Sandbox'da faqat Edge bor (Notepad, Chrome, Word, VS Code yo'q): Chrome sinovlari Edge'da yuradi, Notepad o'rniga
  skript o'zi yig'adigan Win32 matn oynasi ("Native edit") ishlatiladi.

  Ishga tushirish (repo ildizidan):
    dotnet build src\Ozbeca.App -c Release
    powershell -ExecutionPolicy Bypass -File tools\e2e\sandbox\run_sandbox_e2e.ps1 [-Limit 15] [-Only 'Native,Chrome'] [-KeepOpen]

  Sandbox oynasi ochiladi va sinov tugagach o'zi yopiladi. Oynani kichraytirmang (minimize) va ichiga bosmang.
  Natija: tools\e2e\results\sandbox_<vaqt>\ (hisobot .md, console.log, boot.log, ozbeca.log).
  Faqat ASCII - BOM'siz ham to'g'ri o'qilsin.
#>
param(
    [int]$Limit = 0,                    # 0 - hamma jumlalar
    [string]$Only = 'Native,Chrome',    # Sandbox'da bor: skript yig'adigan Win32 matn oynasi va Edge (Chrome o'rnida)
    [int]$PwRepeat = 1,                 # parol maydoni sinovi necha marta (har safar brauzer yangidan); faqat shuni: -Only parol
    [string]$Exe = '',
    [int]$TimeoutMin = 60,
    [switch]$KeepOpen,                  # sinovdan keyin Sandbox yopilmasin (qo'lda ko'rish uchun)
    [switch]$NoWait                     # ishga tushirib, kutmasdan chiqish
)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$E2E = Split-Path -Parent $Here
$Root = Resolve-Path (Join-Path $E2E '..\..')
if (-not $Exe) { $Exe = Join-Path $Root 'src\Ozbeca.App\bin\Release\net48\Ozbeca.exe' }
if (-not (Test-Path $Exe)) { Write-Host "XATO: $Exe topilmadi. Avval: dotnet build src\Ozbeca.App -c Release" -ForegroundColor Red; exit 1 }
$sandboxExe = Join-Path $env:WINDIR 'System32\WindowsSandbox.exe'
if (-not (Test-Path $sandboxExe)) { Write-Host "XATO: Windows Sandbox yoqilmagan." -ForegroundColor Red; exit 1 }
if (Get-Process WindowsSandbox, WindowsSandboxClient, WindowsSandboxRemoteSession -ErrorAction SilentlyContinue) {
    Write-Host "XATO: Sandbox allaqachon ochiq - avval uni yoping." -ForegroundColor Red; exit 1
}

$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$In = Join-Path $E2E 'work\sandbox-in'
$Out = Join-Path $E2E "results\sandbox_$Stamp"
if (Test-Path $In) { Remove-Item $In -Recurse -Force }
New-Item -ItemType Directory -Force $In, $Out | Out-Null

Copy-Item $Exe (Join-Path $In 'Ozbeca.exe')
Copy-Item (Join-Path $E2E 'autotype_e2e.ps1') $In
Copy-Item (Join-Path $Here 'boot.ps1') $In
$onlyArg = $Only -replace "'", "''"
@"
`$E2EArgs = @{ Limit = $Limit; Only = '$onlyArg'; PwRepeat = $PwRepeat }
`$KeepOpen = `$$([bool]$KeepOpen)
"@ | Set-Content -Path (Join-Path $In 'params.ps1') -Encoding ASCII

# Tarmoq va clipboard ulanmaydi: sinov clipboard'dan ko'p foydalanadi - xostnikiga tegmasin.
$wsb = Join-Path $In 'ozbeca_e2e.wsb'
@"
<Configuration>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <PrinterRedirection>Disable</PrinterRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$In</HostFolder><SandboxFolder>C:\e2e-in</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$Out</HostFolder><SandboxFolder>C:\e2e-out</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\e2e-in\boot.ps1</Command>
  </LogonCommand>
</Configuration>
"@ | Set-Content -Path $wsb -Encoding UTF8

$ver = (Get-Item $Exe).VersionInfo.ProductVersion
Write-Host "Sandbox ishga tushmoqda: $ver; Only=$Only; Limit=$Limit" -ForegroundColor Cyan
Write-Host "Natija papkasi: $Out"
Start-Process $sandboxExe -ArgumentList "`"$wsb`""
if ($NoWait) { exit 0 }

$done = Join-Path $Out 'DONE.txt'
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not (Test-Path $done)) {
    if ($sw.Elapsed.TotalMinutes -gt $TimeoutMin) { Write-Host "XATO: $TimeoutMin daqiqada tugamadi. Sandbox oynasini tekshiring." -ForegroundColor Red; exit 2 }
    Start-Sleep -Seconds 5
}
Write-Host "Tugadi: $([int]$sw.Elapsed.TotalMinutes) daqiqa $($sw.Elapsed.Seconds) soniya" -ForegroundColor Green
$report = Get-ChildItem $Out -Filter 'autotype_*.md' | Sort-Object LastWriteTime | Select-Object -Last 1
if ($report) {
    Get-Content $report.FullName -Encoding UTF8 | Select-Object -First 25
    Write-Host "Hisobot: $($report.FullName)"
} else {
    Write-Host "Hisobot topilmadi - boot.log va console.log ni ko'ring: $Out" -ForegroundColor Yellow
    exit 3
}
