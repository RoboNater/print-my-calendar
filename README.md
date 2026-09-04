# Yahoo Month Print

Yahoo Month Print is a read-only Windows desktop application for viewing and printing useful full-month layouts from Yahoo Calendar. Implementation follows a four-phase, test-first delivery plan.

The product requirements are in [`yahoo-month-print-spec.md`](yahoo-month-print-spec.md), and the phased delivery plan is in [`dev-notes/implementation-plan.md`](dev-notes/implementation-plan.md).

Developer setup and validation instructions are in [`docs/development.md`](docs/development.md).

## Current implementation

Phase 2 provides a complete offline month-view demo backed by a deterministic sample college schedule. Run it with:

```powershell
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj
```

The demo supports month navigation, three detail levels, text/calendar/title filtering, and occurrence-only hide/restore behavior. Yahoo connectivity and credentials are intentionally deferred to Phase 3; printing remains Phase 4 scope.
