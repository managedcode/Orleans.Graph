# Native asynchronous-enumeration policy

ManagedCode.Orleans.Graph applies the same caller and method transition rules to native Orleans `IAsyncEnumerable<T>` methods as to ordinary grain calls. The adapter decorates only the original `IAsyncEnumerableRequest<T>` argument supplied to `IAsyncEnumerableGrainExtension.StartEnumeration`; it does not replace Orleans' extension, scheduler, enumerator table, cancellation, batching, expiration, or disposal.

At graph registration, the configured grain-interface inventory builds a finite immutable catalog keyed by the original generated method. Generic wrapper factories are closed once for each registered item type. Client and silo outgoing filters check the original method metadata. The silo incoming filter checks that method before replacing argument 1 with a delegating request. An unknown or open-generic method is rejected before its producer can run.

The request adapter delegates the complete Orleans request contract and `MaxBatchSize`. It scopes the original caller and one forked `CallHistory` branch around implementation invocation, enumerator creation, every pull, and enumerator disposal, restoring the prior `RequestContext` values in `finally`. Each stream owns its branch; child grain calls continue through the regular outgoing/incoming filters. Native system `MoveNext` and `DisposeAsync` RPCs are not represented as extra application graph transitions.

Use a closed, registered grain-interface method which returns exactly `IAsyncEnumerable<T>`. Open-generic stream methods are not catalogued and fail closed. Call policy on a stream remains separate from any application authorization policy.


## Acceptance coverage

| Requirement | Acceptance | Owning evidence |
|---|---|---|
| REQ-GSE-001 | Client and silo callers use the original closed stream method for allow/deny decisions; allowed nested calls after the first and a later pull retain exact caller, edge methods, and native grain identities. | `NativeAsyncEnumerationPolicyTests`, `NativeAsyncEnumerationContextTests` |
| REQ-GSE-002 | Actual stream context survives suspension and is restored after nested errors, cancellation, and early disposal; distinct streams retain separate history branches. | `NativeAsyncEnumerationContextTests`, `NativeAsyncEnumerationLifetimeTests` |
| REQ-GSE-003 | The adapter delegates native request metadata, arguments, options, and batch size; startup catalog membership is finite and unknown/open methods fail closed. | `NativeAsyncEnumerationCatalogTests`, native stream policy/lifetime tests |

These tests exercise the owning Orleans dependency in a real TestCluster. Their source presence is not release or runtime qualification; formatter, full build, full TUnit suite, publication, and KeyLoad Aspire gates remain required.
