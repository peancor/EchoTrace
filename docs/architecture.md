# EchoTrace Architecture

EchoTrace is split into a small firmware receiver and a Windows desktop app.

```text
BLE advertisements -> EchoTrace.Node -> USB CDC JSON Lines -> EchoTrace.App -> SQLite/CSV + live charts
```

Projects:

- `EchoTrace.App`: WPF dashboard and application composition.
- `EchoTrace.Core`: protocol models, parser, simulation, RSSI aggregation.
- `EchoTrace.Serial`: COM port discovery and serial JSON Lines reader.
- `EchoTrace.Storage`: SQLite session storage and CSV export.
- `EchoTrace.Node`: Zephyr firmware for the nRF52840 board.

The simulator and serial reader feed the same parser and aggregation path, so UI and storage can be tested without hardware.

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
