# T49 — Integration, Compatibility, and Security Acceptance

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Tranche:** T49 — integration, compatibility, and security acceptance  
**Development version:** `0.6.0-alpha.8`  
**Predecessor:** T48 — scoped hyperlink lease and nesting  
**Status:** Acceptance evidence added; repository matrix validation pending

---

## 1. Purpose

T49 broadens the OSC 8 acceptance evidence beyond focused unit behavior. The goal
is not to add new public surface. It is to prove that the already-established
URI, parameter, writer, session-ordering, and scoped-ownership contracts remain
safe and deterministic when considered as one system.

The tranche therefore concentrates on:

- injection resistance;
- resource boundaries;
- URI authority edge cases;
- session-owned output ordering;
- redirected and failing output;
- compatibility expectations;
- privacy/security scope;
- cross-platform determinism.

---

## 2. Security acceptance matrix

`TerminalHyperlinkSecurityAcceptanceTests` adds concentrated acceptance for the
complete outbound security boundary.

### URI controls

Every character in these ranges is rejected when supplied raw in URI text:

```text
U+0000–U+001F
U+007F
U+0080–U+009F
```

This explicitly covers ESC and BEL and prevents caller URI data from terminating
or injecting OSC framing.

### Percent-encoded control-looking bytes

Percent-encoded octets remain URI text. For example:

```text
https://example.com/%1b%5c%07
```

normalizes only percent-hex letter case:

```text
https://example.com/%1B%5C%07
```

The encoded URI does not become literal ESC, ST, or BEL bytes inside the OSC
payload.

### Identifier delimiters

Hyperlink identifiers remain restricted to RFC 3986 unreserved ASCII. The
acceptance matrix explicitly rejects characters that could alter OSC 8
parameter or URI field structure, including:

```text
: ; = % @ / ? #
```

as well as spaces, control characters, and non-ASCII text.

### Authority structure

The T47A RFC 3986 authority hardening is now covered at the framing boundary as
well. Invalid examples include:

- repeated user-info delimiters;
- alphabetic ports;
- multiple port delimiters;
- unbracketed IPv6;
- malformed bracketed IP literals;
- scoped IPv6 zone identifiers;
- malformed IPvFuture forms.

Invalid authority input therefore cannot reach OSC framing.

---

## 3. Resource acceptance

The frozen bounds remain:

```text
maximum hyperlink URI payload       2083 bytes
maximum non-empty hyperlink id        128 bytes
```

T49 verifies both exact limits in a complete OSC 8 frame and verifies that one
over either limit is rejected before framing.

The complete frame remains bounded by those payload limits plus the fixed OSC 8
selector, separators, optional `id=` prefix, and 7-bit OSC/ST framing bytes.

No network, filesystem, or terminal-emulator lookup is involved in resource
validation.

---

## 4. Ordering acceptance

Earlier T47A and T48 tests already provide the required session-ordering evidence:

- bounded `WriteHyperlinkAsync(...)` waits behind an in-progress application
  write;
- the complete begin/text/end bounded operation holds one session-owned output
  serialization interval;
- title and OSC 7 operations wait until that bounded interval completes;
- hyperlink operations wait behind an explicitly-held control-output lease;
- scoped begin/restore/close frames use the same session-owned output boundary;
- direct writes through borrowed `session.Output` remain explicitly outside the
  session synchronization guarantee.

This means OSC 8 does not introduce a second writer lane or bypass the existing
session output coordinator.

---

## 5. Query, presentation, and rich-input coexistence

OSC 8 does not introduce an input reader, query router, or private transport.
The existing query subsystem continues to own the one session input path.

Presentation and rich-input managers already acquire the same session
control-output gate for protocol transitions. Hyperlink begin/restore/close and
bounded hyperlink output use that same ordering boundary. As a result:

- query request emission cannot overlap a hyperlink control frame;
- presentation transitions cannot overlap a hyperlink control frame;
- rich-input enable/disable transitions cannot overlap a hyperlink control frame;
- hyperlink operations do not alter query correlation or input-routing state.

T49 does not add redundant emulator-dependent tests for these already-shared
semaphores. The acceptance conclusion is based on the common coordinator plus the
dedicated hyperlink ordering fixtures.

---

## 6. Redirected and failing output

The accumulated T46–T48 tests cover:

- known redirected output rejected before OSC 8 writes;
- invalid URI/id rejected with zero writes;
- cancellation before acquisition/transmission rejected with zero writes;
- begin transport failure propagated;
- text failure followed by best-effort cleanup;
- simultaneous text and cleanup failure aggregation;
- normal close failure propagation;
- failed scoped release retained for retry;
- failed bounded close retained for final session cleanup;
- session disposal best-effort closure of outstanding scopes.

No OSC 8 operation silently converts a transport failure into success.

---

## 7. Compatibility notes

### iTerm2

The emitted form follows the documented OSC 8 structure:

```text
OSC 8 ; params ; URI ST
```

with an empty URI used for close. `id` is the only parameter exposed publicly.
Emulator-specific extensions remain outside the 0.6 contract.

### VTE-family terminals

The URI and identifier ceilings remain deliberately conservative relative to the
historically documented VTE/iTerm2 limits. `Icod.Terminal` does not rely on
emulator detection to validate or emit a request.

### xterm.js and host-embedded terminals

OSC 8 acceptance may depend on the host application and security policy even
when the terminal parser recognizes OSC 8. `Icod.Terminal` therefore treats a
successful write as emission success only.

### Windows Terminal and other modern emulators

Modern terminal emulators commonly support OSC 8, but support is not inferred
from operating system, `TERM`, terminal name, or package identity. Explicit
operation emission remains optimistic when output is an interactive terminal
and support is otherwise unknown.

---

## 8. Privacy and security scope

`Icod.Terminal` does not:

- detect URLs in application text;
- create hyperlinks implicitly;
- generate identifiers automatically;
- fetch or resolve hyperlink targets;
- perform DNS lookup;
- open a browser;
- inspect file targets;
- enforce a consumer scheme allow-list;
- disclose process location or environment as part of OSC 8 behavior.

Hyperlink emission occurs only through an explicit caller operation.

Applications receiving untrusted hyperlink targets remain responsible for
higher-level trust and scheme policy. The library guarantees protocol framing,
validation, bounds, and session ownership; it does not decide whether a URI is
safe for a human to activate.

---

## 9. Cross-platform determinism

The OSC 8 implementation contains no host-OS path lookup, process environment
lookup, current-directory lookup, locale-sensitive parsing, DNS resolution, or
terminal-emulator probing.

The URI and identifier encoders operate on deterministic ASCII grammar and
ordinal comparisons. All automated tests use injected terminal services rather
than the CI runner's terminal emulator.

The final T49 gate therefore depends on the normal Windows/Linux/macOS Staging
matrix going green with the same byte fixtures and semantics.

---

## 10. Gate T49

T49 is complete when the repository matrix is green with the accumulated T44–T49
fixtures.

At that point the 0.6 implementation has evidence for:

1. byte-exact canonical framing;
2. URI and parameter injection resistance;
3. exact resource boundaries;
4. deterministic session-owned ordering;
5. strict scoped-state ownership and retryable cleanup;
6. redirected/failing-output semantics;
7. emulator-agnostic support semantics;
8. no automatic privacy disclosure or URI activation;
9. host-independent behavior on Windows, Linux, and macOS.

The next tranche is **T50 — public API, documentation, and sample audit**.
