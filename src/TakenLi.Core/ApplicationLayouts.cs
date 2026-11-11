namespace TakenLi.Core;

// Session-only state: no window titles, typed text or document names are stored.
public sealed class ApplicationLayouts
{
    private readonly Dictionary<string, long> _layouts = new(StringComparer.OrdinalIgnoreCase);
    private string? _application;
    private long _window;
    private long? _automaticLayout;
    private long? _pending;
    private long _deadline;

    public long? Observe(string? application, long window, long layout, long now)
    {
        if (application is null)
        {
            _application = null;
            _window = 0;
            _pending = null;
            _automaticLayout = null;
            return null;
        }
        if (window != _window || !string.Equals(application, _application, StringComparison.OrdinalIgnoreCase))
        {
            _application = application;
            _window = window;
            _automaticLayout = null;
            _pending = null;
            if (_layouts.TryGetValue(application, out var remembered) && remembered != layout)
            {
                _pending = remembered;
                _deadline = now + 2000;
                return remembered;
            }
        }
        if (_pending is long expected)
        {
            if (layout != expected && now < _deadline) return null;
            _pending = null;
        }
        if (_automaticLayout == layout) return null;
        _automaticLayout = null;
        if (layout != 0) _layouts[application] = layout;
        return null;
    }

    public void AutomaticReturn(long layout, long now)
    {
        _automaticLayout = layout;
        _pending = layout;
        _deadline = now + 2000;
    }

    // An explicit text correction is a user action, even if it selects the
    // same Hebrew layout that the automatic timer previously selected.
    public void ExplicitChange(string application, long layout, long now)
    {
        if (layout != 0) _layouts[application] = layout;
        _automaticLayout = null;
        _pending = layout;
        _deadline = now + 2000;
    }
}
