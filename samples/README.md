# Icod.Terminal Samples

The sample projects are repository consumers built through project references.
The release package itself is validated separately by the package-verification
harnesses under `tools/`. Focused package consumers use only the freshly produced
NuGet artifact and run under `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.PointerShape.Sample

`Icod.Terminal.PointerShape.Sample` is the focused 0.11 OSC 22 terminal mouse-pointer demonstration.

```text
dotnet run --project samples/Icod.Terminal.PointerShape.Sample/Icod.Terminal.PointerShape.Sample.csproj -f net10.0
```

The sample demonstrates explicit semantic mutation and reset:

```csharp
await session.SetPointerShapeAsync(
	TerminalPointerShape.Crosshair
);
await session.ResetPointerShapeAsync();
```

It then demonstrates nested scoped ownership:

```csharp
await using ( TerminalPointerShapeLease pointer =
	await session.AcquirePointerShapeAsync(
		TerminalPointerShape.Pointer
	) ) {
	await using ( TerminalPointerShapeLease wait =
		await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Wait
		) ) {
		// Wait controls while the inner owner is active.
	}

	// Pointer is restored here.
}

// Final release resets pointer shape to terminal policy.
```

The sample also issues explicit bounded Kitty-compatible support/current-shape queries. Query timeout is reported as an unanswered query rather than being misinterpreted as proof that OSC 22 is unsupported.

`TerminalPointerShape.Default` means the CSS `default` pointer shape. It is deliberately distinct from `ResetPointerShapeAsync()`, which emits an empty OSC 22 payload and releases pointer shape back to terminal policy.

Successful pointer mutation proves that the complete OSC 22 frame was emitted; it does not prove that the attached terminal recognizes or visually applies the requested pointer shape.

## Icod.Terminal.Progress.Sample

`Icod.Terminal.Progress.Sample` is the focused 0.10 OSC 9;4 terminal-progress demonstration.

```text
dotnet run --project samples/Icod.Terminal.Progress.Sample/Icod.Terminal.Progress.Sample.csproj -f net10.0
```

The sample acquires one semantic progress lease, reports three determinate stages as completed/total values, switches to indeterminate progress for work without a known duration, then demonstrates the neutral attention state:

```csharp
await using TerminalProgressLease progress =
	await session.AcquireProgressAsync();

await progress.ReportAsync( 1, 3 );
await progress.ReportAsync( 2, 3 );
await progress.SetIndeterminateAsync();
await progress.ReportAsync(
	TerminalProgressState.Attention,
	3,
	3
);
```

Callers never need to compute OSC percentages or construct escape strings. Disposing the final progress owner clears library-owned terminal progress automatically.

Successful completion proves that the OSC 9;4 frames were emitted; it does not prove that the attached terminal renders terminal progress. Nested progress owners are identity-aware and may be disposed out of order. A newer owner which has not yet reported a value does not mask a lower reported owner.

## Icod.Terminal.SynchronizedOutput.Sample

`Icod.Terminal.SynchronizedOutput.Sample` is the focused 0.9 DEC private mode 2026 demonstration.

```text
dotnet run --project samples/Icod.Terminal.SynchronizedOutput.Sample/Icod.Terminal.SynchronizedOutput.Sample.csproj -f net10.0
```

The sample opens an interactive `TerminalSession`, acquires one semantic synchronized-output lease, performs ordinary session writes and a window-title update, and then releases the lease:

```csharp
await using ( TerminalSynchronizedOutputLease synchronized =
	await session.AcquireSynchronizedOutputAsync() ) {
	await session.WriteTextAsync( "line 1\r\n" );
	await session.SetWindowTitleAsync( "Icod.Terminal synchronized output" );
}
```

The first logical owner emits canonical `ESC[?2026h`. The final logical owner emits canonical `ESC[?2026l` followed by one flush. Nested leases share the same physical mode request and may be disposed out of order.

Successful completion proves only that the appropriate protocol frames were emitted. It does not prove that the attached terminal implements or continues honoring synchronized output, and acquisition does not perform an automatic capability probe.

Synchronized output is a terminal-side presentation-timing bracket. It does not create an application-side byte buffer inside `Icod.Terminal`, and operations performed within the lease retain their existing framing and flush behavior.

## Icod.Terminal.CursorStyle.Sample

`Icod.Terminal.CursorStyle.Sample` is the focused 0.8 DECSCUSR cursor-style demonstration.

Run it in an interactive terminal. The optional argument is any `TerminalCursorStyle`
name; the default is `SteadyBar`.

```text
dotnet run --project samples/Icod.Terminal.CursorStyle.Sample/Icod.Terminal.CursorStyle.Sample.csproj -f net10.0 -- SteadyUnderline
```

The sample first demonstrates an explicit observation:

```csharp
TerminalCursorStyleObservation observation =
	await session.QueryCursorStyleAsync(
		TimeSpan.FromMilliseconds( 750 )
	);
```

It then acquires a truthful scoped lease:

```csharp
await using TerminalCursorStyleLease lease =
	await session.AcquireCursorStyleAsync(
		TerminalCursorStyle.SteadyUnderline,
		TimeSpan.FromMilliseconds( 750 )
	);
```

These are deliberately two separate observations. The first query demonstrates the
explicit observation API. `AcquireCursorStyleAsync(...)` independently re-observes
the terminal's current semantic cursor style immediately before mutation and uses
that second observation as its restoration baseline. The lease never assumes that
an earlier caller query is still current.

If either explicit observation reports DECRQSS cursor-style state as unsupported,
the sample does not claim support. If lease acquisition cannot establish its own
baseline, no leased style is retained. Query timeout is reported as timeout rather
than being misinterpreted as proof of unsupported behavior.

Cursor style and cursor visibility are separate concepts. This sample changes
shape/blink policy only; it does not hide or show the cursor.

## Icod.Terminal.Clipboard.Sample

`Icod.Terminal.Clipboard.Sample` is the focused 0.7 OSC 52 clipboard/selection demonstration.

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -f net10.0 -- "copied text"
```

It explicitly writes strict UTF-8 text and then requests the same terminal-managed
selection with a caller-visible timeout. Opening the session does not read clipboard
data, and read timeout does not prove lack of OSC 52 support.

## Icod.Terminal.Hyperlink.Sample

`Icod.Terminal.Hyperlink.Sample` is the focused 0.6 OSC 8 hyperlink demonstration.
It requires the target URI and visible link text explicitly; an optional third
argument supplies the OSC 8 `id` value.

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

The sample demonstrates both bounded hyperlink output and strict-LIFO scoped
hyperlink restoration.

## Icod.Terminal.Location.Sample

`Icod.Terminal.Location.Sample` is the focused 0.5 OSC 7 current-location demonstration.
It requires callers to supply path grammar and absolute path explicitly and never
publishes `Environment.CurrentDirectory` automatically.

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows C:\Development\Icod

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- unc \\server\share\project
```

## Icod.Terminal.Title.Sample

`Icod.Terminal.Title.Sample` is the focused 0.4 OSC title demonstration.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

It demonstrates semantic OSC 0/1/2 operations without exposing raw OSC construction.

## Icod.Terminal.Query.Sample

`Icod.Terminal.Query.Sample` is the explicit 0.3 active-query demonstration.
Opening its `TerminalSession` remains passive; the sample explicitly issues Primary
DA, Secondary DA, DSR, CPR, DECRQSS SGR, and XTGETTCAP `TN` operations with short
caller-visible deadlines before returning to ordinary input/lifecycle consumption.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

## Icod.Terminal.RichInput.Sample

`Icod.Terminal.RichInput.Sample` is the interactive 0.2 event inspector. It requests
one reversible compound input-protocol lease for bracketed paste, focus reporting,
and button mouse tracking when supported.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

## Icod.Terminal.Sample

`Icod.Terminal.Sample` is the intentionally minimal live-session example. It opens
the process terminal, requests cbreak/no-echo input policy, reports the selected
terminal identity and current dimensions, writes through `TerminalSession`, and
restores captured state on disposal.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

All sample projects target `net8.0`, `net9.0`, and `net10.0`.
