using OView.Tray.Platform;

namespace OView.Tray.Tests.Platform;

/// <summary>
/// ADR-0008 D9b amended OVI-601's Windows foreground rule, proved against fakes only — no
/// real window, no real Win32 call. <see cref="ForegroundWindowTaker"/> is constructed with
/// its five Win32 calls injected so the <c>AttachThreadInput</c> fallback path is exercised
/// without a live desktop.
/// </summary>
public class ForegroundWindowTakerTests
{
    private const nint TargetHwnd = 0x1234;
    private const nint OtherForegroundHwnd = 0x5678;
    private const uint OwnThreadId = 11;
    private const uint OtherThreadId = 22;

    [Fact]
    public void Zero_handle_calls_nothing()
    {
        var calls = new List<string>();
        var taker = new ForegroundWindowTaker(
            _ => Record(calls, "SetForegroundWindow", true),
            () => Record(calls, "GetForegroundWindow", TargetHwnd),
            _ => Record(calls, "GetWindowThreadProcessId", OwnThreadId),
            () => Record(calls, "GetCurrentThreadId", OwnThreadId),
            (_, _, _) => Record(calls, "AttachThreadInput", true));

        taker.Take(0);

        Assert.Empty(calls);
    }

    [Fact]
    public void A_plain_request_that_succeeds_never_attaches_thread_input()
    {
        var attachCalls = 0;
        var taker = new ForegroundWindowTaker(
            _ => true,
            () => TargetHwnd,
            _ => OwnThreadId,
            () => OwnThreadId,
            (_, _, _) => { attachCalls++; return true; });

        taker.Take(TargetHwnd);

        Assert.Equal(0, attachCalls);
    }

    [Fact]
    public void A_refused_request_falls_back_to_AttachThreadInput_and_always_detaches()
    {
        var setForegroundCalls = new List<nint>();
        var attachCalls = new List<(uint AttachTo, uint AttachFrom, bool Attach)>();
        var taker = new ForegroundWindowTaker(
            hwnd =>
            {
                setForegroundCalls.Add(hwnd);
                return false; // refused every time — GetForegroundWindow is the source of truth
            },
            () => OtherForegroundHwnd,
            _ => OtherThreadId,
            () => OwnThreadId,
            (attachTo, attachFrom, attach) =>
            {
                attachCalls.Add((attachTo, attachFrom, attach));
                return true;
            });

        taker.Take(TargetHwnd);

        Assert.Equal(new[] { TargetHwnd, TargetHwnd }, setForegroundCalls);
        Assert.Equal(
            new[] { (OwnThreadId, OtherThreadId, true), (OwnThreadId, OtherThreadId, false) },
            attachCalls);
    }

    [Fact]
    public void No_foreground_window_at_all_stops_after_the_second_lookup()
    {
        var attachCalls = 0;
        var taker = new ForegroundWindowTaker(
            _ => false,
            () => 0,
            _ => OtherThreadId,
            () => OwnThreadId,
            (_, _, _) => { attachCalls++; return true; });

        taker.Take(TargetHwnd);

        Assert.Equal(0, attachCalls);
    }

    [Fact]
    public void Foreground_already_on_this_thread_does_not_attach()
    {
        var attachCalls = 0;
        var taker = new ForegroundWindowTaker(
            _ => false,
            () => OtherForegroundHwnd,
            _ => OwnThreadId,
            () => OwnThreadId,
            (_, _, _) => { attachCalls++; return true; });

        taker.Take(TargetHwnd);

        Assert.Equal(0, attachCalls);
    }

    [Fact]
    public void A_refused_attach_does_not_retry_SetForegroundWindow_and_does_not_detach()
    {
        var setForegroundCalls = 0;
        var attachCalls = new List<bool>();
        var taker = new ForegroundWindowTaker(
            _ => { setForegroundCalls++; return false; },
            () => OtherForegroundHwnd,
            _ => OtherThreadId,
            () => OwnThreadId,
            (_, _, attach) => { attachCalls.Add(attach); return false; });

        taker.Take(TargetHwnd);

        Assert.Equal(1, setForegroundCalls); // only the first, plain attempt
        Assert.Equal(new[] { true }, attachCalls); // attach refused — no matching detach
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ForegroundWindowTaker(null!, () => 0, _ => 0, () => 0, (_, _, _) => true));
        Assert.Throws<ArgumentNullException>(() => new ForegroundWindowTaker(_ => true, null!, _ => 0, () => 0, (_, _, _) => true));
        Assert.Throws<ArgumentNullException>(() => new ForegroundWindowTaker(_ => true, () => 0, null!, () => 0, (_, _, _) => true));
        Assert.Throws<ArgumentNullException>(() => new ForegroundWindowTaker(_ => true, () => 0, _ => 0, null!, (_, _, _) => true));
        Assert.Throws<ArgumentNullException>(() => new ForegroundWindowTaker(_ => true, () => 0, _ => 0, () => 0, null!));
    }

    private static T Record<T>(List<string> calls, string name, T result)
    {
        calls.Add(name);
        return result;
    }
}
