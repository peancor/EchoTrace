namespace EchoTrace.Core;

public sealed record CaptureSession
{
    public long Id { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public string ReceiverId { get; init; } = "A";
    public string Source { get; init; } = "Simulator";
    public string? Notes { get; init; }
}
