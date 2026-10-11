using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OView.App;

namespace OView.Linux.Presentation;

/// <summary>What the weekly-reset entry dialog's own <c>ShowAsync</c> resolved to — see
/// <c>O-view.Tray</c>'s identically named enum for the full reasoning (independently implemented
/// here, not shared, D1).</summary>
internal enum WeeklyResetEntryOutcome
{
    Cancelled,
    Cleared,
    Saved,
}

/// <summary>
/// The manually entered weekly reset this dialog hands back to its caller (gate G7 parity slice
/// P24, OVI-718) — a plain skin-local value, independently declared from <c>O-view.Tray</c>'s own
/// identically shaped type (D1). See that type's remarks for why no <c>O-view.Core</c> type
/// exists for this yet, and why this slice does not add one.
/// </summary>
public readonly record struct WeeklyResetEntry(DayOfWeek Day, string TimeText);

/// <summary>
/// Parses and validates the weekly-reset entry dialog's two fields (gate G7 parity slice P24,
/// OVI-718), the Linux counterpart of <c>O-view.Tray</c>'s own <c>WeeklyResetEntryValidator</c> —
/// independently implemented, not shared (D1), though the rule is identical: an empty time field
/// means "clear", and a non-empty one must be a strict 24-hour <c>HH:mm</c> time.
/// </summary>
internal static class WeeklyResetEntryValidator
{
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
/// <see cref="BrandedDialogRenderProof"/> renders offscreen.</summary>
internal sealed class WeeklyResetEntryVisual
{
    public required Border Root { get; init; }
    public required ComboBox DayBox { get; init; }
    public required TextBox TimeBox { get; init; }
    public required TextBlock HintText { get; init; }
    public required TextBlock ErrorText { get; init; }
    public required Border ClearButton { get; init; }
    public required Border CancelButton { get; init; }
    public required Border SaveButton { get; init; }
    public required TextBlock SaveLabel { get; init; }
}

internal static class WeeklyResetEntryLayout
{
    public static WeeklyResetEntryVisual Build(LinuxWindowThemeColors colors)
    {
        var fg = BrandedDialogChrome.ToBrush(colors.Foreground);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(BrandedDialogChrome.BuildBrandMark(colors.Accent));
        titleRow.Children.Add(new TextBlock
        {
            Text = "Weekly reset time",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
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

        var dayBox = new ComboBox();
        dayBox.Height = 30;
        dayBox.FontSize = 13;
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            dayBox.Items.Add(day.ToString());
        }

        var timeBox = new TextBox { Height = 30, FontSize = 13, Padding = new Thickness(9, 0, 9, 0) };

        var fieldsGrid = new Grid();
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition(10, GridUnitType.Pixel));
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition(96, GridUnitType.Pixel));

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
            IsVisible = false,
            Foreground = BrandedDialogChrome.ToBrush(LinuxWindowThemePalette.BandColor(UsageBarBand.Red)),
        };

        var clearButton = BrandedDialogChrome.BuildSecondaryAction("Clear", colors.Foreground, colors.Border, out _);
        clearButton.HorizontalAlignment = HorizontalAlignment.Left;
        clearButton.IsVisible = false;

        var cancelButton = BrandedDialogChrome.BuildSecondaryAction("Cancel", colors.Foreground, colors.Border, out _);
        cancelButton.Margin = new Thickness(0, 0, 8, 0);

        var saveButton = BrandedDialogChrome.BuildPrimaryAction("Save", colors.Accent, colors.AccentHover, out var saveLabel);

        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
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
/// P24, OVI-718), the Linux counterpart of <c>O-view.Tray</c>'s own <c>WeeklyResetEntryDialog</c>
/// — independently implemented, not shared (D1). Same behaviour: no pre-filled guess, a third
/// "Clear" action distinct from Cancel, no persistence wiring — nothing in this skin yet calls
/// <see cref="ShowAsync"/> either (see <c>O-view.Tray</c>'s own copy for the full reasoning,
/// which applies identically here).
///
/// <para>Not unit-tested itself (no interactive Linux display in this environment) — validation
/// is <see cref="WeeklyResetEntryValidator"/>, proven with no window.</para>
/// </summary>
internal sealed class WeeklyResetEntryDialog : Window
{
    private readonly WeeklyResetEntryVisual _visual;
    private WeeklyResetEntryOutcome _outcome = WeeklyResetEntryOutcome.Cancelled;
    private WeeklyResetEntry? _result;

    public WeeklyResetEntryDialog(LinuxWindowThemeColors colors)
    {
        _visual = WeeklyResetEntryLayout.Build(colors);

        Width = 400;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = Brushes.Transparent;
        Content = _visual.Root;

        _visual.SaveButton.PointerPressed += (_, e) => { if (IsLeftClick(e)) { e.Handled = true; TrySave(); } };
        _visual.CancelButton.PointerPressed += (_, e) => { if (IsLeftClick(e)) { e.Handled = true; Finish(WeeklyResetEntryOutcome.Cancelled); } };
        _visual.ClearButton.PointerPressed += (_, e) => { if (IsLeftClick(e)) { e.Handled = true; Finish(WeeklyResetEntryOutcome.Cleared); } };
        _visual.TimeBox.TextChanged += (_, _) => _visual.ErrorText.IsVisible = false;
        KeyDown += OnKeyDown;
    }

    private bool IsLeftClick(PointerPressedEventArgs e) => e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

    private void OnKeyDown(object? sender, KeyEventArgs e)
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
            _visual.ErrorText.IsVisible = true;
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
        Close(outcome != WeeklyResetEntryOutcome.Cancelled);
    }

    public void Populate(WeeklyResetEntry? current)
    {
        _visual.DayBox.SelectedItem = (current?.Day ?? DayOfWeek.Monday).ToString();
        _visual.TimeBox.Text = current?.TimeText ?? "";

        _visual.HintText.Text = current is null
            ? "Local time, 24-hour, e.g. 22:59. Left empty, O-view keeps deriving the reset by "
              + "watching your weekly percentage fall."
            : "Local time, 24-hour. Clearing this returns to the derived reset.";

        _visual.ClearButton.IsVisible = current is not null;
    }

    /// <summary>
    /// Shows the dialog modally. Returns the outcome and, when
    /// <see cref="WeeklyResetEntryOutcome.Saved"/>, the entry. Activation mirrors
    /// <c>DetailWindow.SetVisible</c>: <c>Activate()</c> on <see cref="Window.Opened"/>, with no
    /// extra platform mechanism (see <see cref="BrandedDialogWindow.ShowAsync"/>'s own remarks).
    /// </summary>
    public static async System.Threading.Tasks.Task<(WeeklyResetEntryOutcome Outcome, WeeklyResetEntry? Entry)> ShowAsync(
        Window owner, WeeklyResetEntry? current, ThemePreference theme)
    {
        var dialog = new WeeklyResetEntryDialog(LinuxWindowThemePalette.Resolve(theme));
        dialog.Populate(current);
        dialog.Opened += (_, _) =>
        {
            dialog.Activate();
            dialog._visual.TimeBox.Focus();
        };

        await dialog.ShowDialog(owner);
        return (dialog._outcome, dialog._result);
    }
}
