namespace TakenLi.Core;

public sealed class ApplicationLayouts
{
    private readonly Dictionary<string, long> _layouts;
    private readonly Action? _changed;
    private string? _application;
    private long _window;
    private long _observedLayout;
    private long? _automaticLayout;
    private long? _pending;
    private long _deadline;

    public ApplicationLayouts(IEnumerable<KeyValuePair<string, long>>? remembered = null, Action? changed = null)
    {
        _layouts = remembered is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(remembered, StringComparer.OrdinalIgnoreCase);
        _changed = changed;
    }

    public IReadOnlyDictionary<string, long> Remembered => _layouts;

    public long? Observe(string? application, long window, long layout, long now)
    {
        if (application is null || window == 0)
        {
            _application = null;
            _window = 0;
            _pending = null;
            _automaticLayout = null;
            return null;
        }
        if (!IsCurrent(application, window))
        {
            _application = application;
            _window = window;
            _observedLayout = layout;
            _automaticLayout = null;
            _pending = null;
            if (_layouts.TryGetValue(application, out var remembered))
            {
                if (remembered == layout) return null;
                AwaitLayout(remembered, now);
                return remembered;
            }
            Remember(application, layout);
            return null;
        }
        if (_pending is long expected)
        {
            if (layout == expected)
            {
                _observedLayout = layout;
                _pending = null;
                return null;
            }
            // Another actual layout change can overtake an asynchronous request.
            if (layout == _observedLayout && now < _deadline) return null;
            _pending = null;
        }
        if (layout == 0 || layout == _observedLayout) return null;
        _observedLayout = layout;
        if (_automaticLayout == layout) return null;
        _automaticLayout = null;
        Remember(application, layout);
        return null;
    }

    public void AutomaticReturn(long layout, long now)
    {
        _automaticLayout = layout;
        AwaitLayout(layout, now);
    }

    public void RequestRejected()
    {
        _pending = null;
    }

    public void Typing(string application, long window, long layout, long now)
    {
        if (!IsCurrent(application, window) || layout == 0) return;
        // An app may still be applying its remembered layout when the key arrives.
        if (_pending is long expected && _automaticLayout is null && layout != expected && now < _deadline) return;
        if (layout == _automaticLayout) _automaticLayout = null;
        if (layout == _pending) _pending = null;
        _observedLayout = layout;
        Remember(application, layout);
    }

    public void ExplicitChange(string application, long window, long layout, long now)
    {
        if (IsCurrent(application, window))
        {
            _automaticLayout = null;
            AwaitLayout(layout, now);
        }
        Remember(application, layout);
    }

    private bool IsCurrent(string application, long window) =>
        window == _window && string.Equals(application, _application, StringComparison.OrdinalIgnoreCase);

    private void AwaitLayout(long layout, long now)
    {
        _pending = layout;
        _deadline = now + 2000;
    }

    private void Remember(string application, long layout)
    {
        if (layout == 0 || (_layouts.TryGetValue(application, out var previous) && previous == layout)) return;
        _layouts[application] = layout;
        _changed?.Invoke();
    }
}
