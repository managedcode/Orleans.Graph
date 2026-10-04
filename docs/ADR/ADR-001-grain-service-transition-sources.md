# ADR-001: explicit GrainService transition sources

Status: Accepted for implementation; owning package qualification and publication pending.

## Context

The Graph fluent builder currently constrains transition sources to `IGrain`. Orleans `GrainService` is a native per-silo system target, not an application grain, so the current API cannot express a narrowly permitted service-to-application call. Making the coordinator client-callable or fabricating Graph context would broaden the security boundary.

## Decision

Add one fluent builder method constrained to the real `Orleans.Runtime.GrainService` base and an `IGrain` destination. It records the concrete service implementation type to the destination type, with `Constants.AnyMethod` as the explicit service callback source method and only the caller-supplied destination method names. Validate the entire array before adding any edge. Keep all existing builder/runtime behavior unchanged.

## Implementation and verification

- REQ/AC: `docs/Features/GrainServiceTransitions.md`, REQ-SGRAPH-001..003 and AC-SGRAPH-001..003.
- Implementation owner: ManagedCode.Orleans.Graph; only the existing `IGrainCallsBuilder.cs` and `GrainCallsBuilder.cs` surfaces change.
- Regression owner: ManagedCode.Orleans.Graph.Tests; add focused argument/graph-rule tests and a real Orleans TestCluster GrainService callback test. The test allows one target method, denies another, observes concrete source identity, proves a subsequent allowed call, keeps direct client entry denied, and joins service shutdown.
- No serializer, wire, caller resolution, client authorization, storage or KeyLoad runtime contract changes.
- The dependency owner runs and retains its required build, formatter, TUnit, release, CI, publication and feed-availability evidence. KeyLoad changes its package reference only after the published version is verified.

Rollback leaves existing transition configuration intact and removes only the additive service-source rule and API from a later package release; autonomous KeyLoad service registration remains disabled until the package is available.
