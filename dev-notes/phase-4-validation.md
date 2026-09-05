# Phase 4 validation record

## Automated evidence

- Printing model tests cover Letter/A4 landscape geometry, five/six week grids, visible-set parity, ordering/formatting, deterministic overflow reduction, the 7 pt minimum, details-page fallback, long unbroken/multiline text, and document page geometry.
- App tests cover cached selected-subset restoration, cached-data retention after refresh failure, and flushing calendar-selection writes before shutdown.
- CI builds/tests the solution, publishes a self-contained `win-x64` application, proves self-contained startup, compiles the production lowest-privilege installer, creates its SHA-256 checksum, and renders Letter/A4 sample pages.
- The installer compiler compatibility probe uses a disposable temporary output directory and the production installer contains the Inno Setup 6.3 compatibility guard.

## Required manual release evidence

These checks require external state and are intentionally not claimed by automated tests:

- Clean standard-user Windows 10/11 install, upgrade/reinstall, and uninstall without UAC.
- Microsoft Print to PDF plus another physical or virtual printer, including cancellation and unavailable-printer behavior.
- Grayscale review of both Letter and A4 sample output.
- Complete real-Yahoo scenario from specification section 31 using a non-production account.
- Offline restart from the last complete cache and successful reconnection.
- Post-run inspection proving Yahoo data is unchanged, only HTTPS/read-only requests occurred, no secret entered logs/settings/cache/artifacts, and uninstall removed the Credential Manager entry.

Record sanitized screenshots/log summaries here or link the release checklist after the merged-default-branch candidate is validated. Do not attach account names, calendar descriptions, Authorization values, or app passwords.
