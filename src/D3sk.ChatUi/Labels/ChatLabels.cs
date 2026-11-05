using System.Globalization;

namespace D3sk.ChatUi.Labels;

/// <summary>The fields of a chat that its left-pane status pill reads.</summary>
public sealed record ChatStatusInput(
    bool Queued = false,
    bool Running = false,
    bool TerminalWorking = false,
    double? ResumeAt = null,
    bool HasError = false,
    bool Unread = false);

/// <summary>A status pill: its kind (queued, running, resuming, error, done) and its text.</summary>
public sealed record StatusPill(string Kind, string Text);

/// <summary>
/// Small wording rules of the transcript, equal to the web UI's labels.js. The working phrases
/// are the owner's Québécois list (2026-09-19): copy them exactly, add none.
/// </summary>
public static class ChatLabels
{
    /// <summary>Activity name to the two wordings its line alternates between.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> WorkingPhrases { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["starting"] = ["Patentage…", "En plein patentage…"],
            ["thinking"] = ["Zigonnage…", "Je me casse le bicycle…"],
            ["searching"] = ["Pigrassage…", "Je gratte le fond du baril…"],
            ["shell"] = ["Gossage…", "Je gosse là-dessus…"],
            ["editing"] = ["Bizounage…", "Picossage…"],
            ["writing"] = ["Pitonnage…", "Un instant, je pitonne ça…"],
            ["handling"] = ["Taponnage…", "Schleppage…"],
            ["assembling"] = ["Raboutinage…", "Je raboutine des morceaux…"],
            ["wrapping"] = ["Brochage…", "Pitonnage…"],
            ["recovering"] = ["M'm'arranger avec le trouble…", "Picossage…"],
        };

    /// <summary>Phrases for very long requests; "Je démêle les fils…" (last) leads when the agent is wired.</summary>
    public static IReadOnlyList<string> WorkingLongHaul { get; } =
    [
        "Le processeur est dans le prélart…", "Ça chauffe sous le capot…", "Je bûche sur le problème…",
        "Ça demande pas mal de jus…", "Je démêle les fils…",
    ];

    /// <summary>The plain words, for places too small for a phrase (the agent map).</summary>
    public static IReadOnlyList<string> WorkingWords { get; } =
    [
        "Patentage", "Gossage", "Zigonnage", "Bizounage", "Pitonnage",
        "Picossage", "Taponnage", "Pigrassage", "Raboutinage", "Brochage", "Schleppage",
    ];

    /// <summary>The line of a queued message.</summary>
    public const string QueuedLine = "Queued — starts on its own once the other chat in this project is done";

    /// <summary>The line between Send and the hub's start.</summary>
    public const string SendingLine = "Sending — getting the chat ready…";

    /// <summary>The line while the chat's terminal is at a reply.</summary>
    public const string TerminalWorkingLine = "Working in the terminal — its reply comes in here as it writes";

    private const int StepSeconds = 5;
    private const int LongSeconds = 120;
    private const int VeryLongSeconds = 300;
    private static readonly string[] WeekdayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <summary>
    /// The left-pane pill: null when there is nothing to show. <paramref name="zone"/> is the clock
    /// the "Resumes" time is written in (default: this computer's).
    /// </summary>
    public static StatusPill? ChatStatusLabel(ChatStatusInput? chat, double nowSeconds, TimeZoneInfo? zone = null)
    {
        if (chat is null)
        {
            return null;
        }
        if (chat.Queued)
        {
            return new StatusPill("queued", "Queued");
        }
        if (chat.Running || chat.TerminalWorking)
        {
            return new StatusPill("running", "Working");
        }
        if (chat.ResumeAt is { } resumeAt && resumeAt != 0 && !double.IsNaN(resumeAt))
        {
            var when = resumeAt > nowSeconds ? ResumeTimeText(resumeAt, nowSeconds, zone ?? TimeZoneInfo.Local) : "soon";
            return new StatusPill("resuming", $"Resumes {when}");
        }
        if (chat.HasError)
        {
            return new StatusPill("error", "Error");
        }
        return chat.Unread ? new StatusPill("done", "Done") : null;
    }

    /// <summary>"00:52" today, "Mon 00:52" on another day, 24-hour.</summary>
    public static string ResumeTimeText(double resumeAt, double nowSeconds, TimeZoneInfo zone)
    {
        var at = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(resumeAt * 1000)), zone);
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(nowSeconds * 1000)), zone);
        var clock = string.Create(CultureInfo.InvariantCulture, $"{at.Hour:00}:{at.Minute:00}");
        return at.Date == today.Date ? clock : $"{WeekdayNames[(int)at.DayOfWeek]} {clock}";
    }

    /// <summary>What the agent is doing, from the tool it just called (names as Claude Code spells them).</summary>
    public static string WorkingActivity(string? toolName)
    {
        var name = (toolName ?? "").ToLowerInvariant();
        if (StartsWithAny(name, "bash", "shell", "run_", "execute") || name is "killshell" or "bashoutput")
        {
            return "shell";
        }
        if (StartsWithAny(name, "read", "grep", "glob", "list", "ls", "view", "webfetch", "websearch", "toolsearch", "codebase_search"))
        {
            return "searching";
        }
        if (StartsWithAny(name, "edit", "multiedit", "search_replace", "str_replace", "replace", "notebookedit"))
        {
            return "editing";
        }
        if (StartsWithAny(name, "write", "create"))
        {
            return "writing";
        }
        return StartsWithAny(name, "task", "agent", "skill") ? "assembling" : "handling";
    }

    /// <summary>The line under a reply being written. Deterministic: same inputs, same words.</summary>
    public static string WorkingPhrase(string? activity = "starting", double elapsedSeconds = 0)
    {
        var pool = WorkingPhrases[activity is not null && WorkingPhrases.ContainsKey(activity) ? activity : "starting"];
        var step = (long)Math.Max(0, Math.Floor(elapsedSeconds / StepSeconds));
        var every = elapsedSeconds >= VeryLongSeconds ? 2 : elapsedSeconds >= LongSeconds ? 3 : 0;
        if (every != 0 && step % every == every - 1)
        {
            const int longFrom = (LongSeconds + StepSeconds - 1) / StepSeconds;
            const int veryLongFrom = (VeryLongSeconds + StepSeconds - 1) / StepSeconds;
            var turn = step < veryLongFrom
                ? (step - longFrom) / 3
                : ((veryLongFrom - longFrom + 2) / 3) + ((step - veryLongFrom) / 2);
            var wired = activity is "shell" or "searching";
            var last = WorkingLongHaul.Count - 1;
            var index = (int)(turn % WorkingLongHaul.Count);
            // Wired: the last phrase first, then the others in order.
            return WorkingLongHaul[wired ? (index == 0 ? last : index - 1) : index];
        }
        return pool[(int)(step % pool.Count)];
    }

    /// <summary>One plain word per chat, always the same for that chat (the agent map uses it).</summary>
    public static string WorkingWord(string? seed)
    {
        uint hash = 0;
        var text = seed ?? "";
        for (var i = 0; i < text.Length; i++)
        {
            int codePoint = text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }
            hash = unchecked((hash * 31) + (uint)codePoint);
        }
        return WorkingWords[(int)(hash % (uint)WorkingWords.Count)];
    }

    private static bool StartsWithAny(string name, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
