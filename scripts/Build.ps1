param(
    [string]$Dotnet = 'dotnet',
    [switch]$SmokeTest
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & $Dotnet build OledAurora.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Dotnet run --project tests/OledAurora.Checks -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    & $Dotnet publish src/OledAurora/OledAurora.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o artifacts/win-x64 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if ($SmokeTest) {
        $report = Join-Path $projectRoot 'artifacts/smoke-test.json'
        $smokeProcess = Start-Process -FilePath './artifacts/win-x64/OledAurora.exe' -ArgumentList '--smoke-test', "`"$report`"" -WindowStyle Hidden -Wait -PassThru
        if ($smokeProcess.ExitCode -ne 0) { throw "Desktop smoke test failed. See $report" }
    }
    Copy-Item -LiteralPath './artifacts/win-x64/OledAurora.exe' -Destination './artifacts/OledAurora.exe' -Force
    Copy-Item -LiteralPath './QUICKSTART.txt' -Destination './artifacts/win-x64/QUICKSTART.txt' -Force
    Compress-Archive -Path './artifacts/win-x64/OledAurora.exe','./artifacts/win-x64/QUICKSTART.txt' -DestinationPath './artifacts/OledAurora-win-x64.zip' -Force
    $hashes = @('./artifacts/OledAurora.exe', './artifacts/OledAurora-win-x64.zip') | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path $_ -Leaf)
    }
    $hashes | Set-Content -LiteralPath './artifacts/SHA256SUMS.txt' -Encoding ascii
    Write-Output "Ready: $(Join-Path $projectRoot 'artifacts/OledAurora.exe')"
}
finally { Pop-Location }
