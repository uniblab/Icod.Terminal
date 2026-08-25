# T10 integration prerequisite — lifecycle participants

`Icod.Terminal 0.1.0-alpha.10` adds the higher-layer suspend/resume seam discovered while rebasing
`Icod.DCurses` onto the Terminal substrate.

A live `TerminalSession` remains the sole owner of platform signal and console-cancellation plumbing.
Higher layers may register an `ITerminalSessionLifecycleParticipant` when they own terminal state that
must be neutralized before Terminal restores its own presentation and host mode state.

For a caught POSIX suspension the ordering is:

```text
higher-layer participants prepare (reverse registration order)
Terminal presentation leases suspend
Terminal output setup is released
Terminal input baseline is restored
Suspending is published
process suspension is completed
```

After resume:

```text
Terminal output/input/presentation state is re-entered
higher-layer participants resume (registration order)
Resumed is published
```

A registration released after its participant has already prepared does not suppress the matching
resume callback for that in-progress cycle. The registration only prevents participation in future
cycles.

This contract lets DCurses reset SGR/rendition state and serialize its refresh engine around suspend
without installing a second `PosixSignalRegistration` or `Console.CancelKeyPress` handler.
