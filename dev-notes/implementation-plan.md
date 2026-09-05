# Yahoo Month Print — Four-Phase Implementation Plan

## Purpose

This plan turns the v1.0 requirements in `yahoo-month-print-spec.md` into four implementation phases. Each phase is intended to be completed on its own branch, committed, opened as a pull request, reviewed by other agents, revised until every review comment is adjudicated, and merged before work begins on the next phase.

The phase boundaries deliberately follow dependency and risk boundaries:

1. establish a reproducible Windows development and release toolchain;
2. prove the calendar experience against deterministic local data;
3. replace local data with secure, read-only Yahoo CalDAV data and resilient local state;
4. make the visible calendar printable, package the application, and validate the complete release workflow.

P1 features from the specification are excluded until all P0 release criteria pass.

## Delivery and review rules

Every phase uses the same delivery loop:

1. Create a `codex/phase-N-...` branch from the merged default branch.
2. Implement only that phase's agreed scope, keeping unrelated cleanup out of the PR.
3. Run the phase's automated checks and record any required manual evidence in the PR.
4. Commit the completed phase and open one PR whose description links its requirements to tests or manual verification.
5. Ask other agents to review architecture, correctness, security/privacy, usability/accessibility, and test coverage as relevant to the phase.
6. Adjudicate every comment: apply valid feedback; ask for clarification when necessary; or explain, with evidence, why a change should not be made. Add regression tests for corrected defects.
7. Re-run all checks, resolve every review thread, and request re-review for material changes.
8. Merge only after required checks pass and no actionable review comment remains. Start the next phase from that merged result.

Each PR should include:

- a concise scope and explicit out-of-scope list;
- the relevant acceptance checklist from this document;
- commands and results for automated validation;
- screenshots for changed WPF or print-preview UI;
- any manual test notes, known limitations, and follow-up issues;
- confirmation that no credentials, Authorization headers, or private calendar content were committed or logged.

## Architectural constraints carried through all phases

- Target `net8.0-windows` on Windows 10/11 x64 with WPF and a self-contained final deployment.
- Keep `YahooMonthPrint.Core` free of WPF and network dependencies.
- Treat fetched calendar data as immutable input. Filtering and hiding produce view state only and never alter Yahoo data.
- Keep the Yahoo client read-only by construction: it exposes discovery/query operations but no event create, update, or delete API. Production HTTP operations are limited to HTTPS CalDAV/WebDAV reads (`PROPFIND` and `REPORT`, plus `GET` only if discovery/query requires it); never send `PUT` or `DELETE`.
- Represent recurring instances as occurrences with a stable key composed from calendar ID, UID, and recurrence ID or occurrence start.
- Use one visibility pipeline everywhere: enabled calendar, enabled title, quick include/exclude text, then manually hidden occurrence.
- Convert visible data into a presentation-independent month/layout model. The screen and print paths consume the same visible occurrence set; print preview and printer output consume the exact same `FixedDocument` generation path.
- Store the Yahoo app password only in Windows Credential Manager. Settings, cache, fixtures, logs, crash information, and PR artifacts must never contain it.
- Keep network, parsing, cache, and print generation work off the WPF UI thread where it could make the app unresponsive. Support cancellation where month navigation or refresh supersedes work.
- Test with sanitized fixtures by default. Real-Yahoo tests are optional, explicitly enabled, and supplied credentials only through uncommitted environment variables.

---

## Phase 1 — Development toolchain and solution foundation

### Goal

Make a clean Windows development machine able to restore, build, test, publish, and compile an installer reproducibly. Establish the project boundaries and quality gates needed by all later phases without implementing product behavior prematurely.

### Tool installation and configuration

Document and validate the following prerequisites in `docs/development.md`:

- Git and the GitHub CLI for the branch/PR workflow;
- .NET 8 SDK with the Windows Desktop/WPF targeting pack;
- Visual Studio 2022 Community or Build Tools with the .NET desktop build tools when an IDE/full MSBuild environment is needed;
- Inno Setup 6.3 or newer and its `ISCC.exe` compiler;
- a Windows PDF printer or physical printer for later print-dialog validation;
- a clean non-administrator Windows account or disposable Windows VM/Sandbox for installer acceptance testing;
- optional Authenticode tooling (`signtool.exe`) for a supplied certificate, without requiring signing for local development.

Add a repository-pinned `global.json` for .NET 8 with an explicit roll-forward policy. Do not pin to a developer-specific absolute path. Record commands that verify every prerequisite and distinguish mandatory build tools from tools required only for packaging or manual acceptance.

Current baseline observed before implementation: .NET SDK 8.0.424 and Git 2.48.1 are installed; `ISCC.exe` is not currently on `PATH`. Phase 1 must install or make Inno Setup discoverable and then prove it can compile a minimal installer input non-interactively.

### Repository bootstrap

Create the structure proposed by the specification:

```text
YahooMonthPrint.sln
src/
  YahooMonthPrint.App/
  YahooMonthPrint.Core/
  YahooMonthPrint.YahooCalDav/
  YahooMonthPrint.Printing/
tests/
  YahooMonthPrint.Core.Tests/
  YahooMonthPrint.YahooCalDav.Tests/
  YahooMonthPrint.Printing.Tests/
installer/
docs/
```

Configure:

- nullable reference types, implicit usings, deterministic builds, warnings-as-errors for repository code, and analyzers in shared build properties;
- centralized NuGet versions and a deliberate dependency policy;
- xUnit test projects and code coverage collection;
- `Ical.Net` in the Yahoo CalDAV layer after checking its .NET 8 compatibility, recurrence/exception/timezone behavior, license, and current maintenance status;
- a small composition root in the WPF app so later services can be replaced with deterministic fakes in tests and design-time/manual demos;
- `.gitignore` entries for build output, local settings, logs, publish output, installer output, secrets, and manual-integration-test configuration;
- sanitized fixture directories and a naming convention for CalDAV XML and ICS fixtures;
- CI on a Windows runner to restore, build, test, and publish the self-contained `win-x64` application;
- a packaging smoke path that invokes `ISCC.exe` against a minimal non-production installer definition, proving the compiler is available before the final installer work;
- `docs/architecture.md` with dependency direction, key interfaces, cancellation/error boundaries, and the read-only Yahoo guarantee;
- `docs/release.md` with unsigned local-build steps and a signing hook that accepts secrets only from the release environment.

The WPF app may contain only a minimal launchable shell and dependency wiring in this phase. Product calendar behavior belongs in Phase 2.

### Validation

Run from a fresh clone or a clean working tree:

- `dotnet --info` reports a compatible .NET 8 SDK and Windows Desktop runtime/targeting support;
- `dotnet restore YahooMonthPrint.sln` succeeds;
- `dotnet build YahooMonthPrint.sln --configuration Release --no-restore` succeeds with no warnings;
- `dotnet test YahooMonthPrint.sln --configuration Release --no-build` succeeds;
- `dotnet publish` creates a self-contained `win-x64` WPF executable;
- the published shell launches on a Windows machine without a separately installed .NET runtime;
- the Inno Setup smoke script compiles non-interactively;
- the same commands pass in CI;
- a repository scan finds no credentials or generated personal data.

### Phase 1 PR acceptance gate

- A new contributor can follow `docs/development.md` from a clean Windows environment and reproduce all build/test/publish/package checks.
- Project references enforce the intended dependency direction.
- CI is required and green.
- No production feature or P1 scope has leaked into the foundation PR.
- Tool versions or discovery rules are explicit enough to prevent “works only on one machine” behavior.

---

## Phase 2 — Offline month experience and deterministic filtering

### Goal

Deliver the complete interactive month-view experience using deterministic fake data. This phase proves calendar calculations, rendering, filtering, occurrence identity, and accessibility without involving credentials, network behavior, or print APIs.

### Core model and behavior

Implement in `YahooMonthPrint.Core`:

- `CalendarSource`, `CalendarOccurrence`, `OccurrenceKey`, `MonthViewState`, detail-level and quick-filter mode types;
- local-time and all-day semantics that do not discard source timezone information needed later;
- a Sunday-first five- or six-week grid range generator that includes leading/trailing dates and exposes the exact fetch range Phase 3 will use;
- description normalization for escaped/newline-rich plain text, meaningful paragraph boundaries, excessive whitespace, and HTML-like input that must remain inert text;
- the deterministic visibility pipeline defined in the specification;
- distinct-title generation for “Items This Month” and exact-title enable/disable state;
- occurrence-only hide/restore behavior, including “Restore All”; and
- immutable/raw occurrence retention so filter changes never require refetching.

### WPF application experience

Implement in `YahooMonthPrint.App`:

- a conventional Sunday-through-Saturday month grid with subdued out-of-month dates;
- previous month, next month, and Today navigation, including month/year boundaries;
- fake college-schedule data that includes timed events, all-day events, multiline descriptions, recurring-looking occurrences, an exam exception, dense days, and multiple calendars;
- `Titles Only`, `Compact`, and `Detailed` display modes, with Detailed and three description lines as defaults;
- readable wrapping for title, time, description, and optional location;
- calendar checkboxes, distinct-title checkboxes, Show All/Hide All, quick text filtering in both modes, and a short UI-safe debounce;
- a keyboard-accessible hover/focus Hide control for each occurrence, with an ordinary UI glyph and tooltip rather than an emoji;
- an expandable Hidden Items list with individual Show actions and Restore All;
- status/event counts based on the visible occurrence set;
- view-model commands and observable state that remain testable without rendering WPF windows; and
- basic accessibility: sensible tab order, access names/tooltips for icon controls, keyboard activation, adequate targets, and no color-only meaning.

The Refresh button can invoke a fake source and show deterministic progress/error states, but Yahoo setup, persistence, and real networking remain out of scope.

### Tests

Add automated coverage for:

- five- and six-week month grids, leap years, and year boundaries;
- inclusion of out-of-month grid dates in the requested range;
- all-day and timed event ordering/display data;
- multiline and hostile/HTML-like description normalization;
- detail-level projections and description-line limits;
- case-insensitive substring matching over title, description, and location;
- include and exclude quick-filter modes;
- calendar and exact-title filters;
- the exact filter ordering when multiple filters are active;
- unique occurrence keys, especially separate instances from one recurring UID;
- hiding only one occurrence, restoring it, and restoring all;
- deterministic visible counts and title-list contents; and
- cancellation/debounce behavior where practical without timing-fragile tests.

### Manual acceptance scenario

Using only the built-in fake schedule, verify that the current month is useful at normal desktop sizes; the Calculus exam description is visible; every filter updates the month immediately; hiding one Calculus occurrence leaves other Calculus occurrences visible; keyboard users can hide and restore it; and month navigation remains responsive.

### Phase 2 PR acceptance gate

- The complete non-network month/filter workflow can be demonstrated with no Yahoo account.
- Core logic has no WPF dependency and is covered by deterministic tests.
- UI and view models consume the same visibility result rather than reimplementing filtering.
- No operation is labeled or behaves like deletion.
- Dense content is visibly constrained but not silently claimed to be print-ready; printing remains Phase 4.

---

## Phase 3 — Secure Yahoo CalDAV connection, recurrence, cache, and recovery

### Goal

Replace the fake source with a secure, read-only Yahoo source and provide the first-run, settings, refresh, caching, and failure behavior needed for everyday use. Retain a deliberate demo/fake mode for automated tests and UI development.

### CalDAV and ICS integration

Implement in `YahooMonthPrint.YahooCalDav`:

- an injectable `HttpClient` pipeline that accepts only HTTPS endpoints and applies Basic authentication without exposing secrets to logs or exceptions;
- standards-based authenticated-principal, calendar-home-set, and calendar-collection discovery from `caldav.calendar.yahoo.com` rather than hard-coding a user's collection URL;
- safe URI resolution, namespace-aware WebDAV XML parsing, display names, stable identifiers, resourcetype, and optional color metadata;
- bounded `calendar-query` REPORT requests for the entire visible month grid and selected calendars;
- response parsing that isolates malformed resources so one bad item does not discard the rest;
- Ical.Net-based parsing and recurrence expansion for VEVENT, RRULE, RDATE, EXDATE, RECURRENCE-ID overrides, cancellations, all-day values, UTC/local values, and VTIMEZONE;
- conversion to Core occurrences in the Windows user's local timezone while preserving all-day dates and correct DST behavior;
- cancellation and stale-response protection when refresh/navigation changes the requested range; and
- typed failures for authentication, connectivity, server/protocol, and per-resource parsing errors, with technical detail separated from friendly UI messages.

Enforce the read-only contract with both API design and handler tests that fail if production code attempts an unsafe scheme or an event-mutating HTTP method.

### Credentials, first run, and settings

Implement in `YahooMonthPrint.App` behind narrow interfaces:

- the four-screen first-run wizard (Welcome, Yahoo Account, Test Connection, Choose Calendars);
- the external Yahoo Account Security link with an explicit user action;
- Windows Credential Manager storage for the app password, keyed predictably per account/application;
- non-secret settings under `%LOCALAPPDATA%\YahooMonthPrint` for account name, chosen calendars, display/filter preferences, and print defaults needed later;
- Settings pages for account status/test/change password/disconnect, calendars, display, printing defaults, and privacy;
- Disconnect behavior that deletes the local credential, invalidates discovered account/calendar state, and returns to first run while explaining that the Yahoo app password is not revoked at Yahoo; and
- secret-safe validation so password controls, binding, logs, exception messages, settings, and test artifacts do not retain the credential unnecessarily.

### Cache, refresh, and diagnostics

Implement:

- a versioned, atomic local cache containing normalized event/calendar data but no credentials;
- immediate cached-month display at startup followed by an asynchronous Yahoo refresh;
- manual refresh for the visible grid range and selected calendars;
- last-successful-refresh status and replacement of cache only after a successful, internally consistent refresh;
- offline use and printing-from-cache readiness with a clear stale timestamp and Try Again action;
- rediscovery when a persisted calendar URL is rejected or no longer valid;
- corruption/version handling that safely ignores or quarantines an unreadable cache rather than crashing;
- Clear Cached Calendar Data without disconnecting the credential unless the user chooses Disconnect; and
- lightweight rotating logs under LocalAppData with request category, status, resource identifier, timestamp, version, and exception type—but never credentials, Authorization headers, or full event descriptions by default.

### Tests

Use sanitized local handlers/fixtures for:

- principal, home-set, and multi-calendar discovery;
- namespaces, relative/absolute hrefs, encoded paths, missing optional properties, and non-calendar collections;
- bounded calendar-query request bodies and complete six-week range boundaries;
- recurring rules, RDATE, EXDATE, modified occurrences, cancelled occurrences, RECURRENCE-ID, UTC/local values, and DST transitions;
- all-day events across timezone boundaries;
- malformed XML/ICS isolation and friendly error classification;
- explicit rejection of HTTP and mutating HTTP methods;
- redaction of Authorization and sensitive event/credential values;
- Credential Manager create/read/replace/delete behavior through a test double plus a Windows-only integration test that uses a disposable target name;
- settings/cache round trips, atomic replacement, stale cache, corrupt cache, and clear/disconnect semantics;
- startup cached display followed by success/failure refresh state transitions; and
- cancellation or out-of-order response handling during rapid navigation.

Provide optional real-Yahoo integration tests using `YMP_TEST_YAHOO_USER` and `YMP_TEST_YAHOO_APP_PASSWORD`. They must be skipped unless explicitly enabled and must perform discovery/query only.

### Manual acceptance scenario

With a non-production Yahoo test account, complete first run, choose calendars, retrieve the spec's recurring Calculus/Physics scenario, confirm the modified exam occurrence and excluded/cancelled occurrence behavior, restart without re-entering the app password, verify manual refresh, disconnect, and verify cached/offline behavior. Inspect settings, cache, and logs to confirm the password and Authorization header are absent. Confirm captured requests contain no mutation method.

### Phase 3 PR acceptance gate

- A user can connect with a Yahoo app password, select calendars, and use the Phase 2 experience with real data.
- Recurrence exceptions and DST tests pass from sanitized fixtures.
- Credential, HTTPS-only, log-redaction, and read-only method guarantees have explicit tests.
- Loss of network access does not prevent viewing cached data or crash the app.
- Real-account evidence is sanitized before being attached to the PR.

---

## Phase 4 — WYSIWYG printing, installer, and v1.0 release validation

### Goal

Complete the primary value proposition: preview and print the currently visible month safely and legibly, install the self-contained application without elevation, and exercise every v1.0 release/security criterion end to end.

### Print model and rendering

Implement in `YahooMonthPrint.Printing`:

- a presentation-independent `MonthLayoutModel` built from the same displayed month, grid range, detail settings, and visible occurrence result used by the app;
- deterministic `FixedDocument`/`FixedPage` rendering for seven equal columns and five or six week rows;
- compact month header, date numbers, out-of-month styling, titles, times, descriptions, and optional locations;
- landscape Letter and A4 layout with explicit margins and grayscale-readable boundaries/typography;
- measurement-based per-cell and page overflow detection that never silently removes an event;
- automatic reduction in the specified order: remove location, reduce description lines, tighten spacing, reduce font size no lower than the practical minimum, then create an additional-details page;
- an overflow-details page that clearly associates omitted detail with date, time, and title; and
- deterministic diagnostics explaining which reduction step was required.

Implement in `YahooMonthPrint.App`:

- the application's print-preview window with printer, paper, orientation, margins, detail level, description lines, and safe scale/font controls;
- a visible overflow warning and choices to reduce detail automatically, use smaller text within the safe minimum, or print overflow details on page 2;
- the normal Windows printer dialog and printer-capability validation/fallbacks;
- preview pagination driven by exactly the same `FixedDocument` instance/model that is sent to the printer; and
- exclusion of interactive controls, filter panels, hover buttons, and hidden occurrences from output.

Screen and print do not need pixel-identical typography, but they must have identical month/date scope and occurrence visibility. Physical overflow transformations must be explicitly previewed and never silently change filtering.

### Print tests and visual verification

Add automated tests for:

- Letter and A4 landscape page geometry, margins, seven columns, and five/six rows;
- visible occurrence parity between month state and layout model;
- hidden/filter-excluded occurrences never entering the print model;
- deterministic ordering and all-day/timed formatting;
- each automatic overflow-reduction step, minimum font size, and page-2 fallback;
- dense individual days, long unbroken text, multiline descriptions, and no silent event loss;
- stable document/page counts for representative fixtures; and
- printer cancellation/error handling without loss of app state.

Render representative FixedDocuments to a reviewable format or capture preview screenshots for visual regression review. Manually check Letter and A4 output with Microsoft Print to PDF and at least one real or virtual Windows printer configuration, including grayscale readability.

### Installer and release pipeline

Complete `installer/YahooMonthPrint.iss` and release automation so that:

- the release artifact is one `YahooMonthPrint-Setup.exe`;
- install mode uses lowest privileges and produces no UAC prompt;
- files install beneath `%LOCALAPPDATA%\Programs\YahooMonthPrint`;
- the package contains the self-contained `win-x64` publish output;
- Start Menu, optional desktop shortcut, Add/Remove Programs entry, and conventional uninstaller work;
- install, upgrade, repair/reinstall as supported, and uninstall preserve/remove user data according to an explicit documented policy;
- installer and application version metadata are derived from one release version source;
- Authenticode signing is optional and injected only in the release environment; no certificate/private-key path or secret is committed; and
- CI produces checksummed unsigned artifacts for development, while the documented release path can sign when credentials are supplied.

### Full acceptance and release hardening

Run all automated test suites and the complete manual acceptance scenario from the specification. Additionally verify on a clean standard-user Windows 10 or 11 environment:

- installation and first launch require no administrator access or separate .NET installation;
- setup, reconnection, cache/offline restart, navigation, every filter, individual hide/restore, preview, and print work together;
- the visible occurrence set exactly matches preview/output, subject only to previewed overflow-detail reduction;
- both Letter and A4 remain legible in landscape and do not rely only on color;
- uninstall is conventional and leaves no credential behind; any retained cache/settings behavior matches the documented uninstall choice;
- Yahoo calendar data is unchanged after the full run;
- logs/settings/cache/install artifacts contain no app password or Authorization header;
- only HTTPS and read-only request methods were observed; and
- malformed data, offline startup, printer cancellation, and unexpected printer/server errors yield actionable non-technical messages with optional technical details.

Perform a final P0 traceability review against sections 3, 30, 31, 32, and 33 of the specification. File P1 ideas separately; do not add them to the release PR.

### Phase 4 PR acceptance gate

- Automated build/test/publish/installer checks are green.
- Print preview and printer output are generated by the same deterministic document path and no event is silently dropped.
- A clean standard-user install/uninstall and the complete real-Yahoo manual scenario have documented, sanitized evidence.
- Every security acceptance criterion passes.
- The single installer artifact and checksum are produced reproducibly, with signing support documented but no signing secret in the repository.
- All P0 release criteria are either demonstrated or linked to passing automated tests, and there are no unresolved actionable review comments.

## Final v1.0 completion condition

Phase 4 merging is necessary but not sufficient if any acceptance evidence is missing. v1.0 is ready to tag only after the merged default branch has been rebuilt through the release workflow, its resulting installer has passed the clean-machine standard-user test, and the complete real-Yahoo/read-only/print scenario has passed against that same build. Any defect found during that validation is fixed in a focused follow-up PR using the same review and adjudication loop before the release tag is created.
