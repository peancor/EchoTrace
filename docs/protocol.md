# EchoTrace Protocol V1

EchoTrace.Node writes UTF-8 JSON Lines over USB CDC ACM. Each line is one event.

Advertisement event:

```json
{"v":1,"type":"adv","seq":12,"receiver":"A","uptimeMs":345678,"addr":"AA:BB:CC:DD:EE:FF","addrType":"random","rssi":-67,"name":"Device","advType":"connectable","dataLen":31}
```

Fields:

- `v`: protocol version.
- `type`: `adv` for BLE advertisement events.
- `seq`: firmware-local monotonically increasing sequence.
- `receiver`: receiver id, `A` in V1.
- `uptimeMs`: firmware uptime in milliseconds.
- `addr`: BLE advertiser address.
- `addrType`: `public`, `random`, `public_id`, `random_id`, or `unknown`.
- `rssi`: received signal strength in dBm.
- `name`: advertised local name, or `null`.
- `advType`: advertisement type.
- `dataLen`: advertisement payload length in bytes.

The desktop app adds `ReceivedAtUtc` when a line is received.
