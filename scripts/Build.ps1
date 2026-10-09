param(
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory = 'artifacts',
    [switch]$SmokeTest
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot $OutputDirectory }
$publishDirectory = Join-Path $artifactRoot 'win-x64'
Push-Location $projectRoot
try {
    & $Dotnet build OledAurora.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Dotnet run --project tests/OledAurora.Checks -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    & $Dotnet publish src/OledAurora/OledAurora.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o $publishDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $publishExecutable = Join-Path $publishDirectory 'OledAurora.exe'
    $visualReport = Join-Path $artifactRoot 'visual-test.json'
    $visualProcess = Start-Process -FilePath $publishExecutable -ArgumentList '--visual-test', "`"$visualReport`"" -WindowStyle Hidden -Wait -PassThru
    if ($visualProcess.ExitCode -ne 0) { throw "Visual regression checks failed. See $visualReport" }
    if ($SmokeTest) {
        $report = Join-Path $artifactRoot 'smoke-test.json'
        $smokeProcess = Start-Process -FilePath $publishExecutable -ArgumentList '--smoke-test', "`"$report`"" -WindowStyle Hidden -Wait -PassThru
        if ($smokeProcess.ExitCode -ne 0) { throw "Desktop smoke test failed. See $report" }
    }
    $executable = Join-Path $artifactRoot 'OledAurora.exe'
    $archive = Join-Path $artifactRoot 'OledAurora-win-x64.zip'
    $quickstart = Join-Path $publishDirectory 'QUICKSTART.txt'
    Copy-Item -LiteralPath $publishExecutable -Destination $executable -Force
    Copy-Item -LiteralPath './QUICKSTART.txt' -Destination $quickstart -Force
    Compress-Archive -LiteralPath $publishExecutable,$quickstart -DestinationPath $archive -Force
    $hashes = @($executable, $archive) | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path $_ -Leaf)
    }
    $hashes | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ascii
    Write-Output "Ready: $executable"
}
finally { Pop-Location }
