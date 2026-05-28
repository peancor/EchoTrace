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
- `MainViewModel.ShellSections` is the current navigation model. `Dashboard` and `Settings` are enabled; `Sessions` and `Receivers` are intentionally present but disabled until their pages own real workflows.
- `Settings` exposes general app preferences, starting with the application theme.
- Supported themes are `Dark` and `Light`. `MainWindow` applies WPF UI's `ApplicationThemeManager` and then updates EchoTrace-specific brushes used by panels, tables, activity lists, and ScottPlot.
- App preferences are persisted as JSON in `%LocalAppData%\EchoTrace\settings.json`.
- Persisted V1 preferences include theme, source mode, selected port, chart window, minimum RSSI text, and present-only filtering.
- ScottPlot rendering is theme-aware; chart backgrounds, axes, grid lines, legends, and series colors are recalculated whenever the theme changes.

## Dashboard V1

The WPF dashboard separates ingestion from rendering:

- Serial and simulator events update Core aggregation buffers.
- Charts render on a UI timer instead of rendering once per BLE event.
- RSSI charts support rolling windows: `10s`, `30s`, `2m`, and `5m`.
- The selected-device RSSI chart shows raw points plus an exponential moving average.
- A second live chart tracks events per second.
- Device filtering supports name/address text, minimum RSSI, and present-only mode.
- The right panel shows selected-device detail and an RSSI ranking for quick triage.
- Live device lists use incremental `ObservableCollection` reconciliation instead of full clear/reload refreshes. Row view models are updated in place, the table keeps a stable receiver/name/address order, and only the RSSI ranking reorders by signal strength.
- Device selection is preserved across live updates whenever the selected device still passes the active filters.

This keeps the UI responsive when the receiver emits many advertisements per second.
