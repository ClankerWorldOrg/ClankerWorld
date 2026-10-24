# PR #2798 evidence

Author: Codex session 33837055. Base `835f9db1155bfb03fb741b2d5bcde9de50031316`; head `5fab225ed24ac117a2fbd0654cbe849f3acdc7dc`.

Source +25/-209, 184 net removed, 42 files. Tests/docs unchanged. Remove 30 uncalled private animal facade forwarders, unused EnvironmentSystem.AdvanceWorldSystemsTo and FarmingSystem.PlantableCrops. Move the 19 retained animal facade methods unchanged into WorldRuntime.Animals.cs; delete their six now-empty partial files. Remove 80 unused imports in unheld files.

Actual compiler inputs: 1,047 source files, 167 references, C#14/actual constants, one actual emitted RegexGenerator source, zero errors. Full source-symbol/candidate/string scan finds no references to the selected 32 definitions. Interface implementations, attributes, virtual overrides, reflection/test roots and all identifiers in the 16 waiting PR patches are held. All 32 definitions exist in baseline simulation IL with zero incoming call/ldftn/InlineTok references; actual server/test assemblies also have zero references. No changed-file overlap with the 16 open PR file maps.

Retained declaration proof groups partial declarations without dropping duplicate entries. The initial auxiliary inventory rejected an existing duplicate partial method signature (CivicTickText); the corrected proof compares sorted arrays per signature, preserving each declaration and its exact source. There are 9,116 retained method signature groups, zero changed retained method declarations, zero additions, exactly 32 expected removals, and 4,648 unchanged field/property/constructor/operator/event declarations. All six moved partials have no fields, initializers, attributes, base clauses or directives. No CallerFilePath/CallerLineNumber use or reflection enumeration of animal methods exists in simulation code; the sole production GetMethod call reads a stack frame for action-failure reporting. Names/signatures and method text remain unchanged.

Baseline locked Release passed with zero warnings/errors. Native clean baseline/head comparison passed in 85.21s: 68 frames, two seeds, generated/legacy modes, 16 ticks plus initial frame each; strict checkpoint bytes (including map/generation state), events and observation digests match. Both run.json receipts name the actual commits and empty working trees. The actual candidate simulation DLL/PDB were pinned before the dependent build for byte comparison with the actual test copies.

Focused execution passed: 276 cases, 137 distinct methods across 20 classes (18 whole; partial AnimalPipelineTests and GrownAgentHelperMemoryTests), zero failures/skips. Raw TRX set exactly equals the planned FQNs. Full locked dependent Release passed with zero warnings/errors in 62.23s; full root format136.13s and verify145.14s passed without changes. Tests324.81s. The first auxiliary compiled inventory saw six RegexGenerator nested-runner signatures renamed solely by its file-local hash (baseline explicitly emitted source path versus normal generator path). Separate diagnosis compares all six signatures after only F+64hex file-local hash normalization, with count/collision guards; no methods or fields are excluded. Corrected inventory passes: exactly32 intended user methods removed, zero added, all nongenerated state fields unchanged, total method bodies50166->50133 (one unused generated lambda also disappears). Initial failed receipt and diagnosis are retained; product checks were not repeated. Final actual simulation SHA256 `30460ee2bf35be71021c22b7994e8cb2324e82526ec04819d4464c9b5aae7a1f`; native DLL/PDB byte-equal the full-build actual test copies. Source tree remains clean at5fab225e.

CI40055105382: actual scope and checks checkout `d0a80d4378ac9d173e93f56614447ff932bcd23d`, merging5fab225e into835f9db. All eight Release shards, main checks, Windows documentation18cases and protected-storage/recovery98cases passed. Timing/installer/final gates pending. This local evidence is not yet a Ready handoff.

Area: original/main 10,096 lines; head 9,912. The 20% goal requires 2,020 removed; this slice removes 184 and leaves 1,836. Refs #2555. Protected #2296 is deliberately untouched at the owner's request.


Archives:

- [pr2798-source.zip](pr2798-source.zip): 627785 bytes, 180 files, SHA256 `27831938e9a049c1dd5245837cf7902a7715f0b207e27d2625e6149727d5dfaa`.

- [pr2798-binaries.zip](pr2798-binaries.zip): 8880471 bytes, 6 files, SHA256 `46592529478246a1c8ce5db35b6d9b1ea478f4e4fbe0fb46b25e9e5a4df61c0a`.

- [pr2798-native.zip](pr2798-native.zip): 12950353 bytes, 413 files, SHA256 `b56993b098fef269126ca7d4ea7addedbb42d74081d77d4f17d9e5220945d1de`.
