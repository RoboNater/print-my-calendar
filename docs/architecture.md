# Architecture foundation

## Dependency direction

```text
YahooMonthPrint.App
  -> YahooMonthPrint.Core
  -> YahooMonthPrint.YahooCalDav -> YahooMonthPrint.Core
  -> YahooMonthPrint.Printing    -> YahooMonthPrint.Core

YahooMonthPrint.Core -> no application, WPF, printing, or network project
```

- `YahooMonthPrint.Core` owns calendar/occurrence models, month calculations, filters, visibility state, and persistence abstractions. It targets plain `net8.0` and must not reference WPF or HTTP implementation details.
- `YahooMonthPrint.YahooCalDav` owns HTTPS/WebDAV discovery, bounded CalDAV queries, ICS parsing, recurrence expansion, and conversion to Core models. It is the only project that references Ical.Net.
- `YahooMonthPrint.Printing` owns presentation-independent print layout and WPF `FixedDocument` rendering. It targets `net8.0-windows` with WPF enabled.
- `YahooMonthPrint.App` is the WPF composition root and owns windows, view models, user commands, Windows Credential Manager integration, and LocalAppData implementations.

Tests mirror the library boundaries. Network tests use injectable HTTP handlers and sanitized fixture files; they never depend on a developer's Yahoo account by default.

## Read-only Yahoo boundary

The Yahoo layer will expose discovery and range-query interfaces only. There will be no create, update, or delete operation in the production abstraction. Its HTTP pipeline will reject plaintext HTTP and mutation methods, and tests will assert that only read-oriented CalDAV/WebDAV methods are emitted. User filters and occurrence hiding operate exclusively on local view state.

## State flow

```text
credential + non-secret settings
              |
              v
Yahoo CalDAV query -> normalized Core occurrences -> visibility pipeline
                                                      |            |
                                                      v            v
                                                 month screen   print model
                                                                      |
                                                                      v
                                                             FixedDocument
                                                             preview + print
```

Fetched occurrences remain available in memory while filters change. Screen and print receive one authoritative visible-occurrence result. Preview and printer output use the same generated document rather than rendering the live UI tree.

## Async and failure boundaries

- Network, parsing, cache I/O, and substantial print generation must not block the WPF UI thread.
- Refresh/navigation operations accept cancellation and ignore stale completions.
- Authentication, connectivity, protocol, malformed-resource, cache, and printer failures become typed results with user-facing summaries and separately available technical details.
- One malformed calendar resource must not invalidate other successfully parsed resources.
- Cache replacement is atomic and occurs only after a complete successful refresh.

## Composition

The WPF `App` class is the composition root. Concrete services will be constructed there and supplied to view models/windows through constructors. Interfaces live with the consumer-facing abstraction, and deterministic fakes remain available for unit tests and the Phase 2 offline demo. A general-purpose dependency injection package is not required unless constructor wiring becomes materially difficult.
