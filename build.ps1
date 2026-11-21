# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Builds the solution in Release configuration and runs all unit tests.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run (for example
#   -Project <name> in a multi-library repository). Slow or environment-dependent test tiers
#   (GUI automation, integration) may be opt-in switches.
#   Run with no arguments it must still build and run the standard test suite (agents rely
#   on that), and never remove or weaken a test step to make it pass.

$ErrorActionPreference = 'Stop'

Write-Host "Building project..."
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional build steps here.

Write-Host "Running unit tests..."
dotnet test --configuration Release --report-trx
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
