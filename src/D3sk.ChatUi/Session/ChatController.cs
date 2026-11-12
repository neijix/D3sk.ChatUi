using System.Text.Json.Nodes;
using D3sk.ChatUi.Composer;

namespace D3sk.ChatUi.Session;

/// <summary>What Send hands the host: the words, the files and the box's choices.</summary>
public sealed record ChatSendRequest(
    string Text,
    IReadOnlyList<ComposerAttachment> Files,
    string Agent,
    string Model,
    string? Permission = null,
    string? Effort = null);

/// <summary>An approval answered: Allow / Deny with an optional message, or an AskUserQuestion's answers.</summary>
public sealed record ChatApprovalAnswer(string ApprovalId, bool Allow, string? Message = null, JsonObject? Answers = null);

/// <summary>
/// The application behind a chat: it runs the agents and feeds the session the turn's frames. The
/// web package's mountChat takes the same four callbacks (onSend, onStop, onApproval, onToolFix).
/// </summary>
public interface IChatHost
{
    /// <summary>Sends a message (live input while a turn runs). Throw to give the words back.</summary>
    Task SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stops the running turn.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Answers an approval. Throw to keep it pending.</summary>
    Task AnswerApprovalAsync(ChatApprovalAnswer answer, CancellationToken cancellationToken = default);

    /// <summary>True when the host offers Fix on failed tools.</summary>
    bool CanFixTools => false;

    /// <summary>Opens a chat that fixes why a tool call failed.</summary>
    Task FixToolAsync(string callId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// The host-facing rules both front-ends share: a send shows "Sending…" until the turn starts and
/// goes away when the host refuses it; an answered approval leaves the queue once the host took it.
/// </summary>
public sealed class ChatController(ChatSession session, IChatHost host)
{
    /// <summary>The session the view draws.</summary>
    public ChatSession Session { get; } = session;

    /// <summary>The application behind the chat.</summary>
    public IChatHost Host { get; } = host;

    /// <summary>Sends; false when the host refused (its error goes to <paramref name="onError"/>).</summary>
    public async Task<bool> SendAsync(ChatSendRequest request, Action<Exception>? onError = null, CancellationToken cancellationToken = default)
    {
        var isLive = Session.Busy;
        if (!isLive)
        {
            Session.MarkSending(request.Text, request.Files.Select(f => f.Name), request.Agent, request.Model);
        }
        try
        {
            await Host.SendAsync(request, cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (!isLive)
            {
                Session.CancelSending();
            }
            onError?.Invoke(error);
            return false;
        }
    }

    /// <summary>Stops the running turn.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default) => Host.StopAsync(cancellationToken);

    /// <summary>Answers an approval; true when the host took it (it then leaves the queue).</summary>
    public async Task<bool> AnswerAsync(ChatApprovalAnswer answer, Action<Exception>? onError = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await Host.AnswerApprovalAsync(answer, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            onError?.Invoke(error);
            return false;
        }
        Session.DismissApproval(answer.ApprovalId);
        return true;
    }
}
