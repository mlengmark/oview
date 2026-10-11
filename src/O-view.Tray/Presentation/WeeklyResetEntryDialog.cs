using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OView.App;
using OView.Tray.Platform;

namespace OView.Tray.Presentation;

/// <summary>What the weekly-reset entry dialog's own <c>Show</c> resolved to — cancelled,
/// cleared back to the derived reset, or saved with a new entry. Distinct states because
/// "cleared" cannot be expressed by a null entry alone (same reasoning the source's own
/// <c>WeeklyResetDialog.Show</c> gives for its <c>(Entry, Changed)</c> pair).</summary>
internal enum WeeklyResetEntryOutcome
{
    Cancelled,
    Cleared,
    Saved,
}

/// <summary>
/// The manually entered weekly reset this dialog hands back to its caller (gate G7 parity slice
/// P24, OVI-718). Deliberately a plain skin-local value — <c>(DayOfWeek, string)</c> — rather
/// than a new <c>O-view.Core</c> type: grepping <c>O-view.Core/Models</c> and
/// <c>O-view.Core/Storage</c> turns up <see cref="OView.Core.Models.WeeklyResetBoundary"/> and
/// <see cref="OView.Core.Models.WeeklyResetBoundaries"/> (the *derived* reset the graph draws
/// gridlines from) and <c>WeeklyResetAnchorStore</c> (the *reported*, Anthropic-assigned anchor)
/// — nothing shaped like the source's own <c>ManualWeeklyReset</c>, a user-entered override of
/// either. Adding one would be a Core contract change, which needs its own board merge (CLAUDE.md
/// "the one rule that matters most"); this slice is scoped to the dialog only, not the gap in the
/// data model it will eventually feed, and wires to no store — nothing yet consumes this result
/// (see <see cref="WeeklyResetEntryDialog"/>'s own remarks).
/// </summary>
public readonly record struct WeeklyResetEntry(DayOfWeek Day, string TimeText);

/// <summary>
/// Parses and validates the weekly-reset entry dialog's two fields (gate G7 parity slice P24,
/// OVI-718, source <c>ui-spec.md</c> §5 / <c>WeeklyResetDialog.xaml.cs</c>'s own
/// <c>TrySave</c>): a 24-hour <c>HH:mm</c> time and a day of week. Pulled out as a pure function
/// so the validation decision is testable without a window — the dialog's own <c>TrySave</c>
/// does nothing but call this and act on the result.
/// </summary>
internal static class WeeklyResetEntryValidator
{
    /// <summary>
    /// An empty time field means "clear", not an error — a user who deletes the value is asking
    /// to go back to deriving the reset, and making them hunt for a separate control for that is
    /// friction (source's own comment, carried over verbatim in spirit).
    /// </summary>
    public static bool IsClearRequest(string? timeText) => string.IsNullOrWhiteSpace(timeText);

    public static bool TryParse(string? dayText, string? timeText, out WeeklyResetEntry entry, out string error)
    {
        entry = default;
        error = "";

        var trimmed = (timeText ?? "").Trim();
        if (!DateTime.TryParseExact(trimmed, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = $"'{trimmed}' is not a 24-hour time. Enter it as HH:mm — 22:59, not 10:59 PM.";
            return false;
        }

        if (!Enum.TryParse<DayOfWeek>(dayText, ignoreCase: true, out var day))
        {
            error = $"'{dayText}' is not a day of the week.";
            return false;
        }

        entry = new WeeklyResetEntry(day, trimmed);
        return true;
    }
}

/// <summary>The window-independent visual tree <see cref="WeeklyResetEntryDialog"/> shows and
/// <see cref="BrandedDialogRenderProof"/> renders offscreen — see
/// <see cref="BrandedDialogVisual"/>'s identical reasoning for <see cref="BrandedDialogWindow"/>.</summary>
internal sealed class WeeklyResetEntryVisual
{
    public required Border Root { get; init; }
    public required System.Windows.Controls.ComboBox DayBox { get; init; }
    public required System.Windows.Controls.TextBox TimeBox { get; init; }
    public required TextBlock HintText { get; init; }
    public required TextBlock ErrorText { get; init; }
    public required Border ClearButton { get; init; }
    public required Border CancelButton { get; init; }
    public required Border SaveButton { get; init; }
    public required TextBlock SaveLabel { get; init; }
}

internal static class WeeklyResetEntryLayout
{
    public static WeeklyResetEntryVisual Build(WindowThemeColors colors)
    {
        var fg = BrandedDialogChrome.ToBrush(colors.Foreground);

        var titleRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        titleRow.Children.Add(BrandedDialogChrome.BuildBrandMark(colors.Accent));
        titleRow.Children.Add(new TextBlock
        {
            Text = "Weekly reset time",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = fg,
        });

        var explanation = new TextBlock
        {
            Text = "Anthropic assigns each account a fixed weekly reset. Find yours in Claude → Settings → Usage.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 14),
            Foreground = fg,
        };

        var dayBox = new System.Windows.Controls.ComboBox { Height = 30, FontSize = 13 };
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            dayBox.Items.Add(day.ToString());
        }

        var timeBox = new System.Windows.Controls.TextBox { Height = 30, FontSize = 13, Padding = new Thickness(9, 0, 9, 0) };

        var fieldsGrid = new Grid();
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

        var dayStack = new StackPanel();
        dayStack.Children.Add(new TextBlock { Text = "Day", FontSize = 11, Margin = new Thickness(0, 0, 0, 4), Foreground = fg });
        dayStack.Children.Add(dayBox);
        Grid.SetColumn(dayStack, 0);

        var timeStack = new StackPanel();
        timeStack.Children.Add(new TextBlock { Text = "Time", FontSize = 11, Margin = new Thickness(0, 0, 0, 4), Foreground = fg });
        timeStack.Children.Add(timeBox);
        Grid.SetColumn(timeStack, 2);

        fieldsGrid.Children.Add(dayStack);
        fieldsGrid.Children.Add(timeStack);

        var hintText = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = fg,
        };

        var errorText = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed,
            Foreground = BrandedDialogChrome.ToBrush(WindowThemePalette.BandColor(UsageBarBand.Red)),
        };

        var clearButton = BrandedDialogChrome.BuildSecondaryAction("Clear", colors.Foreground, colors.Border, out _);
        clearButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        clearButton.Visibility = Visibility.Collapsed;

        var cancelButton = BrandedDialogChrome.BuildSecondaryAction("Cancel", colors.Foreground, colors.Border, out _);
        cancelButton.Margin = new Thickness(0, 0, 8, 0);

        var saveButton = BrandedDialogChrome.BuildPrimaryAction("Save", colors.Accent, colors.AccentHover, out var saveLabel);

        var rightButtons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        rightButtons.Children.Add(cancelButton);
        rightButtons.Children.Add(saveButton);

        var buttonRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        buttonRow.Children.Add(clearButton);
        buttonRow.Children.Add(rightButtons);

        var stack = new StackPanel();
        stack.Children.Add(titleRow);
        stack.Children.Add(explanation);
        stack.Children.Add(fieldsGrid);
        stack.Children.Add(hintText);
        stack.Children.Add(errorText);
        stack.Children.Add(buttonRow);

        var root = new Border
        {
            Background = BrandedDialogChrome.ToBrush(colors.Background),
            BorderBrush = BrandedDialogChrome.ToBrush(colors.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(BrandedDialogChrome.CardCornerRadius),
            Padding = new Thickness(18),
            Width = 400,
            Child = stack,
        };

        return new WeeklyResetEntryVisual
        {
            Root = root,
            DayBox = dayBox,
            TimeBox = timeBox,
            HintText = hintText,
            ErrorText = errorText,
            ClearButton = clearButton,
            CancelButton = cancelButton,
            SaveButton = saveButton,
            SaveLabel = saveLabel,
        };
    }
}

/// <summary>
/// Where the user enters the weekly reset Anthropic assigned their account (gate G7 parity slice
/// P24, OVI-718; source GitHub issue #186 / <c>WeeklyResetDialog</c>). Its own window rather than
/// <see cref="BrandedDialogWindow"/>, which only confirms — this one collects input, so it needs
/// validation and a third action, clearing the entry to go back to deriving.
///
/// <para><b>No persistence wiring.</b> This slice builds the dialog only. Nothing in this skin
/// yet calls <see cref="Show"/> — the menu action that would (slicing-table row P23, "Weekly
/// reset…") is not built, and no Core type exists to save the result into (see
/// <see cref="WeeklyResetEntry"/>'s own remarks). The result is handed back to whatever future
/// caller wires both of those up; this dialog persists nothing itself.</para>
///
/// <para>Nothing is pre-filled from any derived value: a guess the user confirms becomes a fact,
/// which is worse than one labelled uncertain — the same reasoning the source attaches to its own
/// <c>WeeklyResetDialog</c>.</para>
///
/// <para>Not unit-tested itself (same boundary as <see cref="BrandedDialogWindow"/>) — validation
/// is <see cref="WeeklyResetEntryValidator"/>, proven with no window, and the key routing is the
/// same <see cref="BrandedDialogKeyRouter"/> every branded dialog in this skin shares.</para>
/// </summary>
internal sealed class WeeklyResetEntryDialog : Window
{
    private readonly WeeklyResetEntryVisual _visual;
    private readonly ForegroundWindowTaker _foreground;
    private WeeklyResetEntryOutcome _outcome = WeeklyResetEntryOutcome.Cancelled;
    private WeeklyResetEntry? _result;

    public WeeklyResetEntryDialog(WindowThemeColors colors)
        : this(colors, new ForegroundWindowTaker())
    {
    }

    internal WeeklyResetEntryDialog(WindowThemeColors colors, ForegroundWindowTaker foreground)
    {
        _foreground = foreground;
        _visual = WeeklyResetEntryLayout.Build(colors);

        Width = 400;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Content = _visual.Root;

        _visual.SaveButton.MouseLeftButtonDown += (_, e) => { TrySave(); e.Handled = true; };
        _visual.CancelButton.MouseLeftButtonDown += (_, e) => { Finish(WeeklyResetEntryOutcome.Cancelled); e.Handled = true; };
        _visual.ClearButton.MouseLeftButtonDown += (_, e) => { Finish(WeeklyResetEntryOutcome.Cleared); e.Handled = true; };
        _visual.TimeBox.TextChanged += (_, _) => _visual.ErrorText.Visibility = Visibility.Collapsed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (BrandedDialogKeyRouter.Resolve(e.Key))
        {
            case BrandedDialogKeyAction.Cancel:
                Finish(WeeklyResetEntryOutcome.Cancelled);
                break;
            case BrandedDialogKeyAction.Confirm:
                TrySave();
                break;
        }
    }

    private void TrySave()
    {
        var day = _visual.DayBox.SelectedItem as string;
        var time = _visual.TimeBox.Text;

        if (WeeklyResetEntryValidator.IsClearRequest(time))
        {
            Finish(WeeklyResetEntryOutcome.Cleared);
            return;
        }

        if (!WeeklyResetEntryValidator.TryParse(day, time, out var entry, out var error))
        {
            _visual.ErrorText.Text = error;
            _visual.ErrorText.Visibility = Visibility.Visible;
            _visual.TimeBox.Focus();
            _visual.TimeBox.SelectAll();
            return;
        }

        _result = entry;
        Finish(WeeklyResetEntryOutcome.Saved);
    }

    private void Finish(WeeklyResetEntryOutcome outcome)
    {
        _outcome = outcome;
        DialogResult = outcome != WeeklyResetEntryOutcome.Cancelled;
    }

    public void Populate(WeeklyResetEntry? current)
    {
        _visual.DayBox.SelectedItem = (current?.Day ?? DayOfWeek.Monday).ToString();
        _visual.TimeBox.Text = current?.TimeText ?? "";

        _visual.HintText.Text = current is null
            ? "Local time, 24-hour, e.g. 22:59. Left empty, O-view keeps deriving the reset by "
              + "watching your weekly percentage fall."
            : "Local time, 24-hour. Clearing this returns to the derived reset.";

        _visual.ClearButton.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Shows the dialog modally. Returns the outcome and, when <see cref="WeeklyResetEntryOutcome.Saved"/>,
    /// the entry. Takes the foreground on <see cref="Window.ContentRendered"/> — see
    /// <see cref="BrandedDialogWindow.Show"/>'s remarks for why, which apply identically here.
    /// </summary>
    public static (WeeklyResetEntryOutcome Outcome, WeeklyResetEntry? Entry) Show(WeeklyResetEntry? current, ThemePreference theme)
    {
        var dialog = new WeeklyResetEntryDialog(WindowThemePalette.Resolve(theme));
        dialog.Populate(current);

        dialog.ContentRendered += (_, _) =>
        {
            dialog._foreground.Take(new WindowInteropHelper(dialog).Handle);
            dialog.Activate();
            dialog._visual.TimeBox.Focus();
        };

        dialog.ShowDialog();
        return (dialog._outcome, dialog._result);
    }
}
