# How to Contribute

If you are interested in contributing to Playwright Sharp, Thank you!

Coding is not for lonely wolves. Welcome to the pack!

The project has a clear roadmap we want to follow. If you want to contribute, ask before submitting a PR. We will analyze if it’s the right moment to implement that feature or not.
If you don’t know what to do, ASK! We have many many things to implement :)

## Code reviews

All submissions, including submissions by project members, require review. We
use GitHub pull requests for this purpose. Consult
[GitHub Help](https://help.github.com/articles/about-pull-requests/) for more
information on pull requests.

## Core Guidelines

The primary goal is to create an API as close as possible to Playwright. A developer should be able to switch easily to Playwright Sharp and vice-versa.

Playwright Sharp should have a .NET/C# flavor.

 * A developer should be able to inject its objects using dependency injection.
 * Getter functions should be expressed as properties.
 * Async suffix should be honored.

Our guide for architecture and code style will by [Microsoft.Extensions.Configuration](https://github.com/dotnet/extensions/tree/master/src/Configuration).

## Code Style

Though this list will change over time, these are the things to consider now:
 * [We are team spaces](https://www.youtube.com/watch?v=SsoOG6ZeyUI).
 * Every public API should have an XML documentation.
 * Try to follow the current style.
 * Don’t reinvent the wheel.

### Dotnet Format

CI runs `dotnet format` against `src/PlaywrightSharp.sln`. From the repo root:

```powershell
dotnet format whitespace ./src/PlaywrightSharp.sln --verify-no-changes
dotnet format style ./src/PlaywrightSharp.sln --verify-no-changes
```

To apply formatting, drop `--verify-no-changes`.

## Commit Messages

Don’t worry about commit messages or about how many commits your PR has. [Your PR will be squashed](https://help.github.com/articles/about-pull-request-merges/#squash-and-merge-your-pull-request-commits), so the commit message will be set at that time.


## Writing Tests

* Every feature should be accompanied by a test.
* Every public api event/method should be accompanied by a test.

### Browser binaries

There is no driver process and nothing to copy into `bin/`. Tests resolve
browsers through `BrowserFetcher` (and the `BrowserExecutable` helper in
`PlaywrightSharp.NUnit`). The first run downloads the pinned Chromium / Firefox /
WebKit build into the local cache (`PLAYWRIGHT_BROWSERS_PATH` if set).

### Running Tests Locally

When you run the tests locally for the first time, you might be greeted with the following error message.

This happens because you're missing a certificate. To generate one, you can use the `dotnet dev-certs` tooling.

In your repository root, run the following:

```powershell
dotnet dev-certs https -ep src/PlaywrightSharp.TestServer/testCert.cer
```

Always set `PRODUCT` (`CHROMIUM`, `FIREFOX`, or `WEBKIT`). A bare `dotnet test`
without `PRODUCT=` is wrong — fixtures launch the product selected by that
variable (defaulting to Chromium only if you omit it, which hides Firefox/WebKit
failures).

Chromium (matches CI):

```powershell
$env:PRODUCT = "CHROMIUM"
dotnet test .\src\PlaywrightSharp.Tests\PlaywrightSharp.Tests.csproj -f net10.0 -s src/PlaywrightSharp.Tests/test.runsettings
```

WebKit (matches CI):

```powershell
$env:PRODUCT = "WEBKIT"
dotnet test .\src\PlaywrightSharp.Tests\PlaywrightSharp.Tests.csproj -f net10.0 -s src/PlaywrightSharp.Tests/test.runsettings
```

Firefox launches today but is not in CI:

```powershell
$env:PRODUCT = "FIREFOX"
dotnet test .\src\PlaywrightSharp.Tests\PlaywrightSharp.Tests.csproj -f net10.0 -s src/PlaywrightSharp.Tests/test.runsettings
```

On Unix, prefix the same `dotnet test` line with `PRODUCT=CHROMIUM` (or
`WEBKIT` / `FIREFOX`).

Narrow a run with `--filter` on a class or method name, for example:

```powershell
$env:PRODUCT = "CHROMIUM"
dotnet test .\src\PlaywrightSharp.Tests\PlaywrightSharp.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ShouldCreateNewPage"
```

Additionally, you can use the Test Explorer if you're using Visual Studio.
Remember to set `PRODUCT` in the test environment there too.

## Documentation site

Conceptual docs live in `docfx_project/` and are published to
<https://hardkoded.github.io/playwright-sharp> by `.github/workflows/docs.yml`.

One-time GitHub setting (repo admin): **Settings → Pages → Source: GitHub Actions**.
After that, every push to `main` that builds successfully updates the site.

Build the same way CI does (requires the [DocFX](https://dotnet.github.io/docfx/) .NET tool):

```
dotnet tool update -g docfx --version 2.74.1
docfx docfx_project/docfx.json
```

The static site is written to `docfx_project/_site`.

