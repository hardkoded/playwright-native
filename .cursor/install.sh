#!/usr/bin/env bash
#
# Cloud Agent install script for PlaywrightSharp.
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

# The library multi-targets netstandard2.1 (NuGet packaging only) and net10.0.
# The Cloud Agent dev environment only exercises net10.0 -- the test suite always
# runs with `-f net10.0` -- so we build that target framework here to warm the
# build without depending on the packaging-only netstandard2.1 output.
echo "==> Building PlaywrightSharp.sln (net10.0)"
dotnet build ./src/PlaywrightSharp.sln -f net10.0

echo "==> Creating HTTPS development certificate for the test server"
dotnet dev-certs https --clean
dotnet dev-certs https -ep src/PlaywrightSharp.TestServer/testCert.cer

# PlaywrightSharp's own BrowserFetcher pins a Chromium revision whose archive is
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
