using System;

namespace PowNet.Abstractions.Telemetry
{
    public interface ITelemetrySpan : IDisposable
    {
        void SetTag(string key, object? value);
        void SetError(string message);
    }

    public interface ITelemetrySpanFactory
    {
        ITelemetrySpan Start(string name, string? correlationId = null);
    }
}
