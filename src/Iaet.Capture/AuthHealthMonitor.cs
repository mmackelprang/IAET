using Iaet.Core.Models;

namespace Iaet.Capture;

public sealed class AuthHealthMonitor(int consecutiveFailureThreshold = 3)
{
    private int _consecutiveFailures;

    public bool IsHealthy => _consecutiveFailures < consecutiveFailureThreshold;

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
            _consecutiveFailures++;
            if (_consecutiveFailures >= consecutiveFailureThreshold)
            {
                AuthUnhealthy?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            _consecutiveFailures = 0;
        }
    }
}
