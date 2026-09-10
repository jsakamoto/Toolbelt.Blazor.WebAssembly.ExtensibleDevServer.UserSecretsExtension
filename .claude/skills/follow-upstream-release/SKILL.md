---
name: follow-upstream-release
description: Use when the user asks to align this extension's version with a new release of Toolbelt.Blazor.WebAssembly.ExtensibleDevServer (the upstream dev server package), or asks to bump this package's own version to match — including vague prompts like "the upstream released a new version, follow up", "the dev server released a new version, catch up this extension too", or "bump this extension to match the latest DevServer version".
---

# Following a new Toolbelt.Blazor.WebAssembly.ExtensibleDevServer release

This extension does not reference the stock
`Microsoft.AspNetCore.Components.WebAssembly.DevServer` package at all — it depends on
`Toolbelt.Blazor.WebAssembly.ExtensibleDevServer`, built from the sibling repository
`NET10/DevServer`. When that repository's own version moves, this extension's version number is
conventionally realigned to match, purely so users aren't confused by mismatched version numbers
across the package family — even when nothing about this extension's own behavior changed. Say so
explicitly in the release notes (see below).

There is no `VersionInfo.props` in this repository; version numbers are literal strings repeated in
a few files. Bump all of them together.

## Steps

1. Confirm the new `Toolbelt.Blazor.WebAssembly.ExtensibleDevServer` version (from the `NET10/DevServer`
   repository's own version bump — see its `follow-upstream-release` skill; that repository should be
   updated, packed, and tested first).
2. Update the version in these files:
   - `UserSecretsExtension/UserSecretsExtension.csproj` — the `<Version>` property.
   - `SampleApp/SampleApp.csproj` — four `PackageReference` lines: `Microsoft.AspNetCore.Components.WebAssembly`,
     the commented-out `Microsoft.AspNetCore.Components.WebAssembly.DevServer` example,
     `Toolbelt.Blazor.WebAssembly.ExtensibleDevServer`, and this extension's own
     `Toolbelt.Blazor.WebAssembly.ExtensibleDevServer.UserSecretsExtension` reference.
   - `UserSecretsExtension/PackageContents/tools/net10.0/Toolbelt.Blazor.WebAssembly.ExtensibleDevServer.UserSecretsExtension.deps.json`
     — **don't hand-edit this.** It's regenerated from `$(Version)` by the `GenerateDepsJson` MSBuild
     target (runs `AfterTargets="Build"`). Just run `dotnet build` (or let the test run pack it) once
     after bumping `<Version>` above, and commit whatever it rewrites.
3. Add a new entry at the top of `RELEASE-NOTES.txt` (see the `release-notes` skill for the house
   style). When nothing functional changed, phrase it like:
   ```
   v.X.Y.Z
   - Improve: Aligned the version number with the Toolbelt.Blazor.WebAssembly.ExtensibleDevServer package. This release contains no functional changes.
   ```
4. If the `ExtensibleDevServer` version this extension now asks for is **not yet published on
   nuget.org**, this repository's E2E test can't restore it normally. Build it from `NET10/DevServer`
   (`dotnet pack -c Release`) and copy the resulting `.nupkg` from that repo's `_dist/` folder into
   this repository's `_local-packages/` folder — see `AGENTS.md` for the full mechanism
   (`LocalPackages.CopyToLocalFeed()`, wired into `AssemblySetup.cs`).
5. Run the tests and confirm everything passes:
   ```
   dotnet test --project ./UserSecretsExtension.Test/UserSecretsExtension.Test.csproj --output detailed
   ```
   (Packing this extension's own package happens automatically in `AssemblySetup`'s
   `[OneTimeSetUp]` — no need to `dotnet pack` by hand first.)

## Gotchas

- `UserSecretsExtension.Test/SampleAppE2ETests.cs` pins an exact
  `mcr.microsoft.com/dotnet/sdk:X.Y.Z` container image tag, independent of the DevServer version.
  Only touch it if a test fails with "You must install or update .NET to run this application" —
  that means the pinned tag is older than the SDK that actually built a package under test.
- Files in this repository are stored as LF line endings in git; a normal checkout converts them to
  CRLF locally. Editing with a POSIX tool (`sed`, a bash heredoc) leaves LF, which is harmless but
  worth re-normalizing to CRLF before committing if you want a clean diff.
