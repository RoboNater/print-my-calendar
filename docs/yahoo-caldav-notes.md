# Yahoo CalDAV implementation notes

Phase 3 connects to `https://caldav.calendar.yahoo.com/` with the Yahoo account name and a separately generated app password. The app password is stored only as a generic Windows Credential Manager entry named `YahooMonthPrint:YahooCalendar:<normalized-account>`. JSON settings contain the account name, discovered calendar metadata, selection state, and display/printing preferences, but no credential.

## Read-only protocol path

The client performs the standard WebDAV/CalDAV sequence:

1. `PROPFIND` the service root for `current-user-principal`;
2. `PROPFIND` the principal for `calendar-home-set`;
3. depth-1 `PROPFIND` the home set for calendar collections; and
4. bounded `REPORT calendar-query` requests for every selected calendar and the entire visible month-grid range.

The HTTP pipeline rejects plaintext endpoints and every method except `GET`, `PROPFIND`, and `REPORT`. Discovery hrefs must remain on the authenticated HTTPS origin, preventing credentials from being forwarded to a cross-origin URI. Production code exposes no create, update, or delete operation.

Ical.Net parses each returned resource and expands recurrence rules, dates, exclusions, overrides, and cancellations. Timed occurrences are converted through UTC into the Windows user's local timezone; all-day dates retain their source dates.

## Completeness and issue #4

`ICalendarOccurrenceSource.LoadAsync` has an explicit complete-or-fail contract. A request failure for any selected calendar makes the whole refresh fail, and the prior cache remains untouched. A malformed individual ICS resource is a known-size gap: it is isolated, counted, shown in the status, and logged by resource identifier and exception type. It is never silently treated as a fully clean response.

This boundary is important because Phase 4 will print from the same visible data. An unknown-size partial calendar response must not become a fresh cache entry that can later be printed without a warning.

## Cache and diagnostics

The versioned cache is written atomically under `%LOCALAPPDATA%\YahooMonthPrint`. It contains normalized calendar occurrences only. Startup displays a matching cached grid immediately and then refreshes Yahoo asynchronously. Network/authentication/server failures retain the cached view with an actionable status. A corrupt or unsupported cache is ignored; corrupt JSON is quarantined with a timestamped filename.

Rotating logs live under `%LOCALAPPDATA%\YahooMonthPrint\Logs`. Log fields are limited to timestamp, app version, request category, status, resource identifier, and exception type. Exception messages, event descriptions, credentials, and Authorization headers are not recorded.

## Manual test boundary

The automated suite uses sanitized WebDAV and ICS responses. The optional real-Yahoo test is disabled unless `YMP_RUN_YAHOO_INTEGRATION=1` and both documented credential environment variables are present. It performs discovery and bounded query operations only. Real-account evidence must be sanitized before sharing.
