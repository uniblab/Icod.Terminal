# Icod.Terminal Samples

The sample projects are repository consumers built through project references.
The release package itself is validated separately by `tools/package-smoke`.

## Icod.Terminal.Sample

`Icod.Terminal.Sample` is the intentionally minimal live-session example. It
opens the process terminal, requests cbreak/no-echo input policy, reports the
selected terminal identity and current dimensions, writes through
`TerminalSession`, and restores the captured session state on disposal.

Run it with a selected target framework, for example:

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

## Icod.Terminal.RichInput.Sample

`Icod.Terminal.RichInput.Sample` is the interactive 0.2 event inspector.

It requests one reversible compound input-protocol lease for:

- bracketed paste;
- focus reporting;
- button mouse tracking.

If the selected terminal does not advertise that complete protocol contract, the
sample reports the controlled `TerminalControlResult` and continues with the
ordinary event loop.

When reporting is available, try:

- typing ordinary text;
- arrows, Home/End, Page Up/Page Down, Insert/Delete, and function keys;
- Shift/Alt/Control modified keys supported by the terminal profile;
- clicking mouse buttons or using the wheel;
- moving focus away from and back to the terminal;
- pasting text.

All activity is consumed from `TerminalSession.ReadEventAsync`. Mouse coordinates
are displayed as zero-based terminal-cell coordinates. Bracketed paste is shown
as separate Begin, bounded Data, and End events so the framing contract remains
visible.

Exit with `q`, `Q`, Escape, end-of-input, an interrupt, or a termination event.
The sample releases its rich-input protocol lease before disposing the session.

Run it in a real interactive terminal:

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

The project also targets `net8.0` and `net9.0`.

## Icod.Terminal.Query.Sample

`Icod.Terminal.Query.Sample` is the explicit 0.3 active-query demonstration.

Opening its `TerminalSession` remains passive. The sample then deliberately
requests presentation and rich-input leases when available and explicitly issues
Primary DA, Secondary DA, DSR, CPR, DECRQSS SGR, and XTGETTCAP `TN` operations.

Each probe has a short caller-visible deadline so a terminal which does not
implement a particular query family is reported without hanging the sample.
After probing, the sample returns to `ReadEventAsync` for one ordinary
input/lifecycle event.

Run it in a real interactive terminal:

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

The project also targets `net8.0` and `net9.0`.
