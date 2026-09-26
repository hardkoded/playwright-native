#!/usr/bin/env bash
#
# Cloud Agent install script for PlaywrightNative.
#
# Runs after the repository is checked out, on top of a base snapshot that
# already provides the .NET 10 SDK, Node.js, and Chromium's system libraries
# (installed via `npx playwright install-deps chromium`, plus libgbm-dev/xvfb).
#
# This script only performs idempotent, repository-derived setup:
#   1. restore + build the solution,
#   2. generate the HTTPS dev certificate the test server needs,
#   3. make a Chromium build available in the cache layout the test fixtures
#      discover.
#
# It is safe to run repeatedly and must always terminate.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# The Playwright CLI version is pinned so the Chromium build id (and therefore
# the on-disk cache path) is deterministic across runs.
PLAYWRIGHT_CLI_VERSION="1.62.1"
CHROMIUM_BUILD="1234"
CACHE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/ms-playwright"
CHROMIUM_DIR="$CACHE_DIR/chromium-${CHROMIUM_BUILD}"

# Every project in the solution targets net10.0 only, so we build the solution
# without a `-f`/`--framework` override -- exactly as CI does
# (`dotnet build ./src/PlaywrightNative.sln`). Passing `-f net10.0` here sets
# TargetFramework as a global property across the whole solution, which makes
# the GeneratePackageOnBuild pack step of PlaywrightNative.csproj spawn an inner
# build of the project that races the parallel project-to-project build already
# running for it. That race intermittently fails with "the process cannot access
# <PlaywrightNative>.nupkg because it is being used by another process". Building
# without the override lets MSBuild deduplicate the project build and pack once.
echo "==> Building PlaywrightNative.sln"
dotnet build ./src/PlaywrightNative.sln

echo "==> Creating HTTPS development certificate for the test server"
dotnet dev-certs https --clean
dotnet dev-certs https -ep src/PlaywrightNative.TestServer/testCert.cer

# PlaywrightNative's own BrowserFetcher pins a Chromium revision whose archive is
# no longer served by the Playwright CDN, so we provision Chromium with the
# official Playwright CLI instead. BrowserExecutableFixture then discovers it by
# walking the ms-playwright cache: it expects `chromium-<build>/chrome-linux/chrome`
# next to an `INSTALLATION_COMPLETE` marker. The CLI lays the binary down under
# `chrome-linux64/`, so we add a `chrome-linux` symlink to match the expected path.
echo "==> Ensuring Chromium is installed for the test fixtures"
if [ ! -x "${CHROMIUM_DIR}/chrome-linux/chrome" ]; then
    npx --yes "playwright@${PLAYWRIGHT_CLI_VERSION}" install chromium
    ln -sfn "${CHROMIUM_DIR}/chrome-linux64" "${CHROMIUM_DIR}/chrome-linux"
fi

echo "==> Chromium ready at ${CHROMIUM_DIR}/chrome-linux/chrome"
echo "==> Install complete"
