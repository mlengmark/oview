using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using OView.App;

namespace OView.Linux.Presentation;

/// <summary>
/// The single dedicated thread every offscreen render-proof in this skin
/// (<see cref="DetailWindowRenderProof"/>, <see cref="BrandedDialogRenderProof"/>) runs on.
///
/// <para><b>Discovered fixing OVI-718's own CI</b> (CONFIRMED: both a centralised, lock-guarded
/// "call <c>AppBuilder.SetupWithoutStarting()</c> exactly once" guard, and
/// <c>AssemblyFixtures.cs</c>'s <c>DisableTestParallelization</c>, still left repeated local runs
/// flaking — one in several throwing <c>System.InvalidOperationException: The calling thread
/// cannot access this object because a different thread owns it</c> from inside
/// <c>Win32Platform.Initialize</c>): <see cref="Avalonia.Threading.Dispatcher"/> binds its
/// singleton to whichever actual OS thread first touches it, and disabling *test-collection*
/// parallelization only serialises *which test runs when* — it does not guarantee xUnit invokes
/// every test method on the same underlying thread-pool thread. Two sequential, non-overlapping
/// tests can still land on two different OS threads, and the second one's call either re-enters
/// <c>AppBuilder.Setup()</c> (thrown as "already called") or, worse, finds the Dispatcher already
/// bound to a thread it isn't running on.</para>
///
/// <para>The fix is this type: one dedicated, long-lived background thread that calls
/// <c>AppBuilder.Configure&lt;App&gt;().UsePlatformDetect().SetupWithoutStarting()</c> exactly
/// once, in its own run loop, and <see cref="Invoke{T}"/> marshals every render call onto that
/// same thread for the lifetime of the process — so the Dispatcher's bound thread and the calling
/// thread are always the same one, regardless of which xUnit worker thread invoked the test
/// method.</para>
///
/// <para>Starting the worker thread was still not enough on its own: nothing forced
/// <c>Setup()</c> to run there *before* some unrelated test (e.g. <c>HoverCardTests</c>,
/// constructing a plain <c>TextBlock</c> directly on whichever thread xUnit happened to run it
/// on) could touch Avalonia first and bind the Dispatcher singleton to that thread instead.
/// <c>tests/O-view.Linux.Tests/AssemblyFixtures.cs</c> closes that gap with its own
/// <see cref="ModuleInitializerAttribute"/> hook — the one timing guarantee the CLR gives for
/// free: it runs before the first use of any type in the *test* module, from any thread, before
/// a single test method's own code executes — and calls <see cref="ForceStartup"/>, which blocks
/// until the worker thread's <c>Setup()</c> call has actually completed.</para>
/// </summary>
internal static class AvaloniaWorkerThread
{
    private static readonly BlockingCollection<Action> Queue = new();

    static AvaloniaWorkerThread()
    {
        var thread = new Thread(RunLoop) { IsBackground = true, Name = "AvaloniaRenderProofWorker" };
        thread.Start();
    }

    internal static void ForceStartup() => Invoke(() => true);

    public static T Invoke<T>(Func<T> func)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add(() =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task.GetAwaiter().GetResult();
    }

    private static void RunLoop()
    {
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        foreach (var action in Queue.GetConsumingEnumerable())
        {
            action();
        }
    }
}
