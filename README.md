# Yahoo Month Print

Yahoo Month Print is a read-only Windows desktop application for viewing and printing useful full-month layouts from Yahoo Calendar. Implementation follows a four-phase, test-first delivery plan.

The product requirements are in [`yahoo-month-print-spec.md`](yahoo-month-print-spec.md), and the phased delivery plan is in [`dev-notes/implementation-plan.md`](dev-notes/implementation-plan.md).

Developer setup and validation instructions are in [`docs/development.md`](docs/development.md).

## Current implementation

Phase 4 adds WYSIWYG print preview/output, measured overflow handling, Letter/A4 support, a self-contained per-user installer, checksummed CI artifacts, and optional release signing. Run the normal application with:

```powershell
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj
```

For UI development without a Yahoo account, retain the deterministic college schedule with:

```powershell
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj -- --demo
```

Both modes support month navigation, three detail levels, text/calendar/title filtering, occurrence-only hide/restore behavior, print preview, and Windows printing. The preview and printer use the same deterministic `FixedDocument`; filtered or hidden occurrences never enter the print model.
