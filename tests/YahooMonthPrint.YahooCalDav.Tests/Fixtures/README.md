# Sanitized CalDAV and iCalendar fixtures

Phase 3 tests will keep server responses beneath this directory. Fixtures must be synthetic or irreversibly sanitized: no real Yahoo ID, calendar URI, UID, event text, app password, cookie, token, or Authorization header may be committed.

Use these subdirectories and names:

- `Discovery/<scenario>.xml` for principal, calendar-home, and collection `multistatus` responses;
- `Queries/<scenario>.xml` for `calendar-query` responses; and
- `Calendars/<scenario>.ics` for standalone RFC 5545 payloads.

Pair unusual fixtures with a test whose name explains the behavior being protected. Prefer reserved domains such as `example.test`, stable timestamps, and obviously fictional event text. Keep HTTP status/headers in test code or a separate metadata file only when they are necessary to the scenario.
