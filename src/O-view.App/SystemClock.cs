namespace OView.App;

/// <summary>
/// The one real <see cref="IClock"/> implementation (ADR-0007 D2 point 4: "one clock, one
/// timer, injected"). Every other type in this project takes an <see cref="IClock"/> rather
/// than reading <see cref="DateTimeOffset.UtcNow"/> itself, so this is the only place in
/// O-view.App where that call appears.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
