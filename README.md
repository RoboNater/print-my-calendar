# Yahoo Month Print

Yahoo Month Print is a read-only Windows desktop application for viewing and printing useful full-month layouts from Yahoo Calendar. Implementation follows a four-phase, test-first delivery plan.

The product requirements are in [`yahoo-month-print-spec.md`](yahoo-month-print-spec.md), and the phased delivery plan is in [`dev-notes/implementation-plan.md`](dev-notes/implementation-plan.md).

Developer setup and validation instructions are in [`docs/development.md`](docs/development.md).

## Current implementation

Phase 3 adds the secure, read-only Yahoo CalDAV connection, recurrence expansion, first-run setup, Windows Credential Manager integration, settings, diagnostics, and an offline cache. Run the normal application with:

```powershell
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj
```

For UI development without a Yahoo account, retain the deterministic college schedule with:

```powershell
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj -- --demo
```

Both modes support month navigation, three detail levels, text/calendar/title filtering, and occurrence-only hide/restore behavior. Printing remains Phase 4 scope.
