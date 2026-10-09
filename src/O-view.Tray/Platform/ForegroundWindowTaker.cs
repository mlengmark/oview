using System.Runtime.InteropServices;

namespace OView.Tray.Platform;

/// <summary>
/// Takes the foreground for a window, falling back to <c>AttachThreadInput</c> when the plain
/// request is refused (ADR-0008 D9b amended OVI-601: the detail window "takes the foreground
/// on open ... so it never opens behind other windows").
///
/// <para>Windows grants <c>SetForegroundWindow</c> only to a process that already holds the
/// foreground or received the last input event, and a tray-resident app frequently holds
/// neither. Losing that race is not cosmetic: a window shown but never activated never raises
/// <c>Deactivated</c>, so nothing dismisses it — exactly the "stuck on screen" failure this
/// slice guards against. Sharing an input queue with the current foreground thread for the
/// duration of the call makes the grant succeed.</para>
///
/// <para>The five Win32 calls are injected delegates rather than called directly, so the
/// fallback path is provable against fakes with no real window involved — the parameterless
/// constructor wires the real P/Invoke calls for production use; this type owns only the
/// decision of which to call and when.</para>
/// </summary>
internal sealed class ForegroundWindowTaker
{
    private readonly Func<nint, bool> _setForegroundWindow;
    private readonly Func<nint> _getForegroundWindow;
    private readonly Func<nint, uint> _getWindowThreadProcessId;
    private readonly Func<uint> _getCurrentThreadId;
    private readonly Func<uint, uint, bool, bool> _attachThreadInput;

    public ForegroundWindowTaker()
        : this(
            NativeMethods.SetForegroundWindow,
            NativeMethods.GetForegroundWindow,
            hwnd => NativeMethods.GetWindowThreadProcessId(hwnd, 0),
            NativeMethods.GetCurrentThreadId,
            NativeMethods.AttachThreadInput)
    {
    }

    internal ForegroundWindowTaker(
        Func<nint, bool> setForegroundWindow,
        Func<nint> getForegroundWindow,
        Func<nint, uint> getWindowThreadProcessId,
        Func<uint> getCurrentThreadId,
        Func<uint, uint, bool, bool> attachThreadInput)
    {
        ArgumentNullException.ThrowIfNull(setForegroundWindow);
        ArgumentNullException.ThrowIfNull(getForegroundWindow);
        ArgumentNullException.ThrowIfNull(getWindowThreadProcessId);
        ArgumentNullException.ThrowIfNull(getCurrentThreadId);
        ArgumentNullException.ThrowIfNull(attachThreadInput);

        _setForegroundWindow = setForegroundWindow;
        _getForegroundWindow = getForegroundWindow;
        _getWindowThreadProcessId = getWindowThreadProcessId;
        _getCurrentThreadId = getCurrentThreadId;
        _attachThreadInput = attachThreadInput;
    }

    /// <summary>
    /// Takes the foreground for <paramref name="hwnd"/>. A no-op for a zero handle. Tries the
    /// plain request first; only attaches thread input when that is refused, and always
    /// detaches again afterwards so the window's input queue is not left shared.
    /// </summary>
    public void Take(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        if (_setForegroundWindow(hwnd) && _getForegroundWindow() == hwnd)
        {
            return;
        }

        var foreground = _getForegroundWindow();
        if (foreground == 0)
        {
            return;
        }

        var foregroundThread = _getWindowThreadProcessId(foreground);
        var ownThread = _getCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ownThread)
        {
            return;
        }

        if (!_attachThreadInput(ownThread, foregroundThread, true))
        {
            return;
        }

        try
        {
            _setForegroundWindow(hwnd);
        }
        finally
        {
            _attachThreadInput(ownThread, foregroundThread, false);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(nint hWnd, nint processId);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        internal static extern bool AttachThreadInput(uint attachTo, uint attachFrom, [MarshalAs(UnmanagedType.Bool)] bool attach);
    }
}
