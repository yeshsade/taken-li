using TakenLi.Core;
using System.Text.Json;

var checks = new (string Name, Action Check)[]
{
    ("Per-app layout restoration survives automatic Hebrew", () => {
        var p = new ApplicationLayouts();
        Equal<long?>(null, p.Observe("ChatGPT.exe", 1, 9, 0));
        p.AutomaticReturn(13, 1000);
        p.Observe("ChatGPT.exe", 1, 9, 1250); // old layout before Windows processes request
        p.Observe("ChatGPT.exe", 1, 13, 1500);
        p.Observe("eclipse.exe", 2, 13, 31000);
        Equal<long?>(9, p.Observe("chatgpt.EXE", 3, 13, 32000));
        p.Observe("ChatGPT.exe", 3, 13, 32250); // asynchronous request still pending
        p.Observe("ChatGPT.exe", 3, 9, 32500);
        p.Observe("WhatsApp.exe", 4, 13, 33000);
        Equal<long?>(9, p.Observe("ChatGPT.exe", 1, 13, 34000));
    }),
    ("Manual layout and explicit correction replace memory", () => {
        var p = new ApplicationLayouts();
        p.Observe("a.exe", 1, 9, 0);
        p.AutomaticReturn(13, 1000);
        p.Observe("a.exe", 1, 13, 1000);
        p.ExplicitChange("a.exe", 1, 13, 1100);
        p.Observe("a.exe", 1, 13, 1200);
        p.Observe(null, 0, 0, 1300);
        Equal<long?>(13, p.Observe("a.exe", 1, 9, 1400));
        p.Observe("a.exe", 1, 13, 1500);
        p.Observe("a.exe", 1, 9, 1600);
        p.Observe("b.exe", 2, 13, 1700);
        Equal<long?>(9, p.Observe("a.exe", 1, 13, 1800));
    }),
    ("Unavailable restoration times out without repeated requests", () => {
        var p = new ApplicationLayouts();
        p.Observe("a.exe", 1, 9, 0);
        p.Observe(null, 0, 0, 1);
        Equal<long?>(9, p.Observe("a.exe", 1, 13, 100));
        Equal<long?>(null, p.Observe("a.exe", 1, 13, 2100));
        p.Observe(null, 0, 0, 2200);
        Equal<long?>(9, p.Observe("a.exe", 1, 13, 2300));
        p.RequestRejected();
        Equal<long?>(null, p.Observe("a.exe", 1, 13, 2500));
        Equal(9L, p.Remembered["a.exe"]);
    }),
    ("Actual typing adopts automatic Hebrew, even before the next poll", () => {
        var writes = 0;
        var p = new ApplicationLayouts(changed: () => writes++);
        p.Observe("ChatGPT.exe", 1, 9, 0);
        p.AutomaticReturn(13, 1000);
        p.Typing("ChatGPT.exe", 1, 9, 1050);
        Equal(9L, p.Remembered["ChatGPT.exe"]);
        p.Typing("WhatsApp.exe", 2, 13, 1100);
        Equal(false, p.Remembered.ContainsKey("WhatsApp.exe"));
        p.Typing("ChatGPT.exe", 1, 13, 1150);
        p.Typing("chatgpt.exe", 1, 13, 1200);
        Equal(13L, p.Remembered["CHATGPT.EXE"]);
        Equal(2, writes);
        p.Observe("Eclipse.exe", 2, 9, 1300);
        Equal<long?>(13, p.Observe("ChatGPT.exe", 1, 9, 1500));
    }),
    ("Manual switch overtakes a pending restore and survives restart", () => {
        var previous = new ApplicationLayouts();
        previous.Observe("chatgpt.exe", 1, 9, 0);
        previous.Observe("eclipse.exe", 2, 13, 1000);
        var next = new ApplicationLayouts(previous.Remembered);
        Equal<long?>(9, next.Observe("ChatGPT.exe", 3, 13, 2000));
        next.Observe("ChatGPT.exe", 3, 42, 2100);
        Equal(42L, next.Remembered["chatgpt.exe"]);
        Equal(13L, next.Remembered["eclipse.exe"]);
    }),
    ("A late automatic return still leaves the remembered choice intact", () => {
        var p = new ApplicationLayouts();
        p.Observe("a.exe", 1, 9, 0);
        p.AutomaticReturn(13, 1000);
        p.Observe("a.exe", 1, 9, 3100);
        p.Observe("a.exe", 1, 13, 3200);
        Equal(9L, p.Remembered["a.exe"]);
        p.Observe("a.exe", 1, 9, 3300);
        p.Observe("a.exe", 1, 13, 3400);
        Equal(13L, p.Remembered["a.exe"]);
    }),
    ("English to Hebrew", () => Equal("שלום", TextOperations.FixLayout("akuo"))),
    ("Hebrew to English", () => Equal("akuo", TextOperations.FixLayout("שלום"))),
    ("Mixed languages flip independently", () => Equal("akuo שלום", TextOperations.FixLayout("שלום akuo"))),
    ("All Hebrew letters round trip", () => { const string source = "אבגדהוזחטיכךלמםנןסעפףצץקרשת"; Equal(source, TextOperations.FixLayout(TextOperations.FixLayout(source))); }),
    ("Case swaps each letter", () => Equal("hELLO wORLD 123 שלום", TextOperations.SwapCase("Hello World 123 שלום"))),
    ("Double case swap restores input", () => Equal("Hello שלום 42!", TextOperations.SwapCase(TextOperations.SwapCase("Hello שלום 42!")))),
    ("Reverse each line, preserving CRLF", () => Equal("םולש\r\nםלוע\n\n", TextOperations.ReverseLines("שלום\r\nעולם\n\n"))),
    ("Reverse handles lone CR", () => Equal("ba\rdc", TextOperations.ReverseLines("ab\rcd"))),
    ("Emoji and combining marks are preserved", () => Equal("👨‍👩‍👧‍👦 שָ", TextOperations.ReverseLines("שָ 👨‍👩‍👧‍👦"))),
    ("English punctuation follows keyboard positions", () => Equal("יקךךםץ", TextOperations.FixLayout("hello."))),
    ("Hebrew punctuation follows keyboard positions", () => Equal("akuo/", TextOperations.FixLayout("שלום."))),
    ("Punctuation-only conversion uses active language", () => { Equal(".", TextOperations.FixLayout("/", InputLanguage.English)); Equal("q", TextOperations.FixLayout("/", InputLanguage.Hebrew)); }),
    ("Numbers and whitespace remain intact", () => Equal("123\t \r\n", TextOperations.FixLayout("123\t \r\n"))),
    ("Unknown symbols remain intact", () => Equal("🙂€", TextOperations.FixLayout("🙂€"))),
    ("Last result letter selects input language", () => { Equal(InputLanguage.English, TextOperations.ResultLanguage("שלום hello 123!")); Equal(InputLanguage.Hebrew, TextOperations.ResultLanguage("hello שלום.")); Equal<InputLanguage?>(null, TextOperations.ResultLanguage("42!")); }),
    ("Empty input", () => { foreach (var action in Enum.GetValues<TextAction>()) Equal("", TextOperations.Apply("", action)); }),
    ("Default settings and hotkey labels", () => { var s = new AppSettings(); s.Validate(); Equal("F10", s.FixLayoutKey.Display); Equal("Shift+F10", s.SwapCaseKey.Display); Equal("F6", s.ReverseKey.Display); Equal(true, s.AutoReturnEnabled); Equal(30, s.AutoReturnSeconds); }),
    ("Independent draft settings", () => { var original = new AppSettings(); var draft = original.Copy(); draft.ExcludedApplications.Add("notepad.exe"); draft.Language = "en"; Equal(0, original.ExcludedApplications.Count); Equal("he", original.Language); }),
    ("Caps Lock preference defaults on for existing settings and persists when disabled", () => { Equal(true, JsonSerializer.Deserialize<AppSettings>("{\"Language\":\"he\"}")!.DisableCapsLockOnHebrew); var original = new AppSettings { DisableCapsLockOnHebrew = false }; Equal(false, original.Copy().DisableCapsLockOnHebrew); }),
    ("Conflicting hotkeys are rejected", () => { var s = new AppSettings { ReverseKey = new(121) }; Throws(s.Validate); }),
    ("Unsafe unmodified typing keys are rejected", () => { var s = new AppSettings { ReverseKey = new(65) }; Throws(s.Validate); }),
    ("Invalid delay is rejected", () => { var s = new AppSettings { AutoReturnSeconds = 0 }; Throws(s.Validate); }),
    ("Idle timer restarts while typing", () => { var p = new IdleReturnPolicy(); Equal(false, p.ShouldReturn(1, true, 0, 30)); p.Typed(20000); Equal(false, p.ShouldReturn(1, true, 30000, 30)); Equal(false, p.ShouldReturn(1, true, 49999, 30)); Equal(true, p.ShouldReturn(1, true, 50000, 30)); Equal(false, p.ShouldReturn(1, true, 60000, 30)); }),
    ("Window changes start a fresh timer", () => { var p = new IdleReturnPolicy(); p.ShouldReturn(1, true, 0, 30); Equal(false, p.ShouldReturn(2, true, 29000, 30)); Equal(false, p.ShouldReturn(2, true, 30000, 30)); Equal(true, p.ShouldReturn(2, true, 59000, 30)); }),
    ("Non-English input never requests a return", () => { var p = new IdleReturnPolicy(); Equal(false, p.ShouldReturn(1, false, 0, 30)); Equal(false, p.ShouldReturn(1, false, 100000, 30)); }),
    ("Disabling or pausing resets timer", () => { var p = new IdleReturnPolicy(); p.ShouldReturn(1, true, 0, 30); p.Reset(29000); Equal(false, p.ShouldReturn(1, true, 30000, 30)); Equal(true, p.ShouldReturn(1, true, 60000, 30)); }),
    ("Typing after a return request starts a new wait", () => { var p = new IdleReturnPolicy(); p.ShouldReturn(1, true, 0, 30); Equal(true, p.ShouldReturn(1, true, 30000, 30)); p.Typed(40000); Equal(false, p.ShouldReturn(1, true, 69999, 30)); Equal(true, p.ShouldReturn(1, true, 70000, 30)); })
};

var failures = 0;
foreach (var (name, check) in checks)
{
    try { check(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
}
Console.WriteLine($"{checks.Length - failures}/{checks.Length} checks passed.");
return failures == 0 ? 0 : 1;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'.");
}
static void Throws(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid settings were accepted.");
}
