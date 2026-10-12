namespace ClankerWorld.Simulation.World;

/// <summary>
/// A reference that has already passed whatever approval pipeline owns the
/// actual asset bytes. The catalog stores only the approved reference, never bytes
/// or an untrusted client-side asset payload.
/// </summary>
public sealed record OwnerApprovedAssetReference(string AssetId, string AssetDigest);

/// <summary>
/// Decides whether an asset reference has already been approved by the
/// server-owned asset catalog. The runtime deliberately accepts a policy
/// rather than a client-provided allow-list, so an owner request can name an
/// asset but cannot make that asset approved.
/// </summary>
public interface IOwnerApprovedAssetReferencePolicy
{
    /// <summary>
    /// Returns whether this exact canonical ID/digest pair is approved for
    /// attachment where a host-supported asset path permits it.
    /// </summary>
    bool IsApproved(OwnerApprovedAssetReference reference);
}
