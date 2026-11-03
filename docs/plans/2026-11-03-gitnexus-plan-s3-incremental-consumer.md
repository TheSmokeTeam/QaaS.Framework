# GitNexus Engineering Plan

> Task: S3 consumer incremental bounded reads only.
> Evidence verified at commit 11f3ec66126cf878dc34c4fab8c152ced521f3ea. depth:deep form:full freshness:strict.
> GitNexus: fresh implementation-worktree index refreshed this session using --index-only --pdg; 12,952 nodes,24,831 edges. Indexed at2026-11-03T06:37:52.616Z. Analyzer distribution1.6.10; CLI artifact SHA256982c8d790f26931ccc06c37075a42dfaaf7ce58650ed768f705221b874544813 matches prior index. No analyzer build performed.
> Evidence provenance schema2; exact generated plan path excluded; full helper snapshot in section11.

## 1. Objective

Remove full-bucket body buffering specifically from the S3 consumer, without changing existing configuration requirements, public data shapes, generic readers, Runner retention, generators or publishing. Add optional bounded concurrency. Deliver local package evidence, Docker before/after and reviewable PRs; no remote NuGet publication.

## 2. Current Behaviour

[verified] S3Protocol.ReadChunk (49-113) captures start time and waits at invocation, then body mode calls the shared eager bulk helper before filtering/order. S3Client (179-229) accumulates all byte arrays. Single-object method (150-176) already preserves bytes and disposes response/streams. Metadata-only branch skips GETs but currently defaults empty filtering inconsistently.
[verified] ReaderFactory (49-55) returns S3Protocol as IChunkReader. IChunkReader:10 exposes synchronous IEnumerable; change can preserve this signature.
[inferred] The prior actual Runner probe proves this patch cannot bound memory of a caller retaining every yielded body.

## 3. Relevant Architecture

[verified] Protocol ownership is Framework. Shared S3Client bulk method also serves generators; leave it unchanged. Consumer-specific selection and orchestration belong in S3Protocol. Keep body byte[] and DetailedData metadata/timestamp structure. Follow AGENTS/CLAUDE build/test/format instructions, and Protocols.Tests/CLAUDE mocked-transport boundary.

## 4. GitNexus Findings

[graph] impact(repo=qaas-s3-framework-latest,target=ReadChunk,file_path=.../S3Protocol.cs,direction=upstream,maxDepth=3,includeTests=true) reports7 direct dependents, MEDIUM/lower-bound, dispatchBoundary3. Exact same HEAD as target source; no claim of complete cross-repository coverage.
Direct dependents accounted for: ControlledTimeS3Protocol; tests ReadChunkAndSend_WorkWithInjectedClient, ReadChunk_DefaultsToStorageKeyOnly_AndDoesNotFetchHeaders, ReadChunk_FiltersObjectsFromRunStartTime_AndUsesGeneratedKeys, ReadChunk_PreservesBytesForMultipleDeserializers, ReadChunk_ReturnsNullBodyForEmptyObjects_WhenSkipEmptyObjectsDisabled, ReadChunk_SkipsObjectsWithoutLastModified. All reside in ProtocolAdvancedBehaviorTests and must remain valid with new fake single-object behavior. Graph's override label for ControlledTimeS3Protocol is not treated as a verified override of ReadChunk.
Seeded investigation already traced acquisition/copy/Runner retention; no repeated global exploration. Implementation index alias qaas-s3-implementation is fresh at the pinned commit. Runner identity matches the prior distribution artifact/build/dependency digests. Executor must validate index/build status before symbol edits; source evidence remains authoritative.

## 5. Statement-Level PDG Findings

[graph] Seeded old graph had no PDG layer. Strict refresh completed with gitnexus analyze . --index-only --pdg --name qaas-s3-implementation --workers 2 (84.8seconds). File-scoped controls returned44 edges (8shown) for S3Protocol, including header guard/control paths; the UID-shaped ReadChunk anchor resolved as a file with zero edges, so it is not reliable function evidence. No claim of missing dependencies or reconstructed edges is made. Central ReadChunk constraints below are source-derived.
[verified source-derived constraints] Read-start timestamp mutation precedes inactivity wait. Body filter gates GET. Current relevance predicate reads mutable _readStartTimeUtc. Snapshot filtered metadata at ReadChunk invocation so later calls/enumeration do not change its time selection. OrderBy must operate on metadata before GET, never byte-bearing outputs. Header retrieval remains opt-in.

## 6. Proposed Changes

[proposed] S3BucketReaderConfig: add MaxConcurrentDownloads int default1, Description/DefaultValue/Range(1,int.MaxValue); also reject nonpositive values at protocol entry for direct callers bypassing config validation. Default1 intentionally prioritizes lowest buffering; optional4 restores useful parallel throughput.
[proposed] S3Protocol.ReadChunk: preserve call-time start timestamp and inactivity wait, snapshot relevant config values/client and list/filter/order metadata once. Forward SkipEmptyObjects consistently. Return a private incremental iterator; no body GET during ReadChunk itself.
[proposed] Iterator fills fixed windows of at mostD candidates. D1 calls single-object path directly; D>1 uses Parallel.For with MaxDegreeOfParallelism=D and indexed result slots. Wait for entire window before yielding ordered results. Do not launch later windows until caller consumes current window. Clear yielded slots and all slots on iterator disposal/failure. No worker remains active across a yield, so early stopping has deterministic cleanup without new cancellation APIs. Empty objects yield null without GET; unreadable/null nonempty body retains legacy omission; exceptions propagate and no later window starts. Headers use existing normalization and opt-in semantics.
[inferred] This bounds reader-held bodies by window size plus current yielded reference and existing copy buffers, not strict bytes. A single huge object can still exceed RAM. Fixed windows trade head-of-line latency for simple ownership and exact shutdown; no promises about cancelling a blocking current GET.
[proposed] Existing test fakes gain single-object response behavior; new tests assert externally observable GET/HEAD counts and returned payloads rather than private-field values. Existing tests inject clients through reflection; reuse established injection helper only as needed, do not add public interface solely for tests.

## 7. Implementation Sequence

1. Verify plan receipt/provenance and current graph; add failing tests for deferred GET, bound/order, early stop and old-object filtering.
2. Add config property/default/validation; implement call-time metadata snapshot and ordered window iterator; preserve shared helper.
3. Update existing S3 fixtures from bulk-only responses to metadata+single-object responses; preserve send and serialization regression coverage.
4. Run focused Protocols tests then Framework build/full tests and format. Investigate failures, do not skip them.
5. Update appropriate hand-written docs guide in separate docs worktree, explicitly unreleased. Document repeated enumeration, partial errors, object-count bound, default1/opt4, and unchanged Runner retention.
6. Pack local uniquely versioned prerelease artifacts; run baseline and candidate Docker150/4500 byte-integrity checks under identical memory caps with degrees1/4. Capture package commit/version, memory/time, GET counts, outputs and exit states. Run retaining-reader control to demonstrate residual limitation.
7. Commit/push feature branches and open PRs without release tags; inspect CI publish conditions before pushing. Wait CI and required ten-minute auto-review, resolve actionable findings and rerun affected validation.

## 8. Test Strategy

[verified] ProtocolAdvancedBehaviorTests (51-100) fake single GET throws today; body tests at514-583,738-891 and895-943 populate bulk responses. Adapt them, do not inadvertently make empty-source tests pass vacuously. ProtocolConfigurationObjectsTests:123-136 checks existing defaults.
New focused scenarios are in section11. Use synchronization primitives for concurrency tests, not timing-only assertions. Add degrees1/4/>count, invalid0/negative, byte identity, same timestamp stable order, failed header read, iterator never started, Take1/dispose, repeated enumeration, config mutation after ReadChunk, missing timestamp and old-only bucket. Verify active worker count0 at each yield/disposal, and <=D total attempted body reads before first item. Mixed-size tests establish count bound, not claim byte guarantee. Existing helper tests continue protecting unchanged generators.
[verified] Build/test commands from CLAUDE and CI are dotnet build/test QaaS.Framework.sln; CI targets .NET10 Windows. Full baseline build already passed with warnings (parent evidence). Docker tests stay outside mocked unit project.

## 9. Risk and Impact Analysis

Account for every7 direct graph-dependent item in section4 through existing test fixture updates. Interface dispatch means Runner remains downstream even though not in graph caller list.
Intentional differences: body errors occur at enumeration, and prior successful windows may already be observed; each enumeration re-fetches bodies against the captured metadata snapshot instead of replaying cached bytes. Remote keys are not version-pinned. Stable metadata listing order resolves timestamp ties; old ConcurrentBag tie order was nondeterministic. Correct metadata-only SkipEmptyObjects forwarding is an adjacent directly touched behavior correction requiring explicit test/docs.
No active workers span yields, but current-window GET retries/timeouts retain existing semantics. Default1 may lower throughput. Very large configuredD may recreate memory pressure; allocate min(D,remaining metadata), validate positive. SDK/Runner data retention and publisher source caching remain out of scope.

## 10. Files Expected to Change

| File | Responsibility |
|---|---|
| QaaS.Framework.Protocols/Protocols/S3Protocol.cs | acquisition orchestration |
| QaaS.Framework.Protocols/ConfigurationObjects/S3/S3BucketReaderConfig.cs | optional degree |
| QaaS.Framework.Protocols.Tests/ProtocolsTests/ProtocolAdvancedBehaviorTests.cs | adapt fake/read regressions |
| QaaS.Framework.Protocols.Tests/ProtocolsTests/ProtocolConfigurationObjectsTests.cs | config validation/default |
| QaaS.Framework.Protocols.Tests/ProtocolsTests/S3IncrementalReadTests.cs | new focused tests |

Docs and lab artifacts are parent-coordinated separate worktrees, not changes to shared helpers/interfaces.

## 11. Reusable Implementation Context

```json
{
  "implementation_context": {
    "task_summary": "Only S3 consumer: metadata-first incremental ordered bounded body acquisition; no Runner retention changes",
    "acceptance_criteria": [
      "No body GET before returned sequence is enumerated",
      "Default sequential; configurable bounded windows",
      "Preserve raw bytes, metadata, ascending timestamp ordering",
      "No workers survive a yield or iterator disposal",
      "Shared S3Client bulk helper remains unchanged",
      "Local NuGet packaging only; Docker before/after 150 and4500; PR CI and ten-minute auto-review"
    ],
    "evidence_provenance": {
      "schema_version": 2,
      "head_commit": "11f3ec66126cf878dc34c4fab8c152ced521f3ea",
      "generated_plan_path": "docs/plans/2026-11-03-gitnexus-plan-s3-incremental-consumer.md",
      "global_dirty_digest": {
        "algorithm": "sha256",
        "canonicalization": "gitnexus-evidence-provenance-v2 NUL-framed UTF-8 records",
        "value": "0a9c85780067d9afcd0764f307b60891e3cee927ee11eaeb5ec7826d10fd82cd"
      },
      "cited_path_manifest": [
        {
          "path": ".github/workflows/ci.yml",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:2aa3a16e73f3453c0e5a4ea568a90b1b6416559ab069702de6c785eb59d99106",
          "index_digest": "sha256:2aa3a16e73f3453c0e5a4ea568a90b1b6416559ab069702de6c785eb59d99106",
          "worktree_digest": "sha256:bd5f4de3577da70718134ac6e8d46a9115a0b13f81ef1cb4009147303ac06a2d",
          "untracked_digest": "absent"
        },
        {
          "path": "AGENTS.md",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:4eb36416da9e7f55f370a53420588e9f80d87d60d3c547f3143997887d53512d",
          "index_digest": "sha256:4eb36416da9e7f55f370a53420588e9f80d87d60d3c547f3143997887d53512d",
          "worktree_digest": "sha256:708569b2da273f846532b41267fc7e11769d7f150af67a407d7d2aad5635bdfc",
          "untracked_digest": "absent"
        },
        {
          "path": "CLAUDE.md",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:a8a260993e6249c7d74273e85b424a14b7f3f8a7d6646a91a5d1de9156f00cd5",
          "index_digest": "sha256:a8a260993e6249c7d74273e85b424a14b7f3f8a7d6646a91a5d1de9156f00cd5",
          "worktree_digest": "sha256:5e16bc12f141dfa5a3d8bae4207e30437f341b375ba34e751bdde863f28b059e",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols.Tests/CLAUDE.md",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:16e1f8518bfc3cb585b1ca3242392dc745936583c2d557938de4cd98db3997e1",
          "index_digest": "sha256:16e1f8518bfc3cb585b1ca3242392dc745936583c2d557938de4cd98db3997e1",
          "worktree_digest": "sha256:7ac9de5b5b31a5d680e1cd9bfa71127d2405ac76f9d7fa3acd62b2519fd74cbc",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols.Tests/ProtocolsTests/ProtocolAdvancedBehaviorTests.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:d6a387a76cfc069491ebdcd12fa1135e7e76aca891b14f0cd7195b75cd494801",
          "index_digest": "sha256:d6a387a76cfc069491ebdcd12fa1135e7e76aca891b14f0cd7195b75cd494801",
          "worktree_digest": "sha256:c80938bc3fe3bebd081eda85d7d2e1811228636f717117f8a7db0dd46c587205",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols.Tests/ProtocolsTests/ProtocolConfigurationObjectsTests.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:e1bce7fec39515017c641c47e1a19820c4ffb55f59849680ee0d845b1cf2a430",
          "index_digest": "sha256:e1bce7fec39515017c641c47e1a19820c4ffb55f59849680ee0d845b1cf2a430",
          "worktree_digest": "sha256:bd300b37dd6c49239696c639b36c6ab02c7f636259f92c6ae208ae08c061d54a",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols/ConfigurationObjects/S3/S3BucketReaderConfig.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:9cb06dec44e876e9b6390aa88a7e4b7c3611c3e607a3d3c373ebd50377d84a8c",
          "index_digest": "sha256:9cb06dec44e876e9b6390aa88a7e4b7c3611c3e607a3d3c373ebd50377d84a8c",
          "worktree_digest": "sha256:67f5892e0cbb75ec10af0b43895a3e1febcd9a7651ab07831494b39c1190b852",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols/Protocols/Factories/ReaderFactory.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:084e5cfdeb8d05ed192a134f1e7b6287ee69663b3e1253edab232913b25ad7f2",
          "index_digest": "sha256:084e5cfdeb8d05ed192a134f1e7b6287ee69663b3e1253edab232913b25ad7f2",
          "worktree_digest": "sha256:4c788298b25638cd5c203b29dd861a960b8cd1a608031de5751866d7315b50ce",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols/Protocols/IChunkReader.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:c8426f3e9f59ba5eab5753a6b383ab4bdd3391bbfcee82a9bddb94465e6f1b5b",
          "index_digest": "sha256:c8426f3e9f59ba5eab5753a6b383ab4bdd3391bbfcee82a9bddb94465e6f1b5b",
          "worktree_digest": "sha256:4ff9e550cb6e85388d880cfb916eb195b187936186268fe9a800ea3be4893521",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols/Protocols/S3Protocol.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:92844e1522c0141174f43559b23d0312ac16fc52552501c6d398917e382bb94c",
          "index_digest": "sha256:92844e1522c0141174f43559b23d0312ac16fc52552501c6d398917e382bb94c",
          "worktree_digest": "sha256:182c329c70b35e262f3018d43378f0e5efb622aac0c1cbb5e524f7c7d4b8e38e",
          "untracked_digest": "absent"
        },
        {
          "path": "QaaS.Framework.Protocols/Utils/S3Utils/S3Client.cs",
          "object_kind": {
            "head": "regular",
            "index": "regular",
            "worktree": "regular",
            "untracked": "absent"
          },
          "state": "clean",
          "rename_from": null,
          "rename_to": null,
          "head_digest": "sha256:68d9264c833430c89128fbf77c4cb56c10fee5a52be61a33ef428657751d7135",
          "index_digest": "sha256:68d9264c833430c89128fbf77c4cb56c10fee5a52be61a33ef428657751d7135",
          "worktree_digest": "sha256:558c913377d8adec89760a721afa98d3731449eb03a12e69a113422acbd1a085",
          "untracked_digest": "absent"
        }
      ]
    },
    "primary_symbols": [
      {
        "symbol": "S3Protocol.ReadChunk",
        "file": "QaaS.Framework.Protocols/Protocols/S3Protocol.cs",
        "lines": "49-113",
        "role": "Replace consumer bulk body loading with metadata snapshot plus iterator"
      },
      {
        "symbol": "S3BucketReaderConfig",
        "file": "QaaS.Framework.Protocols/ConfigurationObjects/S3/S3BucketReaderConfig.cs",
        "lines": "6-62",
        "role": "Add positive MaxConcurrentDownloads default1"
      }
    ],
    "related_symbols": [
      {
        "symbol": "S3Client.GetObjectFromObjectMetadata",
        "relationship": "CALLS",
        "relevance": "Reuse existing single-object byte preserving retry/disposal path"
      },
      {
        "symbol": "ReaderFactory",
        "relationship": "factory",
        "relevance": "Existing S3 config routes to S3Protocol unchanged"
      },
      {
        "symbol": "ProtocolAdvancedBehaviorTests.FakeS3Client",
        "relationship": "test-of",
        "relevance": "Existing fake throws on single-object method; update fixtures"
      }
    ],
    "execution_path": [
      "ReadChunk captures config and start timestamp at invocation",
      "Wait inactivity and list/filter/sort metadata",
      "Return deferred body iterator",
      "Fill at mostD result slots with bounded Parallel.For (D1 direct)",
      "Wait complete window, yield in metadata order, release slots",
      "Dispose clears unyielded references; no active tasks remain"
    ],
    "pdg_constraints": [],
    "architectural_patterns": [
      {
        "pattern": "Sync IChunkReader contract",
        "example_location": "QaaS.Framework.Protocols/Protocols/IChunkReader.cs:10",
        "usage_guidance": "Keep existing public interface; no async contract expansion"
      }
    ],
    "files_to_modify": [
      {
        "file": "QaaS.Framework.Protocols/Protocols/S3Protocol.cs",
        "symbols": [
          "ReadChunk"
        ],
        "intended_change": "metadata-first incremental fixed-window enumeration"
      },
      {
        "file": "QaaS.Framework.Protocols/ConfigurationObjects/S3/S3BucketReaderConfig.cs",
        "symbols": [
          "S3BucketReaderConfig"
        ],
        "intended_change": "positive download degree default1"
      },
      {
        "file": "QaaS.Framework.Protocols.Tests/ProtocolsTests/ProtocolAdvancedBehaviorTests.cs",
        "symbols": [
          "FakeS3Client"
        ],
        "intended_change": "adapt existing fixtures to single-object calls; retain regression coverage"
      },
      {
        "file": "QaaS.Framework.Protocols.Tests/ProtocolsTests/ProtocolConfigurationObjectsTests.cs",
        "symbols": [
          "S3BucketReaderConfig_DefaultsKeepLegacyReadBehavior"
        ],
        "intended_change": "assert default and invalid degrees"
      },
      {
        "file": "QaaS.Framework.Protocols.Tests/ProtocolsTests/S3IncrementalReadTests.cs",
        "symbols": [],
        "intended_change": "new focused acquisition/ordering/disposal/failure tests"
      }
    ],
    "tests": [
      {
        "file": "QaaS.Framework.Protocols.Tests/ProtocolsTests/S3IncrementalReadTests.cs",
        "scenarios": [
          "Create sequence -> zero GETs; first MoveNext default -> exactly one GET",
          "D4 with slow earlier object -> <=4 active GETs; ordered output; next window only after current consumed",
          "Take1 and dispose -> no future window GETs; zero active workers",
          "Old or missing timestamp -> zero corresponding GETs",
          "Empty object with skip false -> null body noGET; skip true -> absent",
          "Bodyfalse -> zero GETs, header flag controls HEAD",
          "Failure in window -> propagated, all workers complete, no later windows; previous windows may already yielded",
          "Repeated enumeration -> same metadata snapshot, repeated GETs",
          "Mutate config after ReadChunk -> original call-time selection/degree used",
          "Distinct binary bodies survive timestamps/pagination; null/unreadable single-object results match legacy omission"
        ]
      }
    ],
    "verification_commands": [
      "dotnet build QaaS.Framework.sln --nologo -clp:ErrorsOnly",
      "dotnet test QaaS.Framework.sln --nologo --no-build",
      "dotnet format QaaS.Framework.sln --no-restore",
      "csharpier format <changed-files>"
    ],
    "risks": [
      "Runner still retains bodies; no bounded full-run claim",
      "Window size is object-count bound, not strict byte budget",
      "Partial failure timing and repeated enumeration change",
      "Existing synchronous GET cannot be hard-cancelled; no cancellation promise",
      "Default1 trades throughput for minimal transient buffering"
    ],
    "assumptions": [
      "Existing baseline worktree clean at pinned commit; executor verify provenance before edit",
      "No requirement for immutable captured remote bytes across separate enumerations"
    ],
    "open_questions": [
      "Exact degree upper bound: proposed Range1..int.MaxValue; avoid allocating D when fewer metadata items remain"
    ],
    "avoid": [
      "Do not repeat full discovery",
      "Do not modify shared S3Client bulk helper or IS3Client",
      "Do not add disk storage, lazy Body SDK contract or generic consumer parallelism",
      "Do not publish remote NuGet packages or create release tags",
      "Do not claim protocol streaming fixes Runner retention",
      "Do not extend scope to adjacent retry/disposal defects"
    ]
  }
}
```

## 12. Assumptions and Open Questions

[assumed] Consumers accept re-fetch on repeated enumeration and partial progress before later failure; this is documented/tested, not hidden. Current configs continue to work with default1. No strict byte budget or hard cancellation is introduced.
Freshness gate passed: target-worktree graph and PDG refresh completed. Function-scoped ReadChunk PDG query did not resolve usefully; central constraints remain source-verified rather than fabricated PDG edges. No analyzer source build allowed in planning. Deferred: body-store retention, generator cleanup bug, generic retry overhaul, publisher parallelism, async reader interface, memory pooling, versioned snapshots and release/mirror publication.

## 13. Definition of Done

GET laziness, bounded concurrency, ordered exact bytes and cleanup verified by focused tests; existing S3 and full Framework suite green (environment-dependent skip disclosed); docs precise; local package Docker before/after150/4500 recorded; retaining control not misrepresented as fixed; feature PRs CI green; ten-minute automatic review addressed; no remote NuGet publication or release tags.
