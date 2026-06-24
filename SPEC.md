# The Mover — Product specification

## In one sentence
A Windows desktop companion that lives in the system tray, fires animated stretch-break reminders
on a configurable two-tier schedule, displays a rotating exercise card each time, suppresses
reminders during active Outlook meetings, and pauses automatically when the user steps away.

## Primary jobs to be done
- When I'm deep in work and losing track of time, I want to be reminded to stand up and move,
  so that I avoid back pain and stiffness from prolonged sitting.
- When a break fires, I want to see a specific exercise or stretch with an animated demonstration,
  so that I actually do something useful rather than standing up and immediately sitting back down.
- When I have an Outlook or Teams meeting in progress, I want the reminder to wait until it ends,
  so that clients and colleagues are not interrupted by a pop-up on my screen.
- When I naturally step away from my desk, I want the timer to reset, so that the next reminder
  doesn't fire the moment I return.
- When I need to push through a critical thought, I want to snooze the current reminder for a few
  minutes, so that I can finish without losing my focus.

## Target personas

- **The Desk Worker** — a work-from-home individual who sits at a computer for 6–10 hours a day
  with no natural movement cues; the primary recipient of reminders and exercise cards.
  Top 3 tasks:
  1. Receive a break reminder and follow the displayed exercise card.
  2. Snooze or dismiss a reminder during a critical task.
  3. Confirm the app is running via the system tray icon.

- **The Configurator** — the same person during initial setup or periodic reconfiguration; sets
  break intervals, content preferences, and wires up the Outlook calendar.
  Top 3 tasks:
  1. Set micro-break and long-break intervals to match their personal work rhythm.
  2. Connect the app to their Outlook / Microsoft 365 calendar.
  3. Browse the exercise library to preview what will be shown during breaks.

## Capabilities

### Must have (v1)

| Capability | One-line description | Personas |
|---|---|---|
| Configurable break schedule | Two-tier schedule: micro-breaks (short, frequent) and long breaks (longer, less frequent); user sets interval and duration for each tier independently. | Desk Worker, Configurator |
| System tray background service | App runs silently in the Windows system tray from login; near-zero CPU/memory footprint while idle; no foreground window required to operate. | Desk Worker |
| In-app break overlay | When a break fires, a dismissible window appears on screen showing the exercise card and a countdown timer; not routed through the Windows notification system so it is never silently suppressed by Focus Assist. | Desk Worker |
| Skip / snooze | From the overlay or tray icon, user can snooze the current break by a configurable increment (default 5 min) or dismiss it entirely. | Desk Worker |
| Animated exercise / stretch library | A curated set of exercises shown one-per-break on the overlay; each exercise has a title, brief instruction text, and an animated demonstration; exercises rotate each break to maintain variety. | Desk Worker, Configurator |
| Meeting-aware Outlook suppression | Reads the user's Outlook / Microsoft 365 calendar; if a meeting is active when a break is due, the reminder is held until the meeting ends and then delivered. | Desk Worker, Configurator |

### Differentiators (v1)

| Capability | Why it matters | Personas |
|---|---|---|
| Idle / away detection | Pauses the break timer when keyboard and mouse have been idle for ≥ 2 minutes; resets the countdown when activity resumes; prevents the "I just sat down and it's already nagging me" failure mode that drives uninstalls. | Desk Worker |

### Explicitly out of scope (v1)

- **Break compliance logging / habit streaks** — user chose fire-and-forget; tracking adds data
  persistence complexity with no v1 payoff.
- **Eye-strain (20-20-20) reminders** — user explicitly excluded; one reminder type only.
- **Full-screen lockout / forced breaks** — removes user agency; negatively reviewed across every
  player that ships it without a clear opt-in.
- **Team / group wellness features** — personal single-user tool; no org accounts, no dashboards.
- **Mobile companion app** — no player in this segment ships one; reminders live where sitting
  happens.
- **Gamification (points / badges / streaks)** — no tracking layer means no data to gamify.
- **Slack / chat integration** — Outlook suppression covers the active-call case; Slack OAuth adds
  surface area for marginal gain.
- **Cloud sync / backend** — all data stays local; no accounts, no servers, no GDPR surface area
  in v1.
- **Daily screen-time cap** — RSI-management use case, not a break reminder; different product.
- **macOS / Linux support** — Windows is the primary target; cross-platform deferred to v2.

## RBAC model (initial)

This is a single-user personal desktop application with no network backend, no accounts, and no
authentication. The RBAC model has one role:

- **User** — the person running the app on their machine. Full access to all features: configure
  schedules, connect the Outlook calendar, browse and customise the exercise library, snooze or
  dismiss reminders, start and quit the app. Operating-system user isolation provides the only
  access boundary; the app does not enforce its own identity layer.

## Regulatory constraints

- **GDPR / local privacy law** — all user data (schedule config, Outlook OAuth token) is stored
  locally and never transmitted. If cloud sync is added in a future version, a data-processing
  notice and explicit opt-in must precede it. No telemetry that phones home is permitted in v1
  without an opt-in toggle.
- **Microsoft identity platform (OAuth 2.0 / Graph API)** — if the Outlook integration uses
  Microsoft Graph, the OAuth flow must request the minimum scope (`Calendars.Read`), and the
  resulting refresh token must be stored in the Windows Credential Manager (not a plaintext file).
  If the Outlook COM interop approach is chosen instead, no OAuth is required but the app must
  handle the case where Outlook is not installed gracefully (skip meeting suppression, inform user).
- **Windows Focus Assist / Do Not Disturb (Windows 11)** — the system can silently suppress
  third-party notification toasts. The break overlay must render as an in-process window (not a
  Windows toast) so it is never swallowed without the user's knowledge. This is a hard platform
  constraint, not an optional UX choice.

## Success metrics

- **Day-1 activation rate** — ≥ 90% of installs result in the user receiving at least one
  scheduled reminder during their first work session. Measured by a local event written to an
  app log on first overlay display.
- **Setup completion rate** — ≥ 80% of first launches reach "first reminder scheduled" state
  (break intervals saved + Outlook connection confirmed or skipped). Measured by a local
  first-run completion event.
- **Overlay render success rate** — ≥ 99% of break events that fire while the user is active
  result in the overlay appearing on screen. Measured by comparing "break fired" vs "overlay
  rendered" events in the local log.
- **Snooze-to-dismiss ratio** — ratio of snooze actions to outright dismissals; stored in local
  log. A dismiss rate consistently above 80% is a signal that break frequency is too high or the
  overlay is too intrusive; surfaces as a settings recommendation.
- **30-day retention** — app still running in system tray 30 days after install. Measured by a
  daily heartbeat written to the local log; target ≥ 60%.

## Open questions for plan-system

1. **Outlook integration approach** — Microsoft Graph API (cloud, requires Azure AD app
   registration and OAuth dance, works even when the desktop Outlook client is not open) vs.
   local Outlook COM interop (offline-capable, no cloud auth, but requires Outlook desktop to
   be installed and running). The choice materially affects the architecture and the Azure resource
   plan; if Graph is chosen, an Azure AD app registration is needed even for a personal tool.

2. **Exercise content format** — animated exercises bundled as GIFs (simple, universal, heavy on
   disk), Lottie JSON animations (lightweight, smooth, requires renderer), or short embedded MP4
   clips (richest but heaviest). Determines the asset pipeline, bundle size, and whether an asset
   CDN or update mechanism is needed.

3. **Windows auto-start behaviour** — register in the Windows startup registry automatically on
   first launch, or prompt the user during onboarding? Auto-start is the expected behaviour for a
   system-tray tool, but it requires the installer (or the app itself) to write a registry key,
   which some enterprise endpoint-protection tools block without elevation.
