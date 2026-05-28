# EchoTrace.Node

Zephyr/NCS firmware for a nice!nano / Pro Micro nRF52840 board with UF2 bootloader.

## Build

Run from an NCS-enabled terminal, or use the explicit `west.exe` path:

```powershell
C:\ncs\toolchains\936afb6332\opt\bin\Scripts\west.exe build -p always -b promicro_nrf52840/nrf52840/uf2 D:\dev\EchoTrace\firmware\EchoTrace.Node -d D:\dev\EchoTrace\firmware\EchoTrace.Node\build
```

If `west` has trouble because NCS is on `C:` and this repo is on `D:`, use CMake/Ninja directly after loading the NCS toolchain environment as described in `docs/firmware.md`.

The UF2 artifact is expected at:

```text
firmware/EchoTrace.Node/build/zephyr/zephyr.uf2
```

## Flash

1. Connect the board over USB.
2. Bridge `RST` to `GND` twice quickly.
3. Wait for the UF2 bootloader drive to appear.
4. Copy `zephyr.uf2` to that drive.
5. Reconnect/open the emitted COM port from EchoTrace.

The firmware emits one JSON object per line over USB CDC ACM.
