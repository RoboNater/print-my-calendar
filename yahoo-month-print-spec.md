# Yahoo Month Print
## Product and Implementation Specification

**Working name:** Yahoo Month Print  
**Target platform:** Windows 10/11, x64  
**Primary user:** Non-technical Yahoo Calendar user  
**Application type:** Read-only Windows desktop calendar viewer and printer  
**Initial release target:** v1.0

---

# 1. BLUF

Build a simple Windows desktop application that connects directly to a user's Yahoo Calendar, retrieves current calendar events, and produces a substantially more useful printable full-month calendar than Yahoo Calendar's built-in month printing.

The application must:

- install from a single `.exe` installer;
- install entirely for the current user without administrator privileges;
- require minimal initial configuration;
- securely remember the Yahoo connection;
- retrieve calendar data directly from Yahoo via CalDAV;
- display a conventional full-month calendar;
- display useful portions of each event's description/details directly in the month cells;
- allow events to be easily shown or suppressed;
- provide quick text-based filtering;
- provide list-based filtering;
- allow an individual visible event to be hidden by clicking a control that appears while hovering over it;
- show exactly what will be printed;
- print using the normal Windows printer dialog;
- never modify the user's Yahoo Calendar.

The core design principle is:

> **What the user sees in the month preview is what gets printed.**

---

# 2. Primary Use Case

A college student maintains a class schedule in Yahoo Calendar.

Recurring events might look like:

**Title**  
`Calculus II`

**Description**  
`Exam 2 today. Chapters 5–7. Bring calculator.`

Yahoo's normal month view may show only:

`Calculus II`

The desired printed calendar should instead be capable of showing something like:

**Calculus II**  
9:00 AM  
Exam 2 today.  
Chapters 5–7.

The student should be able to suppress irrelevant events, such as routine reminders or another calendar, so that important information remains readable on a printed month.

---

# 3. Goals

## P0 — Required for v1.0

1. Easy installation on Windows.
2. No administrative privileges.
3. Simple Yahoo Calendar setup.
4. Secure credential storage.
5. Direct retrieval of current Yahoo Calendar data.
6. Correct handling of recurring events.
7. Full-month calendar display.
8. Event descriptions visible in month cells.
9. Previous-month / next-month navigation.
10. Manual Refresh button.
11. Automatic refresh when the application starts.
12. Text-based filtering.
13. List-based filtering.
14. Individual-event show/hide controls.
15. Print preview.
16. Native Windows printing.
17. Letter and A4 paper support.
18. Landscape month printing.
19. Read-only access to Yahoo data.
20. Graceful offline/error behavior.

## P1 — Desirable after v1.0

- configurable automatic refresh interval;
- saved named filter sets;
- automatic emphasis of keywords such as EXAM, TEST, QUIZ and DUE;
- optional calendar colors;
- dark/light application themes;
- application update checking;
- configurable fonts;
- direct PDF generation rather than relying on "Microsoft Print to PDF."

Do not allow P1 work to delay a reliable P0 implementation.

---

# 4. Non-Goals

v1.0 does NOT need to:

- create Yahoo Calendar events;
- edit Yahoo Calendar events;
- delete Yahoo Calendar events;
- alter recurring events;
- synchronize changes back to Yahoo;
- support email;
- support contacts;
- support Google Calendar;
- support Outlook/Exchange;
- run as a background Windows service;
- require a web server;
- require Docker;
- require Python or Node.js to be installed;
- require the user to understand CalDAV.

The application should deliberately use Yahoo Calendar in **read-only mode**.

---

# 5. Recommended Technology Stack

Use:

- **C#**
- **.NET 8**
- **WPF**
- `HttpClient` for CalDAV/WebDAV communication
- a mature .NET iCalendar library such as **Ical.Net** for ICS parsing, recurrence expansion and timezone handling
- Windows Credential Manager for secrets
- `FixedDocument` / `FixedPage` / `DocumentPaginator` for printing
- Inno Setup or an equivalent reliable installer capable of per-user installation

Do not use Electron unless a compelling implementation issue requires it.

Avoid dependencies on:

- Python;
- Node.js runtime;
- Java;
- external browser extensions;
- Office;
- Outlook.

The application should be shipped as a self-contained .NET application.

---

# 6. Installation and Deployment

## 6.1 Installer

The release artifact presented to the end user shall be a single file such as:

`YahooMonthPrint-Setup.exe`

The installer must:

- require no administrator privileges;
- avoid a Windows UAC elevation prompt;
- install under the current user's profile, preferably:

`%LOCALAPPDATA%\Programs\YahooMonthPrint`

- create an Add/Remove Programs entry;
- optionally create a desktop shortcut;
- create a Start Menu shortcut;
- include all runtime dependencies required by the application;
- provide a conventional uninstaller.

Use a per-user installer configuration such as Inno Setup's lowest-privilege mode.

The target PC must NOT need a separate .NET installation.

## 6.2 Code Signing

The build/release process should support Authenticode signing if a certificate is supplied.

Signing credentials must never be committed to the repository.

Unsigned development builds are acceptable.

---

# 7. First-Run Experience

First run should display a wizard rather than the normal calendar screen.

## Screen 1 — Welcome

Text approximately:

**Yahoo Month Print**

"Print your Yahoo Calendar with event details visible directly in the monthly calendar."

Buttons:

- `Get Started`
- `Cancel`

## Screen 2 — Yahoo Account

Fields:

- Yahoo email address / Yahoo ID
- Yahoo app password

Include a short explanation:

"Yahoo requires a separate app password for third-party calendar applications. Do not enter your normal Yahoo password."

Provide:

`Open Yahoo Account Security`

This launches the appropriate Yahoo account-security page in the user's default browser.

Do not embed Yahoo login into the application.

## Screen 3 — Test Connection

Button:

`Connect to Yahoo`

While connecting:

`Connecting...`

Success:

`Connected successfully.`

Failure messages must be understandable to a non-technical user.

Examples:

**Authentication failed**

"Yahoo did not accept the account name or app password. Verify the Yahoo ID and create a new app password if necessary."

**Cannot reach Yahoo**

"Yahoo Calendar could not be reached. Check your Internet connection and try again."

Provide:

- `Try Again`
- `Back`

Do not expose HTTP status codes unless the user opens technical details.

## Screen 4 — Choose Calendars

After successful CalDAV discovery, show calendars returned by Yahoo as a checkbox list.

Example:

☑ My Calendar  
☑ College  
☐ Birthdays  
☐ Holidays

Select all normal writable/personal calendars by default, but allow the user to decide.

Button:

`Finish`

After Finish, open the current month.

---

# 8. Yahoo Integration

Yahoo Calendar currently supports CalDAV using the host:

`caldav.calendar.yahoo.com`

The application shall use HTTPS only.

Authentication shall use:

- Yahoo ID/email;
- Yahoo-generated app password.

Never request or store the user's normal Yahoo account password.

## 8.1 CalDAV Discovery

Do not hard-code an individual user's calendar collection URL.

Starting from Yahoo's documented CalDAV service, perform normal CalDAV/WebDAV discovery to determine:

1. authenticated principal;
2. calendar home set;
3. available calendar collections;
4. calendar display names;
5. calendar identifiers/URLs;
6. optional calendar colors where available.

Persist discovered calendar identifiers, but rediscover if Yahoo indicates that they are invalid.

## 8.2 Event Retrieval

Retrieve events intersecting the displayed month.

The requested range should cover the complete six-week grid shown by the UI rather than only dates numerically belonging to that month.

Example:

If September's visual grid includes August 30 through October 10, retrieve enough data to correctly populate that complete grid.

Prefer CalDAV `REPORT` / `calendar-query` requests with an appropriate date range instead of downloading the user's entire calendar history.

## 8.3 ICS Processing

Correctly process:

- VEVENT;
- UID;
- SUMMARY;
- DESCRIPTION;
- LOCATION;
- DTSTART;
- DTEND;
- all-day events;
- RRULE;
- RDATE;
- EXDATE;
- RECURRENCE-ID;
- modified instances of recurring events;
- cancelled occurrences where represented;
- VTIMEZONE;
- UTC timestamps;
- local timestamps.

Use an established iCalendar parser/recurrence library rather than implementing recurrence parsing manually.

## 8.4 Recurring Events

This is a critical acceptance requirement.

A recurring class scheduled Monday/Wednesday at 10:00 must appear on all valid occurrences.

Exceptions must be honored.

For example:

- recurring class normally occurs every Monday;
- September 7 is excluded;
- September 14 contains an overridden description saying "MIDTERM."

The application must display:

- no occurrence on September 7;
- the modified occurrence and description on September 14.

## 8.5 Read-Only Guarantee

The Yahoo client layer shall expose no event-create, event-update or event-delete operations to the UI.

v1.0 shall never send PUT or DELETE requests modifying calendar resources.

---

# 9. Credential Security

Store the Yahoo app password using **Windows Credential Manager**.

Do not store credentials in:

- JSON settings files;
- XML configuration files;
- registry values in plain text;
- application logs;
- crash reports.

Store only non-secret configuration such as:

- Yahoo account name;
- selected calendars;
- filtering preferences;
- print preferences;

in a normal per-user settings file.

Suggested location:

`%LOCALAPPDATA%\YahooMonthPrint\`

Provide:

`Settings > Yahoo Account > Disconnect`

Disconnect shall:

1. remove the locally stored Yahoo app password;
2. clear discovered Yahoo calendar information as appropriate;
3. return the program to first-run configuration.

Explain that removing it locally does not revoke the Yahoo app password itself.

---

# 10. Main Window

Suggested layout:

```text
┌──────────────────────────────────────────────────────────────────┐
│ Yahoo Month Print            September 2026       ↻ Refresh      │
├──────────────────────────────────────────────────────────────────┤
│ ◀ Previous      Today        Next ▶         Print...   Settings │
├──────────────────────────────────────────────────────────────────┤
│ Filters                         │                                │
│                                │       MONTH CALENDAR           │
│ Search: [____________]         │                                │
│ ( ) Show matches only         │  Sun Mon Tue Wed Thu Fri Sat   │
│ (•) Hide matching            │                                │
│                                │                                │
│ Calendars                      │                                │
│ ☑ College                     │                                │
│ ☑ Personal                    │                                │
│ ☐ Holidays                    │                                │
│                                │                                │
│ Items this month              │                                │
│ ☑ Calculus II                 │                                │
│ ☑ Physics                     │                                │
│ ☐ Work Schedule               │                                │
│                                │                                │
│ Hidden items (3)              │                                │
│                                │                                │
├──────────────────────────────────────────────────────────────────┤
│ Last updated: 6:42 AM                       23 events displayed │
└──────────────────────────────────────────────────────────────────┘
```

The calendar receives most of the window area.

The filter sidebar may be collapsible.

---

# 11. Calendar Month View

Display a conventional seven-column calendar:

Sunday through Saturday by default.

Allow Monday-first as a future enhancement, but it is not required for v1.0.

Display either five or six week rows depending on the month, or consistently use six rows if that simplifies print layout.

Dates outside the selected month should remain visible but visually subdued.

## 11.1 Event Rendering

Each event should support displaying:

1. title;
2. start time;
3. description preview;
4. location, optionally.

Example:

```text
9:00 AM
Calculus II
EXAM 2 — Chapters 5–7
Bring calculator.
```

All-day events omit the time.

The title should be visually stronger than the detail text.

Long text shall wrap within the day cell.

Do not simply truncate descriptions to one line.

---

# 12. Detail-Level Control

Provide a toolbar setting:

`Details:`

- `Titles Only`
- `Compact`
- `Detailed`

## Titles Only

Show:

- time;
- event title.

## Compact

Show:

- time;
- title;
- first useful description line.

## Detailed

Show:

- time;
- title;
- multiple description lines;
- optional location.

Default:

`Detailed`

for the initial product use case.

Also provide a settings option:

`Maximum description lines per event`

Suggested choices:

- 1
- 2
- 3
- 4
- Unlimited on screen

Default:

`3`

The print preview may impose additional physical limits when necessary.

---

# 13. Filtering

Filtering is a central feature rather than an advanced setting.

All filtering must be applied identically to:

- month screen;
- print preview;
- printed output.

## 13.1 Quick Text Filter

Provide:

`Filter text: [________________]`

Perform a case-insensitive substring search.

Default searchable fields:

- title;
- description;
- location.

Provide two modes:

- `Show only matching`
- `Hide matching`

Examples:

`exam`

can show only events containing "exam."

`office hours`

can hide events containing "office hours."

No regular expressions are required in v1.0.

Filtering should update immediately while typing, with a small debounce if required.

## 13.2 Calendar List

Display every selected Yahoo calendar with a checkbox.

Example:

☑ College  
☑ Personal  
☐ Holidays

Unchecking a calendar immediately hides its events.

This selection should persist between application runs.

## 13.3 "Items This Month" List

Generate a list of distinct event titles appearing in the current month.

Example:

☑ Calculus II  
☑ Chemistry  
☑ Physics  
☐ Work Schedule  
☑ Study Group

Unchecking an item suppresses events with that exact title.

Provide:

- `Show All`
- `Hide All`
- optionally a search box for long lists.

For v1.0, these title-list selections may be month-session preferences rather than permanent rules.

If easy to implement safely, remember selections between months by exact event title.

## 13.4 Individual Event Hover Hide

When the mouse pointer hovers over an event in the month calendar, display a small eye/hide control in its upper-right corner.

Conceptually:

```text
┌──────────────────────┐
│ Calculus II       👁 │
│ EXAM 2               │
│ Chapters 5–7         │
└──────────────────────┘
```

The icon itself should use a conventional UI glyph rather than an emoji.

Clicking the hide control shall:

1. hide that specific occurrence immediately;
2. add the occurrence to `Hidden items`;
3. update the print preview/output.

It must NOT:

- delete the Yahoo event;
- modify the Yahoo event;
- suppress every recurring occurrence.

The identity of a hidden occurrence should be based on:

- calendar;
- UID;
- occurrence start time / recurrence ID.

## 13.5 Restore Hidden Items

The sidebar shall show:

`Hidden items (N)`

Clicking it expands a list such as:

```text
Calculus II — Sep 14, 9:00 AM        Show
Doctor Appointment — Sep 18          Show
```

Provide:

`Restore All`

Hidden occurrences are local display state only.

For v1.0 they may be remembered while the application is running.

Persistence across application launches is optional.

---

# 14. Filter Rule Semantics

Filtering should follow a deterministic pipeline:

1. Calendar enabled/disabled state
2. "Items This Month" enabled state
3. Quick text include filter, if active
4. Quick text exclude filter, if active
5. Individual occurrence hide state

An event failing any active visibility condition is hidden.

The underlying fetched event must remain in memory so filters can be changed without another Yahoo request.

---

# 15. Saved Filters — P1

Architecture should permit later addition of saved rules such as:

```text
☑ Hide "office hours"
☑ Hide "commute"
☐ Show only "Calculus"
```

Each saved rule should contain:

- friendly name;
- matching text;
- include or exclude;
- enabled/disabled state;
- fields to search.

Do not require this capability for initial v1.0 if it materially increases complexity.

---

# 16. Printing

Printing is a first-class feature.

Button:

`Print...`

should first display the application's own print preview.

## 16.1 Print Preview

The preview must use the same layout engine as the final printed document.

Controls:

- Printer
- Paper size
- Orientation
- Margins
- Detail level
- Description lines
- Font size / scaling where appropriate
- Print

Default:

- Paper: current Windows printer default, with Letter expected for US users;
- Orientation: Landscape;
- Fit: One calendar month to one page.

Also support A4.

## 16.2 WYSIWYG Requirement

Events currently hidden from the month screen shall not appear in print.

Events currently visible shall appear in print subject to physical overflow handling.

No filter controls, hover buttons or sidebar elements appear on the printed calendar.

## 16.3 Printed Header

Suggested header:

**September 2026**

Optional small secondary text:

`Printed September 3, 2026`

Do not waste significant vertical space with headers.

## 16.4 Month Grid

Printed output should contain:

- seven equal-width day columns;
- five or six week rows;
- date number;
- event contents;
- subtle separation between days;
- readable grayscale output.

The design must remain understandable on a black-and-white laser printer.

Do not rely exclusively on color for meaning.

---

# 17. Print Overflow Handling

A major risk is trying to fit excessive event detail into a single page.

The program shall detect when content does not fit at the requested font/detail level.

Do NOT silently discard events.

When overflow occurs, show a visible warning in print preview:

**Some event details do not fit on one page.**

Then offer choices such as:

- `Reduce detail automatically`
- `Use smaller text`
- `Print overflow details on page 2`

Recommended default:

`Reduce detail automatically`

Automatic reduction should proceed approximately:

1. remove location;
2. reduce description line limit;
3. slightly reduce event spacing;
4. slightly reduce font size;
5. if still overflowing, generate an overflow-details page.

Never make the main printed text unreasonably small merely to maintain one page.

Suggested practical minimum body font:

approximately 7 pt.

## 17.1 Overflow Details Page

When needed, page 2 may contain:

```text
September 2026 — Additional Event Details

September 14
Calculus II — 9:00 AM
Exam 2. Chapters 5–7. Bring calculator and student ID.

September 22
Physics
...
```

The primary month grid remains page 1.

---

# 18. Print Engine

Use a deterministic print-layout model.

Recommended implementation:

1. Convert visible month/events into a presentation-independent `MonthLayoutModel`.
2. Lay out this model onto WPF `FixedPage` objects.
3. Use the same `FixedDocument` for:
   - on-screen print preview;
   - printer output.

Avoid taking a screenshot of the UI.

Avoid printing the live WPF control tree directly if this results in unpredictable scaling.

The print renderer should be independently unit-testable where practical.

---

# 19. Live Data and Refresh Behavior

## Application startup

When the normal application window opens:

1. immediately display locally cached data if available;
2. start a Yahoo refresh;
3. replace cached events when refresh succeeds;
4. display the latest refresh time.

Status example:

`Updated from Yahoo at 6:42 AM`

## Manual Refresh

Toolbar:

`Refresh`

Refresh the currently relevant date range and selected Yahoo calendars.

## Automatic Refresh

v1.0 minimum:

- refresh at startup;
- manual Refresh button.

Optional:

- refresh every 15 minutes while the application remains open.

Do not continuously poll Yahoo at a high rate.

---

# 20. Local Cache

Maintain a local cache of recently retrieved calendar data so the application remains useful if temporarily offline.

The cache may contain event information because that information is already visible to the local Windows user.

Store under the user's LocalAppData directory.

The cache must not contain the Yahoo app password.

If Yahoo cannot be reached:

```text
Yahoo Calendar is temporarily unavailable.

Showing calendar data last updated:
September 3 at 6:42 AM

[Try Again]
```

The user should still be able to view and print cached calendar data.

---

# 21. Settings

Provide a simple Settings dialog.

## Yahoo Account

- account email/ID;
- connection status;
- `Test Connection`;
- `Change App Password`;
- `Disconnect`.

Never display the stored app password.

## Calendars

Checkboxes for discovered Yahoo calendars.

## Display

- default detail level;
- maximum description lines;
- show/hide event locations;
- optionally Sunday/Monday week start in later releases.

## Printing

- default paper size;
- default orientation;
- default detail level;
- default overflow policy.

## Privacy

- `Clear Cached Calendar Data`

---

# 22. Error Handling

Messages should describe corrective action.

Do not show raw stack traces to ordinary users.

Provide:

`Technical Details`

for diagnostics when useful.

Example error categories:

### Authentication failure

"Yahoo did not accept the saved app password."

Buttons:

- `Enter New App Password`
- `Cancel`

### Network unavailable

"Yahoo Calendar could not be reached. Cached calendar data is still available."

### CalDAV server error

"Yahoo returned an unexpected calendar response."

Provide:

- Retry
- Technical Details

### ICS parsing error

One malformed calendar resource must not crash the entire application.

Log the affected resource identifier and continue processing other events when possible.

---

# 23. Logging

Use lightweight rotating local logs.

Suggested path:

`%LOCALAPPDATA%\YahooMonthPrint\Logs`

Logs may contain:

- application version;
- timestamps;
- Yahoo request type;
- HTTP status codes;
- calendar identifiers;
- exception types.

Logs shall NOT contain:

- app passwords;
- Authorization headers;
- authentication tokens;
- full DESCRIPTION values by default.

Provide enough logging to troubleshoot CalDAV compatibility without exposing unnecessary personal calendar content.

---

# 24. Accessibility and Non-Technical UX

The application should be usable without documentation after initial setup.

Requirements:

- standard Windows controls where practical;
- keyboard-accessible navigation;
- adequate hit targets;
- readable default font;
- tooltips for icon-only buttons;
- no requirement to understand "CalDAV," "ICS," "VEVENT," etc.;
- destructive-sounding terminology should be avoided for local filters.

Use:

`Hide`

rather than:

`Delete`

for visibility operations.

The UI should repeatedly make it clear that hiding an event affects only the application's display/print output.

---

# 25. Internal Architecture

Suggested solution organization:

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
  YahooMonthPrint.iss

docs/
  architecture.md
  yahoo-caldav-notes.md
  release.md
```

## YahooMonthPrint.Core

Own:

- calendar models;
- event occurrence models;
- filters;
- visibility rules;
- month date ranges;
- application settings interfaces.

No WPF dependencies.

## YahooMonthPrint.YahooCalDav

Own:

- authentication;
- WebDAV discovery;
- calendar discovery;
- CalDAV REPORT requests;
- ICS retrieval;
- conversion into Core models.

## YahooMonthPrint.Printing

Own:

- month print layout model;
- overflow calculations;
- FixedDocument creation;
- print preview document generation.

## YahooMonthPrint.App

Own:

- WPF UI;
- setup wizard;
- view models;
- commands;
- navigation;
- settings UI;
- Windows Credential Manager integration.

Use dependency injection where useful, but avoid unnecessary framework complexity.

---

# 26. Core Data Model

Representative logical model:

```text
CalendarSource
    Id
    DisplayName
    CalDavUri
    Color
    IsEnabled

CalendarOccurrence
    CalendarId
    Uid
    RecurrenceId
    Start
    End
    IsAllDay
    Title
    Description
    Location
    SourceResourceId

OccurrenceVisibility
    OccurrenceKey
    IsManuallyHidden

MonthViewState
    DisplayedMonth
    DetailLevel
    QuickFilterText
    QuickFilterMode
    EnabledCalendars
    EnabledTitles
    HiddenOccurrences
```

`OccurrenceKey` must uniquely distinguish individual recurring instances.

---

# 27. Description Processing

Calendar descriptions may contain:

- CRLF;
- escaped newlines;
- HTML-like content;
- very long paragraphs;
- URLs;
- repeated whitespace.

Normalize descriptions for month rendering:

1. decode ICS escaping;
2. normalize line endings;
3. trim leading/trailing whitespace;
4. collapse pathological whitespace where appropriate;
5. preserve meaningful paragraph/newline boundaries;
6. never execute HTML or embedded content.

Month display should contain plain text only.

---

# 28. Timezone Behavior

Calendar dates shall be displayed in the Windows user's local timezone unless the event is explicitly an all-day event.

Recurring events must retain correct local times across daylight-saving-time transitions.

Do not implement timezone math manually.

Unit tests must include DST transitions.

---

# 29. Performance Targets

For an ordinary user:

- startup UI visible: approximately <2 seconds on a modern PC;
- cached month display: effectively immediate;
- normal Yahoo refresh: network-dependent but UI must remain responsive;
- filter changes: <100 ms perceived delay for a typical month;
- month navigation using already fetched/cached data: immediate;
- print preview generation: preferably <1 second for typical data.

Never perform network operations on the WPF UI thread.

Cancellation tokens should be used for refresh/navigation operations where appropriate.

---

# 30. Testing

Testing is mandatory.

## Unit Tests

Include tests for:

- month grid generation;
- five-week months;
- six-week months;
- month/year boundaries;
- leap years;
- all-day events;
- timed events;
- multiline descriptions;
- text filtering;
- include filter;
- exclude filter;
- calendar filter;
- title-list filter;
- manually hidden occurrence;
- restore hidden occurrence;
- recurrence rules;
- EXDATE;
- modified recurring occurrence;
- RECURRENCE-ID;
- DST boundaries;
- print overflow decisions.

## CalDAV Tests

Do not make automated test suites depend on the developer's real Yahoo account.

Provide sanitized fixture responses for:

- principal discovery;
- calendar-home discovery;
- calendar enumeration;
- calendar-query REPORT;
- recurring-event ICS;
- recurring exceptions.

Separate optional integration tests may use environment variables such as:

```text
YMP_TEST_YAHOO_USER
YMP_TEST_YAHOO_APP_PASSWORD
```

These tests must be skipped unless explicitly enabled.

Never commit actual credentials.

---

# 31. Manual Acceptance Test

Before calling v1.0 complete, perform this scenario against a real Yahoo account.

Create or use:

### Event A
Recurring every Monday at 9:00 AM:

`Calculus II`

Description:

`Normal class meeting.`

### Event B
Recurring every Wednesday:

`Physics`

Description:

`Normal lecture.`

### Exception

Modify one Calculus occurrence to contain:

`EXAM 2 — Chapters 5–7. Bring calculator.`

### Other event

`Office Hours`

Description:

`Optional.`

Verify:

1. application installs without admin rights;
2. Yahoo connection works using an app password;
3. both recurring classes appear;
4. modified exam occurrence shows its special description;
5. normal occurrences remain normal;
6. searching `office hours` and selecting Hide Matching hides Office Hours;
7. clearing the filter restores it;
8. unchecking `Physics` in Items This Month hides Physics;
9. clicking the hover Hide control on one Calculus occurrence hides only that date;
10. Hidden Items can restore it;
11. print preview exactly reflects visible events;
12. descriptions appear in month cells;
13. printed landscape page is readable;
14. Yahoo's original calendar remains completely unchanged.

---

# 32. Security Acceptance Criteria

Release is blocked if any of these occur:

- normal Yahoo password is requested;
- Yahoo app password appears in a settings file;
- Yahoo app password appears in logs;
- Authorization header appears in logs;
- calendar modifications are sent to Yahoo;
- installer requires elevation unnecessarily;
- network access is made over plaintext HTTP.

---

# 33. Release Acceptance Criteria

v1.0 is complete when a non-technical Windows user can:

1. download one installer;
2. double-click it;
3. install without administrator credentials;
4. launch the program;
5. enter Yahoo email/ID;
6. enter a Yahoo app password;
7. connect successfully;
8. choose Yahoo calendars;
9. see the current month;
10. see event descriptions/details directly inside calendar days;
11. move backward and forward between months;
12. filter events using text;
13. hide categories/items using checkboxes;
14. hide one event occurrence from its hover control;
15. restore hidden events;
16. preview the resulting printed month;
17. print it;
18. reopen the application later without re-entering the app password;
19. refresh from Yahoo with one click;
20. do all of this without changing anything stored in Yahoo.

---

# 34. Suggested Implementation Order for Codex

Implement incrementally.

## Milestone 1 — Application Skeleton

- .NET 8 solution;
- WPF shell;
- month navigation;
- fake local event data;
- basic month grid.

Acceptance:

A fake college schedule renders correctly.

## Milestone 2 — Month Rendering

- titles;
- times;
- multiline descriptions;
- detail levels;
- long-text handling.

Acceptance:

The target exam scenario is visibly useful in month view.

## Milestone 3 — Filtering

Implement:

- quick text filtering;
- calendar checkboxes;
- Items This Month list;
- hover Hide;
- Hidden Items restoration.

Acceptance:

The rendered month reacts instantly and predictably.

## Milestone 4 — Printing

Implement:

- FixedDocument renderer;
- landscape Letter/A4;
- print preview;
- print dialog;
- overflow detection.

Acceptance:

Screen visibility and print visibility match.

## Milestone 5 — Yahoo CalDAV

Implement:

- Yahoo authentication;
- principal/home discovery;
- calendar enumeration;
- date-range retrieval;
- ICS parsing;
- recurrence;
- timezone support.

Initially test through fixtures, then against a real Yahoo account.

## Milestone 6 — Setup Wizard and Credential Security

Implement:

- first-run wizard;
- Windows Credential Manager;
- reconnect/disconnect;
- human-readable errors.

## Milestone 7 — Cache and Reliability

Implement:

- local cache;
- startup refresh;
- offline mode;
- logs;
- error recovery.

## Milestone 8 — Installer

Build:

`YahooMonthPrint-Setup.exe`

Verify:

- clean Windows user account;
- no admin rights;
- no external runtime setup;
- clean uninstall.

## Milestone 9 — Release Validation

Run:

- unit tests;
- sanitized CalDAV fixture tests;
- optional Yahoo integration test;
- manual acceptance scenario;
- installer test.

Do not begin P1 features until the complete P0 workflow passes.

---

# 35. Important Implementation Guidance

Prioritize reliability and understandable behavior over architecture sophistication.

In particular:

- keep Yahoo synchronization read-only;
- use standard CalDAV discovery rather than depending on undocumented Yahoo page scraping;
- use a mature ICS recurrence parser;
- model recurring occurrences separately from recurring series;
- separate the print renderer from the interactive WPF UI;
- make filtering a transformation of the fetched calendar model, not a modification of calendar data;
- keep credentials entirely out of normal configuration;
- ensure every print decision is previewable;
- treat overly crowded calendar days explicitly rather than silently dropping content.

The intended user experience should be approximately:

> Open app → current month appears → optionally hide irrelevant things → click Print.

After initial setup, Yahoo authentication and CalDAV mechanics should be essentially invisible to the user.

---

## Notes on Yahoo Calendar Integration

Yahoo currently documents CalDAV/ICS support using the Yahoo ID plus a separately generated app password. The implementation should therefore use Yahoo's CalDAV service over HTTPS and store only the app password in Windows Credential Manager, never the user's normal Yahoo account password.
