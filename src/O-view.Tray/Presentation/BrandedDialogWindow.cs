using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OView.App;
using OView.Tray.Platform;

namespace OView.Tray.Presentation;

/// <summary>
/// The window-independent visual tree a <see cref="BrandedDialogWindow"/> shows, and a
/// <see cref="BrandedDialogRenderProof"/> renders offscreen — split out so the render proof never
/// has to construct a real <see cref="Window"/> (gate G7 parity slice P24, OVI-718).
/// </summary>
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
/// <c>ui-spec.md</c> §5): the brand mark beside a title, a message, an optional muted detail
/// line, and the two weighted actions. A pure builder with no window attached, so
/// <see cref="BrandedDialogWindow"/> and <see cref="BrandedDialogRenderProof"/> share exactly one
/// copy of the layout rather than the render proof hand-duplicating it (unlike
/// <see cref="DetailWindowRenderProof"/>, which duplicates <c>DetailWindow.BuildLayout</c>
/// because that method is a private instance member bound to that window's own fields — this
/// builder takes no such dependency, so there is nothing to duplicate).
/// </summary>
internal static class BrandedDialogLayout
{
    public static BrandedDialogVisual Build(WindowThemeColors colors, string confirmLabel, string cancelLabel)
    {
        var titleRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        titleRow.Children.Add(BrandedDialogChrome.BuildBrandMark(colors.Accent));
        var title = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = BrandedDialogChrome.ToBrush(colors.Foreground),
        };
        titleRow.Children.Add(title);

        var message = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(25, 10, 0, 0),
            Foreground = BrandedDialogChrome.ToBrush(colors.Foreground),
        };

        var detail = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(25, 7, 0, 0),
            Visibility = Visibility.Collapsed,
            Foreground = BrandedDialogChrome.ToBrush(colors.Foreground),
        };

        var cancelButton = BrandedDialogChrome.BuildSecondaryAction(cancelLabel, colors.Foreground, colors.Border, out var cancelLabelBlock);
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        var confirmButton = BrandedDialogChrome.BuildPrimaryAction(confirmLabel, colors.Accent, colors.AccentHover, out var confirmLabelBlock);

        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
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
/// The app's own confirm/cancel dialog, replacing <c>MessageBox</c> (ADR-0008 slicing-table slice
/// P24, OVI-718, source <c>ui-spec.md</c> §5): "the one surface that gave the app away — raw
/// Win32 chrome, a stock blue 'i' glyph, Yes/No buttons, and no room for a mark". Built in plain
/// C#, like every other window in this project (see <see cref="OView.Tray.DetailWindow"/>'s own
/// remarks) — this project has no markup compiler and a handful of bound elements do not need
/// one.
///
/// <para><b>No live <c>MessageBox</c> call site exists in this skin to replace yet</b> (CONFIRMED
/// by grep: zero hits for <c>MessageBox</c> under <c>src/</c>). The Windows tray menu items that
/// would eventually show one — slicing-table rows P21-P23 — are not built. This dialog is
/// therefore infrastructure ahead of its consumer, the same pattern P5/P6 already used for the
/// Core-to-skin contract (contract-first, consumer later); a future slice wires a menu action to
/// <see cref="Show"/>.</para>
///
/// <para>Not unit-tested itself (no interactive Windows desktop in this environment, the same
/// boundary <see cref="OView.Tray.DetailWindow"/> already records) — the key-handling decision is
/// pulled out into <see cref="BrandedDialogKeyRouter"/>, proven against no window at all, and the
/// content is <see cref="BrandedDialogLayout.Build"/>'s output, proven by
/// <see cref="BrandedDialogRenderProof"/>.</para>
/// </summary>
internal sealed class BrandedDialogWindow : Window
{
    private readonly BrandedDialogVisual _visual;
    private readonly ForegroundWindowTaker _foreground;
    private bool _confirmed;

    public BrandedDialogWindow(WindowThemeColors colors, string confirmLabel, string cancelLabel)
        : this(colors, confirmLabel, cancelLabel, new ForegroundWindowTaker())
    {
    }

    internal BrandedDialogWindow(WindowThemeColors colors, string confirmLabel, string cancelLabel, ForegroundWindowTaker foreground)
    {
        _foreground = foreground;
        _visual = BrandedDialogLayout.Build(colors, confirmLabel, cancelLabel);

        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Content = _visual.Root;

        _visual.CancelButton.MouseLeftButtonDown += (_, e) => { Finish(confirmed: false); e.Handled = true; };
        _visual.ConfirmButton.MouseLeftButtonDown += (_, e) => { Finish(confirmed: true); e.Handled = true; };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>The primary action's exact label (gate G7 parity slice P24's "primary names the
    /// action" requirement) — exposed so a render-proof fixture can assert on it directly rather
    /// than only on a rendered PNG's existence.</summary>
    public string ConfirmLabelText => _visual.ConfirmLabel.Text;

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
        DialogResult = confirmed;
    }

    public void Populate(string title, string message, string detail = "")
    {
        _visual.Title.Text = title;
        _visual.Message.Text = message;
        _visual.Detail.Text = detail;
        _visual.Detail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Fills and shows the dialog modally, returning <see langword="true"/> if the primary action
    /// was taken. Takes the foreground on <see cref="Window.ContentRendered"/>, not
    /// <see cref="Window.SourceInitialized"/> (ADR-0008 D9b amended OVI-601's own reasoning,
    /// restated here for a modal: <c>SourceInitialized</c> fires while the HWND exists but is not
    /// yet <c>WS_VISIBLE</c>, and <c>SetForegroundWindow</c> silently fails on an invisible
    /// window, so the whole <c>AttachThreadInput</c> fallback would be spent on a no-op.
    /// <c>ContentRendered</c> is the first point the window is genuinely visible — and a dialog
    /// that opens behind another window is worse than the flyouts' own version of this problem:
    /// it is modal, so the app appears to have frozen rather than merely drawing behind
    /// something).
    /// </summary>
    public static bool Show(
        string title, string message, string confirmLabel, string cancelLabel, ThemePreference theme, string detail = "")
    {
        var dialog = new BrandedDialogWindow(WindowThemePalette.Resolve(theme), confirmLabel, cancelLabel);
        dialog.Populate(title, message, detail);

        dialog.ContentRendered += (_, _) =>
        {
            dialog._foreground.Take(new WindowInteropHelper(dialog).Handle);
            dialog.Activate();
        };

        dialog.ShowDialog();
        return dialog._confirmed;
    }
}
