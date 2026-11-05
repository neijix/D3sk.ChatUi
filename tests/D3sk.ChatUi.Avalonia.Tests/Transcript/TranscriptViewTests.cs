using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D3sk.ChatUi.Avalonia.Tests.Composer;
using D3sk.ChatUi.Avalonia.Transcript;
using D3sk.ChatUi.Labels;

namespace D3sk.ChatUi.Avalonia.Tests.Transcript;

public class TranscriptViewTests
{
    private const int Count = 2000;
    private const double WindowHeight = 400;

    private static (TranscriptView View, Window Window) Show(int count)
    {
        var view = new TranscriptView();
        var window = new Window { Width = 600, Height = WindowHeight, Content = view };
        window.Show();
        for (var i = 0; i < count; i++)
        {
            view.Items.Add(new TranscriptItem(i % 2 == 0 ? TranscriptRole.User : TranscriptRole.Assistant, $"message {i}"));
        }
        window.UpdateLayout();
        return (view, window);
    }

    [AvaloniaFact]
    public void Only_the_visible_rows_plus_the_buffer_are_realized()
    {
        var (view, window) = Show(Count);
        var rows = view.GetVisualDescendantsOf<TranscriptRow>().Where(r => r.IsEffectivelyVisible).ToList();
        var rowHeight = rows.Average(r => r.Bounds.Height);
        var visible = (int)Math.Ceiling(WindowHeight / rowHeight);
        Assert.True(view.RealizedRowCount > 0);
        Assert.True(view.RealizedRowCount <= visible + TranscriptView.BufferRows,
            $"{view.RealizedRowCount} realized, {visible} visible, buffer {TranscriptView.BufferRows}");
        Assert.True(view.RealizedRowCount < Count / 10);
        window.Close();
    }

    [AvaloniaFact]
    public void Opens_at_the_bottom()
    {
        var (view, window) = Show(Count);
        Assert.True(view.IsAtBottom);
        Assert.True(view.DistanceFromBottom <= 1, $"{view.DistanceFromBottom}px from the bottom");
        window.Close();
    }

    [AvaloniaFact]
    public void Streaming_keeps_the_bottom_when_it_was_at_the_bottom()
    {
        var (view, window) = Show(50);
        var reply = new TranscriptItem(TranscriptRole.Assistant, "start");
        view.Items.Add(reply);
        window.UpdateLayout();
        for (var i = 0; i < 20; i++)
        {
            reply.Text += $"\n\nparagraph {i}";
            window.UpdateLayout();
        }
        Assert.True(view.IsAtBottom);
        Assert.True(view.DistanceFromBottom <= 1, $"{view.DistanceFromBottom}px from the bottom");
        window.Close();
    }

    [AvaloniaFact]
    public void Streaming_does_not_move_the_scroll_when_the_user_scrolled_up()
    {
        var (view, window) = Show(50);
        var reply = new TranscriptItem(TranscriptRole.Assistant, "start");
        view.Items.Add(reply);
        window.UpdateLayout();
        var scroller = view.GetVisualDescendantsOf<ScrollViewer>().First();
        scroller.Offset = new global::Avalonia.Vector(0, 300);
        window.UpdateLayout();
        Assert.False(view.IsAtBottom);
        var before = view.VerticalOffset;
        for (var i = 0; i < 20; i++)
        {
            reply.Text += $"\n\nparagraph {i}";
            window.UpdateLayout();
        }
        Assert.False(view.IsAtBottom);
        Assert.Equal(before, view.VerticalOffset, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Jump_to_bottom_returns_to_the_newest_message_and_follows_again()
    {
        var (view, window) = Show(Count);
        var scroller = view.GetVisualDescendantsOf<ScrollViewer>().First();
        scroller.Offset = new global::Avalonia.Vector(0, 5000);
        window.UpdateLayout();
        Assert.False(view.IsAtBottom);
        view.JumpToBottom();
        window.UpdateLayout();
        Assert.True(view.IsAtBottom);
        Assert.True(view.DistanceFromBottom <= 1, $"{view.DistanceFromBottom}px from the bottom");
        view.Items.Add(new TranscriptItem(TranscriptRole.Assistant, "newest"));
        window.UpdateLayout();
        Assert.True(view.DistanceFromBottom <= 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Agent_text_is_shown_as_text_and_tools_show_their_summary()
    {
        var view = new TranscriptView { ProjectRoot = "/work/app" };
        var window = new Window { Width = 600, Height = WindowHeight, Content = view };
        window.Show();
        view.Items.Add(new TranscriptItem(TranscriptRole.User, "<b>hi</b>"));
        view.Items.Add(new TranscriptItem(TranscriptRole.Assistant, "ok")
        {
            Tools = [new ToolPart("edit_file", "edit", System.Text.Json.Nodes.JsonNode.Parse("""{"file_path":"/work/app/src/a.js"}"""))],
            IsWorking = true,
            Activity = "editing",
        });
        window.UpdateLayout();
        var texts = view.GetVisualDescendantsOf<TextBlock>().Select(t => t.Text ?? string.Concat(t.Inlines?.Select(i => i.ToString()) ?? [])).ToList();
        var user = view.GetVisualDescendantsOf<SelectableTextBlock>().First(t => t.Text == "<b>hi</b>");
        Assert.NotNull(user);
        var tool = view.GetVisualDescendantsOf<SelectableTextBlock>().First(t => t.Inlines is { Count: 2 });
        Assert.Equal("Edit", ((global::Avalonia.Controls.Documents.Run)tool.Inlines![0]).Text);
        Assert.Contains("src/a.js", ((global::Avalonia.Controls.Documents.Run)tool.Inlines[1]).Text);
        Assert.Contains(ChatLabels.WorkingPhrase("editing", 0), texts);
        window.Close();
    }

    [AvaloniaFact]
    public void Status_pill_text_comes_from_the_labels()
    {
        var view = new TranscriptView();
        var window = new Window { Content = view };
        window.Show();
        view.SetStatus(new ChatStatusInput(Queued: true, Running: false, TerminalWorking: false, ResumeAt: null, HasError: false, Unread: false), 0);
        window.UpdateLayout();
        var text = view.GetVisualDescendantsOf<TextBlock>().First(t => t.IsVisible && t.Text == ChatLabels.ChatStatusLabel(
            new ChatStatusInput(true, false, false, null, false, false), 0)!.Text);
        Assert.NotNull(text);
        window.Close();
    }
}
