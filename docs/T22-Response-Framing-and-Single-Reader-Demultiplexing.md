# T22 — Response Framing and Single-Reader Demultiplexing

**Project:** `Icod.Terminal`
**Release line:** `0.3.0`
**Development version:** `0.3.0-alpha.2`
**Tranche:** T22 — bounded response framing and single-reader demultiplexing
**Predecessor:** T21 — 0.3 foundation and contract reset
**Status:** Complete

---

## 1. Purpose

T22 adds the first production machinery for the 0.3 query/response-routing
milestone without adding a public query API.

The tranche teaches the existing incremental terminal input path how to:

- recognize bounded CSI and DCS response frames;
- associate a candidate frame with one internal response expectation;
- consume a frame only when that expectation accepts it;
- leave rejected or unrelated input on the established application event path;
- preserve 0.2 keyboard, text, mouse, focus, paste, and lifecycle behavior;
- retain exactly one underlying `ITerminalInput` reader.

T22 intentionally does not implement request emission, public query methods,
query deadlines, late-response ownership, or autonomous query driving. Those
remain T23 concerns.

---

## 2. Single-Reader Architecture

`TerminalInputDecoder` remains the only component which reads terminal input
bytes.

T22 does not add:

- a response reader thread;
- a protocol-specific `ReadAsync` loop;
- a second stream consumer;
- a public raw-frame reader.

An internal response expectation is consulted by the existing decoder before
ambiguous keyboard fallback. A matched response is removed from the same bounded
byte buffer already used by the 0.2 decoder.

When no response expectation exists, the new routing path is inert and the 0.2
decoder behavior is unchanged.

---

## 3. Internal Response Contract

T22 introduces internal-only contracts:

- `TerminalResponseFrameKind` distinguishes CSI and DCS framing;
- `ITerminalResponseMatcher` identifies the expected frame family and decides
  whether one complete frame satisfies the expectation;
- `TerminalResponseFrame` retains exact terminal bytes for later protocol
  parsers;
- `TerminalResponseExpectation` owns the internal completion task for one active
  matcher;
- `TerminalResponseFramer` performs strict bounded framing.

These types are internal implementation seams. They are not part of the public
0.3 API baseline and do not constitute general protocol extensibility.

Only one response expectation may be registered with a decoder at one time.
That deliberately matches the ambiguity-sensitive serialization direction
frozen in T21. T23 will add the queue/transaction ownership above this seam.

---

## 4. Expectation-Driven Demultiplexing

A response-shaped frame is consumed only when all of the following are true:

1. an internal response expectation is active;
2. the byte stream at the current decoder position forms a complete bounded
   frame of the expectation's CSI or DCS family;
3. the expectation matcher accepts the exact framed bytes;
4. the same expectation is still active when the decoder commits consumption.

If a frame is syntactically complete but the matcher rejects it, no response
bytes are consumed by the router. The established input decoder remains free to
interpret those bytes as a terminfo key, mouse/focus/paste sequence, Escape, or
ordinary fallback input.

This rule implements the T21 expectation-driven ambiguity contract. In
particular, `CSI 1;2 R` can remain a traditional modified-F3 input sequence when
no cursor-position expectation claims it.

---

## 5. CSI Framing

T22 accepts both:

- the 7-bit introducer `ESC [`;
- the 8-bit C1 CSI introducer `0x9B`.

The bounded CSI framer follows the ECMA-48 byte classes used by the 0.3 protocol
reference hierarchy:

- parameter bytes: `0x30` through `0x3F`;
- intermediate bytes: `0x20` through `0x2F`;
- final byte: `0x40` through `0x7E`.

Parameter bytes may not follow intermediate bytes. A complete frame ends at the
first legal final byte.

Malformed candidates are not consumed as responses.

---

## 6. DCS Framing

T22 accepts both:

- the 7-bit introducer `ESC P`;
- the 8-bit C1 DCS introducer `0x90`.

The DCS header uses the same parameter/intermediate/final byte classes as CSI.
After the DCS final byte, T22 scans bounded string data until either supported
String Terminator form:

- `ESC \\`;
- the 8-bit C1 ST byte `0x9C`.

`CAN` (`0x18`) and `SUB` (`0x1A`) invalidate a response candidate. An `ESC` in
DCS payload must begin the 7-bit ST sequence; other embedded Escape forms are
rejected by this strict T22 framing layer.

That policy is sufficient for the planned DECRQSS and XTGETTCAP response
families. T25 may deliberately refine DCS payload grammar if concrete protocol
evidence requires it.

---

## 7. Bounds and Backpressure

T22 introduces no second deferred application-event queue.

Unclaimed bytes remain in the existing `TerminalInputDecoder` byte buffer and
ordinary input is returned promptly through the established decoder path. This
means T22 adds no new source of unbounded application-input accumulation.

The response framer has an internal default maximum frame size of 4096 bytes and
an internal hard ceiling of 65536 bytes. The effective decoder framing limit is
the smaller of the response-frame default and that decoder instance's existing
`maximumBufferedBytes` value.

These numbers are internal implementation policy, not public compatibility
constants.

An incomplete candidate which reaches the effective framing bound is treated as
invalid response framing and falls back to the normal input path rather than
forcing the decoder to exceed its byte-buffer limit.

T23 may add bounded deferred-event coordination if autonomous query driving
proves to require it. T22 deliberately does not pre-commit that design.

---

## 8. Fragmentation and Ordering

CSI and DCS framing is incremental across arbitrary `ITerminalInput.ReadAsync`
boundaries.

The decoder preserves:

- partial 7-bit introducers;
- partial CSI parameter/intermediate/final sequences;
- partial DCS headers;
- partial DCS payloads;
- a trailing `ESC` which may become the first byte of `ST`;
- bytes following a matched frame in the same transport read.

A matched response is consumed and the decoder immediately continues with any
remaining application input already buffered behind it.

Application input which appears before a response is returned normally. Because
T22 adds no autonomous query pump, a response behind earlier application input
is reached as the application continues consuming the existing ordered event
stream. T23 owns the additional coordination required for a public query await
to make progress independently of an application's `ReadEventAsync` cadence.

---

## 9. Rich-Input Preservation

Response routing runs only while an expectation is active and before ordinary
mouse/key fallback for the current non-paste input position.

Bracketed paste has stronger input ownership: while paste mode is active,
response-looking bytes remain paste content unless they complete the exact paste
terminator. T22 therefore does not steal CPR-shaped or other CSI-looking bytes
from a bracketed paste payload.

Existing mouse, focus, traditional modified-key, UTF-8, Escape-ambiguity, and
lifecycle tests remain the compatibility safety net. T22 adds targeted tests for
response/key ambiguity and response-looking paste data.

---

## 10. Validation

T22 tests cover:

- an expected CPR-shaped CSI frame winning over the same bytes configured as a
  traditional modified-F3 key;
- the same CPR-shaped bytes remaining a modified-F3 key when no expectation is
  active;
- a rejected CSI candidate remaining on the normal application input path;
- CSI response fragmentation at every byte boundary;
- 7-bit DCS framing adjacent to ordinary input;
- selected 8-bit C1 CSI framing;
- selected 8-bit C1 DCS/ST framing;
- response-shaped bytes inside bracketed paste remaining paste data;
- an oversized incomplete DCS candidate falling back without exceeding a small
  decoder buffer;
- rejection of a second simultaneously active response expectation.

The tests use injected scripted `ITerminalInput` implementations and do not
require or mutate a physical terminal.

---

## 11. Compatibility

T22 adds no public API member.

Existing 0.2 consumers which do not participate in future internal query
routing observe the same public event model and session behavior.

The internal expectation/matcher/framer contracts remain free to evolve before
the 1.0 contract freeze.

---

## 12. Gate Result

T22 is accepted when:

1. exactly one underlying terminal input reader remains;
2. bounded incremental CSI framing exists;
3. bounded incremental DCS framing exists;
4. selected 7-bit and 8-bit control forms are covered;
5. only an accepted expected frame is removed from the application input stream;
6. CPR-shaped key input remains ordinary input without a compatible expectation;
7. rich-input behavior is preserved;
8. oversized/incomplete response candidates remain bounded;
9. no public unfinished query or arbitrary matcher API is exposed.

**Gate T22: complete.**

The next tranche is **T23 — Query Transactions, Deadlines, and Late-Response
Ownership**.
