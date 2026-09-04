# T45 — Reusable Hyperlink URI and Parameter Encoding

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Tranche:** T45 — reusable hyperlink URI and parameter encoding  
**Development version:** `0.6.0-alpha.3`  
**Predecessor:** T44 — OSC 8 contract and reference freeze  
**Status:** Implemented; OSC 8 terminal emission remains deferred to T46

---

## 1. Purpose

T45 implements the deterministic, terminal-I/O-free encoding layer frozen by
T44.

It validates two independent caller-controlled OSC 8 domains:

1. the absolute URI target;
2. the optional semantic `id` parameter.

No OSC bytes are emitted by this tranche.

---

## 2. Internal encoder

T45 adds:

```csharp
internal static class TerminalHyperlinkEncoder {
    internal const int MaximumUriByteCount = 2083;
    internal const int MaximumIdentifierByteCount = 128;

    internal static string EncodeUri( string uri );
    internal static string EncodeParameters( string? identifier );
}
```

The encoder remains internal so T46/T47/T48 can build evidence before the T50
public API freeze.

---

## 3. URI behavior

`EncodeUri(...)` accepts caller-supplied URI text rather than native path data.

It requires:

- non-empty input;
- well-formed managed Unicode;
- visible ASCII URI text after validation;
- an RFC 3986-style scheme beginning with an ASCII letter and continuing only
  with ASCII letters/digits or `+`, `-`, `.`;
- syntactically valid generic authority/path/query/fragment characters;
- complete `%HH` escapes;
- no second fragment delimiter;
- a maximum of 2083 bytes.

It rejects relative references, whitespace, raw non-ASCII IRI text, controls,
malformed percent escapes, malformed authority brackets, malformed Unicode, and
one-byte-over resource cases.

The encoder does not impose a scheme allow-list and does not activate, resolve,
fetch, or canonicalize a target.

---

## 4. Preservation and normalization

The URI is not decoded and reconstructed.

T45 preserves caller URI structure and spelling, including:

- scheme case;
- host case;
- path case;
- query and fragment text;
- reserved characters where RFC 3986 permits them;
- escaped-octet meaning.

Only percent-escape hexadecimal digits normalize to uppercase:

```text
%2f -> %2F
%7e -> %7E
```

The escaped octet is never decoded during normalization.

---

## 5. Generic RFC 3986 validation

T45 deliberately does not route arbitrary hyperlink input through `System.Uri`.

The encoder validates the generic RFC 3986 component grammar needed by the
frozen contract:

- scheme;
- optional `//authority`;
- path;
- optional query;
- optional fragment.

This avoids runtime/browser normalization becoming accidental terminal-protocol
semantics while still rejecting characters that cannot legally occupy the
relevant generic URI component.

Scheme-specific validation remains outside the 0.6 contract. For example, the
encoder does not attempt to prove that an HTTP authority names a real host or
that a custom scheme's scheme-specific data has application-defined meaning.

---

## 6. Identifier parameter behavior

`EncodeParameters(...)` canonicalizes:

```text
null -> <empty parameter field>
""   -> <empty parameter field>
```

A non-empty identifier becomes:

```text
id=<identifier>
```

Only RFC 3986 unreserved ASCII is accepted:

```text
A-Z a-z 0-9 - . _ ~
```

The 128-byte limit is enforced before output integration.

Because delimiter characters are outside the accepted grammar, identifier data
cannot inject `:`, `=`, `;`, OSC/ST controls, percent escapes, or unknown OSC 8
parameters.

---

## 7. Test evidence

`TerminalHyperlinkEncoderTests` covers:

- `https`, `http`, `ftp`, `mailto`, `file`, and custom schemes;
- hierarchical and opaque-style absolute URI forms;
- query and fragment preservation;
- reserved path/query/fragment characters;
- percent-escape uppercase normalization;
- empty/relative/malformed scheme rejection;
- raw spaces and raw Unicode rejection;
- ESC/BEL/control rejection;
- malformed percent escapes;
- malformed authority brackets;
- malformed UTF-16;
- exact 2083-byte URI acceptance and one-byte-over rejection;
- null/empty identifier canonicalization;
- valid unreserved identifiers;
- identifier delimiter, percent, whitespace, control, and non-ASCII rejection;
- exact 128-byte identifier acceptance and one-byte-over rejection;
- Turkish-culture independence;
- exhaustive single-byte ASCII identifier acceptance/rejection proving that
  accepted identifier data cannot introduce OSC 8 parameter delimiters.

The tests are host-independent and perform no terminal or network I/O.

---

## 8. Separation from OSC 7

T45 intentionally does not replace `TerminalLocationUriEncoder`.

The two encoders have different contracts:

```text
OSC 7: native filesystem path -> constructed RFC 8089 file URI
OSC 8: caller URI text        -> validated generic absolute URI text
```

This separation prevents hyperlink support from weakening the explicit native
path/privacy contract proven in 0.5.

---

## 9. T45 gate

T45 is complete when the repository matrix proves the URI and identifier fixtures
on Windows, Linux, and macOS.

No OSC 8 bytes are emitted by this tranche.

The next tranche is **T46 — OSC 8 writer integration**, which will wrap these
validated payload components in canonical begin/end frames and prove
validation-before-write, complete-frame emission, cancellation-before-commit,
no-implicit-flush, and output-failure behavior.
