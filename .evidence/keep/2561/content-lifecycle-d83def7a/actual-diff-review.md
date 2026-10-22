# Actual content-lifecycle diff check

Codex session d83def7a/content-reader. Read-only actual-diff check at base63e5970fd131a341d9ae86cc26fa6c43fac327bf in /workspace/ClankerWorld-2561-content, branch codex/2561-content-lifecycle-d83def7a. Reviewed both complete head files against git show of the base, the full two-file diff (+19/-32, net13 fewer lines) and ledger.json. This reader previously recommended the consolidation and is NOT an independent merge reviewer.

Result: no behavioral/assertion/input/lifetime blocker identified. Proceed to the parent's required build, discovery, unused-support check, scoped coverage, formatter and CI checks. This static review does not claim any of those checks passed.

## Rollback exactness

ContentRollbackSafetyTests.cs is byte-for-byte the expected base after only these substitutions: sole-row Theory attributes to Facts; remove stage/completed parameters; replace the sole stage input and conditional work value by completed/10; delete the unreachable completed=true block. A read-only text comparison confirmed this exact substitution identity.

- RollbackPreservesRecordedProjectReferences retains seed rollback-project; deterministic chooseIdle providers; MaterialPackage(2); actual activation and one initial world tick; actor selection and project candidate/display/start/transition fields; default restore; Pause; rollback exception; identical pre/post encoded state. Its only original input completed yields WorkDone10 exactly.
- RollbackWithCommittedProductionReferencesRejectsWithoutDeletingWorldState retains the sole original false behavior. The removed two setup ticks never ran for that row. WorldWithRollbackJob is unchanged, including rollback-safety seed, both activated packages, actual initial tick, movement/site swaps, deterministic idle restore, paid workshop placement and real started job/JobId assertions. Pause, migration diagnostic, no mutation, strict decode/restore/encode, Resume and both later completion ticks/assertion remain in exactly the same scopes and order.
- WithdrawingUnusedPackageDoesNotCancelOtherPackagesWork and the complete helper body remain byte-identical.

No cases/declarations removed: these source files still contribute three rollback cases/three methods and five recipe cases/three methods. These are static counts, not TRX/discovery output. Entire WorldRuntimeTests partial class remains mandatory in the parent's comparison filter.

## Recipe serialization exactness

RecipeOutputAdmissionTests.cs public tests/attributes/body prefix is byte-identical to base. CheckHostContinuation and IdleChoices tail is byte-identical. Therefore both water-output rows at both entry points, wood control, every assertion, all bounded four-tick/replay loops, native job shape, provider identity/metadata, Offered list, paused-tick rollback, host-save continuation, two host ticks, directory finally cleanup and all using lifetimes remain untouched.

Activated retains exact invocation/evaluation sites:

1. Construct make-output recipe with the original digest/localId/displayName/inputwood3/outputs/duration2/workstation/tags/version.
2. Immediately call Definition(recipe), where the original first ContentDefinition construction occurred.
3. Construct control-output with the same original fixed wood1 output.
4. Immediately call Definition(control), where the original second serializer occurred.
5. Construct the same manifest, dependencies and definition order; execute the same native lifecycle and encoded reload proof.

Definition's target-typed new returns ContentDefinition, the exact original constructor type. Kind, LocalId, DisplayName and PayloadDigest remain identical. recipe.Version is the readonly ContentVersion value assigned directly from the same local version in each RecipeDefinition constructor, so replacing the local argument with that getter changes no value or reference contract. No capture, options, defaults, caches, static initializer or provider is added.

The anonymous JSON payload has the exact same property names, types and order at both sites: schema literal recipe/v1; Inputs; Outputs; DurationTicks; WorkstationBuildingId; Tags. JsonSerializerOptions.Web is unchanged. Both original anonymous types already shared this shape. Argument/property evaluation order inside ContentDefinition construction is retained. Neither constructor/property has a side effect depending on the removed local variable name.

## Frozen source fingerprints

SHA256 of reviewed head source:

- ContentRollbackSafetyTests.cs: b010eaa6eb031eb02513b8f6cde5da078f6bb402983c5ef2f50828dc57a8811d
- RecipeOutputAdmissionTests.cs: 04660cf17834cdc006398faebaa505d885b23e46fdff94f5cdf63e3446259d90

No tracked edits, tests/builds, GitHub changes or claims performed by this reader. Only this authorized evidence file was written.

Signed: Codex session d83def7a/content-reader; same-session actual-diff second reader, not independent merge review.
