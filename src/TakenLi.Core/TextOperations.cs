using System.Globalization;
using System.Text;

namespace TakenLi.Core;

public enum TextAction { FixLayout, SwapCase, Reverse }
public enum InputLanguage { English, Hebrew }

public static class TextOperations
{
    // Standard Hebrew keyboard, positional conversion. No keyboard layout is installed.
    private const string English = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";
    private const string Hebrew = "/'קראטוןםפ][שדגכעיחלךף,זסבהנמצתץ.;";
    private static readonly Dictionary<char, char> ToHebrew = MakeMap(English, Hebrew);
    private static readonly Dictionary<char, char> ToEnglish = MakeMap(Hebrew, English);

    private static Dictionary<char, char> MakeMap(string from, string to)
    {
        if (from.Length != to.Length) throw new InvalidOperationException("Keyboard mapping is invalid.");
        return from.Zip(to).ToDictionary(pair => pair.First, pair => pair.Second);
    }

    public static string Apply(string text, TextAction action, InputLanguage sourceLanguage = InputLanguage.English) => action switch
    {
        TextAction.FixLayout => FixLayout(text, sourceLanguage),
        TextAction.SwapCase => SwapCase(text),
        TextAction.Reverse => ReverseLines(text),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    public static bool IsHebrewLetter(char c) => c is >= '\u05D0' and <= '\u05EA';
    public static bool IsEnglishLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    public static string FixLayout(string text, InputLanguage sourceLanguage = InputLanguage.English)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsEnglishLetter(c)) result.Append(ToHebrew[char.ToLowerInvariant(c)]);
            else if (IsHebrewLetter(c)) result.Append(ToEnglish.GetValueOrDefault(c, c));
            else if (char.IsWhiteSpace(c) || char.IsDigit(c)) result.Append(c);
            else
            {
                // Shared punctuation has no language of its own. Use the nearest
                // original letter in this word, then the active input language.
                var language = PunctuationLanguage(text, i, sourceLanguage);
                var map = language == InputLanguage.Hebrew ? ToEnglish : ToHebrew;
                result.Append(map.GetValueOrDefault(c, c));
            }
        }
        return result.ToString();
    }

    private static InputLanguage PunctuationLanguage(string text, int index, InputLanguage fallback)
    {
        for (var i = index - 1; i >= 0 && !char.IsWhiteSpace(text[i]); i--)
        {
            if (IsHebrewLetter(text[i])) return InputLanguage.Hebrew;
            if (IsEnglishLetter(text[i])) return InputLanguage.English;
        }
        for (var i = index + 1; i < text.Length && !char.IsWhiteSpace(text[i]); i++)
        {
            if (IsHebrewLetter(text[i])) return InputLanguage.Hebrew;
            if (IsEnglishLetter(text[i])) return InputLanguage.English;
        }
        return fallback;
    }

    public static InputLanguage? ResultLanguage(string text)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (IsHebrewLetter(text[i])) return InputLanguage.Hebrew;
            if (IsEnglishLetter(text[i])) return InputLanguage.English;
        }
        return null;
    }

    public static string SwapCase(string text) => string.Concat(text.Select(c =>
        c is >= 'a' and <= 'z' ? char.ToUpperInvariant(c) :
        c is >= 'A' and <= 'Z' ? char.ToLowerInvariant(c) : c));

    public static string ReverseLines(string text)
    {
        var result = new StringBuilder(text.Length);
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i != text.Length && text[i] is not ('\r' or '\n')) continue;
            var line = text[start..i];
            var elements = new List<string>();
            var iterator = StringInfo.GetTextElementEnumerator(line);
            while (iterator.MoveNext()) elements.Add(iterator.GetTextElement());
            for (var j = elements.Count - 1; j >= 0; j--) result.Append(elements[j]);
            if (i < text.Length)
            {
                result.Append(text[i]);
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') result.Append(text[++i]);
            }
            start = i + 1;
        }
        return result.ToString();
    }
}
