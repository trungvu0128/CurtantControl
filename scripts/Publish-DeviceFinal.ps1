param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.9.0')
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$name = "CurtantControl-Final-v$Version-pi-buster-arm"
$bundle = Join-Path $repository "artifacts/bundles/$name"
$archive = "$bundle.tar.gz"
if ((Test-Path -LiteralPath $bundle) -or (Test-Path -LiteralPath $archive)) { throw 'Choose an unused final bundle version.' }

& (Join-Path $PSScriptRoot 'Publish-RaspberryPi.ps1') -Version "$Version-buster" -Architecture arm -LegacyBuster
$release = Join-Path $repository "artifacts/releases/CurtantControl-v$Version-buster-linux-arm"
$payload = Join-Path $bundle 'payloads/buster-arm'
New-Item -ItemType Directory -Force -Path (Split-Path $payload -Parent) | Out-Null
Copy-Item -LiteralPath $release -Destination $payload -Recurse

# Fresh installs use the explicitly supplied device profile; setup preserves installed settings.
$profilePath = Join-Path $repository 'packaging/bundle/photobooth.default.json'
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
$pins = @($profile.BcmPinsByChannel)
if ($pins.Count -ne 8 -or @($pins | Sort-Object -Unique).Count -ne 8 -or
    @($pins | Where-Object { $_ -lt 0 -or $_ -gt 27 }).Count -gt 0 -or $null -eq $profile.ActiveLow) {
    throw 'Invalid supplied device relay profile.'
}
$order = @($profile.CurtainOrderFrontToBack)
if ((($order | Sort-Object) -join ',') -ne '1,2,3,4') { throw 'Invalid relay layer profile.' }
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $payload 'photobooth.json'), ([IO.File]::ReadAllText($profilePath)).Replace("`r`n", "`n"), $utf8)
$palettePath = Join-Path $payload 'colors.json'
$palette = Get-Content -LiteralPath $palettePath -Raw | ConvertFrom-Json
$palette | Add-Member -NotePropertyName RelayOrderFrontToBack -NotePropertyValue $order -Force
[IO.File]::WriteAllText($palettePath, ($palette | ConvertTo-Json -Depth 10) + "`n", $utf8)

foreach ($file in @('install.sh', 'setup.py', 'legacy_kiosk.py', 'rollback.sh')) {
    $text = [IO.File]::ReadAllText((Join-Path $repository "packaging/bundle/$file")).Replace("`r`n", "`n")
    [IO.File]::WriteAllText((Join-Path $bundle $file), $text, $utf8)
}
Copy-Item -LiteralPath (Join-Path $repository 'packaging/raspberry-pi/rollback.py') -Destination (Join-Path $bundle 'rollback.py')
New-Item -ItemType Directory -Force -Path (Join-Path $bundle 'keycheck') | Out-Null
foreach ($file in @('key_check_new.py', 'configure_autostart.py', 'keycheck.service', 'override.conf', 'install.sh', 'README.md', 'HUONG-DAN.txt')) {
    $text = [IO.File]::ReadAllText((Join-Path $repository "packaging/raspberry-pi/keycheck/$file")).Replace("`r`n", "`n")
    [IO.File]::WriteAllText((Join-Path $bundle "keycheck/$file"), $text, $utf8)
}
Copy-Item -LiteralPath (Join-Path $repository 'packaging/raspberry-pi/keycheck/123click-logo.png') -Destination (Join-Path $bundle 'keycheck/123click-logo.png')
$guide = [IO.File]::ReadAllText((Join-Path $repository 'packaging/bundle/FINAL-PI.md')).Replace('{{VERSION}}', $Version).Replace("`r`n", "`n")
[IO.File]::WriteAllText((Join-Path $bundle 'HUONG-DAN.md'), $guide, $utf8)
[IO.File]::WriteAllText((Join-Path $bundle 'README.md'), $guide, $utf8)
[IO.File]::WriteAllText((Join-Path $bundle 'VERSION.txt'), "$Version`n", $utf8)

function Write-Manifest([string]$directory) {
    $hashes = [ordered]@{}
    foreach ($entry in Get-ChildItem -LiteralPath $directory -Recurse -File | Sort-Object FullName) {
        if ($entry.Name -eq 'SHA256SUMS.json') { continue }
        $relative = [IO.Path]::GetRelativePath($directory, $entry.FullName).Replace([IO.Path]::DirectorySeparatorChar, [char]47)
        $hashes[$relative] = (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    [IO.File]::WriteAllText((Join-Path $directory 'SHA256SUMS.json'), ($hashes | ConvertTo-Json), $utf8)
}
Write-Manifest $payload
Write-Manifest (Join-Path $bundle 'keycheck')
Write-Manifest $bundle
& tar -czf $archive -C (Split-Path $bundle -Parent) $name
if ($LASTEXITCODE -ne 0) { throw 'Final archive failed.' }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$hash  $([IO.Path]::GetFileName($archive))`n", $utf8)
Write-Output "Final bundle: $archive"
Write-Output "SHA256: $hash"
