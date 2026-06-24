# Industry research: workplace-wellness

> **Scope note:** This research covers the *personal break-reminder / movement-nudge* sub-segment —
> desktop tools that remind an individual to stand up, stretch, or rest their eyes while at a
> computer. The broader corporate wellness platform market (Virgin Pulse, Wellhub, etc.) is a
> different category (B2B, per-seat SaaS, HR-bought) and is out of scope for The Mover.

## Top commercial players

1. **Stretchly** (hovancik/stretchly) — The gold-standard free break-reminder: open-source,
   cross-platform, excellent defaults, huge community. Founded ~2015. 60k+ GitHub stars.
   Segment: individual / power-user.

2. **DeskBreak** (deskbreak.app) — Freemium desk-health app with the richest feature set in the
   segment: smart scheduling, eye-strain (20-20-20) reminders, hydration prompts, exercise library,
   habit streaks, and a team wellness layer. Founded ~2020. Customers: SMB/individual, ~unknown.
   Segment: individual → SMB team.

3. **Workrave** — Free, open-source RSI-prevention tool; the most configurable and the only one
   with both animated exercise demonstrations and a mandatory daily screen-time cap. Windows/Linux
   only. Founded ~2001. Segment: power-user (especially developers/writers with RSI history).

4. **Restier** (restier.app) — Paid (€8–25/yr or €20–60 lifetime), modern, polished. The only
   player with out-of-the-box meeting-aware suppression *and* idle detection together, plus
   multi-monitor handling. Founded ~2023. Segment: remote workers on Mac/Win/Linux.

5. **Stand Up!** (standupapp.io) — Free, Mac/Windows/Web. Differentiator is calendar integration
   (Google Calendar, Outlook, Apple Calendar) with Slack status updates during breaks — no other
   free tool does this. No exercise content. Founded ~2018. Segment: meeting-heavy remote workers.

## Capability matrix

| Capability | Stretchly | DeskBreak | Workrave | Restier | Stand Up! |
|---|---|---|---|---|---|
| **Configurable work interval** | ✓ deep | ✓ deep | ✓ deep | ✓ deep | ✓ deep |
| **Configurable break duration** | ✓ deep | ✓ deep | ✓ deep | ✓ deep | ✓ basic |
| **Micro-break + long-break tiers** | ✓ deep | ✓ deep | ✓ deep | ✓ deep | — |
| **Pomodoro / focus-cycle mode** | — | — | — | ✓ basic | — |
| **Daily screen-time cap / limit** | — | — | ✓ deep | — | — |
| **Full-screen lockout (hard)** | — | — | ✓ deep | ✓ basic | — |
| **Skip / snooze break** | ✓ deep | ✓ deep | ✓ basic | ✓ deep | ✓ deep |
| **Strict / no-skip mode** | ✓ basic | — | ✓ deep | — | — |
| **Idle / away detection** | ✓ deep | — | — | ✓ deep | — |
| **Calendar integration** | — | — | — | ✓ deep | ✓ deep |
| **Meeting-aware suppression** | — | — | — | ✓ deep | ✓ deep |
| **Focus-app / DND detection** | ✓ basic | ✓ basic | — | ✓ basic | — |
| **System notification overlay** | ✓ deep | ✓ deep | ✓ basic | ✓ deep | ✓ deep |
| **Full-screen break overlay** | — | — | ✓ deep | ✓ deep | — |
| **System tray / menu-bar presence** | ✓ deep | ✓ deep | ✓ deep | ✓ deep | ✓ deep |
| **Multi-monitor support** | — | — | — | ✓ deep | — |
| **Built-in exercise / stretch library** | ✓ basic | ✓ deep | ✓ deep | ✓ deep | — |
| **Animated exercise demonstrations** | — | ✓ basic | ✓ deep | ✓ deep | — |
| **Eye-strain (20-20-20) reminders** | — | ✓ deep | — | ✓ deep | — |
| **Hydration reminders** | — | ✓ basic | — | — | — |
| **Custom break content** | ✓ deep | ✓ basic | — | ✓ basic | — |
| **Break compliance analytics** | — | ✓ deep | ✓ deep | ✓ deep | — |
| **Habit streaks** | — | ✓ deep | — | ✓ deep | — |
| **Gamification (points / badges)** | — | ✓ deep | — | — | — |
| **Team wellness dashboard** | — | ✓ deep | — | — | — |
| **Windows support** | ✓ deep | ✓ deep | ✓ deep | ✓ deep | ✓ basic |
| **macOS support** | ✓ deep | ✓ deep | — | ✓ deep | ✓ deep |
| **Linux support** | ✓ deep | ✓ deep | ✓ deep | ✓ deep | — |
| **Mobile companion (iOS/Android)** | — | — | — | — | — |
| **Slack / chat integration** | — | — | — | — | ✓ deep |
| **Pricing: free tier** | ✓ deep | ✓ basic | ✓ deep | — | ✓ deep |

## Synthesized capabilities

### Must-have (v1)
Capabilities present in at least 4 of 5 players.

- **Configurable break schedule** — work interval + break duration, at minimum; two-tier
  (micro + long) in 4 of 5.
- **System tray / background service** — always resident; low footprint; visible in tray.
- **Desktop notification / break overlay** — visible prompt when a break is due; at minimum a
  system notification, ideally a dismissible overlay.
- **Skip / snooze** — user must be able to defer or dismiss a reminder; four of five provide this.
- **Exercise / stretch content** — built-in library of what to do (text tips minimum, animated
  demos if possible); four of five provide this.
- **Cross-platform desktop (Windows + macOS)** — present in all five; at minimum Windows, since
  that is The Mover's primary target.

### Differentiator (v1)
Capabilities present in 1–2 players with high impact.

- **Idle / away detection** (Stretchly, Restier) — automatically pauses the timer when the user
  steps away; prevents false breaks and the urge to dismiss. Absent in three players, which is the
  single biggest UX complaint in reviews.
- **Meeting-aware suppression** (Restier, Stand Up!) — reads the calendar and holds a reminder
  until the meeting ends; directly relevant to WFH workers who live on video calls.
- **Eye-strain (20-20-20) reminders** (DeskBreak, Restier) — a separate, high-frequency reminder
  to look 20 ft away for 20 s every 20 min; common WFH complaint distinct from body movement.

### Skip for v1
Niche or low-ROI capabilities.

- **Full-screen lockout** — alienates focus-sensitive users; Workrave gets negative reviews for
  being too aggressive. Don't ship hard enforcement without opt-in.
- **Daily screen-time cap** — Workrave-only; different use case (RSI management, not reminders).
  Adds complexity with little v1 payoff.
- **Team wellness dashboard** — DeskBreak-only; The Mover targets a single user, not an org.
- **Gamification (points / badges)** — DeskBreak-only; nice-to-have; risks feeling gimmicky on a
  tool with no social layer.
- **Mobile companion app** — no player in this segment ships one. The nudges live where the
  sitting happens: the desktop.
- **Slack / chat integration** — Stand Up!-only, niche. Useful but not worth the OAuth surface
  area in v1.

## Notable UX patterns observed

- **Always-in-tray, never a window** — every player lives in the system tray or menu bar. No
  player ships a regular foreground window as the primary shell. Seen in: all five.
- **Overlay vs. notification split** — Players split on enforcement philosophy. Stretchly/Stand Up!
  use a dismissible translucent overlay; Workrave/Restier support a full-screen option. The common
  pattern: start soft, add a strict-mode toggle for users who need it. Seen in: Stretchly, DeskBreak,
  Workrave, Restier.
- **Break-content card during the overlay** — showing a stretch image or one-liner ("look at
  something 20 feet away") inside the break window dramatically improves follow-through vs. a bare
  timer. Seen in: DeskBreak, Workrave, Restier.
- **Countdown timer visible during break** — all players that show an overlay render a visible
  countdown so the user knows when they can return. Without it, users dismiss immediately.
  Seen in: Stretchly, DeskBreak, Workrave, Restier.
- **Natural-break grace period** — the best-reviewed apps delay the next cycle when idle is
  detected (you already walked away), avoiding the "I just sat down and it's already nagging me"
  complaint. Seen in: Stretchly, Restier.

## Compliance / regulatory considerations

This segment handles no health records (no PHI, no PII in the medical sense), so heavy compliance
frameworks do not apply. Two lightweight considerations:

- **GDPR / local privacy law** — if the app ever phones home (analytics, telemetry, cloud-sync
  of habit data), EU users must be able to opt out. Even local habit streaks stored in a file are
  out of scope for GDPR, but transmitting them is not. The Mover should keep all data local by
  default; if cloud sync is added later, add a data-processing notice.
- **Windows Notification / Focus Assist API** — Windows 11 Focus Assist (now "Do Not Disturb") can
  suppress third-party notifications silently. An app that only uses system toasts will be silenced
  without user awareness. The correct pattern is to also render an in-app overlay that is not
  routed through the notification system, or to register as a priority notification sender.
  This is not a regulatory constraint but a platform compliance concern that every serious player
  handles.

## Open questions for the user

1. **Break content depth:** Do you want guided stretches shown during breaks (e.g., animated GIFs
   or illustrated cards), or is a simple "time to stand up!" notification enough for v1?

2. **Calendar / meeting awareness:** Do you take video calls (Zoom, Teams, Google Meet) during
   the day, and if so, which calendar do you use (Google Calendar, Outlook, Apple Calendar)?
   Meeting-aware suppression is the #1 differentiating feature here; answering this decides
   whether to include calendar integration in v1.

3. **Break enforcement:** Should missed breaks be silently forgotten, or tracked so you can see
   how often you actually took them? (Determines whether a break-compliance log and habit streak
   are in v1 scope.)

4. **Eye-strain reminders:** Do you want a separate high-frequency reminder for the 20-20-20 rule
   (look away from screen every 20 min for 20 s), distinct from the stand-up/stretch reminder?
