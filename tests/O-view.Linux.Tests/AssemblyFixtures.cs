using System.Runtime.CompilerServices;
using OView.Linux.Presentation;
using Xunit;

// Avalonia's Dispatcher binds a single, process-wide singleton to whichever thread first touches
// it (gate G7 parity slice P24, OVI-718 — discovered proving BrandedDialogRenderProofTests, the
// first test in this project ever to call AppBuilder.SetupWithoutStarting()). xUnit's default
// parallel test collections race multiple test classes across different threads; if one collection
// constructs a plain Avalonia control (HoverCardTests' own TextBlock()) on one thread while another
// collection calls AppBuilder.Setup() on a different one, Avalonia's own thread-affinity check
// throws regardless of which runs "first" in wall-clock terms. Disabling collection parallelization
// keeps every test in this assembly on one consistent execution thread, so nothing races to bind
// the singleton to two threads at once.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OView.Linux.Tests;

/// <summary>
/// Closes the second half of <see cref="AvaloniaWorkerThread"/>'s own remarks: disabling
/// collection parallelization above stops two tests racing *each other*, but nothing stopped an
/// unrelated test (e.g. <c>HoverCardTests</c>, constructing a plain <c>TextBlock</c> directly)
/// from running before any render-proof test ever does, on whatever thread xUnit happened to pick,
/// and binding Avalonia's Dispatcher there first. <see cref="ForceAvaloniaWorkerThreadStartup"/>'s
/// <see cref="ModuleInitializerAttribute"/> runs before the first use of any type in *this* test
/// module, on any thread, before any test method's own code — the one ordering guarantee the CLR
/// gives for free — and blocks until <see cref="AvaloniaWorkerThread"/>'s own <c>Setup()</c> call
/// has actually completed on its dedicated thread, so nothing else in this assembly can bind the
/// Dispatcher first.
/// </summary>
internal static class AssemblyStartup
{
    [ModuleInitializer]
    internal static void ForceAvaloniaWorkerThreadStartup() => AvaloniaWorkerThread.ForceStartup();
}
