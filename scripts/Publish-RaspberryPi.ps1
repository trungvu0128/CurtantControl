param(
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.2',
    [ValidateSet('arm', 'arm64')]
    [string]$Architecture = 'arm64',
    [switch]$LegacyBuster,
    [string]$CameraStreamUrl
)

$ErrorActionPreference = 'Stop'
if ($CameraStreamUrl) {
    $streamUri = $null
    if (-not [Uri]::TryCreate($CameraStreamUrl, [UriKind]::Absolute, [ref]$streamUri) -or
        $streamUri.Scheme -notin @('http','https')) { throw 'CameraStreamUrl must be an absolute HTTP/HTTPS URL.' }
}
if ($LegacyBuster -and $Architecture -ne 'arm') { throw 'The Buster compatibility target requires -Architecture arm.' }
$legacyProperty = "-p:LegacyBuster=$($LegacyBuster.IsPresent.ToString().ToLowerInvariant())"
$repository = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $repository 'artifacts/releases'
$runtimeIdentifier = "linux-$Architecture"
$bits = if ($Architecture -eq 'arm64') { '64' } else { '32' }
$machine = if ($Architecture -eq 'arm64') { 183 } else { 40 }
$profile = if ($Architecture -eq 'arm64') { 'RaspberryPi64' } else { 'RaspberryPi32' }
$packageName = "CurtantControl-v$Version-$runtimeIdentifier"
$packagePath = Join-Path $releaseRoot $packageName
$archive = Join-Path $releaseRoot "$packageName.tar.gz"
$buildRoot = Join-Path $repository "artifacts/release-build-$Version-$Architecture"
if ((Test-Path -LiteralPath $packagePath) -or (Test-Path -LiteralPath $archive)) {
    throw "Release $Version already exists. Choose a new version or inspect the existing artifacts."
}

Push-Location $repository
try {
    foreach ($configuration in @('Debug', 'Release')) {
        & dotnet build CurtantControl.slnx -c $configuration --artifacts-path $buildRoot "-p:Version=$Version" $legacyProperty
        if ($LASTEXITCODE -ne 0) { throw "$configuration build failed." }
    }
    $checks = Join-Path $buildRoot 'bin/CurtantControl.RelayChecks/release/CurtantControl.RelayChecks.dll'
    & dotnet $checks
    if ($LASTEXITCODE -ne 0) { throw 'Release checks failed.' }

    & dotnet publish src/CurtantControl "-p:PublishProfile=$profile" --artifacts-path $buildRoot "-p:Version=$Version" $legacyProperty -o $packagePath
    if ($LASTEXITCODE -ne 0) { throw "$runtimeIdentifier publish failed." }

    foreach ($required in @('CurtantControl', 'CurtantControl.dll', 'CurtantControl.runtimeconfig.json',
        'photobooth.json', 'libcoreclr.so', 'libSkiaSharp.so', 'libHarfBuzzSharp.so')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packagePath $required))) {
            throw "Missing required release file: $required"
        }
    }

    if ($LegacyBuster) {
        $nativeFiles = @(Get-Item (Join-Path $packagePath 'CurtantControl')) + @(Get-ChildItem $packagePath -Filter '*.so')
        foreach ($native in $nativeFiles) {
            $strings = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($native.FullName))
            foreach ($match in [regex]::Matches($strings, 'GLIBC_([0-9]+\.[0-9]+)')) {
                if ([version]$match.Groups[1].Value -gt [version]'2.28') {
                    throw "$($native.Name) contains a GLIBC requirement newer than Buster 2.28."
                }
            }
            foreach ($match in [regex]::Matches($strings, 'GLIBCXX_([0-9]+\.[0-9]+\.[0-9]+)')) {
                if ([version]$match.Groups[1].Value -gt [version]'3.4.25') {
                    throw "$($native.Name) contains a GLIBCXX requirement newer than Buster's standard library."
                }
            }
        }
    }
    foreach ($nativeFile in @('CurtantControl', 'libcoreclr.so', 'libSkiaSharp.so', 'libHarfBuzzSharp.so')) {
        $executable = [IO.File]::ReadAllBytes((Join-Path $packagePath $nativeFile))
        if ([BitConverter]::ToString($executable[0..3]) -ne '7F-45-4C-46' -or
            [BitConverter]::ToUInt16($executable, 18) -ne $machine) {
            throw "$nativeFile does not match $runtimeIdentifier ELF architecture."
        }
    }

    $settingsPath = Join-Path $packagePath 'photobooth.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $settings.SimulateRelays = $true
    if ($CameraStreamUrl) { $settings.CameraStreamUrl = $CameraStreamUrl }
    [IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 10) + "`n", [Text.UTF8Encoding]::new($false))

    $packageDocs = Join-Path $packagePath 'docs'
    New-Item -ItemType Directory -Path $packageDocs -Force | Out-Null
    foreach ($name in @('PHOTOBOOTH_UI.md', 'RELAY_CONTROL.md')) {
        Copy-Item -LiteralPath (Join-Path $repository "docs/$name") -Destination $packageDocs
    }
    $readmeTemplate = if ($LegacyBuster) { 'README-buster.md' } else { 'README.md' }
    $readme = [IO.File]::ReadAllText((Join-Path $repository "packaging/raspberry-pi/$readmeTemplate")).Replace('@VERSION@', $Version).Replace('@ARCH@', $runtimeIdentifier).Replace('@BITS@', $bits)
    [IO.File]::WriteAllText((Join-Path $packagePath 'README.md'), $readme, [Text.UTF8Encoding]::new($false))
    $launcher = [IO.File]::ReadAllText((Join-Path $repository 'packaging/raspberry-pi/start.sh')).Replace("`r`n", "`n").Replace('@BITS@', $bits)
    [IO.File]::WriteAllText((Join-Path $packagePath 'start.sh'), $launcher, [Text.UTF8Encoding]::new($false))
    foreach ($name in @('install-shortcut.py', 'curtant-control.svg', 'diagnose.sh', 'apply-update.py')) {
        $content = [IO.File]::ReadAllText((Join-Path $repository "packaging/raspberry-pi/$name")).Replace("`r`n", "`n")
        [IO.File]::WriteAllText((Join-Path $packagePath $name), $content, [Text.UTF8Encoding]::new($false))
    }
    [IO.File]::WriteAllText((Join-Path $packagePath 'VERSION.txt'), "$Version`n", [Text.UTF8Encoding]::new($false))

    & tar -czf $archive -C $releaseRoot $packageName
    if ($LASTEXITCODE -ne 0) { throw 'Archive creation failed.' }
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$archive.sha256", "$hash  $packageName.tar.gz`n", [Text.UTF8Encoding]::new($false))
    Write-Output "Release: $archive"
    Write-Output "SHA256: $hash"
}
finally { Pop-Location }
