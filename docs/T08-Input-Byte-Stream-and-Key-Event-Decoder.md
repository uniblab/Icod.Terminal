# T08 — Input Byte Stream and 0.1 Key-Event Decoder

**Development version:** `0.1.0-alpha.8`

**Tranche:** T08 — Input byte stream and 0.1 key-event decoder

**Status:** Complete; build/test gate closed and retained in the T12B 0.1 API review

## Purpose

T08 moves generic terminal byte decoding into `Icod.Terminal` so higher layers do not need private escape-sequence tables, UTF-8 buffering, or timeout races merely to read ordinary terminal keys.

The implementation is deliberately limited to the input required by the `0.1.0` acceptance consumers. Mouse, focus, bracketed paste, and modern keyboard protocols remain later work.

## Public event model

`TerminalInputEvent` represents decoded keyboard input or end-of-input. Its `Kind` is:

- `Text` for ordinary Unicode scalar values;
- `Key` for named keys or control-modified characters;
- `EndOfInput` when the byte source reaches EOF/disconnect.

`TerminalKey` covers the 0.1 contract:

- character;
- Enter;
- Space;
- Escape;
- Backspace;
- Tab;
- arrows;
- Home and End;
- Page Up and Page Down;
- Insert and Delete;
- numbered function keys.

`TerminalKeyModifiers` currently defines Shift, Control, and Alt as representable flags. T08 decodes Shift+Tab and delivered control bytes. General Alt/modifier protocol decoding is not claimed by the 0.1 contract.

## TermInfo-derived key sequences

Traditional special-key byte sequences come from the selected `Icod.TermInfo.TerminalDescription`.

T08 consumes at least:

- `kbs` / `KeyBackspace`;
- `kcbt` / `KeyBackTab`;
- cursor keys;
- Home and End;
- `kent` / `KeyEnter`;
- Page Up and Page Down;
- Insert and Delete;
- `kf0` through `kf63` when present.

Capability strings are converted with `Encoding.Latin1`, preserving the same reversible one-byte terminfo semantics used by T06 output. A capability containing data outside the reversible 8-bit range is rejected rather than silently re-encoded.

T08 does not synthesize ANSI/xterm key sequences when the selected terminal description does not advertise them.

## Incremental decoding

The decoder preserves state across arbitrary read boundaries. It therefore supports:

- one byte per read;
- partial UTF-8 scalars;
- partial terminal key sequences;
- several events delivered in one read;
- an exact key sequence which is also a prefix of a longer configured sequence.

Ordinary text input is decoded as UTF-8 Unicode scalar values. Malformed UTF-8 consumes one offending byte and emits U+FFFD so the decoder always makes bounded forward progress.

Single-byte semantic handling is:

- `CR` or `LF` → Enter;
- `HT` → Tab;
- `BS` or `DEL` → Backspace;
- space → Space;
- ESC → Escape when it does not resolve to a configured key sequence;
- other C0 control bytes → `Character` plus the Control modifier.

Control bytes are represented only when the host input mode actually delivers them. For example, in a mode with POSIX signal processing enabled, Ctrl+C may instead arrive through the T07 interrupt lifecycle path.

## Bounded Escape ambiguity

An ESC byte can be either a literal Escape key or the beginning of a terminal key sequence.

`TerminalSession.DefaultEscapeSequenceTimeout` is 100 milliseconds. When buffered bytes beginning with ESC are a prefix of a configured terminfo key sequence, the decoder waits at most that interval for continuation bytes.

If the buffered bytes already form an exact key and are also a prefix of a longer key, the longer key wins when its continuation arrives within the window. If no continuation arrives, the shorter exact key wins. If no exact key exists, ESC becomes a literal Escape event.

An input read started during this ambiguity window is retained after the timeout. Bytes which arrive later are therefore not discarded; they are decoded by a later session read.

## Bounded buffering

T08 retains at most `TerminalSession.MaximumBufferedInputBytes`, currently 4096 bytes, of undecoded terminal input.

A terminfo key capability larger than that bound is rejected when the decoder is constructed. The decoder also constrains every transport read to its remaining capacity and validates the byte count returned by `ITerminalInput`.

This bound prevents malformed or hostile input/capability data from creating unbounded decoder storage.

## Unified session reader

`TerminalSession.ReadEventAsync` is the ordinary interactive-loop API. Overloads support:

- indefinite waiting;
- a relative `TimeSpan` timeout;
- an absolute `DateTimeOffset` deadline;
- caller cancellation.

It returns a `TerminalEvent` whose kind is:

- `Input`;
- `Lifecycle`;
- `Timeout`;
- `Cancelled`.

Caller timeout and cancellation end only the current wait. They do not cancel the underlying terminal read. The pending read and decoder state remain available to the next call, which avoids losing fragmented UTF-8 or escape-prefixed keys.

This is intentionally different from session-lifetime cancellation. Disposal cancels the internal lifetime token so a cancellable borrowed `ITerminalInput` can stop an outstanding read.

## Lifecycle coordination

When T07 lifecycle observation is available, `ReadEventAsync` races decoded input against the normalized lifecycle queue. A resize event therefore wakes the same periodic loop which is waiting for keyboard input.

`ReadEventAsync` and `ReadLifecycleEventAsync` consume the same lifecycle queue. Applications should choose one ownership pattern rather than concurrently consuming both APIs from different readers.

Lifecycle events are preferred when lifecycle and input are already simultaneously available, allowing resize/termination processing to reach the application promptly.

## Concurrency

The unified event reader serializes calls with an internal gate. Multiple callers do not start concurrent reads against the borrowed `ITerminalInput`.

Timeout or caller cancellation while waiting for that gate is represented as a `Cancelled`/`Timeout` event as appropriate and does not disturb another in-flight terminal read.

## T08 gate coverage

The deterministic test suite covers:

- byte-by-byte terminfo sequence fragmentation;
- multiple events in one input read;
- fragmented multi-byte UTF-8;
- overlapping key-sequence prefixes;
- shorter exact-key resolution after the Escape timeout;
- isolated Escape timeout behavior;
- navigation, function keys, Shift+Tab, control keys, semantic single-byte keys, and EOF;
- bounded capability/input buffering;
- session timeout without canceling the transport read;
- caller cancellation represented as an event without losing pending input;
- absolute deadlines;
- resize waking the unified reader.

The tests use injected byte sources, terminal descriptions, terminal-control providers, and lifecycle sources. They do not modify the process terminal.

## Deferred work

T08 does not add:

- mouse events;
- focus events;
- bracketed paste events;
- CSI-u/Kitty keyboard negotiation;
- generalized Alt/modifier sequence inference;
- terminal query/response routing;
- presentation-state leases.

Those remain in later roadmap tranches.
