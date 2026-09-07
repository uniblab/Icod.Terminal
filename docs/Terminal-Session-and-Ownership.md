# Terminal Session and Ownership

This document defines the permanent 1.x ownership contract for `TerminalSession`.

A terminal session is not just a pair of streams. It is an owner of a live terminal conversation and of the reversible state transitions it performs on behalf of its callers.

## 1. What a session owns

A successfully opened `TerminalSession` owns responsibility for the session-level state it changes, including as applicable:

- the configured input mode and echo policy;
- host output setup performed for the session;
- session-managed rich-input protocol state;
- session-managed presentation state;
- session-managed scoped terminal state such as color/progress/pointer ownership;
- active query transactions and their response-correlation lifetime;
- lifecycle release/re-entry of state owned by the session;
- deterministic restoration/cleanup at disposal.

“Owns” means the session is responsible for making transitions truthful and for restoring the relevant prior/baseline state according to each contract. It does not mean the session owns the lifetime of every object supplied to it.

## 2. What a session borrows

The custom open overload accepts:

```csharp
TerminalSession.OpenAsync(
	ITerminalControlProvider controlProvider,
	TerminalEndpoint inputEndpoint,
	TerminalEndpoint outputEndpoint,
	ITerminalInput input,
	ITerminalOutput output,
	TerminalSessionOptions? options = null,
	CancellationToken cancellationToken = default
)
```

The provider, endpoints, input service, and output service are borrowed.

The session does not dispose or close those caller-supplied objects merely because `TerminalSession.DisposeAsync()` completes. Their lifetime remains with the caller.

The convenience `OpenAsync(options, cancellationToken)` uses process standard input/output through the same ownership model: disposing the session restores session-owned terminal state; it does not mean “close standard input/output for the process.”

## 3. Endpoint identity and observation

`InputEndpoint` and `OutputEndpoint` identify the native terminal-control endpoints associated with the session.

`InputObservation` and `OutputObservation` are the attachment/platform/capability observations captured when the session opens. They describe what was observed; they are not mutable handles to terminal state.

Input must be an interactive terminal because the session captures and owns an input-mode transition.

Output is interactive by default. A caller may explicitly allow redirected/non-interactive output with `RequireInteractiveOutput = false`; in that case `IsInteractive` remains truthful and reports `false` because both endpoints are not interactive terminals.

The selected `Terminal` capability description and `Identity` are session properties derived during opening. Terminal capability identity and native endpoint identity are related but distinct concepts.

## 4. Session options are open-time policy

`TerminalSessionOptions` configures session construction and live policy.

Its scalar configuration properties are init-only. `TerminalInputDecoderOptions` is likewise init-only. `ApplicationEncoding` is cloned for the session's live application-text encoding.

The public `Options` property reports the options object used to open the session. Consumers should treat it as configuration/context, not as a live mutation API.

Changes to terminal state after opening must use the appropriate session operation or lease rather than attempting to “edit the options.”

## 5. One authoritative input owner

A live session owns the only authoritative reader over its input transport.

The public input path is:

```csharp
await session.ReadEventAsync(...)
```

and the typed query methods that share the same query/decoder machinery.

`ITerminalInput` remains public because custom hosts must be able to supply an input transport when opening a session. However, `TerminalSession` does **not** expose a public `Input` transport property in the 1.0 contract.

This distinction is fundamental.

A competing raw read could consume bytes that belong to:

- a UTF-8 scalar split across reads;
- an escape/control sequence split across reads;
- an active query response;
- a paste frame;
- a mouse/focus report;
- a modern keyboard frame;
- ordinary input buffered around correlated response traffic.

Because the session cannot observe bytes consumed by another reader, no locking scheme inside `Icod.Terminal` can repair a competing raw reader. Code migrating from pre-1.0 `session.Input` must use `ReadEventAsync(...)` or typed query APIs instead.

A caller that supplied an `ITerminalInput` may retain its original reference for its own lifetime management, but must not read from the same transport concurrently while the session owns the terminal conversation.

## 6. Output ownership and the advanced raw transport

Session-managed output should normally use `TerminalSession` methods.

Examples include:

- `WriteTextAsync(...)`;
- `WriteTerminalStringAsync(...)` for already-resolved terminfo strings;
- `WriteCapabilityAsync(...)`;
- semantic title/location/hyperlink/clipboard/progress/pointer/prompt/color operations;
- typed query request traffic;
- presentation and rich-input state transitions.

These operations participate in the session's appropriate output serialization/ownership rules.

`TerminalSession.Output` remains public as an **advanced borrowed transport escape hatch**. It is intentionally different from the input side.

Direct operations on `session.Output`:

- are not automatically serialized with `WriteTextAsync(...)`;
- are not automatically serialized with semantic output operations;
- are not automatically serialized with query request traffic;
- are not automatically serialized with lifecycle/presentation/input-protocol control output;
- can therefore interleave with session-managed output if the caller uses them concurrently.

The caller is responsible for such direct transport use. Ordinary application code should prefer session-managed operations.

The session may itself flush the borrowed output during lifecycle/disposal because flushing is part of restoring/handing off the live terminal conversation; that does not transfer lifetime ownership of the transport.

## 7. Application text vs terminal protocol strings

`WriteTextAsync(...)` emits application text using the session's application encoding.

`WriteTerminalStringAsync(...)` has a different role: it emits an already-resolved terminfo terminal string using the terminal/capability output path, including terminfo padding semantics. It exists so capability-driven renderers such as `Icod.DCurses` can use resolved terminal capabilities without bypassing the session.

`WriteTerminalStringAsync(...)` is not a recommendation to construct arbitrary OSC/CSI/vendor control strings manually. Where `Icod.Terminal` has a semantic public operation, consumers should prefer the semantic API.

## 8. Reversible state uses leases

When terminal state may have overlapping logical owners, the public contract generally uses leases.

Examples include:

- presentation state;
- rich-input protocol state;
- synchronized output/progress/pointer state where defined;
- scoped terminal color ownership.

The general lease model is:

1. acquisition validates that the requested state can be supported/restored according to its contract;
2. several logical leases may reconcile to one physical desired state;
3. disposing a lease removes that logical request;
4. the physical state is transitioned to the remaining desired state;
5. last-owner release restores the appropriate baseline/prior state;
6. session disposal remains authoritative if an individual lease is abandoned;
7. stale lease disposal after owner-driven cleanup is safe/idempotent where that contract promises it.

A lease means logical ownership, not proof that no external actor can mutate the terminal behind the library.

## 9. State validity and external mutation

`IsStateValid` reports whether the session currently trusts its own applied-state assumptions.

External terminal activity can make those assumptions stale. `InvalidateState()` explicitly marks state untrustworthy without inventing a restoration or reapplication.

Managed lifecycle handling invalidates and re-enters owned state automatically where supported.

Invalidation does not mean every logical lease disappears. It means the library must not treat previous believed physical state as authoritative until the owning contract has re-established or otherwise reconciled it.

## 10. Acquiring state during lifecycle or teardown

New public presentation/rich-input state ownership is rejected when lifecycle has released terminal state or session teardown has begun.

The permanent ordering is:

```text
state composition
    -> lifecycle/teardown availability
        -> manager
            -> control output
```

This prevents a new lease from entering after lifecycle has already begun handing the terminal back to the host.

Cleanup paths are intentionally different: existing lease release, rollback, lifecycle re-entry, and final manager close must retain authority to emit the control output required for restoration even after ordinary new application output/acquisition has stopped.

## 11. Disposal is owner-driven cleanup

`TerminalSession.DisposeAsync()` is the final owner-driven cleanup boundary.

Disposal:

- stops accepting new ordinary session output/state acquisition;
- closes active query transactions;
- stops lifecycle processing;
- restores/closes session-owned rich-input state;
- restores/closes session-owned presentation state;
- flushes output as part of final restoration;
- releases session-owned output setup;
- restores the captured baseline terminal mode when required;
- aggregates multiple restoration failures rather than silently discarding them.

Disposal is idempotent at the underlying restoration boundary: the session does not intentionally apply the baseline repeatedly merely because cleanup is requested more than once.

The session does not dispose caller-owned input/output/provider objects.

## 12. Exact restoration vs terminal policy reset

An ownership contract that promises restoration uses captured or observed prior state where that state can be obtained and restored truthfully.

A terminal “reset to policy/default” command is not automatically equivalent to restoring the state that existed before the library changed it.

This distinction is especially important for:

- native terminal mode snapshots;
- scoped terminal colors;
- presentation state;
- negotiated input protocol state.

Where exact prior state cannot be observed/restored, the API must either use a different explicitly documented policy contract or decline to claim reversible ownership.

## 13. Multiple sessions and one physical terminal

`Icod.Terminal` is instance-based and permits multiple session objects.

That does **not** imply that two independent sessions may safely own the same physical terminal conversation simultaneously.

The library coordinates state within one session. It does not provide a process-global lock that reconciles separately created sessions targeting the same tty/console. Applications must keep physical terminal ownership unambiguous or provide their own higher-level coordination.

## 14. Ownership summary

The 1.x ownership rule can be summarized as:

```text
caller owns object lifetime of borrowed provider/endpoints/transports
TerminalSession owns its live terminal conversation and state transitions
leases own scoped logical requests inside that session
TerminalSession disposal remains final restoration authority
```

When in doubt, prefer the semantic/session-owned API over direct transport/native manipulation.
