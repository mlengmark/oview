using Avalonia.Input;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// ADR-0008 slicing-table slice P24 (OVI-718, source <c>ui-spec.md</c> §5: "Esc cancels; Enter
/// takes the primary action"), the Linux counterpart of <c>O-view.Tray</c>'s own
/// <c>BrandedDialogKeyRouterTests</c> — independently implemented, not shared (D1).
/// <see cref="BrandedDialogKeyRouter.Resolve"/> is a pure function over <see cref="Key"/> with no
/// window, no compositor and no dispatcher involved.
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
