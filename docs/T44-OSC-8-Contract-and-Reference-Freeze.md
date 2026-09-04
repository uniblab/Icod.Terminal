# T44 — OSC 8 Contract and Reference Freeze

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Tranche:** T44 — OSC 8 contract and reference freeze  
**Development version:** `0.6.0-alpha.2`  
**Status:** Contract frozen; production OSC 8 implementation begins in T45

---

## 1. Purpose

T44 freezes the protocol, URI, parameter, resource, ownership, nesting,
cancellation, failure, cleanup, endpoint, and security contracts for the
`Icod.Terminal 0.6.0` OSC 8 hyperlink milestone before production
implementation begins.

The release remains deliberately semantic. It adds hyperlink behavior, not a
public generic OSC construction surface.

---

## 2. Pinned references

The 0.6 contract is based on the following references.

### 2.1 OSC 8 hyperlink behavior

1. **Hyperlinks in Terminal Emulators — iTerm2 / terminal-wg feature specification**  
   `https://iterm2.com/feature-reporting/Hyperlinks_in_Terminal_Emulators.html`

   This defines the de-facto OSC 8 begin/end model, the colon-separated
   parameter grammar, the `id` parameter, and compatibility guidance for VTE
   and iTerm2.

2. **iTerm2 escape-code documentation — Anchor (OSC 8)**  
   `https://iterm2.com/documentation-escape-codes.html`

   This documents the interoperable form:

   ```text
   OSC 8 ; [params] ; [url] ST
   ```

   and the empty-URI close operation.

3. **xterm.js supported terminal sequences**  
   `https://xtermjs.org/docs/api/vtfeatures/`

   xterm.js documents OSC 8 support as `OSC 8 ; params ; uri` and recognizes
   ordinary OSC string terminators.

### 2.2 URI syntax

4. **RFC 3986 — Uniform Resource Identifier (URI): Generic Syntax**  
   `https://www.rfc-editor.org/rfc/rfc3986.html`

   RFC 3986 is authoritative for the generic absolute-URI syntax used by the
   0.6 hyperlink validator.

5. **RFC 8089 — The `file` URI Scheme**  
   `https://www.rfc-editor.org/rfc/rfc8089.html`

   RFC 8089 remains authoritative when callers use `file:` hyperlinks. The 0.5
   native-path-to-file-URI encoder remains useful for callers that start from a
   filesystem path, but OSC 8 itself is not restricted to `file:`.

OSC 8 remains de-facto terminal protocol behavior rather than an ECMA-48
standardized selector. ECMA-48 remains relevant to generic OSC/ST framing.

---

## 3. Canonical wire forms

`Icod.Terminal 0.6.0` SHALL emit OSC 8 begin frames in exactly this logical
form:

```text
ESC ] 8 ; <params> ; <uri> ESC \
```

and SHALL emit the canonical close frame:

```text
ESC ] 8 ; ; ESC \
```

Rules:

- OSC introduction is the 7-bit bytes `ESC ]`;
- selector text is ASCII `8`;
- both semicolon separators are always present;
- the URI is non-empty for begin operations;
- the canonical end operation has empty parameters and an empty URI;
- String Terminator is always the 7-bit `ESC \\` sequence;
- BEL termination is not emitted;
- 8-bit C1 OSC/ST forms are not emitted.

This continues the canonical framing policy established by 0.4 and 0.5.

---

## 4. URI input contract

OSC 8 targets are **URI text**, not native filesystem paths.

The ordinary 0.6 hyperlink API SHALL accept an already URI-encoded absolute URI
string. The library SHALL validate that URI text, but SHALL NOT decode and
rebuild it through `System.Uri`, browser/WHATWG URL rules, or filesystem path
logic.

This is intentionally different from OSC 7:

- OSC 7 accepts native filesystem paths and constructs a `file:` URI;
- OSC 8 accepts URI text because hyperlinks legitimately target arbitrary URI
  schemes and URI component semantics are scheme-specific.

A caller that starts with a native filesystem path may first use the same
file-URI policy proven by 0.5, but that path conversion is not silently applied
to arbitrary OSC 8 input.

---

## 5. Absolute URI and scheme policy

A hyperlink begin target MUST be an absolute RFC 3986 URI containing a scheme.

Examples of syntactically valid target families include:

```text
https://example.com/
http://example.com/path?q=v#part
ftp://example.com/file
mailto:user@example.com
file:///tmp/report.txt
custom-scheme:value
```

The library SHALL validate generic URI syntax but SHALL NOT impose a fixed
scheme allow-list in 0.6.

Rationale:

- OSC 8 itself is scheme-agnostic;
- terminal emulators and host applications decide which schemes are actionable;
- a library-level allow-list would age poorly and conflate URI syntax with
  application security policy.

Applications handling untrusted targets remain responsible for their own scheme
policy if they need one.

The library SHALL NOT fetch, resolve, open, probe, or otherwise activate a URI.

Relative references are rejected.

An empty URI is reserved for the OSC 8 close operation and is therefore rejected
as a hyperlink begin target.

---

## 6. URI encoding ownership

The caller supplies URI-encoded text. The 0.6 hyperlink encoder SHALL validate
but SHALL NOT perform broad URI percent-encoding on the caller's behalf.

Consequences:

- raw spaces are rejected;
- raw non-ASCII Unicode is rejected;
- malformed `%` escapes are rejected;
- well-formed `%HH` escapes are preserved;
- query and fragment delimiters such as `?` and `#` are preserved as URI
  structure;
- reserved characters are not generically percent-encoded because their meaning
  depends on their URI component and scheme;
- the URI string is not decoded and re-encoded;
- no Unicode normalization is performed;
- no host-name IDNA conversion is performed;
- no path canonicalization, dot-segment removal, or case normalization is
  performed.

T45 SHALL normalize only percent-escape hexadecimal letter case to uppercase for
byte-deterministic output. `%2f` and `%2F` therefore both emit as `%2F`, without
decoding the escaped octet.

All other accepted URI characters preserve caller spelling and case.

Malformed UTF-16 is rejected before URI validation.

---

## 7. URI character safety

The URI payload SHALL be ASCII after validation.

The encoder SHALL reject:

- U+0000 through U+001F;
- U+007F;
- U+0080 through U+009F;
- ESC and BEL by the C0 rule above;
- all raw non-ASCII characters;
- ASCII space and other whitespace;
- malformed percent escapes;
- syntax that does not contain a valid RFC 3986 scheme followed by `:`.

Rejecting ESC is sufficient to prevent an injected `ESC \\` ST terminator from
being constructed out of URI text. Rejecting BEL preserves safety even though
`Icod.Terminal` itself does not use BEL termination.

---

## 8. URI resource limit

The de-facto OSC 8 specification records that both VTE and iTerm2 have used a
2083-byte URI limit.

`Icod.Terminal 0.6.0` therefore SHALL set:

```text
maximum encoded hyperlink URI payload = 2083 bytes
```

Because accepted hyperlink URI text is ASCII, managed string length after
percent-escape normalization equals output byte length.

The limit applies only to the URI component, excluding OSC framing and
parameters.

The entire URI SHALL be validated and bounded before any OSC byte is written.

---

## 9. Parameter grammar

The OSC 8 parameter field is a colon-separated list of `key=value` assignments.
The de-facto specification currently defines `id` and allows future extension.

`Icod.Terminal 0.6.0` SHALL expose **only the `id` semantic parameter**.

It SHALL NOT expose:

- arbitrary dictionaries;
- arbitrary parameter strings;
- unknown key/value pairs;
- emulator-specific parameters such as current implementation extensions.

This keeps the stable API semantic and avoids freezing generic OSC parameter
extensibility merely for convenience.

The canonical parameter forms are therefore:

```text
<empty>
id=<identifier>
```

No trailing or repeated colon is emitted.

---

## 10. Hyperlink identifier policy

A hyperlink identifier is optional.

The de-facto OSC 8 specification treats an omitted `id` and an empty `id` as
interchangeable. `Icod.Terminal` SHALL canonicalize both to **no parameter**.

A non-empty identifier SHALL be restricted to the RFC 3986 unreserved ASCII
set:

```text
A-Z a-z 0-9 - . _ ~
```

This restriction deliberately avoids parameter delimiters (`:`, `=`, `;`),
generic OSC control characters, percent-encoding ambiguity, and undocumented
terminal-specific escaping conventions.

The maximum identifier length SHALL be:

```text
128 bytes
```

The de-facto specification notes that VTE has historically limited `id` to 250
bytes and recommends applications stay well below that value so intermediate
software has room to prefix or rewrite identifiers. A 128-byte library limit
preserves that headroom.

Identifier output is therefore byte-for-byte ASCII with no percent-encoding.

---

## 11. Identifier semantics

`id` identifies hyperlink cell grouping, not URI identity or security identity.

Two links may share a URI and use different identifiers. Links with the same URI
and the same non-empty identifier may be treated by supporting terminals as one
logical hyperlink run even when painted separately.

`Icod.Terminal` SHALL NOT generate identifiers automatically in 0.6. Callers that
need stable grouping may supply one explicitly; simple streaming applications
may omit it.

Automatic unique-ID generation remains application or higher-level presentation
policy.

---

## 12. Public API direction, not final spelling

T44 freezes semantics but not exact public type/member names.

T47 SHOULD provide a semantic operation capable of beginning a hyperlink with:

- one validated absolute URI;
- an optional identifier;
- no raw OSC selector or parameter-string knowledge.

T48 SHOULD provide a scoped abstraction conceptually similar to:

```csharp
await using TerminalHyperlinkLease hyperlink =
    await session.AcquireHyperlinkAsync(
        uri,
        options
    );

await session.WriteTextAsync( "linked text" );
```

The exact type and method names remain subject to implementation evidence and the
T50 regret audit.

---

## 13. Scoped state ownership

OSC 8 begin changes how subsequently painted terminal cells are associated with
a hyperlink until another OSC 8 begin or an OSC 8 close frame changes that
state.

`Icod.Terminal` SHALL track only hyperlink state that it created itself.

The library SHALL NOT claim knowledge of hyperlink state that existed before its
first library-owned hyperlink begin operation.

When the first library-owned hyperlink scope eventually ends, the library SHALL
emit the canonical OSC 8 close frame. It SHALL NOT attempt to reconstruct an
unknown pre-existing external hyperlink state.

---

## 14. Nesting contract

Nested library-owned hyperlink scopes SHALL be supported as a strict LIFO stack.

Example:

```text
begin A
  text A
  begin B
    text B
  end B -> re-emit begin A
  text A
end A -> emit canonical close
```

Rules:

- each successful acquisition pushes one semantic hyperlink state;
- nested acquisition always emits a begin frame, even when target/id equal the
  current state, so acquisition remains explicit and deterministic;
- disposing the innermost scope restores the immediately previous library-owned
  hyperlink by re-emitting its canonical begin frame;
- disposing the outermost scope emits the canonical close frame;
- out-of-order scope disposal is invalid;
- an out-of-order disposal attempt SHALL fail without emitting protocol output or
  changing the tracked stack;
- direct writes through borrowed `session.Output` remain outside this ownership
  model.

Strict LIFO avoids ambiguous ownership and gives later scoped terminal-state
features a clear precedent.

---

## 15. Ordinary output while a hyperlink is active

`WriteTextAsync(...)` is permitted while a hyperlink scope is active. That is the
ordinary purpose of OSC 8 state.

Other session-owned semantic operations may also occur while a hyperlink is
active. OSC title operations, OSC 7 publication, terminal queries, presentation
transitions, and rich-input transitions do not themselves paint hyperlink text
and SHALL preserve session output serialization.

A hyperlink scope does not create a private output stream. It creates tracked
terminal output state around the existing serialized session output path.

`Icod.DCurses` remains responsible for higher-level cell/presentation policy and
is not required to adopt a hyperlink cell model in 0.6.

---

## 16. Output ordering and atomicity

OSC 8 operations SHALL use the existing session-owned control-output ordering
boundary.

For each individual begin, restore, or close frame:

- all validation completes before output begins;
- the complete frame is constructed before output begins;
- one complete frame SHOULD be submitted through one `ITerminalOutput.WriteAsync`
  call;
- no implicit flush occurs.

The scope as a whole is not one atomic output transaction. Application text and
other explicitly invoked serialized terminal operations may occur between begin
and end by design.

---

## 17. Cancellation policy

Hyperlink acquisition observes caller cancellation before the begin frame is
committed to output.

Once a complete begin frame has been validated and transmission is committed,
ordinary caller cancellation SHALL NOT intentionally truncate that frame.

A scoped lease's normal `DisposeAsync()` cleanup SHALL NOT be caller-cancellable.
Cleanup must remain possible even if the operation that used the hyperlink was
cancelled.

If a future explicit non-scoped end API accepts cancellation, cancellation may be
observed only before close/restore transmission is committed.

---

## 18. Begin failure policy

If validation fails before a begin write:

- no bytes are emitted;
- no hyperlink state is pushed.

If the output write reports failure:

- the acquisition fails;
- no lease is returned;
- the hyperlink is not recorded as successfully owned by the session;
- the library does not claim to know whether the terminal consumed a partial
  frame before the transport failure.

The transport exception remains a transport exception.

The library SHALL NOT fabricate recovery success after a failed begin write.

---

## 19. Release/restore failure policy

A scope is removed from the tracked hyperlink stack only after its required
close or outer-state restore frame is reported successfully written.

If release output fails:

- `DisposeAsync()` surfaces the output failure;
- the scope remains logically active in the session's ownership stack;
- a subsequent cleanup attempt may retry;
- session disposal remains the final best-effort cleanup authority.

This avoids silently forgetting a state that the library was unable to close or
restore.

---

## 20. Session disposal policy

When `TerminalSession.DisposeAsync()` begins:

- new hyperlink acquisitions are rejected with the same closing/disposed policy
  used for other session-owned output;
- any in-progress session-owned output lease is allowed to reach the established
  serialization boundary;
- if one or more library-owned hyperlink scopes remain active, session cleanup
  SHALL make a best-effort attempt to emit one canonical OSC 8 close frame;
- a successful final close clears all tracked hyperlink scopes because the
  terminal is no longer in a library-owned hyperlink state;
- no attempt is made to reconstruct unknown hyperlink state predating the
  session/library-owned scopes.

The exact interaction between a final hyperlink-close failure and the existing
session-disposal exception aggregation policy SHALL follow the session's current
cleanup model rather than inventing a hyperlink-only exception channel.

---

## 21. Endpoint and support semantics

OSC 8 follows the established semantic OSC policy.

If session output is known to be redirected/non-terminal, hyperlink operations
SHALL fail without emitting OSC bytes.

If output is a terminal but OSC 8 support is unknown, explicit hyperlink
operations MAY be emitted optimistically.

Successful completion proves only that the complete requested frame was written.
It does not prove that:

- the terminal supports OSC 8;
- the terminal recognizes the URI scheme;
- the terminal will render an underline or other visual affordance;
- the terminal will permit activation;
- the target exists or is reachable.

`TERM`, terminfo identity, emulator name, or operating-system identity SHALL NOT
be fabricated into proof of support.

The iTerm2 feature-reporting extension is not adopted as a general OSC 8 support
negotiation mechanism in 0.6.

---

## 22. Security boundary

OSC 8 marks terminal text with metadata; it does not itself require
`Icod.Terminal` to activate or dereference the target.

The 0.6 safety boundary therefore focuses on:

- impossible OSC/ST injection;
- impossible BEL termination injection;
- bounded URI and identifier sizes;
- well-formed absolute URI syntax;
- well-formed percent escapes;
- restricted identifier grammar;
- explicit caller invocation;
- no browser/network/file access by the library.

Scheme allow-listing, trust decisions, and whether an untrusted link should be
shown to a user remain consumer policy.

A terminal emulator may itself apply additional security policy when activating
links. `Icod.Terminal` does not attempt to predict or override that policy.

---

## 23. Compatibility notes

### iTerm2

iTerm2 documents the canonical OSC 8 begin/end forms and `id` grouping
semantics. It also has emulator-specific parameter extensions in newer versions.
Those extensions are deliberately outside the 0.6 public contract.

### VTE family

The de-facto OSC 8 specification was written for interoperable VTE/iTerm2
behavior and documents VTE's historical 2083-byte URI and 250-byte identifier
limits. The 0.6 limits are chosen conservatively against that evidence.

### xterm.js

xterm.js documents OSC 8 support and recognizes explicit hyperlinks. Its host
application may independently decide whether non-HTTP schemes are actionable.
That reinforces the separation between URI syntax and activation policy adopted
here.

### Other terminals

Other terminal emulators may support OSC 8, ignore it, partially implement it,
or place tighter resource/policy limits on it. `Icod.Terminal` does not infer
support from identity alone.

---

## 24. Explicit non-goals

The following are out of scope for stable 0.6:

- OSC 52 clipboard/selection operations;
- arbitrary public OSC selectors;
- arbitrary public OSC parameter dictionaries;
- emulator-specific OSC 8 parameters;
- automatic linkification of plain text;
- URL detection heuristics;
- URI fetching, reachability checking, DNS lookup, or browser launching;
- relative-URI hyperlink targets;
- raw Unicode IRI input;
- automatic ID generation;
- terminal-side hyperlink querying;
- restoration of unknown pre-existing external hyperlink state;
- shell-integration bundles;
- `Icod.DCurses` cell hyperlink storage/presentation policy as part of the
  `Icod.Terminal` public contract;
- cursor style;
- synchronized output.

---

## 25. T45 implementation obligations

T45 may now implement deterministic URI and parameter encoders without terminal
I/O.

At minimum T45 SHALL test:

- `https`, `http`, `ftp`, `mailto`, `file`, and a syntactically valid custom
  absolute scheme;
- query and fragment preservation;
- valid reserved URI characters;
- percent escapes and uppercase percent-hex normalization;
- malformed `%` escapes;
- raw spaces;
- raw non-ASCII text;
- malformed UTF-16;
- missing/malformed scheme;
- relative URI rejection;
- empty URI rejection for begin semantics;
- exact 2083-byte URI acceptance;
- one-byte-over rejection;
- omitted/null/empty identifier canonicalization;
- valid unreserved identifiers;
- identifier delimiter/control/non-ASCII rejection;
- exact 128-byte identifier acceptance;
- one-byte-over rejection;
- host- and culture-independent output;
- no terminal output from the encoder layer.

T45 SHOULD add focused property/fuzz-style coverage for arbitrary ASCII input so
accepted encoder output can never contain C0, DEL, C1, malformed percent escapes,
or parameter delimiters introduced through identifier data.

---

## 26. Gate T44

T44 is complete because the following are now frozen before production OSC 8
implementation:

- pinned interoperability references;
- canonical begin and end wire forms;
- absolute URI-only input policy;
- URI-encoded caller-input ownership;
- generic scheme syntax versus consumer scheme policy;
- strict ASCII/control/percent validation;
- 2083-byte URI limit;
- `id`-only parameter policy;
- 128-byte unreserved-ASCII identifier policy;
- no arbitrary public parameters;
- stack-based strict-LIFO nesting;
- outer hyperlink restoration by re-emission;
- canonical close of the outermost scope;
- no restoration claim for unknown pre-existing state;
- session-owned ordering and no implicit flush;
- acquisition/release cancellation policy;
- begin/release failure semantics;
- final session-disposal cleanup policy;
- redirected-output and optimistic-support semantics;
- security boundary and explicit non-goals;
- T45 acceptance obligations.

The next tranche is **T45 — reusable hyperlink URI and parameter encoding**.
