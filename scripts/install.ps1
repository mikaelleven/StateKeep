[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'StateKeep'),
    [switch]$NoPathUpdate
)

$ErrorActionPreference = 'Stop'
$repository = 'mikaelleven/StateKeep'
$apiHeaders = @{ Accept = 'application/vnd.github+json' }

try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repository/releases/latest" -Headers $apiHeaders
}
catch {
    throw "Could not find the latest StateKeep release. $_"
}

$asset = @($release.assets | Where-Object { $_.name -match '^StateKeep-.*-win-x64\.zip$' }) |
    Select-Object -First 1
if ($null -eq $asset) {
    throw "The latest StateKeep release does not include a Windows x64 installer."
}

$checksumAsset = @($release.assets | Where-Object { $_.name -eq "$($asset.name).sha256" }) |
    Select-Object -First 1
if ($null -eq $checksumAsset) {
    throw "The latest StateKeep release does not include a checksum for $($asset.name)."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("StateKeep-" + [guid]::NewGuid())
$archivePath = Join-Path $tempRoot $asset.name
$stagingPath = Join-Path $tempRoot 'staging'

try {
    New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
    Write-Host "Downloading StateKeep $($release.tag_name)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath
    $expectedHash = ((Invoke-RestMethod -Uri $checksumAsset.browser_download_url).Trim() -split '\s+')[0]
    $actualHash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) {
        throw "The downloaded StateKeep archive failed SHA-256 verification."
    }
    Expand-Archive -Path $archivePath -DestinationPath $stagingPath -Force

    $executable = Get-ChildItem -Path $stagingPath -Filter 'statekeep.exe' -File -Recurse | Select-Object -First 1
    if ($null -eq $executable) {
        throw 'The downloaded release did not contain statekeep.exe.'
    }

    if (Test-Path $InstallPath) {
        Remove-Item -Path $InstallPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Copy-Item -Path (Join-Path $executable.Directory.FullName '*') -Destination $InstallPath -Recurse -Force
}
finally {
    Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $NoPathUpdate) {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $pathEntries = @($userPath -split ';' | Where-Object { $_ })
    if ($pathEntries -notcontains $InstallPath) {
        [Environment]::SetEnvironmentVariable('Path', (($pathEntries + $InstallPath) -join ';'), 'User')
        $env:Path = "$env:Path;$InstallPath"
        Write-Host 'Added StateKeep to your user PATH. Open a new terminal to use it everywhere.'
    }
}

Write-Host "StateKeep $($release.tag_name) installed to $InstallPath"
Write-Host 'Configure a backup location with: statekeep setup'
