using Iaet.Core.Models;

namespace Iaet.Capture;

public sealed class AuthHealthMonitor(int consecutiveFailureThreshold = 3)
{
    private int _consecutiveFailures;

    public bool IsHealthy => Volatile.Read(ref _consecutiveFailures) < consecutiveFailureThreshold;

    public event EventHandler? AuthUnhealthy;

    public static bool IsAuthFailure(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.ResponseStatus is 401 or 403;
    }

    public void RecordResponse(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsAuthFailure(request))
        {
            var current = Interlocked.Increment(ref _consecutiveFailures);
            if (current == consecutiveFailureThreshold)
            {
                AuthUnhealthy?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            Interlocked.Exchange(ref _consecutiveFailures, 0);
        }
    }
}
