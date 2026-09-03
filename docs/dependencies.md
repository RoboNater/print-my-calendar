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

Built-in .NET analyzers are enabled at the .NET 8 recommended level; an additional analyzer package is intentionally unnecessary. NuGet auditing is explicitly set to the `low` threshold for direct and transitive dependencies, and `NU1901` through `NU1904` are errors. This intentionally favors early remediation over uninterrupted builds. CI also emits a human-readable vulnerability report; locked restore is the enforcement gate.

If an advisory is inapplicable and no corrected dependency is available, a temporary `NuGetAuditSuppress` item may be added to `Directory.Build.props`. It must name the advisory URL, link to a tracking issue in an adjacent comment, explain why the affected path is unreachable, and include a removal date. Suppressions require security-focused review and must be removed as soon as an updated dependency is available.

GitHub Actions are pinned to immutable commit SHAs, with the corresponding major version recorded in comments. The pinned action releases use the runner's supported Node.js 24 runtime. The CI packaging smoke test pins the reviewed Inno Setup Chocolatey package at 6.7.1 rather than following an unbounded latest version. Inno Setup 6.3 is the minimum supported local version because the smoke installer uses `x64compatible` architecture identifiers; 6.7.3 was validated for this phase.

Package changes should be isolated in a reviewable PR. Re-check the package's official NuGet page/source repository, license, supported frameworks, release notes, vulnerabilities, and behavior-specific tests before merging.
