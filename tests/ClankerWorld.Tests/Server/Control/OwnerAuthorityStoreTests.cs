using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Server.Pairing;

namespace ClankerWorld.Tests;

public sealed class OwnerAuthorityStoreTests
{
    [Fact]
    public void LocalCodeApprovalChoosesTheExactPendingDeviceAndStillRequiresItsSignature()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var key = CreateP256Key();
        using var otherKey = CreateP256Key();
        var pending = StartPairing(store, key);
        var other = StartPairing(store, otherKey);
        // Keep this exact-device control independent of random short-code collisions.
        other = other with { PairingCode = pending.PairingCode == "000000" ? "000001" : "000000" };
        var state = store.ExportState();
        store = OwnerAuthorityStore.Restore(state with
        {
            Pairings = state.Pairings.Select(pairing => pairing.PairingId == other.PairingId
                ? pairing with { VisibleCodeHashBase64 = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(other.PairingCode))) }
                : pairing).ToArray(),
        }, clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.False(store.ApprovePendingPairingLocally("not-a-code").IsSuccess);
        Assert.True(store.ApprovePendingPairingLocally(pending.PairingCode).IsSuccess);
        Assert.Equal(OwnerPairingState.Pending, store.GetPairingStatus(other.PairingId).Value!.State);
        store = OwnerAuthorityStore.Restore(store.ExportState(), clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.False(store.ApprovePendingPairingLocally(pending.PairingCode).IsSuccess);
        Assert.False(store.ActivatePairing(new(pending.PairingId, pending.ActivationCanonicalProof,
            Sign(otherKey, pending.ActivationCanonicalProof))).IsSuccess);
        Assert.True(store.ActivatePairing(new(pending.PairingId, pending.ActivationCanonicalProof,
            Sign(key, pending.ActivationCanonicalProof))).IsSuccess);
        clock.UtcNow += OwnerAuthorityStore.PendingPairingLifetime;
        Assert.False(store.ApprovePendingPairingLocally(other.PairingCode).IsSuccess);
        Assert.Equal(OwnerPairingState.Expired, store.GetPairingStatus(other.PairingId).Value!.State);
    }

    [Fact]
    public void LocalCodeApprovalRefusesAmbiguousCodesWithoutApprovingEitherDevice()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var firstKey = CreateP256Key();
        using var secondKey = CreateP256Key();
        var first = StartPairing(store, firstKey);
        _ = StartPairing(store, secondKey);
        var state = store.ExportState();
        var hash = state.Pairings.Single(pairing => pairing.PairingId == first.PairingId).VisibleCodeHashBase64;
        store = OwnerAuthorityStore.Restore(state with
        {
            Pairings = state.Pairings.Select(pairing => pairing with { VisibleCodeHashBase64 = hash }).ToArray(),
        }, clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.False(store.ApprovePendingPairingLocally(first.PairingCode).IsSuccess);
        Assert.All(store.ExportState().Pairings, pairing => Assert.Equal(OwnerPairingState.Pending, pairing.State));
    }

    [Fact]
    public void HostComparisonCodesStayProcessLocalAndDecliningCannotApproveADevice()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var key = CreateP256Key();
        var pending = StartPairing(store, key);
        var shown = Assert.Single(store.GetPendingPairingsLocally());
        Assert.Equal(pending.PairingId, shown.PairingId);
        Assert.Equal(pending.PairingCode, shown.PairingCode);
        var state = store.ExportState();
        var saved = JsonSerializer.Serialize(state);
        Assert.DoesNotContain("\"PairingCode\"", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ComparisonCode\"", saved, StringComparison.Ordinal);
        var restored = OwnerAuthorityStore.Restore(state, clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.Null(Assert.Single(restored.GetPendingPairingsLocally()).PairingCode);
        Assert.True(restored.DeclinePendingPairingLocally(pending.PairingId).IsSuccess);
        restored = OwnerAuthorityStore.Restore(restored.ExportState(), clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.Empty(restored.GetPendingPairingsLocally());
        Assert.False(restored.ApprovePendingPairingLocally(pending.PairingId, pending.PairingCode).IsSuccess);
        Assert.False(restored.ActivatePairing(new(pending.PairingId, pending.ActivationCanonicalProof,
            Sign(key, pending.ActivationCanonicalProof))).IsSuccess);
        Assert.Empty(restored.GetDevices());
    }

    private static readonly JsonSerializerOptions LegacyJson = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
    [Fact]
    public void ApprovalOriginSurvivesPendingAndActivatedRestartsWithoutInventingLegacyHistory()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var firstKey = CreateP256Key();
        var first = StartPairing(store, firstKey);
        Assert.True(store.ApprovePendingPairingLocally(first.PairingId, first.PairingCode).IsSuccess);
        var firstDevice = store.ActivatePairing(new(first.PairingId, first.ActivationCanonicalProof,
            Sign(firstKey, first.ActivationCanonicalProof))).Value!;
        Assert.Equal(OwnerAuthorityStore.LocalApproval, firstDevice.ApprovedBy);
        Assert.Equal("Approved on the server's computer", firstDevice.ApprovalDescription);
        using var secondKey = CreateP256Key();
        var second = StartPairing(store, secondKey);
        Assert.True(store.ApprovePendingPairing(second.PairingId, second.PairingCode, firstDevice.DeviceId).IsSuccess);
        store = OwnerAuthorityStore.Restore(JsonSerializer.Deserialize<OwnerAuthorityState>(
            JsonSerializer.Serialize(store.ExportState()))!, clock, CryptographicOwnerAuthorityRandom.Instance);
        var secondDevice = store.ActivatePairing(new(second.PairingId, second.ActivationCanonicalProof,
            Sign(secondKey, second.ActivationCanonicalProof))).Value!;
        Assert.Equal(firstDevice.DeviceId, secondDevice.ApprovedBy);
        Assert.Equal($"Approved by device {firstDevice.DeviceId}", secondDevice.ApprovalDescription);
        Assert.True(store.RevokeDevice(firstDevice.DeviceId).IsSuccess);
        var state = store.ExportState();
        store = OwnerAuthorityStore.Restore(state, clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.Equal(firstDevice.DeviceId, store.GetDevice(secondDevice.DeviceId).Value!.ApprovedBy);
        Assert.Throws<InvalidDataException>(() => OwnerAuthorityStore.Restore(state with
        {
            Devices = state.Devices.Select(device => device with { ApprovedBy = "missing-approver" }).ToArray(),
        }, clock, CryptographicOwnerAuthorityRandom.Instance));
        var legacy = state with
        {
            Pairings = state.Pairings.Select(pairing => pairing with { ApprovedBy = null }).ToArray(),
            Devices = state.Devices.Select(device => device with { ApprovedBy = null }).ToArray(),
        };
        store = OwnerAuthorityStore.Restore(JsonSerializer.Deserialize<OwnerAuthorityState>(
            JsonSerializer.Serialize(legacy, LegacyJson))!, clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.All(store.GetDevices(), device => Assert.Equal("Approval not recorded", device.ApprovalDescription));
    }

    [Fact]
    public void LocalPairingRecoveryPreservesApprovedRequestsAndRejectsInvalidKeysBeforeEviction()
    {
        var store = NewStore(NewClock());
        using var activeKey = CreateP256Key();
        var active = StartPairing(store, activeKey);
        Assert.True(store.ApprovePendingPairingLocally(active.PairingId, active.PairingCode).IsSuccess);
        Assert.True(store.ActivatePairing(new(active.PairingId, active.ActivationCanonicalProof,
            Sign(activeKey, active.ActivationCanonicalProof))).IsSuccess);
        using var approvedKey = CreateP256Key();
        var approved = StartPairing(store, approvedKey);
        Assert.True(store.ApprovePendingPairingLocally(approved.PairingId, approved.PairingCode).IsSuccess);
        for (var index = 1; index < OwnerAuthorityStore.MaximumPendingPairings; index++)
        {
            using var key = CreateP256Key();
            StartPairing(store, key);
        }
        Assert.False(store.StartPairingLocally(new("invalid")).IsSuccess);
        Assert.Equal(OwnerAuthorityFailure.PublicKeyAlreadyRegistered,
            store.StartPairingLocally(new(Convert.ToBase64String(activeKey.ExportSubjectPublicKeyInfo()))).Failure);
        Assert.Equal(OwnerAuthorityStore.MaximumPendingPairings,
            store.ExportState().Pairings.Count(item => item.State is OwnerPairingState.Pending or OwnerPairingState.Approved));
        using var ownerKey = CreateP256Key();
        var recovered = store.StartPairingLocally(new(Convert.ToBase64String(ownerKey.ExportSubjectPublicKeyInfo())));
        Assert.True(recovered.IsSuccess);
        Assert.Equal(OwnerPairingState.Approved, store.GetPairingStatus(approved.PairingId).Value!.State);
        Assert.Single(store.ExportState().Pairings, item => item.State == OwnerPairingState.Expired);
    }

    [Fact]
    public void OneKeyActivatesOneDeviceAndRevokingItCancelsItsOtherPairings()
    {
        var store = NewStore(NewClock());
        using var key = CreateP256Key();
        var first = StartPairing(store, key);
        var second = StartPairing(store, key);
        var third = StartPairing(store, key);
        Assert.True(store.ApprovePendingPairingLocally(first.PairingId, first.PairingCode).IsSuccess);
        Assert.True(store.ApprovePendingPairingLocally(second.PairingId, second.PairingCode).IsSuccess);
        var device = store.ActivatePairing(new(first.PairingId, first.ActivationCanonicalProof,
            Sign(key, first.ActivationCanonicalProof)));
        Assert.True(device.IsSuccess);

        // The same key cannot become a second active device, approved or not.
        Assert.Equal(OwnerAuthorityFailure.PublicKeyAlreadyRegistered, store.ActivatePairing(new(second.PairingId,
            second.ActivationCanonicalProof, Sign(key, second.ActivationCanonicalProof))).Failure);
        Assert.Equal(OwnerAuthorityFailure.PublicKeyAlreadyRegistered,
            store.ApprovePendingPairing(third.PairingId, third.PairingCode, device.Value!.DeviceId).Failure);
        Assert.Single(store.GetDevices(), item => item.State == OwnerDeviceState.Active);

        // Revoking the device cancels every pending or approved pairing for its key.
        Assert.True(store.RevokeDevice(device.Value!.DeviceId).IsSuccess);
        Assert.Equal(OwnerPairingState.Expired, store.GetPairingStatus(second.PairingId).Value!.State);
        Assert.Equal(OwnerPairingState.Expired, store.GetPairingStatus(third.PairingId).Value!.State);
        Assert.Equal(OwnerAuthorityFailure.PairingExpired, store.ActivatePairing(new(second.PairingId,
            second.ActivationCanonicalProof, Sign(key, second.ActivationCanonicalProof))).Failure);
        Assert.DoesNotContain(store.GetDevices(), item => item.State == OwnerDeviceState.Active);
    }

    [Fact]
    public void ARevokedKeyCanPairAgainOnlyFromTheServersOwnComputer()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var key = CreateP256Key();
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var original = StartPairing(store, key);
        Assert.True(store.ApprovePendingPairingLocally(original.PairingId, original.PairingCode).IsSuccess);
        var device = store.ActivatePairing(new(original.PairingId, original.ActivationCanonicalProof,
            Sign(key, original.ActivationCanonicalProof))).Value!;
        Assert.True(store.RevokeDevice(device.DeviceId).IsSuccess);

        Assert.Equal(OwnerAuthorityFailure.DeviceRevoked, store.StartPairing(new(spki)).Failure);

        // The approval listener on the server's own computer can deliberately re-admit it.
        var readmitted = store.StartPairingLocally(new(spki));
        Assert.True(readmitted.IsSuccess);
        Assert.Equal(OwnerAuthorityFailure.DeviceRevoked,
            store.ApprovePendingPairing(readmitted.Value!.PairingId, readmitted.Value.PairingCode, device.DeviceId).Failure);
        Assert.Equal(OwnerPairingState.Pending, store.GetPairingStatus(readmitted.Value.PairingId).Value!.State);
        Assert.True(store.ApprovePendingPairingLocally(readmitted.Value!.PairingId, readmitted.Value.PairingCode).IsSuccess);
        var replacement = store.ActivatePairing(new(readmitted.Value.PairingId, readmitted.Value.ActivationCanonicalProof,
            Sign(key, readmitted.Value.ActivationCanonicalProof)));
        Assert.True(replacement.IsSuccess);
        Assert.NotEqual(device.DeviceId, replacement.Value!.DeviceId);
        Assert.Equal(OwnerDeviceState.Revoked, store.GetDevice(device.DeviceId).Value!.State);

        // The revoked record and its replacement survive a restart together.
        var restored = OwnerAuthorityStore.Restore(JsonSerializer.Deserialize<OwnerAuthorityState>(
            JsonSerializer.Serialize(store.ExportState()))!, clock, CryptographicOwnerAuthorityRandom.Instance);
        Assert.Equal(OwnerDeviceState.Active, restored.GetDevice(replacement.Value.DeviceId).Value!.State);
        Assert.Equal(OwnerAuthorityFailure.PublicKeyAlreadyRegistered, restored.StartPairingLocally(new(spki)).Failure);
    }

    [Fact]
    public void PairingVisibleCodeAttemptsAreBoundedAndSurviveRestore()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var deviceKey = CreateP256Key();
        var pairing = StartPairing(store, deviceKey);
        var wrongCode = pairing.PairingCode == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < OwnerAuthorityStore.MaximumWrongVisibleCodeAttempts - 1; attempt++)
        {
            var rejected = store.ApprovePendingPairingLocally(pairing.PairingId, wrongCode);
            Assert.Equal(OwnerAuthorityFailure.PairingCodeMismatch, rejected.Failure);
        }

        var persistedState = JsonSerializer.Deserialize<OwnerAuthorityState>(
            JsonSerializer.Serialize(store.ExportState()));
        Assert.NotNull(persistedState);
        var restored = OwnerAuthorityStore.Restore(
            persistedState,
            clock,
            CryptographicOwnerAuthorityRandom.Instance);
        var finalRejectedAttempt = restored.ApprovePendingPairingLocally(pairing.PairingId, wrongCode);
        var status = restored.GetPairingStatus(pairing.PairingId);
        var afterLockout = restored.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode);

        Assert.Equal(OwnerAuthorityFailure.PairingCodeMismatch, finalRejectedAttempt.Failure);
        Assert.True(status.IsSuccess);
        Assert.Equal(OwnerPairingState.Expired, status.Value!.State);
        Assert.Equal(OwnerAuthorityFailure.PairingExpired, afterLockout.Failure);
    }

    [Fact]
    public void PendingPairingsAreCappedAndExpiredPairingHistoryIsTrimmed()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var renewalKey = CreateP256Key();

        for (var index = 0; index < OwnerAuthorityStore.MaximumPendingPairings; index++)
        {
            using var key = CreateP256Key();
            StartPairing(store, key);
        }

        using var overCapacityKey = CreateP256Key();
        var overCapacity = store.StartPairing(
            new OwnerPairingRequest(Convert.ToBase64String(overCapacityKey.ExportSubjectPublicKeyInfo())));

        Assert.Equal(OwnerAuthorityFailure.PairingCapacityExceeded, overCapacity.Failure);

        clock.UtcNow = clock.UtcNow.Add(OwnerAuthorityStore.PendingPairingLifetime);
        for (var index = 0; index <= OwnerAuthorityStore.MaximumRetainedExpiredPairings; index++)
        {
            StartPairing(store, renewalKey);
            clock.UtcNow = clock.UtcNow.Add(OwnerAuthorityStore.PendingPairingLifetime);
        }

        var exported = store.ExportState();

        Assert.Equal(
            OwnerAuthorityStore.MaximumRetainedExpiredPairings,
            exported.Pairings.Count(pairing => pairing.State == OwnerPairingState.Expired));
    }

    [Fact]
    public void PendingPairingExpiresAfterTenMinutesOfOperationalTime()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var deviceKey = CreateP256Key();
        var pairing = StartPairing(store, deviceKey);
        clock.UtcNow = clock.UtcNow.Add(OwnerAuthorityStore.PendingPairingLifetime);

        var approval = store.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode);
        var status = store.GetPairingStatus(pairing.PairingId);

        Assert.Equal(OwnerAuthorityFailure.PairingExpired, approval.Failure);
        Assert.True(status.IsSuccess);
        Assert.Equal(OwnerPairingState.Expired, status.Value!.State);
    }

    [Fact]
    public void ActivationRejectsAProofFromADifferentPrivateKey()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var registeredKey = CreateP256Key();
        using var wrongKey = CreateP256Key();
        var pairing = StartPairing(store, registeredKey);
        var approval = store.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode);
        Assert.True(approval.IsSuccess);

        var activation = store.ActivatePairing(
            new OwnerPairingActivationRequest(
                pairing.PairingId,
                pairing.ActivationCanonicalProof,
                Sign(wrongKey, pairing.ActivationCanonicalProof)));
        var device = store.GetDevice(pairing.DeviceId);

        Assert.Equal(OwnerAuthorityFailure.InvalidSignature, activation.Failure);
        Assert.Equal(OwnerAuthorityFailure.DeviceNotFound, device.Failure);
    }

    [Fact]
    public void ChallengeCannotBeReplayedAndRevocationBlocksOutstandingChallenges()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var deviceKey = CreateP256Key();
        var pairing = StartPairing(store, deviceKey);
        Assert.True(store.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode).IsSuccess);
        Assert.True(store.ActivatePairing(
            new OwnerPairingActivationRequest(
                pairing.PairingId,
                pairing.ActivationCanonicalProof,
                Sign(deviceKey, pairing.ActivationCanonicalProof))).IsSuccess);

        var firstChallenge = IssueChallenge(store, deviceKey, pairing.DeviceId, "challenge-request-1");
        const string firstBinding = "control-request-sha256:replay-proof";
        var firstConsumption = CreateConsumptionRequest(store.Identity, pairing.DeviceId, firstChallenge, firstBinding, deviceKey);

        var firstUse = store.ConsumeChallenge(firstConsumption);
        var replay = store.ConsumeChallenge(firstConsumption);

        Assert.True(firstUse.IsSuccess);
        Assert.Equal(OwnerAuthorityFailure.ChallengeConsumed, replay.Failure);

        var outstandingChallenge = IssueChallenge(store, deviceKey, pairing.DeviceId, "challenge-request-2");
        Assert.True(store.RevokeDevice(pairing.DeviceId).IsSuccess);
        var afterRevocation = store.ConsumeChallenge(
            CreateConsumptionRequest(
                store.Identity,
                pairing.DeviceId,
                outstandingChallenge,
                "control-request-sha256:revoked-device",
                deviceKey));
        var issueAfterRevocation = store.IssueChallenge(
            CreateChallengeIssueRequest(store.Identity, pairing.DeviceId, "challenge-request-3", deviceKey));

        Assert.Equal(OwnerAuthorityFailure.DeviceRevoked, afterRevocation.Failure);
        Assert.Equal(OwnerAuthorityFailure.DeviceRevoked, issueAfterRevocation.Failure);
    }

    [Fact]
    public void PendingChallengesAreCappedAndTerminalChallengeHistoryIsTrimmed()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        using var deviceKey = CreateP256Key();
        var pairing = StartPairing(store, deviceKey);
        Assert.True(store.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode).IsSuccess);
        Assert.True(store.ActivatePairing(
            new OwnerPairingActivationRequest(
                pairing.PairingId,
                pairing.ActivationCanonicalProof,
                Sign(deviceKey, pairing.ActivationCanonicalProof))).IsSuccess);

        var earlier = new List<OwnerChallenge>();
        for (var index = 0; index < OwnerAuthorityStore.MaximumPendingChallengesPerDevice; index++)
        {
            earlier.Add(IssueChallenge(store, deviceKey, pairing.DeviceId, $"pending-cap-{index}"));
            clock.UtcNow = clock.UtcNow.AddSeconds(1);
        }

        // A full set never refuses a new challenge, even for a repeated issue
        // request: the oldest unanswered one makes room, and the new one works.
        var beyondCapacity = store.IssueChallenge(
            CreateChallengeIssueRequest(store.Identity, pairing.DeviceId, "pending-cap-0", deviceKey));
        Assert.True(beyondCapacity.IsSuccess);
        Assert.Equal(OwnerAuthorityStore.MaximumPendingChallengesPerDevice,
            store.ExportState().Challenges.Count(challenge => challenge.State == OwnerStoredChallengeState.Pending));
        Assert.Equal(OwnerAuthorityFailure.ChallengeExpired, store.ConsumeChallenge(CreateConsumptionRequest(
            store.Identity, pairing.DeviceId, earlier[0], "control-request-sha256:oldest", deviceKey)).Failure);
        Assert.True(store.ConsumeChallenge(CreateConsumptionRequest(
            store.Identity, pairing.DeviceId, earlier[1], "control-request-sha256:second", deviceKey)).IsSuccess);
        Assert.True(store.ConsumeChallenge(CreateConsumptionRequest(
            store.Identity, pairing.DeviceId, beyondCapacity.Value!, "control-request-sha256:newest", deviceKey)).IsSuccess);

        clock.UtcNow = clock.UtcNow.Add(OwnerAuthorityStore.ChallengeLifetime);
        for (var index = 0; index <= OwnerAuthorityStore.MaximumRetainedTerminalChallenges; index++)
        {
            IssueChallenge(store, deviceKey, pairing.DeviceId, $"terminal-retention-{index}");
            clock.UtcNow = clock.UtcNow.Add(OwnerAuthorityStore.ChallengeLifetime);
        }

        var exported = store.ExportState();

        Assert.Equal(
            OwnerAuthorityStore.MaximumRetainedTerminalChallenges,
            exported.Challenges.Count(challenge => challenge.State != OwnerStoredChallengeState.Pending));
    }

    [Fact]
    public void ANewDeviceChallengeMakesRoomAtTheGlobalLimitWithoutExpiringOtherUsableChallenges()
    {
        var clock = NewClock();
        var store = NewStore(clock);
        var keys = new List<ECDsa>();
        var earlier = new List<(ECDsa Key, OwnerChallenge Challenge)>();
        try
        {
            for (var device = 0; device <= OwnerAuthorityStore.MaximumPendingChallenges /
                     OwnerAuthorityStore.MaximumPendingChallengesPerDevice; device++)
            {
                var key = CreateP256Key();
                keys.Add(key);
                var pairing = StartPairing(store, key);
                Assert.True(store.ApprovePendingPairingLocally(pairing.PairingId, pairing.PairingCode).IsSuccess);
                Assert.True(store.ActivatePairing(new(pairing.PairingId, pairing.ActivationCanonicalProof,
                    Sign(key, pairing.ActivationCanonicalProof))).IsSuccess);
                var count = earlier.Count < OwnerAuthorityStore.MaximumPendingChallenges
                    ? OwnerAuthorityStore.MaximumPendingChallengesPerDevice : 1;
                for (var index = 0; index < count; index++)
                {
                    earlier.Add((key, IssueChallenge(store, key, pairing.DeviceId, $"global-cap-{device}-{index}")));
                    clock.UtcNow = clock.UtcNow.AddSeconds(1);
                }
            }

            // Consume through the real authorization boundary before ExportState can trim anything.
            var oldest = earlier[0];
            Assert.Equal(OwnerAuthorityFailure.ChallengeExpired, store.ConsumeChallenge(CreateConsumptionRequest(
                store.Identity, oldest.Challenge.DeviceId, oldest.Challenge, "global-cap-oldest", oldest.Key)).Failure);
            foreach (var (key, challenge) in earlier.Skip(1))
                Assert.True(store.ConsumeChallenge(CreateConsumptionRequest(
                    store.Identity, challenge.DeviceId, challenge, "global-cap-retained", key)).IsSuccess);
        }
        finally
        {
            foreach (var key in keys) key.Dispose();
        }
    }

    private static OwnerAuthorityStore NewStore(MutableClock clock) => new(
        new OwnerAuthorityIdentity("authority-test-1", "world-test-1"),
        clock,
        CryptographicOwnerAuthorityRandom.Instance);

    private static MutableClock NewClock() => new(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private static ECDsa CreateP256Key() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static OwnerPairingStart StartPairing(OwnerAuthorityStore store, ECDsa deviceKey)
    {
        var pairing = store.StartPairing(new OwnerPairingRequest(Convert.ToBase64String(deviceKey.ExportSubjectPublicKeyInfo())));

        Assert.True(pairing.IsSuccess);
        return pairing.Value!;
    }

    private static OwnerChallenge IssueChallenge(
        OwnerAuthorityStore store,
        ECDsa deviceKey,
        string deviceId,
        string requestId)
    {
        var issued = store.IssueChallenge(CreateChallengeIssueRequest(store.Identity, deviceId, requestId, deviceKey));

        Assert.True(issued.IsSuccess);
        return issued.Value!;
    }

    private static OwnerChallengeIssueRequest CreateChallengeIssueRequest(
        OwnerAuthorityIdentity identity,
        string deviceId,
        string requestId,
        ECDsa deviceKey)
    {
        var canonicalProof = OwnerAuthorityStore.CreateChallengeIssueCanonicalProof(identity, deviceId, requestId);
        return new OwnerChallengeIssueRequest(deviceId, requestId, canonicalProof, Sign(deviceKey, canonicalProof));
    }

    private static OwnerChallengeConsumeRequest CreateConsumptionRequest(
        OwnerAuthorityIdentity identity,
        string deviceId,
        OwnerChallenge challenge,
        string binding,
        ECDsa deviceKey)
    {
        var canonicalProof = OwnerAuthorityStore.CreateChallengeConsumeCanonicalProof(
            identity,
            deviceId,
            challenge.ChallengeId,
            challenge.Nonce,
            binding);
        return new OwnerChallengeConsumeRequest(
            deviceId,
            challenge.ChallengeId,
            challenge.Nonce,
            binding,
            canonicalProof,
            Sign(deviceKey, canonicalProof));
    }

    private static string Sign(ECDsa key, string canonicalProof) => Convert.ToBase64String(
        key.SignData(
            Encoding.UTF8.GetBytes(canonicalProof),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    private sealed class MutableClock(DateTimeOffset utcNow) : IOwnerAuthorityClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
