# winget paketi uchun manifestlar (microsoft/winget-pkgs formatida, 1.6.0 schema).
# Release chiqqandan keyin: exe GitHub release'ga yuklangan bo'lishi kerak — sha256 o'sha fayldan olinadi.
#
#   pwsh tools/winget/New-WingetManifest.ps1 -Version 1.6.0
#
# Natija: tools/winget/out/manifests/a/AzamjonShaydullayev/Ozbeca/<versiya>/ — uchta .yaml.
# Keyin: winget validate <papka>; sinash: winget install --manifest <papka>;
# yuborish: wingetcreate submit <papka> (yoki winget-pkgs'ga PR).
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Url = "https://github.com/AzamjonShaydullayev/Ozbeca/releases/download/v$Version/Ozbeca.exe",
    [string] $License = "Proprietary",   # repoda LICENSE fayli yo'q — muallif tanlasin (masalan, MIT)
    [string] $OutDir = (Join-Path $PSScriptRoot "out")
)
$ErrorActionPreference = "Stop"
$id = "AzamjonShaydullayev.Ozbeca"

$tmp = Join-Path ([IO.Path]::GetTempPath()) "Ozbeca-$Version.exe"
Write-Host "Yuklab olinmoqda: $Url"
Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing
$sha = (Get-FileHash $tmp -Algorithm SHA256).Hash
$fileVersion = (Get-Item $tmp).VersionInfo.FileVersion
Remove-Item $tmp
if (-not $fileVersion.StartsWith($Version)) { throw "exe versiyasi ($fileVersion) $Version emas" }

$dir = Join-Path $OutDir "manifests/a/AzamjonShaydullayev/Ozbeca/$Version"
New-Item -ItemType Directory -Force $dir | Out-Null

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.6.0.schema.json
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: uz-Latn-UZ
ManifestType: version
ManifestVersion: 1.6.0
"@ | Set-Content -Encoding utf8NoBOM (Join-Path $dir "$id.yaml")

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.6.0.schema.json
PackageIdentifier: $id
PackageVersion: $Version
InstallerType: portable
Commands:
- ozbeca
Installers:
- Architecture: neutral
  InstallerUrl: $Url
  InstallerSha256: $sha
ManifestType: installer
ManifestVersion: 1.6.0
"@ | Set-Content -Encoding utf8NoBOM (Join-Path $dir "$id.installer.yaml")

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: uz-Latn-UZ
Publisher: Azamjon Shaydullayev
PublisherUrl: https://github.com/AzamjonShaydullayev
PackageName: Ozbeca
PackageUrl: https://github.com/AzamjonShaydullayev/Ozbeca
License: $License
ShortDescription: O'zbek lotin matnini 2026-yilgi yangi alifboga (o' -> ö, g' -> ğ, sh -> ş, ch -> ç) o'giradi.
Description: Tray dasturi. Belgilangan matnni tezkor tugma bilan, yozish paytida esa so'z tugashi bilan o'giradi. Kirill matnini ham yangi lotinga o'giradi.
Tags:
- uzbek
- ozbek
- alphabet
- transliteration
- keyboard
ReleaseNotesUrl: https://github.com/AzamjonShaydullayev/Ozbeca/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@ | Set-Content -Encoding utf8NoBOM (Join-Path $dir "$id.locale.uz-Latn-UZ.yaml")

Write-Host "Tayyor: $dir"
Write-Host "SHA256: $sha"
