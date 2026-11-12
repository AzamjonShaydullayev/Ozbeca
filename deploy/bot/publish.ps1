# Bot'ni linux-x64 uchun bitta faylga yig'adi: publish/bot/Ozbeca.Bot va publish/ozbeca-bot.zip
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root
try {
    $out = Join-Path $root 'publish\bot'
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish src/Ozbeca.Bot -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:InvariantGlobalization=true -o publish/bot
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish xato bilan tugadi' }
    $zip = Join-Path $root 'publish\ozbeca-bot.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
    Write-Host "Tayyor: $zip"
}
finally { Pop-Location }
