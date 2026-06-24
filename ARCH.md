# The Mover — Architecture

> Phase 7 artifact. `build-system` executes against this document.
> Guardrail adaptations from `PLAN.md` are in force throughout — this is a single-user
> Windows desktop app with no network backend, no database, and no multi-tenancy.

---

## Context (C4 L1)

The Mover is a single-process Windows desktop application. All data stays on the user's
machine. The only outbound network dependency is Microsoft 365 Graph API for calendar
reads; no inbound connections exist.

External actors:

| Actor | Role |
|---|---|
| **Desk Worker / Configurator** | The OS-authenticated user; the only human actor |
| **Microsoft 365 Graph API** | Read-only calendar events (`Calendars.Read` scope); polled every 60 s |
| **Windows Credential Manager** | DPAPI-backed store for the OAuth refresh token |
| **Windows Registry (HKCU)** | Stores the startup registration entry |
| **Local filesystem** | `%LOCALAPPDATA%\TheMover\` — config JSON + structured event log |

Diagram: `docs/diagrams/c1-context.mmd`

---

## Containers (C4 L2)

All containers run in-process within a **single .NET Generic Host**. There is no separate
web server, message broker, database, or cache. The AppHost is a dev-time artifact only.

| Container | Type | Role |
|---|---|---|
| **TheMover.AppHost** | .NET Aspire AppHost | Dev-time only — composes the app for the OTel dashboard; not deployed |
| **TheMover.ServiceDefaults** | .NET class library | OTel + health checks + Polly defaults; applied via `AddServiceDefaults()` |
| **TheMover.App** | WPF + Generic Host | Entry point; system tray (`NotifyIcon`), settings window, local config (`IOptions<T>`), local event log, startup registration |
| **TheMover.Scheduler** | `IHostedService` | Two-tier break timer engine; skip/snooze logic; idle detection (`GetLastInputInfo` P/Invoke) |
| **TheMover.Overlay** | WPF Window | Break overlay: countdown timer, Lottie animation frame, Skip/Snooze buttons |
| **TheMover.Content** | .NET class library | Exercise catalog loader (`exercises.json` → `Exercise[]`); rotation algorithm |
| **TheMover.Calendar** | `IHostedService` | Microsoft Graph client; 60 s calendar poll; Polly retry; Windows Credential Manager integration |

Diagram: `docs/diagrams/c2-containers.mmd`

---

## Components (C4 L3) — TheMover.App

| Component | Type | Role |
|---|---|---|
| `Program` | Static entry point | Generic Host setup: registers all `IHostedService`, `IOptions<T>`, `WpfHostedService`, `ServiceDefaults` |
| `WpfHostedService` | `IHostedService` | Starts the WPF `Application` on a dedicated STA thread; bridges Generic Host lifetime with WPF dispatcher loop |
| `TrayIconService` | `IHostedService` | Owns `NotifyIcon` lifecycle; builds context menu; subscribes to `Channel<BreakDueEvent>` to trigger overlay |
| `SettingsWindow` | WPF `Window` | Tabbed UI (Break Schedule, Outlook, About); bound to live `AppSettings` snapshot |
| `ConfigManager` | Service | Reads/writes `appsettings.local.json`; exposes `IOptionsMonitor<AppSettings>`; validates at startup |
| `EventLogger` | Service | Appends JSONL records to `%LOCALAPPDATA%\TheMover\events.jsonl`; never throws on write failure (fire-and-forget) |
| `StartupRegistrar` | Service | Reads/writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; called when "Start with Windows" toggle changes |

Diagram: `docs/diagrams/c3-components-app.mmd`

---

## Solution layout

Projects exist and why — scaffolding steps are owned by `dotnet-architecture`/`dotnet-aspire-base`.

```
src/
  TheMover.AppHost/               ← Aspire AppHost; dev-time orchestration only
  TheMover.ServiceDefaults/       ← OTel + health checks + Polly defaults (Aspire-generated)
  TheMover.App/                   ← WPF entry point; Generic Host; tray, settings, config, event log
  TheMover.Scheduler/             ← Break timer + idle detection (IHostedService)
  TheMover.Overlay/               ← WPF break overlay window + Lottie player
  TheMover.Content/               ← Exercise catalog class library (no UI dependency)
  TheMover.Calendar/              ← Microsoft Graph poller (IHostedService)

tests/
  TheMover.App.Tests/             ← Unit tests: ConfigManager, EventLogger, StartupRegistrar
  TheMover.Scheduler.Tests/       ← Unit tests: timer logic, skip/snooze, idle detection
  TheMover.Content.Tests/         ← Unit tests: catalog loading, rotation algorithm
  TheMover.Calendar.Tests/        ← Unit + integration tests: Graph client, polling logic
```

No `Infrastructure.<Cloud>` project (`cloud: none`). No `Web` SPA — UI is WPF.

**Epic → module mapping:**

| Epic | Modules |
|---|---|
| 1 — Foundations | TheMover.App, TheMover.AppHost, TheMover.ServiceDefaults |
| 2 — Break Scheduler | TheMover.Scheduler |
| 3 — Break Overlay & Exercise Library | TheMover.Overlay, TheMover.Content |
| 4 — Outlook Meeting Suppression | TheMover.Calendar |
| 5 — Idle / Away Detection | TheMover.Scheduler (added component within existing service) |

---

## Cross-cutting wiring

### AuthN
None at app level. OS user isolation is the sole access boundary. Microsoft Graph uses
OAuth 2.0 with PKCE (Authorization Code + PKCE, no client secret) for the Outlook
integration only. The resulting refresh token is stored in Windows Credential Manager
under key `TheMover.OutlookToken`. See **ADR-0002**.

### RBAC
Single implicit `User` role. Policy names are defined as `const string` values in
`TheMover.App.Policies` (see PLAN.md) but are not enforced by any middleware — enforcement
exists as named constants for forward-compatibility only.

### Multi-tenancy
Not applicable. Single OS user; no database; no tenant resolution.

### Observability
OpenTelemetry via `TheMover.ServiceDefaults` (`AddServiceDefaults()` on `IHostApplicationBuilder`).

- **Dev**: OTel traces + metrics exported to the Aspire dashboard (OTLP HTTP endpoint set
  by AppHost via environment injection).
- **Prod**: `ASPNETCORE_ENVIRONMENT=Production`; no OTLP endpoint configured → fallback to
  `OpenTelemetry.Exporter.Console` writing to stdout (captured by Windows Event Log via
  the service wrapper, or discarded if run interactively). A local file exporter
  (`OpenTelemetry.Exporter.OpenTelemetryProtocol` targeting a local file sink) is added
  as a conditional in `ServiceDefaults` when `OTEL_EXPORTER_OTLP_ENDPOINT` is unset.

Health checks registered via ServiceDefaults; exposed on `http://localhost:5001/health`
during dev (AppHost maps the port). Not exposed in production (no Kestrel pipeline). See **ADR-0005**.

### Resilience
Polly `RetryPolicy` (3 retries, exponential backoff with jitter) on all outbound Graph HTTP
calls in `TheMover.Calendar`. No Polly needed elsewhere — all other work is in-process.
`TheMover.ServiceDefaults` wires default Polly handlers via `AddDefaultResilienceHandler()`.

### Caching
No distributed cache (`cloud: none`). `ExerciseRotation` and `BreakTimerState` are
in-memory only; rebuilt from config + wall clock on startup.

### Background work
Three `IHostedService` implementations registered in the Generic Host:

| Service | Project | Cadence |
|---|---|---|
| `BreakSchedulerService` | TheMover.Scheduler | Ticks every second; fires `BreakDueEvent` at configured intervals |
| `IdleMonitorService` | TheMover.Scheduler | Polls `GetLastInputInfo` every 30 s; pauses/resumes timer |
| `CalendarPollerService` | TheMover.Calendar | Polls Graph API every 60 s; updates `HeldForMeeting` on `BreakTimerState` |

No outbox — all side effects are in-process state mutations. No durability guarantee needed.

**Inter-service communication:** `System.Threading.Channels.Channel<T>` (bounded, capacity 1).

```
BreakSchedulerService  →  Channel<BreakDueEvent>   →  TrayIconService + OverlayWindowService
TrayIconService        →  Channel<BreakCommand>    →  BreakSchedulerService
```

### Configuration & secrets
`IOptions<AppSettings>` backed by `appsettings.json` (shipped defaults) overlaid by
`appsettings.local.json` (user overrides; gitignored). Validated at startup with
`ValidateDataAnnotations().ValidateOnStart()`. OAuth token stored exclusively in
Windows Credential Manager (never in any config file). See **ADR-0004** for startup
registration.

### Compliance posture
- No PII in config files or event log (exercise IDs are GUIDs; no personal data recorded).
- OAuth refresh token in Windows Credential Manager (DPAPI encryption at rest).
- Full data deletion = uninstall + delete `%LOCALAPPDATA%\TheMover\`.
- No telemetry that phones home in v1.

---

## Cloud topology

`cloud: none`. The app runs entirely on the user's Windows machine. No cloud infrastructure
is owned or operated by this system in v1.

| Concern | Decision |
|---|---|
| Provider | None (personal Windows device) |
| Compute | Windows background process (Generic Host, no service wrapper in v1) |
| Data | Local filesystem: `%LOCALAPPDATA%\TheMover\` |
| Secrets | Windows Credential Manager (DPAPI) |
| OTel sink | Local stdout (prod); Aspire dashboard (dev) |
| CDN / Edge | Not applicable |
| Networking | No inbound; one outbound HTTPS connection to `graph.microsoft.com` |

---

## Data model (concrete)

No relational database. All state is in-memory or local files.

### AppSettings (persisted — `appsettings.local.json`)

```csharp
public sealed class AppSettings
{
    public BreakTierSettings MicroBreak { get; set; } = new() { IntervalMinutes = 20, DurationSeconds = 30 };
    public BreakTierSettings LongBreak  { get; set; } = new() { IntervalMinutes = 60, DurationSeconds = 300 };
    public bool AutoStartWithWindows { get; set; } = false;
    public SnoozeSettings Snooze { get; set; } = new();
    public CalendarSettings Calendar { get; set; } = new();
}

public sealed class BreakTierSettings
{
    [Range(1, 240)]  public int IntervalMinutes { get; set; }
    [Range(10, 600)] public int DurationSeconds { get; set; }
}

public sealed class SnoozeSettings
{
    [Range(1, 30)] public int IncrementMinutes { get; set; } = 5;
}

public sealed class CalendarSettings
{
    public bool Enabled  { get; set; } = false;
    public string? TenantId { get; set; }  // null = use /common OAuth endpoint
}
```

### Windows Credential Manager entries

| Key | Content | Notes |
|---|---|---|
| `TheMover.OutlookToken` | OAuth refresh token (plain string) | DPAPI encrypted by Windows. Never logged. Absent = calendar integration not configured. |

### In-memory models (not persisted; rebuilt on startup)

```csharp
// TheMover.Content
public sealed record Exercise(
    Guid Id,
    string Title,
    string InstructionText,
    string AnimationAssetPath,   // relative path within embedded assets
    string[] MuscleGroupTags
);

// TheMover.Content
public sealed class ExerciseRotation
{
    public Guid? LastShownExerciseId { get; set; }
}

// TheMover.Scheduler
public enum BreakTier { Micro, Long }

public sealed class BreakTimerState
{
    public DateTimeOffset NextBreakAt   { get; set; }
    public BreakTier      Tier          { get; set; }
    public DateTimeOffset? IdleDetectedAt { get; set; }
    public bool           HeldForMeeting  { get; set; }
    public DateTimeOffset? SnoozedUntil   { get; set; }
}
```

### Local event log (`%LOCALAPPDATA%\TheMover\events.jsonl`)

Append-only JSONL. Each line is a flat JSON object. Used for SPEC success metrics.

| Event | Fields | Metric |
|---|---|---|
| `BreakFired` | `tier` | overlay render success rate |
| `OverlayShown` | `exerciseId` | day-1 activation, overlay render success rate |
| `Snoozed` | `snoozeMinutes` | snooze-to-dismiss ratio |
| `Dismissed` | — | snooze-to-dismiss ratio |
| `Heartbeat` | — | 30-day retention |

Example:
```json
{"ts":"2026-06-24T10:00:00Z","event":"BreakFired","tier":"Micro"}
{"ts":"2026-06-24T10:00:01Z","event":"OverlayShown","exerciseId":"3f2a..."}
{"ts":"2026-06-24T10:01:30Z","event":"Snoozed","snoozeMinutes":5}
```

---

## API surface (internal)

No external HTTP API. Internal communication only via `Channel<T>`.

```csharp
// Produced by BreakSchedulerService; consumed by TrayIconService + OverlayWindowService
public sealed record BreakDueEvent(BreakTier Tier, DateTimeOffset FiredAt, Guid ExerciseId);

// Produced by TrayIconService / OverlayWindowService; consumed by BreakSchedulerService
public abstract record BreakCommand;
public sealed record SkipBreakCommand   : BreakCommand;
public sealed record SnoozeBreakCommand(int Minutes) : BreakCommand;
```

Channel configuration: `Channel.CreateBounded<BreakDueEvent>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest })`.
A break that fires while the previous overlay is still open replaces it.

---

## MAF agents

Not applicable. The Mover is a deterministic rule-based desktop app; no agentic inference needed.

---

## SPA architecture

Not applicable. The UI is WPF (not a web SPA). All UI components live in `TheMover.App`
(tray + settings window) and `TheMover.Overlay` (break overlay).

**WPF hosting pattern** (bridges Generic Host ↔ WPF dispatcher):

```csharp
// TheMover.App — WpfHostedService.cs
public sealed class WpfHostedService(IServiceProvider services) : IHostedService
{
    private Thread? _uiThread;

    public Task StartAsync(CancellationToken ct)
    {
        _uiThread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var app = services.GetRequiredService<Application>();
            app.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
        return Task.CompletedTask;
    }
}
```

**Lottie animation** (ADR-0003): `LottieSharp` NuGet package renders `.json` Lottie files
in WPF via SkiaSharp. The `LottieAnimationView` control is embedded in the overlay window.
Animation assets are bundled as `EmbeddedResource` in `TheMover.Content`.

---

## Diagrams checked into the repo

- `docs/diagrams/c1-context.mmd`
- `docs/diagrams/c2-containers.mmd`
- `docs/diagrams/c3-components-app.mmd`
