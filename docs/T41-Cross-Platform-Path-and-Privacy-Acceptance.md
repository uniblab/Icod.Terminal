# T41 — Cross-Platform Path and Privacy Acceptance

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Tranche:** T41 — cross-platform path and privacy acceptance  
**Development version:** `0.5.0-alpha.6`  
**Predecessor:** T40 — `TerminalSession` semantic current-location API  
**Status:** Implemented; public API/documentation audit remains T42

---

## 1. Purpose

T41 deepens acceptance around the public OSC 7 operation introduced in T40.

T38 already proved host-independent native-path to `file:` URI conversion and
T39 proved OSC 7 framing. T40 exposed those semantics through
`TerminalSession.PublishCurrentLocationAsync(...)`.

T41 now verifies that the feature remains deterministic when exercised through
the public session API and, equally importantly, that location disclosure occurs
only when the caller explicitly requests it.

---

## 2. Cross-platform public-path acceptance

`TerminalSessionLocationAcceptanceTests` exercises the public API with fixtures
whose expected bytes are independent of the operating system running the test.

The matrix includes:

- POSIX paths containing spaces, `#`, literal `%20`, and Unicode;
- Windows drive paths containing spaces and Unicode;
- UNC paths containing spaces and Unicode;
- explicit bracketed IPv6 authority;
- byte-exact OSC 7 framing for every case.

Because every invocation supplies a `TerminalLocationPathStyle`, no test relies
on the CI runner's native path parser or directory separator convention.

This is the public-surface confirmation of the host-independence already proven
inside the T38 encoder.

---

## 3. Privacy acceptance

OSC 7 can disclose source-tree layout, user names, mount paths, share names, and
host identity. The 0.5 contract therefore requires explicit caller intent.

T41 adds deterministic evidence that:

- opening a `TerminalSession` does not emit OSC 7;
- disposing a session which never published a location does not emit OSC 7;
- writing ordinary application text does not emit OSC 7;
- setting a title does not emit OSC 7;
- an explicit `PublishCurrentLocationAsync(...)` call emits exactly one OSC 7 frame;
- subsequent disposal does not silently republish the last location.

The tests identify OSC 7 by its canonical prefix:

```text
ESC ] 7 ;
```

and count occurrences in injected output rather than touching the host terminal.

No process-current-directory convenience API exists in T41, so the library still
cannot disclose `Environment.CurrentDirectory` except through a path value the
caller itself explicitly supplies.

---

## 4. Output ordering acceptance

T41 extends `TerminalSessionOutputOrderingTests` so OSC 7 participates in the
same session-owned serialization boundary as the existing semantic output.

The new acceptance verifies that:

- a location publication waits for an in-progress `WriteTextAsync(...)` call;
- no concurrent writes are observed while the location frame waits;
- a location publication waits behind an active control-output lease;
- the complete OSC 7 frame appears only after the prior operation releases the
  session-owned output boundary;
- location publication does not flush implicitly;
- after disposal, new location publication is rejected with
  `ObjectDisposedException` alongside ordinary text and title output.

This confirms that OSC 7 did not create a parallel write path or weaken T34's
output-ownership contract.

---

## 5. Failure and endpoint continuity

T40 already established and retains the following behavior:

- known redirected output is rejected before emission;
- invalid location/path/authority data writes zero bytes;
- cancellation before transmission writes zero bytes;
- output failures propagate to the caller;
- no implicit flush is performed.

T41 does not change those semantics. It adds lifecycle, privacy, and ordering
acceptance around them.

---

## 6. What T41 deliberately does not add

T41 introduces no new public API and no new terminal protocol.

In particular it does not add:

- automatic current-directory monitoring;
- a process-current-directory convenience overload;
- terminal support probing for OSC 7;
- OSC 8 hyperlink semantics;
- generic shell-integration hooks;
- location query/readback;
- filesystem canonicalization or existence checks.

Those decisions remain subject to the T42 public API/regret audit or later
milestones.

---

## 7. T41 gate

T41 is complete when the repository matrix proves the new acceptance tests on
Windows, Linux, and macOS.

The resulting 0.5 implementation now has:

1. frozen OSC 7/file-URI semantics;
2. deterministic host-independent URI encoding;
3. canonical safe OSC 7 framing;
4. a semantic public `TerminalSession` API;
5. explicit privacy/non-disclosure evidence;
6. session-owned ordering and lifecycle evidence.

The next tranche is **T42 — public API, documentation, and sample audit**.
