# Bot'ni linux-x64 uchun yig'adi: publish/bot/ (Ozbeca.Bot + libe_sqlite3.so + .env.example + ozbecabot.service + install.sh) va publish/ozbecabot.zip
# -EnvFrom <lokal .env>: undagi BOT_TOKEN/ADMIN_IDS/PREMIUM_EMOJI server yo'llari bilan zip ichidagi .env ga yoziladi (zip'ni hech kimga bermang).
param([string]$EnvFrom)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root
try {
    $out = Join-Path $root 'publish\bot'
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish src/Ozbeca.Bot -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:InvariantGlobalization=true -o publish/bot
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish xato bilan tugadi' }
    Get-ChildItem $out -Filter *.pdb | Remove-Item -Force
    Copy-Item (Join-Path $PSScriptRoot '.env.example'), (Join-Path $PSScriptRoot 'ozbecabot.service'), (Join-Path $PSScriptRoot 'install.sh') $out
    if ($EnvFrom) {
        $keep = Get-Content $EnvFrom | Where-Object { $_ -match '^(BOT_TOKEN|ADMIN_IDS|PREMIUM_EMOJI)=' }
        $paths = Get-Content (Join-Path $PSScriptRoot '.env.example') | Where-Object { $_ -match '^(DB_PATH|WORK_DIR)=' }
        [IO.File]::WriteAllText((Join-Path $out '.env'), (($keep + $paths) -join "`n") + "`n")
    }
    # Linux uchun LF qator oxiri
    foreach ($f in 'install.sh', 'ozbecabot.service', '.env.example') {
        $p = Join-Path $out $f
        [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p) -replace "`r`n", "`n"))
    }
    $zip = Join-Path $root 'publish\ozbecabot.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Get-ChildItem $out -Force).FullName -DestinationPath $zip
    Write-Host "Tayyor: $zip"
}
finally { Pop-Location }
