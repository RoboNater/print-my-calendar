# Phase 2 validation

## Scope

Phase 2 implements the offline month experience and deterministic filtering described in `dev-notes/implementation-plan.md`:

- Core occurrence, source, key, month-state, detail-level, and quick-filter models;
- Sunday-first five/six-week grid and exact visible fetch range;
- plain-text description normalization and detail projection;
- one authoritative calendar/title/text/manual-hide visibility pipeline;
- deterministic multi-calendar college schedule including recurrence-like instances, an exam override, all-day/timed events, multiline details, and a dense day;
- WPF month navigation, detail controls, filters, accessible occurrence hide controls, hidden-item restoration, and live counts/status; and
- cancellation-safe navigation plus a UI-safe text-filter debounce.

Yahoo networking, credentials, persistence/cache, printing, and installer behavior remain explicitly out of scope for this phase.

## Automated evidence

Run from the repository root:

```powershell
dotnet restore YahooMonthPrint.sln
dotnet format YahooMonthPrint.sln --verify-no-changes --no-restore
dotnet build YahooMonthPrint.sln --configuration Release --no-restore
dotnet test YahooMonthPrint.sln --configuration Release --no-build
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj --configuration Release --no-build -- --smoke-test
```

The test suite covers compact and six-row grids, leap/year boundaries, out-of-month range dates, all-day/timed ordering, hostile and multiline descriptions, detail projections, both quick-filter modes, calendar/title filters, filter ordering, occurrence identity, one-occurrence hiding, individual/all restoration, deterministic counts/title lists, debounce supersession, navigation cancellation, and year-boundary navigation.

The `--smoke-test` path constructs the real WPF window and parses the full Phase 2 XAML without leaving a visible window open.

## Manual review checklist

- Start in Detailed mode with three description lines and locations enabled.
- Confirm the Calculus exam text is visible in its day cell.
- Exercise Previous, Today, and Next across a year boundary.
- Switch among Titles Only, Compact, and Detailed.
- Filter on `exam` in both Show only matching and Hide matching modes.
- Disable a calendar and an exact title, then use Show All and Hide All.
- Hover or keyboard-focus an occurrence's Hide control; hide one Calculus instance and confirm the other instances remain.
- Expand Hidden items, restore one item, and use Restore All.
- Confirm dense days scroll within their cells and out-of-month dates remain visually subdued.

No credential, Authorization header, network response, or private calendar data is present in this phase.
