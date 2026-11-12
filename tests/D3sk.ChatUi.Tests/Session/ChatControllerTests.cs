using System.Text.Json.Nodes;
using D3sk.ChatUi.Composer;
using D3sk.ChatUi.Session;
using D3sk.ChatUi.Spec;

namespace D3sk.ChatUi.Tests.Session;

public sealed class ChatControllerTests
{
    private sealed class Host : IChatHost
    {
        public List<ChatSendRequest> Sent { get; } = [];
        public List<ChatApprovalAnswer> Answers { get; } = [];
        public int Stops { get; private set; }
        public Exception? Refuse { get; set; }

        public Task SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Refuse is null ? Task.CompletedTask : Task.FromException(Refuse);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Stops++;
            return Task.CompletedTask;
        }

        public Task AnswerApprovalAsync(ChatApprovalAnswer answer, CancellationToken cancellationToken = default)
        {
            Answers.Add(answer);
            return Refuse is null ? Task.CompletedTask : Task.FromException(Refuse);
        }
    }

    private static ChatSendRequest Message(string text) => new(text, [new ComposerAttachment("a.png", "image/png", 10)], "claude", "claude-opus-4-7");

    [Fact]
    public async Task A_send_shows_sending_until_the_start_frame()
    {
        var session = new ChatSession(clock: () => 0);
        var host = new Host();
        var controller = new ChatController(session, host);
        Assert.True(await controller.SendAsync(Message("Hi"), cancellationToken: TestContext.Current.CancellationToken));
        var pending = session.View().Pending;
        Assert.NotNull(pending);
        Assert.Equal("Hi", pending.Text);
        Assert.Equal(["a.png"], pending.Attachments);
        session.Feed("start", new JsonObject { ["user_message"] = new JsonObject { ["id"] = "u1", ["content"] = "Hi" }, ["agent"] = "claude" });
        Assert.Null(session.View().Pending);
    }

    [Fact]
    public async Task A_refused_send_drops_the_pending_row_and_reports()
    {
        var session = new ChatSession(clock: () => 0);
        var host = new Host { Refuse = new InvalidOperationException("offline") };
        Exception? reported = null;
        Assert.False(await new ChatController(session, host).SendAsync(Message("Hi"), e => reported = e, TestContext.Current.CancellationToken));
        Assert.Null(session.View().Pending);
        Assert.Equal("offline", reported?.Message);
    }

    [Fact]
    public async Task A_send_while_a_turn_runs_is_live_input_with_no_pending_row()
    {
        var session = new ChatSession(clock: () => 0);
        session.Feed("start", new JsonObject { ["agent"] = "claude" });
        var host = new Host();
        await new ChatController(session, host).SendAsync(Message("more"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(session.View().Pending);
        Assert.Single(host.Sent);
    }

    [Fact]
    public async Task An_answered_approval_leaves_the_queue_only_when_the_host_took_it()
    {
        var session = new ChatSession(clock: () => 0);
        session.Feed("start", new JsonObject { ["agent"] = "claude" });
        session.Feed("approval_request", new JsonObject { ["approval_id"] = "a1", ["call_id"] = "t1", ["name"] = "Bash", ["arguments"] = new JsonObject { ["command"] = "ls" } });
        var host = new Host { Refuse = new InvalidOperationException("gone") };
        var controller = new ChatController(session, host);
        Assert.False(await controller.AnswerAsync(new ChatApprovalAnswer("a1", true), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(session.View().Approvals);
        host.Refuse = null;
        Assert.True(await controller.AnswerAsync(new ChatApprovalAnswer("a1", false, "no"), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(session.View().Approvals);
        await controller.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, host.Stops);
    }

    [Fact]
    public void The_attachment_limits_are_the_spec_limits()
    {
        Assert.Equal(AttachmentLimits.MaxCount, (int)ChatSpec.Limit("maxAttachments"));
        Assert.Equal(AttachmentLimits.MaxFileBytes, (long)ChatSpec.Limit("maxAttachmentBytes"));
        Assert.Equal(AttachmentLimits.MaxTotalBytes, (long)ChatSpec.Limit("maxTotalBytes"));
    }
}
