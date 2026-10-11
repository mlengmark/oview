using System.Windows.Input;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 slicing-table slice P24 (OVI-718, source <c>ui-spec.md</c> §5: "Esc cancels; Enter
/// takes the primary action"). <see cref="BrandedDialogKeyRouter.Resolve"/> is a pure function
/// over <see cref="Key"/> with no window, HWND or dispatcher involved — this is the whole of
/// what proves "Esc cancels / Enter confirms" without an interactive desktop session.
/// </summary>
public sealed class BrandedDialogKeyRouterTests
{
    [Fact]
    public void EscapeResolvesToCancel()
    {
        Assert.Equal(BrandedDialogKeyAction.Cancel, BrandedDialogKeyRouter.Resolve(Key.Escape));
    }

    [Fact]
    public void EnterResolvesToConfirm()
    {
        Assert.Equal(BrandedDialogKeyAction.Confirm, BrandedDialogKeyRouter.Resolve(Key.Enter));
    }

    [Theory]
    [InlineData(Key.Tab)]
    [InlineData(Key.Space)]
    [InlineData(Key.A)]
    [InlineData(Key.Delete)]
    public void AnyOtherKeyResolvesToNone(Key key)
    {
        Assert.Equal(BrandedDialogKeyAction.None, BrandedDialogKeyRouter.Resolve(key));
    }
}
