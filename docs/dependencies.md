# Dependency policy and Phase 1 review

Dependencies are centrally pinned in `Directory.Packages.props` and restored with per-project lock files. Add a package only when the platform or existing dependencies do not reasonably provide the capability. Production dependencies require a compatible license, active maintenance, supported target framework, and tests around behavior on which the application relies.

Versions reviewed on 2026-09-03:

| Package | Purpose | License/review result |
| --- | --- | --- |
| Ical.Net 5.2.3 | RFC 5545 parsing, recurrence, exceptions, and timezone handling in the Yahoo layer | MIT; targets .NET 6/.NET Standard and is compatible with .NET 8; current maintained v5 line. A smoke test covers parsing of timezone, recurrence, and exclusion data before Phase 3 adds comprehensive fixtures. |
| xunit.v3 4.0.0 | Unit-test authoring/runtime | Apache-2.0; current .NET 8-compatible major. |
| xunit.runner.visualstudio 4.0.0 | `dotnet test`/VSTest discovery | Apache-2.0; private test asset. |
| Microsoft.NET.Test.Sdk 18.9.0 | .NET test host | MIT; test-only dependency. |
| coverlet.collector 10.0.1 | Cross-platform coverage collection | MIT; test-only private asset and compatible with the pinned SDK. |

Built-in .NET analyzers are enabled at the .NET 8 recommended level; an additional analyzer package is intentionally unnecessary. NuGet auditing is enabled for direct and transitive dependencies. CI also emits a vulnerability check and fails on restore/build warnings.

GitHub Actions are pinned to immutable commit SHAs, with the corresponding major version recorded in comments. The pinned v5 actions use the runner's supported Node.js 24 runtime. The CI packaging smoke test pins the reviewed Inno Setup Chocolatey package at 6.7.1 rather than following an unbounded latest version. Local development may use a newer compatible Inno Setup 6 patch; 6.7.3 was validated for this phase.

Package changes should be isolated in a reviewable PR. Re-check the package's official NuGet page/source repository, license, supported frameworks, release notes, vulnerabilities, and behavior-specific tests before merging.
