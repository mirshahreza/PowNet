using System.Diagnostics;
using PowNet.Abstractions.Telemetry;

namespace PowNet.Implementations.Telemetry;

internal sealed class ActivityTelemetrySpan : ITelemetrySpan
{
    private readonly Activity? _activity;
    public ActivityTelemetrySpan(Activity? activity) => _activity = activity;
    public void Dispose() => _activity?.Stop();
    public void SetTag(string key, object? value) { if (_activity != null && value != null) _activity.SetTag(key, value); }
    public void SetError(string message) { if (_activity != null) { _activity.SetTag("error", true); _activity.SetTag("error.message", message); } }
}

public sealed class TelemetrySpanFactory : ITelemetrySpanFactory
{
    private static readonly ActivitySource Source = new("PowNet");
    public ITelemetrySpan Start(string name, string? correlationId = null)
    {
        var activity = Source.StartActivity(name, ActivityKind.Internal);
        if (activity != null && !string.IsNullOrEmpty(correlationId)) activity.SetTag("correlation.id", correlationId);
        return new ActivityTelemetrySpan(activity);
    }
}
