# Repository Guidelines

## Overview
ManagedCode.Orleans.Graph enforces grain-to-grain call policies for Microsoft Orleans applications. The core library models allowed transitions as a directed graph and validates runtime call chains, while the accompanying test project spins up an Orleans test cluster to exercise real communication flows.

# Conversations

any resulting updates to agents.md should go under the section "## Rules to follow"
When you see a convincing argument from me on how to solve or do something. add a summary for this in agents.md. so you learn what I want over time.
If I say any of the following point, you do this: add the context to agents.md, and associate this with a specific type of task.
if I say "never do x" in some way.
if I say "always do x" in some way.
if I say "the process is x" in some way.
If I tell you to remember something, you do the same, update
if I say "do/don’t", define a process, or confirms success/failure, add a concise rule tied to the relevant task type.
if I say "always/never X", "prefer X over Y", "I like/dislike X", or "remember this", update this file.
When a mistake is corrected, capture the new rule and remove obsolete guidance.
When a workflow is defined or refined, document it here.
Strong negative language indicates a critical mistake; add an emphatic rule immediately.

Update guidelines:
- Actionable rules tied to task types.
- Capture why, not just what.
- One clear instruction per bullet.
- Group related rules.
- Remove obsolete rules entirely.

---
## Rules to Follow
- **Build & Tests**: Always run `dotnet format` before `dotnet test` to keep Roslyn analyzers and CI results consistent.
- **Graph Transitions**: When extending the fluent builders, preserve existing transition sets—never clear prior rules when adding new ones—so multiple method constraints remain effective.
- **Call Tracking**: Resolve outgoing call identities from the Orleans context (`SourceId`/interface metadata) instead of guessing via reflection order to avoid mislabelling callers.
- **Configuration Flags**: Treat `AllowAll`/`DisallowAll` on `GrainCallsBuilder` as authoritative defaults; ensure runtime checks respect the builder’s chosen baseline before reporting violations.
- **Runtime Graph Telemetry**: For live graph features, have filters report observed calls to stateless worker aggregators that periodically flush to an in-memory grain; do not rely on per-request `CallHistory` alone for a global runtime graph.
- **Runtime Graph Identity**: Live graph nodes must never silently fall back to the Orleans base `Grain` type or any guessed identity; use a concrete grain implementation class or a real grain interface, and use the explicit `UNKNOWN_CALLER` vertex when Orleans exposes neither.
- **Type Identity Literals**: Do not hardcode framework type identity strings such as Orleans interface full names; resolve them from `typeof(...).FullName` or an equivalent type-safe API so renames stay correct.
- **Runtime Graph API Shape**: Expose live telemetry as a graph model with explicit `Vertices` and `Edges`; Mermaid output is only a renderer for that same graph, while methods/counts/timestamps belong on edges.
- **No Silent Fallbacks**: In call tracking and runtime graph code, never substitute wildcard methods, base `Grain`, reflection guesses, or history scans when exact caller identity is required; `UNKNOWN_CALLER` is the only allowed unresolved caller marker.
- **Filter Hot Path**: Keep Orleans call filters O(1) on request context data; avoid reflection, interface scanning, or call-history searching in the hot path so graph tracking stays cheap.
- **Telemetry Filtering**: Do not track Orleans.Graph internal telemetry calls by default; expose a configuration switch to include them, and test both filtered and full-tracking modes.
- **Testing Scope**: Exercise new behavior through the Orleans-hosted integration tests in `ManagedCode.Orleans.Graph.Tests`, covering both positive and negative paths to mirror real cluster flows.
- **Cluster-to-Cluster Tests**: For grain-only runtime graph scenarios, add coverage that starts from a silo-side `IGrainFactory` instead of `IClusterClient`, so client-origin edges cannot hide cluster-to-cluster behavior.
- **Activation-Origin Call Tests**: When fixing caller resolution for grain work that can run outside an incoming request, cover `RegisterGrainTimer`, reminder callbacks, and stateless worker activations because they exercise distinct Orleans entry points.
- **Test Framework**: Use TUnit for tests and Shouldly for assertions; do not introduce FluentAssertions, so the test style stays aligned with the newer ManagedCode Orleans projects.
- **Migration Releases**: For major framework/package migrations, complete three strict code-review-and-fix iterations before README polish, feature additions, commit, push, and CI verification, so release branches are hardened before publication.
- **Code Style**: Use enums or constants over magic literals, keep documentation and comments in English, and avoid template placeholders—name files and types for their real domain roles.





## Solution Layout
- `ManagedCode.Orleans.Graph/` – library source with graph primitives under `Common/` and `Models/`, builder DSL in `Builder/`, Orleans filters in `Filters/`, and registration extensions in `Extensions/`.
- `ManagedCode.Orleans.Graph.Tests/` – xUnit suites backed by an Orleans TestCluster (`Cluster/`) that validate transition policies, deadlock detection, and builder behavior end-to-end.
- `Directory.Build.props` – shared project metadata, target framework, and packaging settings.

## Current Status
The directed-graph engine, grain call filters, and builder DSL compile against .NET 10 / Orleans 10 and are validated by TUnit unit and integration tests. Recent fixes ensure additive transition rules, consistent caller identification, respect for `AllowAll` defaults, and reentrant policy-cycle handling; coverage now guards against regressions in these areas.

## Next Steps
- Expand test fixtures to cover mixed client/grain scenarios and additional reentrancy configurations.
- Document recommended upgrade paths for consumers migrating from older Orleans Graph releases.
- Monitor GitHub Actions outcomes (CI, CodeQL, Release) to confirm the new pipelines remain green after future contributions.
