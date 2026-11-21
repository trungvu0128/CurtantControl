param([string]$Version = '0.2.0', [switch]$GenericDefaults)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a stable three-part version.' }
$repository = Split-Path -Parent $PSScriptRoot
$bundleRoot = Join-Path $repository "artifacts/bundles/CurtantControl-Setup-v$Version"
$archive = "$bundleRoot.tar.gz"
if ((Test-Path $bundleRoot) -or (Test-Path $archive)) { throw 'Bundle version already exists.' }
$profilePath = Join-Path $repository "packaging/bundle/photobooth.default.json"
if (-not $GenericDefaults -and -not (Test-Path -LiteralPath $profilePath)) { throw "Missing bundle default profile: $profilePath" }
$variants = @(
    @{ Key='buster-arm'; Version="$Version-buster"; Architecture='arm'; LegacyBuster=$true },
    @{ Key='modern-arm'; Version=$Version; Architecture='arm'; LegacyBuster=$false },
    @{ Key='modern-arm64'; Version=$Version; Architecture='arm64'; LegacyBuster=$false }
)
foreach ($variant in $variants) {
    & (Join-Path $PSScriptRoot 'Publish-RaspberryPi.ps1') -Version $variant.Version -Architecture $variant.Architecture -LegacyBuster:$variant.LegacyBuster
}
New-Item -ItemType Directory -Path "$bundleRoot/payloads" -Force | Out-Null
foreach ($variant in $variants) {
    $source = Join-Path $repository "artifacts/releases/CurtantControl-v$($variant.Version)-linux-$($variant.Architecture)"
    $target = Join-Path $bundleRoot "payloads/$($variant.Key)"
    Copy-Item -LiteralPath $source -Destination $target -Recurse
    $configPath = Join-Path $target 'photobooth.json'
    if ($GenericDefaults) {
        # Explicit generic distribution: no development wiring or camera address.
        $config = Get-Content -Raw $configPath | ConvertFrom-Json
        $config.SimulateRelays = $true
        $config.BcmPinsByChannel = @()
        $config.ActiveLow = $null
        $config.CameraDevice = $null
        $config.CameraStreamUrl = $null
        [IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 10) + "`n", [Text.UTF8Encoding]::new($false))
    } else {
        # Explicit user-provided installation defaults; never modify source app config.
        $config = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
        $pins = @($config.BcmPinsByChannel)
        if ($pins.Count -ne 8 -or @($pins | Sort-Object -Unique).Count -ne 8 -or
            @($pins | Where-Object { $_ -lt 0 -or $_ -gt 27 }).Count -gt 0 -or $null -eq $config.ActiveLow) {
            throw 'Bundle profile requires eight distinct BCM pins and explicit polarity.'
        }
        $order = @($config.CurtainOrderFrontToBack)
        if ((($order | Sort-Object) -join ',') -ne '1,2,3,4') { throw 'Invalid bundle profile layer order.' }
        [IO.File]::WriteAllText($configPath, ([IO.File]::ReadAllText($profilePath)).Replace("`r`n","`n"), [Text.UTF8Encoding]::new($false))
        # The palette order is authoritative at runtime; align fresh installations to the profile.
        $palettePath = Join-Path $target 'colors.json'
        $palette = Get-Content -LiteralPath $palettePath -Raw | ConvertFrom-Json
        $palette | Add-Member -NotePropertyName RelayOrderFrontToBack -NotePropertyValue $order -Force
        [IO.File]::WriteAllText($palettePath, ($palette | ConvertTo-Json -Depth 10) + "`n", [Text.UTF8Encoding]::new($false))
    }
    $hashes = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $target -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($target, $file.FullName).Replace('\','/')
        $hashes[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    [IO.File]::WriteAllText((Join-Path $target 'SHA256SUMS.json'), ($hashes | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
}
foreach ($name in @('install.sh', 'setup.py', 'README.md', 'HUONG-DAN.md')) {
    $text = [IO.File]::ReadAllText((Join-Path $repository "packaging/bundle/$name")).Replace("`r`n","`n")
    [IO.File]::WriteAllText((Join-Path $bundleRoot $name), $text, [Text.UTF8Encoding]::new($false))
}
& tar -czf $archive -C (Split-Path $bundleRoot -Parent) (Split-Path $bundleRoot -Leaf)
if ($LASTEXITCODE -ne 0) { throw 'Bundle archive failed.' }
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$hash  $([IO.Path]::GetFileName($archive))`n")
Write-Output "Setup bundle: $archive"
Write-Output "SHA256: $hash"
