# Firmware

Target hardware: nice!nano / Pro Micro nRF52840 with UF2 bootloader.

NCS installation expected by this repo:

- NCS: `C:\ncs\v3.3.0`
- Toolchain: `C:\ncs\toolchains\936afb6332`
- Board: `promicro_nrf52840/nrf52840/uf2`

Build with the NCS environment loaded:

```powershell
C:\ncs\toolchains\936afb6332\opt\bin\Scripts\west.exe build -p always -b promicro_nrf52840/nrf52840/uf2 D:\dev\EchoTrace\firmware\EchoTrace.Node -d D:\dev\EchoTrace\firmware\EchoTrace.Node\build
```

If `west` is launched from `C:\ncs\v3.3.0` and the app is on `D:`, Windows path handling can fail. The reliable direct build path is:

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

Flash:

1. Bridge `RST` and `GND` twice quickly.
2. Copy `firmware\EchoTrace.Node\build\zephyr\zephyr.uf2` to the UF2 drive.
3. Open the new COM port in EchoTrace.

Do not use Nordic Dongle erase/flash instructions for this board.
