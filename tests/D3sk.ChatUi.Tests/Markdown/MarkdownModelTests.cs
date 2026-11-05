using D3sk.ChatUi.Markdown;

namespace D3sk.ChatUi.Tests.Markdown;

public class MarkdownModelTests
{
    [Fact]
    public void Raw_html_never_becomes_a_node_and_is_escaped()
    {
        var doc = MarkdownParser.Parse("<script>x</script>");
        var p = Assert.IsType<Paragraph>(Assert.Single(doc.Blocks));
        Assert.All(p.Content, i => Assert.IsType<TextRun>(i));
        Assert.Equal("<p>&lt;script&gt;x&lt;/script&gt;</p>", HtmlWriter.Write(doc));
    }

    [Fact]
    public void Disallowed_link_target_keeps_only_the_label_in_the_model()
    {
        var doc = MarkdownParser.Parse("[x](javascript:alert(1))");
        var p = Assert.IsType<Paragraph>(Assert.Single(doc.Blocks));
        Assert.DoesNotContain(p.Content, i => i is Link);
    }

    [Fact]
    public void Writer_escapes_every_string_in_a_hand_built_model()
    {
        var doc = new MarkdownDocument([
            new CodeBlock("c\"><x", "<b>"),
            new Paragraph([new Link(LinkKind.External, "https://a/\"x", [new TextRun("<i>")])]),
        ]);
        var html = HtmlWriter.Write(doc);
        Assert.Equal(
            "<pre><code class=\"language-c&quot;&gt;&lt;x\">&lt;b&gt;</code></pre>" +
            "<p><a href=\"https://a/&quot;x\" target=\"_blank\" rel=\"noopener noreferrer\">&lt;i&gt;</a></p>",
            html);
    }

    [Fact]
    public void Bold_and_italic_nest_and_cross_links()
    {
        Assert.Equal(
            "<p><em>a <strong>b</strong></em> <strong><a href=\"https://x.io\" target=\"_blank\" rel=\"noopener noreferrer\">l</a></strong></p>",
            HtmlWriter.Render("*a **b*** **[l](https://x.io)**"));
    }

    [Fact]
    public void Reserved_private_use_marks_in_input_do_not_break_parsing()
    {
        Assert.Equal("<p>a�b</p>", HtmlWriter.Render("ab"));
    }
}
