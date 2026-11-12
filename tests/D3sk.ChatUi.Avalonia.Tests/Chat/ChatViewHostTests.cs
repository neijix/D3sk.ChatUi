using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using D3sk.ChatUi.Avalonia.Chat;
using D3sk.ChatUi.Session;

namespace D3sk.ChatUi.Avalonia.Tests.Chat;

/// <summary>The host API, as the web package's dom.test.js checks it: send, refusal, stop, approvals, questions.</summary>
public sealed class ChatViewHostTests
{
    private sealed class Host : IChatHost
    {
        public List<ChatSendRequest> Sent { get; } = [];
        public List<ChatApprovalAnswer> Answers { get; } = [];
        public int Stops { get; private set; }
        public bool Refuse { get; set; }

        public Task SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Refuse ? Task.FromException(new InvalidOperationException("offline")) : Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Stops++;
            return Task.CompletedTask;
        }

        public Task AnswerApprovalAsync(ChatApprovalAnswer answer, CancellationToken cancellationToken = default)
        {
            Answers.Add(answer);
            return Task.CompletedTask;
        }
    }

    private static readonly IReadOnlyList<ChatAgent> Agents =
        [new ChatAgent("claude", "Claude Code", [new ChatModel("claude-opus-4-7", "Opus 4.7")])];

    private static (ChatView View, Host Host, Window Window) Show()
    {
        var host = new Host();
        var view = new ChatView(host, Agents, clock: () => 0);
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        return (view, host, window);
    }

    private static JsonObject Obj(string json) => JsonNode.Parse(json)!.AsObject();

    [AvaloniaFact]
    public async Task Send_shows_the_message_at_once_with_the_picked_agent_then_the_start_replaces_it()
    {
        var (view, host, window) = Show();
        view.Composer.State.Text = "Hello there";
        Assert.True(await view.SendAsync());
        Assert.Equal("Hello there", host.Sent.Single().Text);
        Assert.Equal("claude", host.Sent[0].Agent);
        Assert.Equal("claude-opus-4-7", host.Sent[0].Model);
        Assert.Equal("acceptEdits", host.Sent[0].Permission);
        Assert.NotNull(view.Transcript.Items.Last().Pending);
        view.Feed("start", Obj("""{"user_message":{"id":"u1","content":"Hello there"},"agent":"claude","model":"claude-opus-4-7"}"""));
        view.Render();
        Assert.All(view.Transcript.Items, item => Assert.Null(item.Pending));
        Assert.IsType<ChatUserRow>(view.Transcript.Items[0].Row);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_refused_send_gives_the_words_back_and_says_why()
    {
        var (view, host, window) = Show();
        host.Refuse = true;
        string? error = null;
        view.ErrorReported += (_, message) => error = message;
        view.Composer.State.Text = "Will fail";
        Assert.False(await view.SendAsync());
        Assert.Equal("Will fail", view.Composer.State.Text);
        Assert.Equal("offline", error);
        Assert.Empty(view.Transcript.Items);
        window.Close();
    }

    [AvaloniaFact]
    public void Stop_shows_while_a_turn_runs_and_calls_the_host()
    {
        var (view, host, window) = Show();
        Assert.False(view.Composer.StopButton.IsVisible);
        view.Feed("start", Obj("""{"agent":"claude"}"""));
        view.Render();
        Assert.True(view.Composer.StopButton.IsVisible);
        view.Composer.StopButton.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, host.Stops);
        view.Feed("done", Obj("""{"returncode":0}"""));
        view.Render();
        Assert.False(view.Composer.StopButton.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Allow_answers_the_host_and_Later_folds_the_dialog_into_its_banner()
    {
        var (view, host, window) = Show();
        view.Feed("start", Obj("""{"agent":"claude"}"""));
        view.Feed("tool_call", Obj("""{"call_id":"t1","name":"Bash","arguments":{"command":"make"}}"""));
        view.Feed("approval_request", Obj("""{"approval_id":"a1","call_id":"t1","name":"Bash","arguments":{"command":"make"}}"""));
        view.Render();
        var parts = view.Approvals.Parts;
        Assert.True(view.Approvals.IsOpen);
        Assert.Equal("Claude Code wants to use Bash", parts.Title.Text);
        parts.Later.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(view.Approvals.IsOpen);
        Assert.True(parts.Banner.IsVisible);
        parts.Banner.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(view.Approvals.IsOpen);
        parts.Message.Text = "go ahead";
        parts.Allow.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new ChatApprovalAnswer("a1", true, "go ahead"), host.Answers.Single());
        view.Render();
        Assert.False(view.Approvals.IsOpen);
        window.Close();
    }

    [AvaloniaFact]
    public void A_question_is_answered_with_the_picked_options_and_the_own_words()
    {
        var (view, host, window) = Show();
        view.Feed("start", Obj("""{"agent":"claude"}"""));
        view.Feed("approval_request", Obj("""
            {"approval_id":"q","call_id":"c","name":"AskUserQuestion","arguments":{"questions":[
              {"question":"Which database?","multiSelect":false,"options":[{"label":"SQLite"},{"label":"Postgres"}]},
              {"question":"Extras?","multiSelect":true,"options":[{"label":"Auth"},{"label":"Billing"}]}]}}
            """));
        view.Render();
        var parts = view.Approvals.Parts;
        Assert.False(parts.Send.IsEnabled);
        var blocks = parts.Detail.Children.ToList();
        Button Option(int block, int index) => blocks[block].GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("question-option")).ElementAt(index);
        void Click(Button button) => button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Click(Option(0, 1));
        Click(Option(1, 0));
        Click(blocks[1].GetLogicalDescendants().OfType<Button>().Single(b => b.Classes.Contains("question-other")));
        blocks[1].GetLogicalDescendants().OfType<TextBox>().Single(t => t.Classes.Contains("question-own")).Text = "Search";
        Assert.True(parts.Send.IsEnabled);
        Click(parts.Send);
        var answer = host.Answers.Single();
        Assert.Equal("""{"Which database?":"Postgres","Extras?":["Auth","Search"]}""", answer.Answers!.ToJsonString());
        Assert.Equal("q", answer.ApprovalId);
        Assert.True(JsonNode.DeepEquals(Obj("""{"Which database?":"Postgres","Extras?":["Auth","Search"]}"""), answer.Answers));
        window.Close();
    }

    [AvaloniaFact]
    public void Palettes_switch_from_the_spec_tokens()
    {
        var (view, _, window) = Show();
        view.SetPalette("dusk");
        Assert.Equal("#ff1c1614", ((global::Avalonia.Media.SolidColorBrush)view.Resources["Chat.Bg"]!).Color.ToString());
        window.Close();
    }
}
