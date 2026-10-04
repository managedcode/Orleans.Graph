# GrainService transitions

This feature lets an Orleans `GrainService` make narrowly configured calls to application grains through the existing Graph filters. It adds startup configuration only; runtime caller resolution, Orleans service lifecycle, call-history propagation and existing transitions remain unchanged.

## Requirements and acceptance

| Requirement | Acceptance |
|---|---|
| REQ-SGRAPH-001: native service source policy | AC-SGRAPH-001: `IGrainCallsBuilder.AddGrainServiceTransition<TService,TGrain>(params string[] targetMethods)` accepts an actual `Orleans.Runtime.GrainService` type and an `IGrain` target. Each configured transition uses the concrete service type name, `Constants.AnyMethod` as its source method, and one exact supplied target method. All input is validated before any transition is added. Existing builder rules remain present. |
| REQ-SGRAPH-002: service-origin enforcement | AC-SGRAPH-002: a real Orleans-hosted service callback outside a client request can call the configured target method; a different target method is rejected before its body executes; a later allowed call still succeeds. The denial identifies the concrete service implementation as source. A direct client call to the same target remains denied. The native service stop is joined. |
| REQ-SGRAPH-003: published dependency ownership | AC-SGRAPH-003: source and focused builder/native-hosted regressions are maintained in ManagedCode.Orleans.Graph, pass the owning repository's required gates and canonical release, and the package is available from the intended feed before KeyLoad consumes the API. |

## Scope and verification

Owned implementation paths are `ManagedCode.Orleans.Graph/Builder/Interfaces/IGrainCallsBuilder.cs` and `ManagedCode.Orleans.Graph/Builder/GrainCallsBuilder.cs`. Tests register the package's native Graph filters on both the real silos and the real client, as in the existing hosted fixture; no client transition is granted. Tests use the package's TUnit/Shouldly and native `TestCluster` with an actual `GrainService`; they do not construct Graph call history to stand in for runtime enforcement. The downstream KeyLoad RF3 test is separate evidence and remains owned by KeyLoad.

The fluent method is additive. Empty arrays, null arrays, null entries and blank entries are rejected before mutation, including a list with valid entries before an invalid entry. No existing graph transition is cleared or broadened. No client edge, global `AllowAll`, caller-context fabrication, serialization alias, or runtime resolution change is part of this feature.
