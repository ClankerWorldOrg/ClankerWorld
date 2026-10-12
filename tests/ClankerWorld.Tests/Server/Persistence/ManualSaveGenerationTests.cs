using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Control;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class ManualSaveGenerationTests
{
    [Theory]
    [InlineData(null, 0, true)]
    [InlineData("🌱", 59, false)]
    [InlineData("🌱", 58, true)]
    [InlineData("e\u0301", 59, false)]
    [InlineData("🇳🇿", 58, false)]
    [InlineData("👨‍👩", 58, false)]
    public void RepeatedOverwriteSelectsWholeGenerationsAndPreservesRecoveryPairs(string? finalCharacter, int prefixLength, bool characterFits)
    {
        var directory = Directory.CreateTempSubdirectory("save-generation-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var runtime = new WorldRuntime("save-generations");
            runtime.Pause();
            var store = new ManualWorldSaveStore(path);
            var oldSettings = new WorldAutosaveSettings(runtime.Society.WorldId, true, 5, 5, DateTimeOffset.UtcNow, 0);
            var oldAssignments = new AgentProviderAssignment[] { new("founder-scout", "planning", "openai", "old-model") };
            var name = finalCharacter is null ? "Selected"
                : new string('W', prefixLength) + finalCharacter + new string('W', 80 - prefixLength - finalCharacter.Length);
            var recoveryName = finalCharacter is null ? "Before overwriting: Selected"
                : "Before overwriting: " + new string('W', prefixLength) + (characterFits ? finalCharacter : string.Empty);
            var save = store.Create(name, runtime, oldAssignments, oldSettings);
            var original = WorldRuntimeCodec.Encode(store.Read(save.Id));
            runtime.SetJevEnabled(false);
            var newSettings = oldSettings with { Enabled = false, IntervalMinutes = 1 };
            var newAssignments = new AgentProviderAssignment[] { oldAssignments[0] with { Model = "new-model" } };
            var first = store.Overwrite(save.Id, runtime, newAssignments, newSettings);
            Assert.Single(CheckpointFiles(path, save.Id));
            var reopened = new ManualWorldSaveStore(path);
            Assert.Equal(name, reopened.List().Single(item => item.Id == save.Id).Name);
            Assert.Equal(recoveryName, reopened.List().Single(item => item.Id == first.BackupId).Name);
            var committed = reopened.ReadCommitted(save.Id);
            Assert.False(committed.Checkpoint.JevEnabled);
            Assert.Equal(newAssignments, committed.Assignments);
            Assert.Equal(newSettings, committed.AutosaveSettings);
            Assert.Equal(original, WorldRuntimeCodec.Encode(reopened.Read(first.BackupId)));
            Assert.Equal(oldAssignments, reopened.ReadAssignments(first.BackupId));
            runtime.SetJevEnabled(true);
            var second = reopened.Overwrite(save.Id, runtime, oldAssignments, oldSettings);
            Assert.Single(CheckpointFiles(path, save.Id));
            Assert.Equal(recoveryName, new ManualWorldSaveStore(path).List().Single(item => item.Id == second.BackupId).Name);
            Assert.False(reopened.Read(second.BackupId).JevEnabled);
            Assert.Equal(newAssignments, reopened.ReadAssignments(second.BackupId));
            Assert.Equal(newSettings, reopened.ReadAutosaveSettings(second.BackupId));
            Assert.True(reopened.Read(save.Id).JevEnabled);
            Assert.Equal(3, reopened.List().Count);
        }
        finally { directory.Delete(recursive: true); }
    }

    [WindowsSaveFact]
    public void LockedSupersededCheckpointKeepsPublishedOverwriteAndRecoveryReadable()
    {
        var directory = Directory.CreateTempSubdirectory("save-generation-retirement-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var runtime = new WorldRuntime("save-generation-retirement");
            runtime.Pause();
            var logger = new RecordingLogger<ManualWorldSaveStore>();
            var store = new ManualWorldSaveStore(path, logger);
            const string privateSaveName = "Private save name kept out of diagnostics";
            var save = store.Create(privateSaveName, runtime, []);
            var previous = Assert.Single(CheckpointFiles(path, save.Id));
            var original = File.ReadAllBytes(previous);
            runtime.SetJevEnabled(false);
            ManualSaveOverwriteReceipt receipt;
            using (var locked = new FileStream(previous, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                receipt = store.Overwrite(save.Id, runtime, []);
                var reopened = new ManualWorldSaveStore(path);
                Assert.False(reopened.Read(save.Id).JevEnabled);
                Assert.Equal(original, WorldRuntimeCodec.Encode(reopened.Read(receipt.BackupId)));
                Assert.Equal(original, File.ReadAllBytes(previous));
                Assert.Contains(logger.Messages, message => message.Contains("outcome=cleanup_pending", StringComparison.Ordinal) &&
                    message.Contains("published=true", StringComparison.Ordinal));
                Assert.All(logger.Messages, message =>
                {
                    Assert.DoesNotContain(privateSaveName, message, StringComparison.Ordinal);
                    Assert.DoesNotContain(previous, message, StringComparison.Ordinal);
                });
            }
            Assert.False(store.Read(receipt.Saved.Id).JevEnabled);
            Assert.Equal(original, WorldRuntimeCodec.Encode(store.Read(receipt.BackupId)));
        }
        finally { directory.Delete(recursive: true); }
    }

    [WindowsSaveFact]
    public void LockedMetadataCannotPublishHalfAnOverwrite()
    {
        var directory = Directory.CreateTempSubdirectory("save-generation-lock-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var runtime = new WorldRuntime("save-generation-lock");
            runtime.Pause();
            var store = new ManualWorldSaveStore(path);
            var settings = new WorldAutosaveSettings(runtime.Society.WorldId, true, 5, 5, DateTimeOffset.UtcNow, 0);
            var assignments = new AgentProviderAssignment[] { new("founder-scout", "planning", "openai", "old-model") };
            var save = store.Create("Selected", runtime, assignments, settings);
            var original = WorldRuntimeCodec.Encode(store.Read(save.Id));
            runtime.SetJevEnabled(false);
            using (var locked = new FileStream(Path.Combine(path + ".manual", save.Id + ".meta.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failure = Record.Exception(() => store.Overwrite(save.Id, runtime,
                    [assignments[0] with { Model = "new-model" }], settings with { Enabled = false }));
                Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "Expected metadata replacement to fail.");
            }
            var reopened = new ManualWorldSaveStore(path);
            Assert.Equal(original, WorldRuntimeCodec.Encode(reopened.Read(save.Id)));
            Assert.Equal(assignments, reopened.ReadAssignments(save.Id));
            Assert.Equal(settings, reopened.ReadAutosaveSettings(save.Id));
            Assert.Equal(2, reopened.List().Count);
            // A failed publication retains the original selected file and the
            // intentionally unpublished generation, rather than retiring either.
            Assert.Equal(2, CheckpointFiles(path, save.Id).Length);
            using var restored = WorldRuntime.Restore(reopened.Read(save.Id));
            Assert.True(restored.JevEnabled);
            var retry = reopened.Overwrite(save.Id, runtime, [assignments[0] with { Model = "new-model" }], settings with { Enabled = false });
            Assert.False(reopened.Read(retry.Saved.Id).JevEnabled);
            Assert.Equal(2, CheckpointFiles(path, save.Id).Length);
            Assert.False(File.Exists(Path.Combine(path + ".manual", save.Id + ".save")));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static string[] CheckpointFiles(string activePath, string id) =>
        Directory.GetFiles(activePath + ".manual", "*.save")
            .Where(file => Path.GetFileName(file).Split('.')[0] == id).ToArray();
}

public sealed class WindowsSaveFactAttribute : FactAttribute
{
    public WindowsSaveFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires native Windows file-sharing semantics.";
    }
}
