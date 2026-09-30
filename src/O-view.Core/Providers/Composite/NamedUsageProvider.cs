namespace OView.Core.Providers.Composite;

/// <summary>
/// One <see cref="IUsageProvider"/> paired with the stable name
/// <see cref="Models.ProviderHealth.ProviderName"/> reports it under. <see cref="IUsageProvider"/>
/// itself carries no name — a provider is just a read — so <see cref="CompositeUsageProvider"/>
/// needs this pairing to label per-provider health without inventing a naming scheme inside
/// the interface.
/// </summary>
public sealed record NamedUsageProvider(string Name, IUsageProvider Provider);
