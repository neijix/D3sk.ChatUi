using D3sk.ChatUi.Composer;

namespace D3sk.ChatUi.Tests.Composer;

public class ComposerStateTests
{
    private const long Mb = 1024 * 1024;

    private static ComposerAttachment File(string name, long size) => new(name, "text/plain", size);

    [Fact]
    public void Eleventh_file_is_refused_with_an_inline_error()
    {
        var c = new ComposerState();
        for (var i = 0; i < 10; i++)
        {
            Assert.True(c.TryAddAttachment(File($"f{i}", 1)));
        }

        Assert.False(c.TryAddAttachment(File("extra", 1)));
        Assert.Equal(10, c.Attachments.Count);
        Assert.NotNull(c.Error);
    }

    [Fact]
    public void File_over_ten_MB_is_refused_and_exactly_ten_is_kept()
    {
        var c = new ComposerState();
        Assert.False(c.TryAddAttachment(File("big", (10 * Mb) + 1)));
        Assert.True(c.TryAddAttachment(File("edge", 10 * Mb)));
    }

    [Fact]
    public void Total_over_25_MB_is_refused()
    {
        var c = new ComposerState();
        Assert.True(c.TryAddAttachment(File("a", 10 * Mb)));
        Assert.True(c.TryAddAttachment(File("b", 10 * Mb)));
        Assert.False(c.TryAddAttachment(File("c", (5 * Mb) + 1)));
        Assert.True(c.TryAddAttachment(File("d", 5 * Mb)));
        Assert.Equal(3, c.Attachments.Count);
    }

    [Fact]
    public void A_good_add_clears_the_error()
    {
        var c = new ComposerState();
        c.TryAddAttachment(File("big", 11 * Mb));
        c.TryAddAttachment(File("ok", 1));
        Assert.Null(c.Error);
    }

    [Fact]
    public void Architect_prefixes_once()
    {
        var c = new ComposerState { Text = "add a login" };
        c.PrefixArchitect();
        c.PrefixArchitect();
        Assert.Equal("/architect add a login", c.Text);
        c.Text = string.Empty;
        c.PrefixArchitect();
        Assert.Equal("/architect ", c.Text);
        c.PrefixArchitect();
        Assert.Equal("/architect ", c.Text);
    }

    [Fact]
    public void Drafts_follow_the_thread()
    {
        var drafts = new ComposerDrafts();
        var c = new ComposerState { Text = "half typed" };
        c.TryAddAttachment(File("a", 1));
        drafts.Switch("one", "two", c);
        Assert.Equal(string.Empty, c.Text);
        Assert.Empty(c.Attachments);
        c.Text = "other";
        drafts.Switch("two", "one", c);
        Assert.Equal("half typed", c.Text);
        Assert.Single(c.Attachments);
        drafts.Switch("one", "two", c);
        Assert.Equal("other", c.Text);
    }

    [Fact]
    public void Empty_draft_is_not_kept_and_send_empties_the_composer()
    {
        var drafts = new ComposerDrafts();
        var c = new ComposerState { Text = "x" };
        drafts.Save("one", c);
        Assert.True(drafts.HasOther(null));
        var sent = c.Take();
        Assert.Equal("x", sent.Text);
        Assert.False(c.CanSend);
        drafts.Save("one", c);
        Assert.False(drafts.HasOther(null));
    }

    [Fact]
    public void Box_height_is_clamped_and_busy_still_accepts_text()
    {
        var c = new ComposerState { BoxHeight = 5000, Busy = true, Text = "more" };
        Assert.Equal(ComposerState.MaxBoxHeight, c.BoxHeight);
        c.BoxHeight = 1;
        Assert.Equal(ComposerState.MinBoxHeight, c.BoxHeight);
        Assert.True(c.CanSend);
    }
}
