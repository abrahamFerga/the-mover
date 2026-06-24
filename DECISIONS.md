# The Mover — Architecture Decision Records

ADRs are numbered sequentially from ADR-0001 and never renumbered. Reverting a decision
adds a new superseding ADR; the old one is not deleted.

---

## ADR-0001: WPF as the UI framework

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (design-architecture)

### Context

The Mover needs a system tray icon, a settings window, and a dismissible overlay window.
Three Windows UI frameworks are viable: WPF, WinUI 3, and WinForms. The choice gates the
exercise animation format decision (ADR-0003) because Lottie support differs per framework.
The app is a personal single-user tool; no Windows Store distribution is planned for v1.

### Decision

We will use **WPF** as the UI framework for both the settings window and the break overlay.

### Consequences

- **Positive**: `NotifyIcon` (system tray) is fully supported via `System.Windows.Forms.NotifyIcon`
  or the WPF `Hardcodet.NotifyIcon.Wpf` package without ceremony. The WPF/Generic Host
  bridging pattern (`WpfHostedService` on an STA thread) is well-documented. LottieSharp
  provides a WPF-compatible Lottie renderer via SkiaSharp (unblocks ADR-0003).
- **Negative**: WPF is not cross-platform. If a macOS port is ever needed, the UI layer
  must be rewritten. WPF's data-binding model adds boilerplate compared to WinForms.
- **Neutral**: WPF XAML hot-reload is available in VS 2022; design-time iteration is
  reasonable.

### Alternatives considered

- **WinUI 3** — native Lottie support via `CommunityToolkit.WinUI.Lottie`; more modern.
  Rejected because: requires MSIX packaging for system tray registration, adds significant
  AppContainer / identity plumbing, and WinUI 3 system-tray integration has known rough
  edges as of .NET 10. Higher boilerplate for a simple tray app.
- **WinForms** — fastest to ship; `NotifyIcon` is native. Rejected because: limited
  animation support rules out smooth Lottie animations (see ADR-0003). Custom overlay
  window with countdown and animation requires manual GDI+ work that WPF handles
  declaratively.

---

## ADR-0002: Microsoft Graph API for Outlook calendar integration

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (design-architecture)

### Context

Meeting-aware suppression requires reading the user's calendar. Two approaches exist:
Microsoft Graph API (cloud OAuth, `Calendars.Read` scope, requires an Azure AD app
registration, works without Outlook desktop installed) and Outlook COM interop (local,
no Azure dependency, offline-capable, but requires Outlook desktop to be installed and
running). This is the highest-impact architecture decision in the plan.

### Decision

We will use the **Microsoft Graph API** with OAuth 2.0 PKCE (Authorization Code + PKCE,
no client secret). The app registers a single-tenant or multi-tenant Azure AD application
with the `Calendars.Read` delegated permission. The refresh token is stored in Windows
Credential Manager under key `TheMover.OutlookToken`.

### Consequences

- **Positive**: Works without requiring Outlook desktop to be installed or running. The
  `Calendars.Read` scope is read-only and minimal. The Azure AD app registration is a
  one-time manual step, not a runtime dependency. The `Microsoft.Graph` SDK and
  `Microsoft.Identity.Client` (MSAL) packages are well-maintained.
- **Negative**: Adds an Azure AD app registration as a prerequisite (free, but requires an
  Entra ID / Microsoft 365 account to register). Requires an internet connection for the
  initial OAuth dance and for each calendar poll. First-time OAuth flow requires a browser
  pop-up.
- **Neutral**: The `TenantId` in `CalendarSettings` is optional; `null` uses the `/common`
  endpoint and supports personal Microsoft accounts and work accounts alike.

### Alternatives considered

- **Outlook COM interop** — offline-capable; no Azure dependency. Rejected because: requires
  Outlook desktop to be installed and running; if Outlook is closed, meeting detection silently
  fails. COM interop is fragile across Outlook version upgrades. Binding the app's reliability
  to an external desktop application is a worse user experience than requiring an internet
  connection for an optional feature.

---

## ADR-0003: Lottie JSON for exercise animations

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (design-architecture)

### Context

The break overlay must display an animated exercise demonstration. Three formats are
viable: bundled GIFs (~50–150 KB each, no renderer dependency), Lottie JSON
(~5–15 KB each, smooth looping, requires a renderer package), and short MP4 clips
(richest quality, ~200–500 KB each, requires a video component). The choice is
tightly coupled to the UI framework (ADR-0001).

### Decision

We will use **Lottie JSON** animations, rendered via the `LottieSharp` NuGet package
(WPF + SkiaSharp). Animation files are bundled as `EmbeddedResource` in
`TheMover.Content`. The `LottieAnimationView` WPF control is embedded in the overlay window.

### Consequences

- **Positive**: Lottie files are 5–15 KB each vs. 50–150 KB for GIFs — the full exercise
  catalog (10+ exercises) will add < 200 KB to the bundle. Lottie animations loop smoothly
  at any frame rate without flickering artifacts. The `LottieSharp` package is MIT-licensed
  and actively maintained.
- **Negative**: Adds `LottieSharp` and `SkiaSharp` as runtime dependencies (~5 MB combined).
  Lottie designers must export in Bodymovin JSON format; not all animation tools support this.
- **Neutral**: The bundled exercise catalog is not user-replaceable in v1; the asset pipeline
  is build-time only.

### Alternatives considered

- **Bundled GIFs** — no renderer dependency; universal. Rejected because: file sizes are
  10–30× larger than Lottie, the bundle would grow to 1–3 MB for a modest catalog, and GIF
  animation quality (frame rate, color depth) is noticeably worse on HiDPI displays.
- **Short MP4 clips** — richest quality. Rejected because: 200–500 KB per clip results in a
  multi-MB bundle, a `MediaElement` or third-party video control is required, and video
  scrubbing on loop has visible seek artifacts in WPF's `MediaElement`.

---

## ADR-0004: "Start with Windows" as an explicit opt-in toggle (default OFF)

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (design-architecture)

### Context

System-tray apps are conventionally expected to start at Windows login. Two approaches:
auto-register on first launch (write to `HKCU\...\Run` immediately without user action) or
provide an explicit "Start with Windows" toggle in Settings that the user must enable.
Auto-registration is convenient but can be blocked by enterprise endpoint-protection tools
and surprises users who install to evaluate.

### Decision

We will provide an explicit **"Start with Windows" toggle in the Settings window, defaulting
to OFF**. `StartupRegistrar` writes to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
only when the user enables the toggle. The first-run wizard (in the Foundations epic)
highlights the toggle and explains it.

### Consequences

- **Positive**: Never blocked by enterprise endpoint-protection tools (no surprise registry
  writes). The user's intent is explicit; no silent behavior on install. No elevation required
  (HKCU is user-scoped).
- **Negative**: The app does not auto-start after install — the user must manually enable it
  or launch it manually on subsequent sessions. Day-1 activation rate (SPEC metric) depends
  on the first-run wizard surfacing the toggle clearly.
- **Neutral**: The toggle state is stored in `AppSettings.AutoStartWithWindows` (persisted in
  `appsettings.local.json`) and is kept in sync with the actual registry key by
  `StartupRegistrar` on each settings save.

### Alternatives considered

- **Auto-register on first launch** — expected behavior for system-tray tools. Rejected
  because: enterprise endpoint-protection software (CrowdStrike, SentinelOne, etc.) frequently
  flags or blocks silent startup registration writes, causing confusing install failures. Since
  the app targets work-from-home knowledge workers, many run on managed Windows machines.

---

## ADR-0005: Aspire ServiceDefaults applied as a direct NuGet reference in the WPF host

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: architecture phase (design-architecture)

### Context

The enterprise guardrail requires OpenTelemetry via Aspire `ServiceDefaults`. Standard Aspire
usage expects a `WebApplicationBuilder` (Kestrel web host). The Mover's `TheMover.App` uses a
`HostApplicationBuilder` (Generic Host, WPF on STA thread) — not a web host — so the full
Aspire app model's HTTP health endpoint and service discovery middleware cannot be applied
verbatim.

### Decision

We will call `builder.AddServiceDefaults()` on `HostApplicationBuilder` directly. Aspire's
`ServiceDefaults` extension works with `IHostApplicationBuilder` (not just
`IWebApplicationBuilder`), so OTel traces, metrics, and resilience defaults all wire up
correctly. The HTTP health-check endpoint (`MapHealthChecks`) is **not** exposed in production
(no Kestrel pipeline); it is exposed only in dev via the AppHost mapping. The AppHost still
composes `TheMover.App` as a project resource for the OTel dashboard, injecting the OTLP
endpoint via environment variables.

### Consequences

- **Positive**: OTel traces and metrics work correctly in both dev (dashboard) and prod (local
  sink). No custom OTel wiring needed; the standard `ServiceDefaults` project handles it.
  The dev experience (Aspire dashboard, distributed traces) works as expected.
- **Negative**: The HTTP health endpoint is unavailable in production — health is assessed via
  the local event log heartbeat instead. Any future monitoring that depends on an HTTP health
  probe will need an additional Kestrel listener.
- **Neutral**: `TheMover.AppHost` still references `TheMover.App` as a project resource;
  Aspire's environment variable injection (`OTEL_EXPORTER_OTLP_ENDPOINT`) flows to the WPF
  process normally since it is just a child process of the AppHost.

### Alternatives considered

- **Custom OTel wiring without ServiceDefaults** — would work but duplicates the cross-cutting
  wiring that `ServiceDefaults` already encodes, diverging from the guardrail. Rejected.
- **Add a minimal Kestrel listener solely for health checks** — adds complexity (port
  management, firewall rules) for a feature not required in v1. Rejected.
