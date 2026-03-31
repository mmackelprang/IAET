using Iaet.Core.Models;

namespace Iaet.Capture;

public sealed class CaptureContextAnnotator
{
    private CaptureContext? _currentContext;

    public void SetContext(CaptureContext context) => _currentContext = context;

    public void ClearContext() => _currentContext = null;

    public CapturedRequest Annotate(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentContext is null)
            return request;

        var tag = _currentContext.ElementSelector is not null
            ? $"[{_currentContext.Trigger}] {_currentContext.ElementSelector}"
            : $"[{_currentContext.Trigger}]";

        if (_currentContext.Description is not null)
            tag += $" \u2014 {_currentContext.Description}";

        return request with { Tag = tag };
    }
}
