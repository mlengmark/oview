using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OView.App;

namespace OView.Linux.Presentation;

/// <summary>The window-independent visual tree a <see cref="BrandedDialogWindow"/> shows, and a
/// <see cref="BrandedDialogRenderProof"/> renders offscreen — see <c>O-view.Tray</c>'s own
/// identically named type for the reasoning (independently implemented here, not shared, D1).</summary>
internal sealed class BrandedDialogVisual
{
    public required Border Root { get; init; }
    public required TextBlock Title { get; init; }
    public required TextBlock Message { get; init; }
    public required TextBlock Detail { get; init; }
    public required Border CancelButton { get; init; }
    public required TextBlock CancelLabel { get; init; }
    public required Border ConfirmButton { get; init; }
    public required TextBlock ConfirmLabel { get; init; }
}

/// <summary>
/// Builds the generic confirm/cancel dialog's content (gate G7 parity slice P24, OVI-718, source
/// <c>ui-spec.md</c> §5), the Linux counterpart of <c>O-view.Tray</c>'s own
/// <c>BrandedDialogLayout</c> — independently implemented, not shared (D1). A pure builder with
/// no window attached, so <see cref="BrandedDialogWindow"/> and <see cref="BrandedDialogRenderProof"/>
/// share one copy of the layout.
/// </summary>
internal static class BrandedDialogLayout
{
    public static BrandedDialogVisual Build(LinuxWindowThemeColors colors, string confirmLabel, string cancelLabel)
    {
        var fg = BrandedDialogChrome.ToBrush(colors.Foreground);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(BrandedDialogChrome.BuildBrandMark(colors.Accent));
        var title = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = fg,
        };
        titleRow.Children.Add(title);

        var message = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(25, 10, 0, 0),
            Foreground = fg,
        };

        var detail = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(25, 7, 0, 0),
            IsVisible = false,
            Foreground = fg,
        };

        var cancelButton = BrandedDialogChrome.BuildSecondaryAction(cancelLabel, colors.Foreground, colors.Border, out var cancelLabelBlock);
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        var confirmButton = BrandedDialogChrome.BuildPrimaryAction(confirmLabel, colors.Accent, colors.AccentHover, out var confirmLabelBlock);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(confirmButton);

        var stack = new StackPanel();
        stack.Children.Add(titleRow);
        stack.Children.Add(message);
        stack.Children.Add(detail);
        stack.Children.Add(buttons);

        var root = new Border
        {
            Background = BrandedDialogChrome.ToBrush(colors.Background),
            BorderBrush = BrandedDialogChrome.ToBrush(colors.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(BrandedDialogChrome.CardCornerRadius),
            Padding = new Thickness(18, 16, 18, 16),
            Width = 420,
            Child = stack,
        };

        return new BrandedDialogVisual
        {
            Root = root,
            Title = title,
            Message = message,
            Detail = detail,
            CancelButton = cancelButton,
            CancelLabel = cancelLabelBlock,
            ConfirmButton = confirmButton,
            ConfirmLabel = confirmLabelBlock,
        };
    }
}

/// <summary>
/// The app's own confirm/cancel dialog, replacing <c>MessageBox</c> (ADR-0008 slicing-table
/// slice P24, OVI-718, source <c>ui-spec.md</c> §5), the Linux counterpart of
/// <c>O-view.Tray</c>'s own <c>BrandedDialogWindow</c> — independently implemented, not shared
/// (D1). Windows' own <c>MessageBox</c> never existed on this platform in the first place, so
/// there is no system dialog here to look like — this dialog exists so a future action on this
/// skin gets the same branded chrome and the same "primary names the action" rule as the Windows
/// skin, not because this skin is replacing anything of its own.
///
/// <para><b>No live call site exists in this skin to wire up yet</b> (same gap as the Windows
/// skin: the menu action rows that would call this, P21-P23, are not built). Infrastructure
/// ahead of its consumer, same as <c>O-view.Tray</c>'s own copy.</para>
///
/// <para>Not unit-tested itself (no interactive Linux display in this environment, the same
/// boundary <c>DetailWindow</c> already records) — key routing is
/// <see cref="BrandedDialogKeyRouter"/>, proven with no window, and the content is
/// <see cref="BrandedDialogLayout.Build"/>'s output, proven by <see cref="BrandedDialogRenderProof"/>.</para>
///
/// <para>Activation mirrors this skin's existing <c>DetailWindow.SetVisible</c> exactly:
/// <see cref="Window.Show"/> then <see cref="Window.Activate"/>, with no extra platform
/// mechanism — on Linux, activation is only ever a request a compositor is free to refuse
/// (<c>DetailWindow</c>'s own remarks), and this skin builds no Win32-style fallback anywhere
/// else to borrow.</para>
/// </summary>
internal sealed class BrandedDialogWindow : Window
{
    private readonly BrandedDialogVisual _visual;
    private bool _confirmed;

    public BrandedDialogWindow(LinuxWindowThemeColors colors, string confirmLabel, string cancelLabel)
    {
        _visual = BrandedDialogLayout.Build(colors, confirmLabel, cancelLabel);

        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = Brushes.Transparent;
        Content = _visual.Root;

        _visual.CancelButton.PointerPressed += OnCancelPressed;
        _visual.ConfirmButton.PointerPressed += OnConfirmPressed;
        KeyDown += OnKeyDown;
    }

    /// <summary>The primary action's exact label (gate G7 parity slice P24's "primary names the
    /// action" requirement) — exposed for the render-proof fixture, same as the Windows skin's
    /// copy.</summary>
    public string ConfirmLabelText => _visual.ConfirmLabel.Text ?? "";

    private void OnCancelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        Finish(confirmed: false);
    }

    private void OnConfirmPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        Finish(confirmed: true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (BrandedDialogKeyRouter.Resolve(e.Key))
        {
            case BrandedDialogKeyAction.Cancel:
                Finish(confirmed: false);
                break;
            case BrandedDialogKeyAction.Confirm:
                Finish(confirmed: true);
                break;
        }
    }

    private void Finish(bool confirmed)
    {
        _confirmed = confirmed;
        Close(confirmed);
    }

    public void Populate(string title, string message, string detail = "")
    {
        _visual.Title.Text = title;
        _visual.Message.Text = message;
        _visual.Detail.Text = detail;
        _visual.Detail.IsVisible = detail.Length > 0;
    }

    /// <summary>
    /// Fills and shows the dialog modally, returning <see langword="true"/> if the primary
    /// action was taken. Avalonia's <c>ShowDialog</c> is awaitable rather than blocking, unlike
    /// WPF's — the one real difference from the Windows skin's own <c>Show</c>, forced by the
    /// toolkit rather than chosen.
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> ShowAsync(
        Window owner, string title, string message, string confirmLabel, string cancelLabel, ThemePreference theme, string detail = "")
    {
        var dialog = new BrandedDialogWindow(LinuxWindowThemePalette.Resolve(theme), confirmLabel, cancelLabel);
        dialog.Populate(title, message, detail);

        dialog.Opened += (_, _) => dialog.Activate();

        await dialog.ShowDialog(owner);
        return dialog._confirmed;
    }
}
