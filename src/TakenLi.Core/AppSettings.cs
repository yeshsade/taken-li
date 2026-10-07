using System.Text.Json;

namespace TakenLi.Core;

public enum SelectionScope { CurrentLine, WholeField }

public sealed record Hotkey(int Key, bool Control = false, bool Shift = false, bool Alt = false)
{
    public int Modifiers => (Alt ? 1 : 0) | (Control ? 2 : 0) | (Shift ? 4 : 0);
    public string Display
    {
        get
        {
            var name = Key is >= 112 and <= 135 ? $"F{Key - 111}" :
                Key is >= 65 and <= 90 or >= 48 and <= 57 ? ((char)Key).ToString() : $"Key {Key}";
            return (Control ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + name;
        }
    }
}

public sealed class AppSettings
{
    public string Language { get; set; } = "he";
    public bool AutoReturnEnabled { get; set; } = true;
    public int AutoReturnSeconds { get; set; } = 30;
    public SelectionScope UnselectedScope { get; set; } = SelectionScope.CurrentLine;
    public bool StartWithWindows { get; set; }
    public bool ShowWelcome { get; set; } = true;
    public Hotkey FixLayoutKey { get; set; } = new(121);
    public Hotkey SwapCaseKey { get; set; } = new(121, Shift: true);
    public Hotkey ReverseKey { get; set; } = new(117);
    public List<string> ExcludedApplications { get; set; } = [];

    public Hotkey KeyFor(TextAction action) => action switch
    {
        TextAction.FixLayout => FixLayoutKey,
        TextAction.SwapCase => SwapCaseKey,
        TextAction.Reverse => ReverseKey,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    public void SetKey(TextAction action, Hotkey key)
    {
        switch (action)
        {
            case TextAction.FixLayout: FixLayoutKey = key; break;
            case TextAction.SwapCase: SwapCaseKey = key; break;
            case TextAction.Reverse: ReverseKey = key; break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    public AppSettings Copy() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        if (Language is not ("he" or "en")) throw new ArgumentException("language");
        if (AutoReturnSeconds is < 1 or > 3600) throw new ArgumentException("delay");
        if (!Enum.IsDefined(UnselectedScope)) throw new ArgumentException("scope");
        var keys = Enum.GetValues<TextAction>().Select(KeyFor).ToArray();
        if (keys.Any(k => k is null || !(k.Key is >= 112 and <= 135 or >= 65 and <= 90 or >= 48 and <= 57))) throw new ArgumentException("key");
        if (keys.Distinct().Count() != keys.Length) throw new ArgumentException("duplicate");
        // Do not allow an ordinary unmodified letter/digit to consume typing.
        if (keys.Any(k => k.Key < 112 && k.Modifiers == 0)) throw new ArgumentException("typing-key");
        if (ExcludedApplications is null || ExcludedApplications.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("exclusions");
    }
}

public sealed class IdleReturnPolicy
{
    private long _lastTyping;
    private long _window;
    private bool _wasEnglish;
    private bool _requested;

    public void Typed(long now)
    {
        _lastTyping = now;
        _requested = false;
    }

    public bool ShouldReturn(long window, bool english, long now, int delaySeconds)
    {
        if (window != _window || english != _wasEnglish)
        {
            _lastTyping = now;
            _window = window;
            _wasEnglish = english;
            _requested = false;
        }
        if (!english || _requested || now - _lastTyping < delaySeconds * 1000L) return false;
        _requested = true;
        return true;
    }

    public void Reset(long now)
    {
        _lastTyping = now;
        _window = 0;
        _wasEnglish = false;
        _requested = false;
    }
}
