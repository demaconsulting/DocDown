# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Builds the solution in Release configuration and runs all unit tests.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific build or test operations.
#
# MODIFICATION POLICY:
#   Only modify this file to add project-specific operations at the designated
#   [PROJECT-SPECIFIC] extension points.

$ErrorActionPreference = 'Stop'

Write-Host "Building project..."
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional build steps here.

Write-Host "Running unit tests..."
# [PROJECT-SPECIFIC] --max-parallel-test-modules 1 makes the test platform run one test module at a
# time. A module is one assembly built for one target framework, so this serializes both axes at
# once: the test projects no longer overlap each other, and a project's net8.0/net9.0/net10.0 runs
# no longer overlap themselves. Those runs are separate processes, so no in-assembly setting can
# reach them; test/xunit.runner.json covers the in-assembly axis by disabling collection
# parallelism and pinning the runner to a single thread.
# Why: the tool's self-validation drives Microsoft Visio and PowerPoint through COM automation,
# which is single-instance, so two renders at once tear each other's session down. Serializing the
# suite costs wall-clock time and is the accepted trade: the alternative was making a user-facing
# diagnostic tolerate contention that only this harness ever created.
dotnet test --configuration Release --report-trx --max-parallel-test-modules 1
$testExitCode = $LASTEXITCODE

# [PROJECT-SPECIFIC] Preserve a failing run's TRX for diagnosis before exiting. The test TRX are
# written under TestResults/ and are overwritten by the next run; on failure, copy them into a
# timestamped archive OUTSIDE TestResults/** so the evidence survives. The archive location is
# deliberately not matched by the ReqStream gate glob (--tests "TestResults/**/*.trx"), so retained
# failing copies never inflate the compliance case counts.
if ($testExitCode -ne 0) {
    $failingTrx = Get-ChildItem -Path "TestResults" -Filter "*.trx" -Recurse -ErrorAction SilentlyContinue
    if ($failingTrx) {
        $archive = Join-Path "test-history" (Get-Date -Format "yyyyMMdd-HHmmss")
        New-Item -ItemType Directory -Force -Path $archive | Out-Null
        $failingTrx | ForEach-Object { Copy-Item $_.FullName -Destination $archive -Force }
        Write-Host "Preserved failing test results into '$archive'."
    }
    exit $testExitCode
}

# [PROJECT-SPECIFIC] Add additional test or post-build steps here.

Write-Host "Build and tests completed successfully!"
