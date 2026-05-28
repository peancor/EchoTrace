using System.IO.Ports;

namespace EchoTrace.Serial;

public static class SerialPortDiscovery
{
    public static IReadOnlyList<string> GetPortNames()
    {
        return SerialPort.GetPortNames()
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
