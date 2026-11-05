using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using D3sk.ChatUi.Composer;

namespace D3sk.ChatUi.Avalonia.Composer;

/// <summary>Thin composer view over <see cref="ComposerState"/>: text box, Architect button, inline error, drop target.</summary>
public sealed class ComposerBox : UserControl
{
    private readonly TextBox _text = new() { AcceptsReturn = true, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _error = new() { IsVisible = false };
    private readonly Button _architect = new() { Content = "Architect" };
    private readonly Button _send = new() { Content = "Send" };

    /// <summary>Creates a composer over <paramref name="state"/>.</summary>
    public ComposerBox(ComposerState state)
    {
        State = state;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _architect, _send } };
        Content = new StackPanel { Spacing = 4, Children = { _text, _error, buttons } };
        DragDrop.SetAllowDrop(this, true);
        _text.Height = state.BoxHeight;
        _text.Text = state.Text;
        _text.TextChanged += (_, _) => State.Text = _text.Text ?? string.Empty;
        _architect.Click += (_, _) =>
        {
            State.PrefixArchitect();
            Refresh();
        };
        _send.Click += (_, _) => SendRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user asks to send; the host reads <see cref="ComposerState.Take"/>.</summary>
    public event EventHandler? SendRequested;

    /// <summary>The rules this view shows.</summary>
    public ComposerState State { get; }

    /// <summary>Tells the box the state changed outside it (thread switch, busy flip, refused file).</summary>
    public void Refresh()
    {
        _text.Text = State.Text;
        _text.CaretIndex = State.Text.Length;
        _text.Height = State.BoxHeight;
        _error.Text = State.Error;
        _error.IsVisible = State.Error is not null;
        _send.IsEnabled = State.CanSend;
    }

    /// <summary>Resizes the box (the grip or a keyboard step); the state clamps it.</summary>
    public void ResizeBox(double height)
    {
        State.BoxHeight = height;
        _text.Height = State.BoxHeight;
    }
}
