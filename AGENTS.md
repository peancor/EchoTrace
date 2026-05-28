# EchoTrace Agent Guide

This file is the handoff map for future agents working on EchoTrace. Keep it current whenever architecture, build commands, protocol fields, firmware target, or workflow assumptions change.

## Project Shape

EchoTrace captures BLE advertisements with a small nRF52840 receiver and visualizes them in a Windows WPF app.

```text
BLE advertisements -> EchoTrace.Node -> USB CDC JSON Lines -> EchoTrace.App -> SQLite/CSV + live charts
```

Main paths:

- `src/EchoTrace.App`: .NET 10 WPF UI dashboard, MVVM, simulator/serial controls, live table/chart, and shell/page scaffolding.
- `src/EchoTrace.Core`: protocol models, JSON Lines parser, simulator, RSSI aggregation.
- `src/EchoTrace.Serial`: COM port discovery and async serial JSON Lines reader.
- `src/EchoTrace.Storage`: SQLite capture sessions and CSV export.
- `src/EchoTrace.App/Views/Pages`: WPF UI page targets prepared for Dashboard, Sessions, Receivers, and Settings navigation. Settings is currently enabled for general app preferences.
- `tests/EchoTrace.Core.Tests`: parser, aggregator, and storage tests.
- `firmware/EchoTrace.Node`: Zephyr/NCS firmware for nice!nano / Pro Micro nRF52840 UF2 boards.
- `docs/`: project docs that should be updated with meaningful behavior changes.

Reference docs:

- `README.md`: public project overview, quickstart, privacy notes, roadmap, and license summary.
- `docs/architecture.md`: current system layout and data flow.
- `docs/protocol.md`: JSON Lines wire protocol.
- `docs/firmware.md`: NCS/Zephyr build and UF2 flashing workflow.
- `firmware/EchoTrace.Node/README.md`: firmware-specific quickstart.

## Current Hardware/Firmware Assumptions

- Board: nice!nano / Pro Micro nRF52840, not the official Nordic nRF52840 Dongle.
- Bootloader: UF2. Enter bootloader by bridging `RST` and `GND` twice quickly.
- Zephyr board target: `promicro_nrf52840/nrf52840/uf2`.
- NCS install: `C:\ncs\v3.3.0`.
- Toolchain: `C:\ncs\toolchains\936afb6332`.
- Firmware emits USB CDC ACM JSON Lines at `115200 8N1`.
- Receiver id is `A` in firmware V1; simulator uses `SIM`.

Do not use Nordic Dongle erase/flash instructions for this board.

## Verified Commands

From `D:\dev\EchoTrace`:

```powershell
dotnet build EchoTrace.slnx --no-restore
dotnet test EchoTrace.slnx --no-build
```

Firmware build currently works with the NCS environment variables loaded explicitly and may need to run outside the sandbox because Zephyr tools such as `dtc` and `ninja` can fail under restricted execution:

```powershell
$tc='C:\ncs\toolchains\936afb6332'
$env:PATH="$tc;$tc\mingw64\bin;$tc\bin;$tc\opt\bin;$tc\opt\bin\Scripts;$tc\opt\nanopb\generator-bin;$tc\nrfutil\bin;$tc\opt\zephyr-sdk\arm-zephyr-eabi\bin;$tc\opt\zephyr-sdk\riscv64-zephyr-elf\bin;$env:PATH"
$env:PYTHONPATH="$tc\opt\bin;$tc\opt\bin\Lib;$tc\opt\bin\Lib\site-packages"
$env:NRFUTIL_HOME="$tc\nrfutil\home"
$env:ZEPHYR_TOOLCHAIN_VARIANT='zephyr'
$env:ZEPHYR_SDK_INSTALL_DIR="$tc\opt\zephyr-sdk"
$env:ZEPHYR_BASE='C:\ncs\v3.3.0\zephyr'
cmake -S D:\dev\EchoTrace\firmware\EchoTrace.Node -B D:\dev\EchoTrace\firmware\EchoTrace.Node\build -G Ninja -DBOARD=promicro_nrf52840/nrf52840/uf2
ninja -C D:\dev\EchoTrace\firmware\EchoTrace.Node\build
```

Expected firmware artifact:

```text
firmware/EchoTrace.Node/build/zephyr/zephyr.uf2
```

`firmware/**/build/` is ignored by git.

## Protocol Contract

EchoTrace.Node writes one JSON object per line. Current advertisement event:

```json
{"v":1,"type":"adv","seq":12,"receiver":"A","uptimeMs":345678,"addr":"AA:BB:CC:DD:EE:FF","addrType":"random","rssi":-67,"name":"Device","advType":"connectable","dataLen":31}
```

The app adds `ReceivedAtUtc` when a line is received. If this contract changes, update:

- `docs/protocol.md`
- `src/EchoTrace.Core/AdvertisementEvent.cs`
- `src/EchoTrace.Core/AdvertisementEventParser.cs`
- `firmware/EchoTrace.Node/src/main.c`
- parser tests

## Development Guidance

- Preserve the simulator path. It is the fastest way to validate UI, storage, and charting without hardware.
- Keep serial and simulator feeding the same Core pipeline.
- Keep WPF UI work on the dispatcher thread.
- `EchoTrace.App` uses the `WPF-UI` NuGet package. `App.xaml` owns WPF UI theme/control dictionaries, and `MainWindow` currently derives from `Wpf.Ui.Controls.FluentWindow`.
- The live dashboard is still hosted in `MainWindow`; the page files under `Views/Pages` are navigation targets for the next shell split. Do not move the ScottPlot/serial lifecycle into a page without checking shutdown and navigation lifetime.
- Theme support is split between WPF UI and EchoTrace resources. `MainViewModel.SelectedTheme` drives WPF UI's `ApplicationThemeManager`, local EchoTrace brushes, and ScottPlot palette updates. Keep charts readable in both Light and Dark whenever changing colors.
- App UI preferences persist to `%LocalAppData%\EchoTrace\settings.json` through `EchoTrace.App.Services.AppSettingsStore`. Keep new general settings in that store unless they belong in capture/session storage.
- Keep chart rendering timer-driven. Do not render once per BLE event; event bursts should update buffers and let the UI timer paint.
- `MainViewModel` captures the UI `Dispatcher` at construction and uses it for background serial/simulator callbacks. Do not use `Application.Current.Dispatcher` from background paths because `Application.Current` can be null during shutdown.
- Keep live WPF collections incremental. Do not clear and rebuild `FilteredDevices` or `RankedDevices` during normal advertisement updates; reconcile items in place so row containers, selection, and charts remain stable.
- Keep the main device table visually stable. The table uses receiver/name/address ordering, while the RSSI ranking is the section that should reorder by live signal strength.
- Keep dashboard docs aligned with `docs/architecture.md` when changing filters, charts, ranking, or detail panels.
- Prefer focused tests in `tests/EchoTrace.Core.Tests` for parser, aggregation, storage, and any protocol evolution.
- Use SQLite storage through `EchoTrace.Storage`; do not let the WPF app write SQL directly.
- Firmware should continue to emit compact JSON Lines and avoid chatty logs on the CDC data channel.
- If adding multi-receiver support, keep `ReceiverId` part of every aggregation/storage key.

## Update Rule

When a change modifies behavior, setup, protocol, build commands, hardware assumptions, storage schema, or project structure:

1. Update the relevant file in `docs/`.
2. Update this `AGENTS.md` if a future agent would otherwise make the wrong assumption.
3. Add or adjust tests when the change affects parser, aggregation, storage, or exports.
4. Keep the public `README.md` aligned with user-facing setup, hardware, license, and privacy expectations.
