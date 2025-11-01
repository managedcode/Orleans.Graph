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
- **Testing Scope**: Exercise new behavior through the Orleans-hosted integration tests in `ManagedCode.Orleans.Graph.Tests`, covering both positive and negative paths to mirror real cluster flows.
- **Code Style**: Use enums or constants over magic literals, keep documentation and comments in English, and avoid template placeholders—name files and types for their real domain roles.





## Solution Layout
- `ManagedCode.Orleans.Graph/` – library source with graph primitives under `Common/` and `Models/`, builder DSL in `Builder/`, Orleans filters in `Filters/`, and registration extensions in `Extensions/`.
- `ManagedCode.Orleans.Graph.Tests/` – xUnit suites backed by an Orleans TestCluster (`Cluster/`) that validate transition policies, deadlock detection, and builder behavior end-to-end.
- `Directory.Build.props` – shared project metadata, target framework, and packaging settings.

## Current Status
The directed-graph engine, grain call filters, and builder DSL compile against .NET 9 and are validated by unit and integration tests. Recent fixes ensure additive transition rules, consistent caller identification, and respect for `AllowAll` defaults; coverage now guards against regressions in these areas.

## Next Steps
- Expand test fixtures to cover mixed client/grain scenarios and additional reentrancy configurations.
- Document recommended upgrade paths for consumers migrating from older Orleans Graph releases.
- Monitor GitHub Actions outcomes (CI, CodeQL, Release) to confirm the new pipelines remain green after future contributions.
