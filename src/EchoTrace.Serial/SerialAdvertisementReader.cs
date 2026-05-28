using System.IO.Ports;
using System.Runtime.CompilerServices;
using EchoTrace.Core;

namespace EchoTrace.Serial;

public sealed class SerialAdvertisementReader
{
    private readonly AdvertisementEventParser _parser = new();

    public async IAsyncEnumerable<SerialReadResult> ReadEventsAsync(
        string portName,
        int baudRate = 115200,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var port = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 250,
            DtrEnable = true,
            RtsEnable = false
        };

        port.Open();
        port.DiscardInBuffer();

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = null;
            try
            {
                line = await Task.Run(port.ReadLine, cancellationToken);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            AdvertisementParseResult parsed = _parser.ParseLine(line.Trim(), DateTimeOffset.UtcNow);
            yield return parsed.Event is not null
                ? SerialReadResult.FromEvent(parsed.Event)
                : SerialReadResult.FromError(parsed.Error ?? "Could not parse serial line.", line);
        }
    }
}
