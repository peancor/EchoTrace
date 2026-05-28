# EchoTrace Architecture

EchoTrace is split into a small firmware receiver and a Windows desktop app.

```text
BLE advertisements -> EchoTrace.Node -> USB CDC JSON Lines -> EchoTrace.App -> SQLite/CSV + live charts
```

Projects:

- `EchoTrace.App`: WPF UI desktop shell, live dashboard, and application composition.
- `EchoTrace.Core`: protocol models, parser, simulation, RSSI aggregation.
- `EchoTrace.Serial`: COM port discovery and serial JSON Lines reader.
- `EchoTrace.Storage`: SQLite session storage and CSV export.
- `EchoTrace.Node`: Zephyr firmware for the nRF52840 board.

The simulator and serial reader feed the same parser and aggregation path, so UI and storage can be tested without hardware.

## Desktop UI Shell

`EchoTrace.App` uses WPF UI (`WPF-UI` NuGet package) for the Fluent shell, dark theme resources, title bar, and modern command buttons.

- `App.xaml` merges WPF UI `ThemesDictionary` and `ControlsDictionary`.
- `MainWindow` derives from `Wpf.Ui.Controls.FluentWindow`.
- The live BLE dashboard remains in `MainWindow` for now so the ScottPlot controls, serial reader, and capture lifecycle stay stable during the migration.
- `src/EchoTrace.App/Views/Pages/` contains page targets for the planned shell split: `DashboardPage`, `SessionsPage`, `ReceiversPage`, and `SettingsPage`.
- `MainViewModel.ShellSections` is the current navigation model. Only `Dashboard` is enabled in V1; the other sections are intentionally present but disabled until their pages own real workflows.

## Dashboard V1

The WPF dashboard separates ingestion from rendering:

- Serial and simulator events update Core aggregation buffers.
- Charts render on a UI timer instead of rendering once per BLE event.
- RSSI charts support rolling windows: `10s`, `30s`, `2m`, and `5m`.
- The selected-device RSSI chart shows raw points plus an exponential moving average.
- A second live chart tracks events per second.
- Device filtering supports name/address text, minimum RSSI, and present-only mode.
- The right panel shows selected-device detail and an RSSI ranking for quick triage.

This keeps the UI responsive when the receiver emits many advertisements per second.
