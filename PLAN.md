# The Mover — Plan

## Guardrail adaptations

The enterprise guardrails in the workflow target multi-tenant SaaS systems. The Mover is a
single-user personal desktop application with no network backend. Deviations are recorded here
explicitly — they are not silent gaps.

| Standard guardrail | Adaptation for The Mover |
|---|---|
| AuthN via OIDC / Entra ID | Not applicable. OS user isolation is the access boundary. OAuth 2.0 applies only to the Microsoft Graph scope for reading the user's calendar (`Calendars.Read`). |
| Multi-tenancy (tenant_id on every domain table) | Not applicable. Single user, no database, no tenants. |
| Distributed cache (Redis) | Not applicable. Single-process in-memory state is sufficient. |
| External API surface (versioned REST, CORS, rate limiting, idempotency keys) | Not applicable. No public API surface. |
| RBAC authorization policies | Simplified to a single `User` role (see RBAC model below). Policy names are defined so future multi-user modes can bind roles without structural rewrites. |
| OpenTelemetry | Applied locally: traces + metrics exported to the Aspire dashboard during development and to a local file sink in production. No cloud OTel sink in v1. |
| Outbox pattern for external side effects | Adapted: Outlook calendar state is read by a polling job (not an event handler); all side effects are in-process state mutations with no durability requirement. Polly retry handles transient Graph/COM failures. |
| Secrets via cloud secret store | Adapted: the Outlook OAuth refresh token is stored in Windows Credential Manager under the key `TheMover.OutlookToken`. App settings live in a local JSON config file. No cloud secret store in v1. |
| GDPR data export / deletion endpoint | All data is local. Uninstalling the app and deleting its config directory constitutes full data deletion. No network-reachable data export needed in v1. |

---

## Epics (in build order)

1. **Foundations** — Generic Host + Worker Service scaffold with Aspire dev experience (AppHost +
   ServiceDefaults), system tray shell (icon + context menu), settings window skeleton (empty
   container, filled by later epics), local config framework (IOptions<T> backed by a JSON file),
   OpenTelemetry configured to Aspire dashboard (dev) and local file sink (prod), Windows startup
   registration mechanism, and local event log (append-only structured events for the success
   metrics in SPEC).
   Capabilities (from SPEC): *System tray background service*.
   Depends on: nothing.

2. **Break Scheduler** — Two-tier configurable break engine (micro-break at a short interval, long
   break at a longer one), per-tier interval and duration stored in config, configurable defaults
   (20 min micro / 30 s duration; 60 min long / 5 min duration), skip (dismiss current break) and
   snooze (defer by N minutes) actions, break settings screen wired into the settings window.
   Capabilities (from SPEC): *Configurable break schedule*, *Skip / snooze*.
   Depends on: Foundations.

3. **Break Overlay & Exercise Library** — In-app dismissible overlay window rendered as a
   first-class window (not a Windows toast, to avoid Focus Assist suppression), countdown timer
   display, curated exercise / stretch content catalog (bundled JSON + assets), animated exercise
   card shown on the overlay with rotation logic (no same card twice in a row), skip / snooze
   buttons on the overlay surface.
   Capabilities (from SPEC): *In-app break overlay*, *Animated exercise / stretch library*.
   Depends on: Break Scheduler (overlay fires when the scheduler signals a break event).

4. **Outlook Meeting Suppression** — Microsoft 365 calendar integration (Graph API or Outlook COM
   interop — decision deferred to architecture), active-meeting detection, break deferral: when a
   break is due and a meeting is active the scheduler holds it until the meeting ends, Outlook
   connection and credential management wired into the settings window.
   Capabilities (from SPEC): *Meeting-aware Outlook suppression*.
   Depends on: Foundations, Break Scheduler.

5. **Idle / Away Detection** — Keyboard and mouse inactivity monitor: pauses the break timer
   countdown when input has been idle for ≥ 2 minutes, resets the countdown when activity
   resumes; no user configuration required; hooks into the Break Scheduler timer engine.
   Capabilities (from SPEC): *Idle / away detection* (differentiator).
   Depends on: Break Scheduler.

---

## Module list

| Module (.NET project name) | Bounded context | Capabilities served | Skills used to build it |
|---|---|---|---|
| `TheMover.AppHost` | Aspire orchestration | Dev-time service composition, OTel dashboard | dotnet-aspire-base |
| `TheMover.ServiceDefaults` | Cross-cutting | OTel, health checks, Polly resilience pipeline defaults | dotnet-aspire-base |
| `TheMover.App` | App shell | System tray, settings window, startup registration, config, local event log | dotnet-aspire-base |
| `TheMover.Scheduler` | Break scheduling | Configurable two-tier timer, skip/snooze, idle detection | (background service patterns) |
| `TheMover.Overlay` | Break experience | Overlay window, countdown display, skip/snooze UX | (UI framework per arch decision) |
| `TheMover.Content` | Exercise library | Exercise catalog loader, animated card model, rotation algorithm | (asset pipeline per arch decision) |
| `TheMover.Calendar` | Outlook integration | Graph API / COM interop, meeting detection, credential management | (Graph SDK or COM per arch decision) |

---

## Data model sketch

No database in v1. All persistent state is held in a local JSON config file and Windows
Credential Manager.

- **AppSettings** — root IOptions<T> config; not PII; key fields: `MicroBreak.IntervalMinutes`,
  `MicroBreak.DurationSeconds`, `LongBreak.IntervalMinutes`, `LongBreak.DurationSeconds`,
  `AutoStartWithWindows` (bool), `CalendarIntegration.Enabled` (bool), `Snooze.IncrementMinutes`.

- **Exercise** — in-memory catalog entry; seeded at startup from a bundled `exercises.json`
  file; not user-editable in v1; fields: `Id` (GUID), `Title`, `InstructionText`,
  `AnimationAssetPath` (relative path into bundled assets), `MuscleGroupTags` (string[]).
  Not PII.

- **ExerciseRotation** — in-memory only (not persisted; resets on app restart); tracks
  `LastShownExerciseId` to prevent showing the same card twice in a row.

- **BreakTimerState** — in-memory only; fields: `NextBreakAt` (DateTimeOffset), `Tier`
  (Micro | Long), `IdleDetectedAt` (DateTimeOffset?), `HeldForMeeting` (bool), `SnoozedUntil`
  (DateTimeOffset?). Rebuilt from config + wall clock on startup.

- **OutlookCredential** — not stored in the app's config file; held exclusively in Windows
  Credential Manager under the key `TheMover.OutlookToken`. Contains the OAuth refresh token
  (Graph path) or is absent (COM path). PII-adjacent: never logged, never serialized to disk
  outside Credential Manager.

---

## RBAC model (refined)

Single-user personal tool. One runtime role; policy names are defined for forward-compatibility.

| Role | Policies | Notes |
|---|---|---|
| `User` | `Schedule.Configure`, `Schedule.SkipBreak`, `Schedule.SnoozeBreak`, `Calendar.Connect`, `Calendar.Disconnect`, `Library.Browse`, `App.ConfigureStartup`, `App.Quit` | The sole role; implicitly granted to the logged-in OS user. No authentication gate beyond OS session. |

---

## Integration surface

| Connector | Direction | Purpose | Webhook routes | Per-user config |
|---|---|---|---|---|
| Microsoft 365 / Outlook calendar | Outbound (read-only) | Poll calendar events to detect active meetings; no write operations. | None (polling model; no inbound webhooks) | `TenantId` (optional for work accounts), refresh token (Windows Credential Manager) |

---

## Background work

| Job | Trigger | Cadence | Outbox required? |
|---|---|---|---|
| Break timer tick | Scheduled | Per `MicroBreak.IntervalMinutes` (default 20 min); long break every N micro-break cycles | No — fires an in-process `BreakDue` event; no external side effects |
| Idle monitor poll | Scheduled | Every 30 seconds while app is running | No — reads OS input state; mutates in-memory `BreakTimerState` only |
| Calendar poller | Scheduled | Every 60 seconds when `CalendarIntegration.Enabled` | No — reads calendar into in-memory `HeldForMeeting` flag; Graph/COM call wrapped in Polly retry |
| Local event log flush | Reactive | On each loggable event (break fired, overlay shown, snoozed, dismissed) | No — append to a local structured log file; no external side effects |

---

## Open questions for design-architecture

1. **Outlook integration approach** — Microsoft Graph API (cloud OAuth, `Calendars.Read` scope,
   requires an Azure AD app registration, works without Outlook desktop installed) vs. Outlook
   COM interop (offline-capable, no Azure dependency, but requires Outlook desktop to be installed
   and running). This is the highest-impact architecture decision: Graph adds a cloud dependency
   and an OAuth credential flow; COM keeps the app fully local but binds it to Outlook's process.

2. **Exercise content format** — bundled GIFs (universally renderable, ~50–150 KB each, no
   renderer dependency), Lottie JSON (< 10 KB each, smooth looping, requires a WinUI/WPF Lottie
   renderer package), or short MP4 clips (richest, largest, requires a video component). This
   decision is tightly coupled to the UI framework choice below.

3. **UI framework for overlay and settings window** — WPF (mature, broad WinForms/WPF ecosystem,
   straightforward system tray integration via `NotifyIcon`), WinUI 3 (modern, Lottie-native via
   `CommunityToolkit.WinUI.Lottie`, higher boilerplate), or WinForms (fastest to ship, limited
   animation support). The choice gates the exercise content format decision above.

4. **Windows startup registration** — auto-register on first launch (write to
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` from app code, no elevation needed) vs.
   explicit "Start with Windows" toggle in settings (safer, avoids enterprise endpoint-protection
   blocks). Auto-register is the expected behaviour for a tray tool; the toggle is the safer
   default if this is ever distributed beyond personal use.

5. **Aspire app model fit** — confirm that the `ServiceDefaults` OTel + health-check wiring works
   for a tray-resident WPF or WinForms host (not just a Kestrel web host). If not, ServiceDefaults
   should be applied as a direct NuGet reference without the Aspire app model so the dev
   experience still benefits from the OTel dashboard.
