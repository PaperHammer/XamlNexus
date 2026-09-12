# Settings behavior

[English](settings.md) | [简体中文](settings.zh-CN.md)

Generated applications persist settings through `IUserSettingsClient.SaveAsync<ISettings>()`.
The built-in settings controls catch save failures, report errors and become usable again.
Language changes update existing controls immediately; see [localization](localization.md).

JSON saves write a complete temporary file in the same directory before replacing the
existing file. Serialization or replacement failures preserve the previous file and
attempt to remove the temporary file.

## Corrupted settings recovery

Both presets and the Showcase create defaults when the settings file is missing.
Malformed JSON (including a JSON `null` document) is copied, byte for byte, to a sibling
`<settings-file>.corrupt.<UTC timestamp>.<unique id>.bak` before defaults are saved.
The log records the backup path. Backups are retained; they are not automatically pruned.
If the backup fails, recovery stops without replacing the original. If saving defaults
fails, the backup remains available and the original is not truncated.

File locks, permission failures and other non-JSON read errors propagate to the caller;
they do not trigger a reset. Valid settings are loaded without being rewritten.

To restore values, close the application (including the hybrid backend), locate the
backup beside the settings file, and repair its JSON or copy the wanted values into the
current settings file. Preserve a copy before editing and restart after saving. Restoring
the same malformed JSON unchanged will trigger recovery again. There is no recovery UI
or semantic validation of every setting value in this change.

## Storage directory

Both presets copy files to a separate, empty directory before saving the new path.
The operation rejects overlapping paths, existing destination content and links found
in the source tree. Files open for writing cannot be copied by this operation.

Original files are retained even after success. A copy or settings-save failure restores
the previous configured path and may leave partial or complete copies in the destination.
Inspect those copies yourself before retrying with an empty folder; the application does
not clear the destination on failure. This is a file copy, not a consistent backup of
data that other code may be changing concurrently.

The SQLite recipe keeps its own database location. Changing this setting does not
relocate an active SQLite database. Use database-specific backup and migration operations
when that is needed.

## Theme and backdrop

Both presets restore the previous selection when saving fails. Theme changes also restore
the previous applied theme. The saved backdrop takes effect on the next application start.

## Startup registration (pure WinUI)

Opening settings reads the current Windows startup registration. The toggle is disabled
while reading or changing it. If saving the change fails, the application attempts to
restore its previous registration and reports the error. For unpackaged applications,
rollback preserves the previous command rather than generating a replacement command.

Unpackaged applications use their own value in the current user's `Run` registry key.
That registration alone does not prove that Windows will launch the application at logon:
Windows startup approval and policy can still affect execution. Packaged applications
use `StartupTask` and report when Windows blocks the requested change.

The hybrid preset retains its existing backend startup flow; the pure WinUI startup
changes described here do not replace that flow.
