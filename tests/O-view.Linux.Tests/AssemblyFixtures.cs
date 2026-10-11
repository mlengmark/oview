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
