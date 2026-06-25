# The Mover

A Windows desktop companion that lives in the system tray and fires animated stretch-break
reminders on a configurable two-tier schedule. Each break shows a rotating exercise card with a
countdown; reminders are suppressed during active Outlook meetings and pause automatically when you
step away from your desk.

[![CI](https://github.com/abrahamFerga/the-mover/actions/workflows/ci.yml/badge.svg)](https://github.com/abrahamFerga/the-mover/actions/workflows/ci.yml)

> Single-user, local-only, no accounts and no backend. All data (your schedule and the Outlook
> OAuth token) stays on your machine.

## Features

- **Two-tier break schedule** — independent **micro-breaks** (short, frequent — default every
  20 min for 30 s) and **long breaks** (default every 60 min for 5 min). Each tier's interval and
  duration is configurable.
- **System-tray background service** — runs silently from login with a near-zero idle footprint
  and no foreground window. A tooltip shows the live "next break in *N* min" countdown.
- **In-app overlay** — when a break fires, an in-process WPF window appears with the exercise card
  and a countdown. It is *not* a Windows toast, so Focus Assist can never silently swallow it.
- **Snooze / skip** — snooze the current break by a configurable increment (default 5 min) or skip
  it outright, from either the overlay or the tray menu.
- **Rotating exercise library** — 12 bundled desk-friendly stretches and exercises, shown one per
  break and never repeated back-to-back.
- **Meeting-aware suppression** — optionally reads your Outlook / Microsoft 365 calendar via
  Microsoft Graph; while a meeting is active, breaks are held so a pop-up never interrupts a call.
- **Idle / away detection** — pauses the timer after ≥ 2 minutes of keyboard/mouse inactivity and
  resets the countdown when you return, so you are not nagged the moment you sit back down. Also
  resets across system sleep/resume.
- **Auto-start with Windows** — opt-in; registers under `HKCU\…\Run` only when you enable it.

## Getting started

### Prerequisites

- Windows 10/11
- [.NET SDK **10.0.301**](https://dotnet.microsoft.com/download) or later (pinned in
  [`global.json`](global.json))

### Build and run

```bash
dotnet build TheMover.slnx -c Release
dotnet run --project src/TheMover.App
```

The app starts minimized to the system tray. Double-click the tray icon (or right-click → **Open
Settings**) to configure it; right-click → **Quit** to exit.

### Run via Aspire (optional)

The repo includes an [Aspire](https://learn.microsoft.com/dotnet/aspire/) AppHost that wires up the
OpenTelemetry dashboard and health checks for local diagnostics:

```bash
dotnet run --project src/TheMover.AppHost
```

## Configuration

Settings are edited in the in-app **Settings** window and persisted to:

```
%LOCALAPPDATA%\TheMover\appsettings.local.json
```

Writes are atomic (temp file + rename), so a crash mid-save can never leave a torn config that
would block startup. Defaults ship in [`appsettings.json`](src/TheMover.App/appsettings.json) and
are overridden by the local file. Constraints are validated at startup — e.g. micro interval must
be shorter than the long interval, and a break's duration must be shorter than its interval — so an
invalid hand-edit fails fast with a clear message rather than producing an endless-overlay loop.

### Connecting Outlook (optional)

Meeting suppression uses Microsoft Graph with the minimum `Calendars.Read` scope. To enable it:

1. Register an application in **Microsoft Entra ID (Azure AD)** with a redirect URI of
   `http://localhost` and the delegated `Calendars.Read` permission.
2. In the app's **Settings → Outlook** section, enter the **Tenant ID** and **Client ID**, then
   click **Connect** and complete the sign-in.

The refresh token is stored encrypted via MSAL's DPAPI-backed cache — never in plaintext. If Graph
is unreachable the app degrades gracefully: breaks simply fire as normal.

## How it works

The app is a .NET Generic Host. WPF runs on a dedicated STA thread
([`WpfHostedService`](src/TheMover.App/WpfHostedService.cs)); a set of `BackgroundService`s
coordinate through a shared in-memory [`BreakTimerState`](src/TheMover.Scheduler/BreakTimerState.cs)
and two in-process channels:

```
BreakSchedulerService ──BreakDueEvent──▶ OverlayService ──▶ OverlayWindow
        ▲                                                         │
        │ shared BreakTimerState                                  │ snooze / skip / complete
        │                                                         ▼
IdleMonitorService · CalendarSyncService          BreakCommandHandlerService ◀──BreakCommand── TrayIconService
```

- **BreakSchedulerService** ticks once a second, firing a `BreakDueEvent` when an interval elapses
  (long breaks take priority), unless paused.
- **OverlayService** renders the overlay with a rotating exercise and reports the outcome.
- **BreakCommandHandlerService** applies snooze/skip commands to the shared state.
- **IdleMonitorService** and **CalendarSyncService** flip the pause state for idle/meeting holds.
- **EventLogger** appends activity to `events.jsonl` for the local success metrics in the spec
  (writes are serialized so concurrent events are never dropped).

See [ARCH.md](ARCH.md) for the full architecture and [DECISIONS.md](DECISIONS.md) for the ADRs.

## Project structure

| Project | Responsibility |
|---|---|
| `TheMover.App` | Host, DI wiring, scheduler/overlay/tray/idle/calendar services, settings UI, config, logging |
| `TheMover.Scheduler` | Core domain types: `BreakTimerState`, `BreakCommand`, `BreakDueEvent`, tiers |
| `TheMover.Content` | Bundled exercise library and the no-back-to-back rotation picker |
| `TheMover.Calendar` | Microsoft Graph calendar client (`ICalendarClient`) |
| `TheMover.Overlay` | The WPF break overlay window |
| `TheMover.ServiceDefaults` | OpenTelemetry, health checks, resilience defaults |
| `TheMover.AppHost` | Aspire orchestration host for local diagnostics |

Each non-trivial project has a matching test project under `tests/`.

## Development

```bash
dotnet build TheMover.slnx -c Release        # build all projects
dotnet test  TheMover.slnx -c Release        # run the full suite (116 tests)
```

CI runs build + test on `windows-latest` via [GitHub Actions](.github/workflows/ci.yml) on every
push and pull request to `main`.

## Privacy

The Mover is local-only. There is no backend, no account, and no telemetry that phones home. Your
schedule config and the Outlook OAuth token live exclusively on your machine; meeting data is read
just-in-time to answer "is a meeting active right now?" and is never stored or transmitted.

## Roadmap (out of scope for v1)

Deliberately excluded to keep v1 focused: habit streaks / compliance logging, full-screen forced
breaks, team/group features, a mobile companion, gamification, cloud sync, and macOS/Linux support.
See [SPEC.md](SPEC.md) for the full rationale.
