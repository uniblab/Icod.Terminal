# T37 — OSC 7 Contract and Reference Freeze

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Tranche:** T37 — OSC 7 contract and reference freeze  
**Development version:** `0.5.0-alpha.2`  
**Status:** Contract frozen; production OSC 7 implementation begins in T38

---

## 1. Purpose

T37 freezes the protocol, URI, path, authority, privacy, resource, and failure
contracts for the `Icod.Terminal 0.5.0` OSC 7 current-location feature before
production implementation begins.

The purpose of this tranche is specifically to prevent implementation details
such as `System.Uri`, the current host operating system, environment variables,
or terminal-emulator quirks from becoming accidental public semantics.

`0.5.0` remains a deliberately narrow release. It publishes current-location
metadata. It is not a shell-integration framework and does not expose arbitrary
OSC emission.

---

## 2. Pinned references

The 0.5 contract is based on the following references.

### 2.1 OSC 7 terminal behavior

1. **iTerm2 escape-code documentation — CurrentDir / RemoteHost**  
   `https://iterm2.com/documentation-escape-codes.html`

   iTerm2 documents OSC 7 as the combined current-directory/remote-host form
   whose payload is a `file` URL containing a hostname and path, for example
   `file://example.com/usr/bin`.

2. **GNOME VTE current-directory URI terminal property**  
   `https://gnome.pages.gitlab.gnome.org/vte/gtk4/const.TERMPROP_CURRENT_DIRECTORY_URI.html`

   VTE records the current-directory URI supplied by OSC 7 as a URI-valued
   terminal property.

The OSC 7 ecosystem is de-facto terminal protocol behavior rather than an
ECMA-48 standardized selector. ECMA-48 remains relevant to generic OSC/ST
framing, but does not define the OSC 7 current-directory meaning.

### 2.2 URI syntax and file-URI semantics

3. **RFC 3986 — Uniform Resource Identifier (URI): Generic Syntax**  
   `https://www.rfc-editor.org/rfc/rfc3986.html`

   This is authoritative for URI component structure, reserved/unreserved
   characters, and percent encoding.

4. **RFC 8089 — The `file` URI Scheme**  
   `https://www.rfc-editor.org/rfc/rfc8089.html`

   This is authoritative for the standards-track `file:` URI model used by
   0.5, including local file URIs, explicit authorities, Windows drive-letter
   forms, and the authority-mapped UNC form.

The implementation SHALL follow these pinned semantics rather than browser
WHATWG file-URL coercion rules or platform-specific `System.Uri` output.

---

## 3. Canonical outbound OSC 7 wire form

`Icod.Terminal 0.5.0` SHALL emit OSC 7 in exactly this logical form:

```text
ESC ] 7 ; <file-uri> ESC \
```

where:

- OSC is introduced with the 7-bit bytes `ESC ]`;
- selector text is ASCII `7`;
- selector and payload are separated by ASCII `;`;
- the payload is one encoded `file:` URI;
- OSC is terminated with the 7-bit String Terminator `ESC \\`;
- BEL termination is not emitted by `Icod.Terminal`;
- 8-bit C1 OSC/ST forms are not emitted by `Icod.Terminal`.

This continues the canonical framing policy frozen and proven in 0.4.

---

## 4. URI scheme contract

`0.5.0` SHALL emit **only `file:` URIs** for OSC 7.

The semantic API accepts a filesystem location, not an arbitrary URI string.
Callers therefore cannot use the OSC 7 API to publish `http:`, `ssh:`, custom
schemes, query strings, fragments, userinfo, or ports.

The output URI contains only:

```text
file://[authority]/absolute/path
```

for the canonical hierarchical form used by this library.

For local locations without an explicit authority, the library SHALL emit an
empty authority and therefore the familiar three-slash local form:

```text
file:///usr/local/src
file:///C:/Development/Icod
```

The shorter RFC-valid `file:/path` spelling is not emitted. One canonical form
is preferred so byte fixtures remain stable.

---

## 5. Authority and hostname policy

Hostname disclosure is explicit.

### 5.1 Local paths

A local POSIX or drive-letter path SHALL default to an empty authority:

```text
file:///home/alice/project
file:///C:/Development/Icod
```

`Icod.Terminal` SHALL NOT automatically call host-name APIs, inspect shell
variables, inspect SSH variables, or derive a remote host from the environment.

### 5.2 Explicit authority

The later semantic API MAY allow a caller to provide an explicit host authority
for a location. If provided, that authority SHALL be treated as intentional
metadata disclosure and encoded/validated independently from the path.

The 0.5 authority contract SHALL permit only a URI host component. It SHALL NOT
permit:

- userinfo (`user@host`);
- passwords;
- ports;
- path data embedded in the authority;
- query or fragment delimiters;
- empty explicit authority values masquerading as host data.

T38 SHALL implement host parsing/validation deterministically. ASCII DNS names,
IPv4 literals, and bracketed IPv6 literals are in scope. Internationalized host
names are out of scope for 0.5 unless a later T38 review proves a small,
deterministic IDNA contract without widening the public API.

### 5.3 `localhost`

An explicit caller-supplied `localhost` authority is permitted, but the library
SHALL NOT synthesize it merely to fill an authority field. Empty authority and
explicit `localhost` therefore remain observably different caller choices.

---

## 6. Native path contract

The core location encoder SHALL be host-independent. A path's syntax is an
explicit input property; it SHALL NOT be inferred from whichever operating
system happens to execute the encoder test.

### 6.1 POSIX paths

A POSIX location MUST be absolute and begin with `/`.

Examples:

```text
/                         -> file:///
/usr/local/src            -> file:///usr/local/src
/home/alice/My Project    -> file:///home/alice/My%20Project
```

Repeated separators, dot segments, and trailing separators are preserved as
caller-supplied path structure. T38 SHALL NOT perform filesystem lookup,
`realpath`, symlink resolution, or hidden lexical canonicalization.

A relative POSIX path is rejected.

### 6.2 Windows drive-letter paths

A Windows drive location MUST be fully qualified with an alphabetic drive
letter, colon, and root separator.

Examples:

```text
C:\                    -> file:///C:/
C:\Development\Icod   -> file:///C:/Development/Icod
c:\Temp                -> file:///C:/Temp
```

The encoder SHALL:

- convert native backslash separators to URI `/` separators;
- normalize the drive letter to uppercase for deterministic output;
- preserve the drive colon as the RFC 8089 drive delimiter;
- reject drive-relative forms such as `C:foo`;
- reject bare rooted forms such as `\foo` where no drive/UNC authority is
  supplied;
- reject legacy `C|` drive spellings;
- reject Windows device/extended namespace paths (`\\?\`, `\\.\`) in 0.5.

The last rule keeps OSC 7 semantics in the portable `file:` URI domain rather
than exposing NT namespace syntax that terminal emulators are not expected to
interpret consistently.

### 6.3 UNC paths

Standard UNC paths are supported in 0.5 using the RFC 8089 authority-mapped
form:

```text
\\server\share\dir
    -> file://server/share/dir
```

Rules:

- the UNC server becomes the URI authority;
- the share name becomes the first path segment;
- server and share must both be present;
- the path emitted after the authority begins with `/`;
- UNC paths SHALL NOT use the alternate `file:////server/...` transformed-path
  representation;
- device/extended UNC namespace forms remain out of scope.

Because UNC server naming itself discloses an authority, using a UNC path is an
explicit disclosure action by the caller.

---

## 7. Percent-encoding and Unicode policy

URI construction is performed from structured native path data exactly once.
The input is **not** treated as an already escaped URI.

For each path segment:

1. validate the managed string as well-formed Unicode;
2. encode Unicode scalar values as strict UTF-8;
3. emit RFC 3986 unreserved ASCII bytes literally:
   `A-Z a-z 0-9 - . _ ~`;
4. percent-encode all other data bytes using uppercase hexadecimal digits;
5. emit `/` only for structural path separators;
6. for a Windows drive URI, emit the single drive colon after the normalized
   drive letter as structure rather than path data.

Consequences include:

```text
space       -> %20
%           -> %25
#           -> %23
?           -> %3F
ESC         -> rejected before URI construction
é           -> %C3%A9
猫          -> %E7%8C%AB
```

A caller input segment containing the literal text `%20` therefore represents
three literal filename characters and is emitted as `%2520`. This is not
"double escaping"; it is single encoding of the caller's native-path data.

Raw `#` and `?` SHALL never enter the URI payload as path-data delimiters, so
OSC 7 emitted by this library cannot accidentally grow URI fragment or query
semantics.

Malformed UTF-16, including unpaired surrogates, is rejected.

---

## 8. Dot segments, separators, and filesystem normalization

T37 deliberately separates URI encoding from filesystem normalization.

The encoder SHALL NOT implicitly:

- resolve `.` or `..`;
- collapse repeated POSIX `/` separators;
- resolve symlinks;
- resolve mount points;
- normalize Unicode normalization forms;
- change path case except the Windows drive-letter normalization defined above;
- test whether the directory exists;
- require the path to be the process's real current directory.

Trailing separators are preserved.

This allows an application to publish a logical current location without
mutating process-global current-directory state or performing filesystem I/O.

---

## 9. Resource limit

OSC 7 SHALL NOT inherit the 0.4 title payload's 4096-byte limit automatically.
Location URIs can legitimately be substantially longer than titles.

For 0.5 the maximum encoded `file:` URI payload SHALL be:

```text
16384 bytes
```

The count is the exact ASCII byte count of the fully constructed URI payload,
including `file://`, authority text, slashes, and percent-escape triplets, but
excluding OSC framing bytes.

The limit is an `Icod.Terminal` resource policy, not a claim that RFC 3986,
RFC 8089, iTerm2, VTE, or ECMA-48 specifies such a maximum.

The full URI SHALL be constructed and validated against this limit before the
first OSC byte is written. One byte over the limit is rejected with zero
partial terminal output.

---

## 10. Explicit publication and privacy

OSC 7 publication can reveal directory names, source-tree structure, user
names, mount layout, network share names, and host identity.

`Icod.Terminal` therefore SHALL NOT emit OSC 7 automatically:

- while opening `TerminalSession`;
- because `Environment.CurrentDirectory` changes;
- while acquiring or releasing a presentation lease;
- while enabling/disabling input protocols;
- while running terminal queries;
- while setting titles;
- during disposal/restoration.

Only an explicit semantic caller operation may publish a location.

T40 MAY expose a convenience operation which reads
`Environment.CurrentDirectory`, but invoking that operation itself must be the
explicit disclosure decision. The library shall not subscribe to or monitor
process-current-directory changes.

---

## 11. Endpoint and support semantics

OSC 7 follows the 0.4 semantic-operation policy.

If the session already knows that its output endpoint is not a terminal, the
operation SHALL fail without emitting OSC bytes.

If output is a terminal but OSC 7 support is unknown, explicit publication MAY
be emitted optimistically.

Successful completion means only that a complete valid OSC 7 frame was written
to the session output. It does not prove that:

- the terminal recognized OSC 7;
- the terminal retained the URI;
- the terminal will use the URI for new tabs/splits or hyperlink resolution;
- the filesystem location exists;
- the explicit host authority is reachable.

`TERM`, terminfo identity, terminal product names, and operating-system identity
shall not be fabricated into proof of support.

---

## 12. Cancellation, ordering, flushing, and output failure

The operation SHALL reuse the session-owned output-ordering boundary established
in 0.4.

Rules:

- cancellation observed before transmission begins emits nothing;
- URI/path validation completes before transmission begins;
- once complete-frame transmission is committed, ordinary caller cancellation
  does not intentionally truncate the OSC frame;
- OSC 7 does not implicitly flush;
- transport/output failures propagate as transport/output failures rather than
  being converted to a support result;
- OSC 7 emission serializes with `WriteTextAsync(...)`, OSC 0/1/2 semantic title
  operations, active queries, presentation transitions, and rich-input protocol
  transitions;
- direct writes through borrowed `session.Output` remain caller-synchronized.

---

## 13. Public API direction, not yet final spelling

T37 freezes semantic requirements but does not prematurely freeze exact method or
supporting-type names.

T40 SHALL provide a normal application path where a caller supplies a native
absolute location and never needs to know:

- OSC selector 7;
- OSC/ST framing bytes;
- file-URI escaping rules.

Because path syntax must be deterministic independent of host OS, the public or
internal contract will need an explicit way to distinguish POSIX, Windows-drive,
and UNC path semantics. T38/T40 will determine the least-regrettable API shape.

A process-current-directory convenience operation may be added only if it can
reuse that same core encoding contract without creating implicit disclosure.

---

## 14. Compatibility notes

### iTerm2

iTerm2 explicitly documents OSC 7 with a `file` URL containing hostname and
path. The 0.5 canonical form is directly compatible with that model.

### GNOME VTE family

VTE exposes a current-directory URI property populated from OSC 7. The 0.5
contract supplies a standards-based `file:` URI suitable for this URI-valued
property.

### Other terminals

Other modern terminals may accept OSC 7, ignore it, or support different
shell-integration mechanisms. `Icod.Terminal` does not claim support merely from
terminal identity and does not emit proprietary fallback sequences in 0.5.

Proprietary forms such as iTerm2 OSC 1337 `CurrentDir` are deliberately not
emitted; OSC 7 remains the only current-location wire protocol in this release.

---

## 15. Explicit non-goals

T37 freezes the following as out of scope for 0.5:

- OSC 8 hyperlinks;
- OSC 52 clipboard/selection;
- arbitrary caller-provided URI schemes;
- arbitrary raw OSC selectors;
- automatic shell prompt integration;
- shell-hook installation;
- current-directory monitoring;
- automatic host-name or SSH-environment discovery;
- userinfo/password/port URI authorities;
- URL query strings or fragments;
- WHATWG/browser file-URL coercion behavior;
- filesystem existence checks;
- symlink or canonical-path resolution;
- Windows NT device/extended path namespaces;
- proprietary OSC 1337 current-directory emission;
- URI decoding or accepting pre-escaped URI strings as the ordinary OSC 7 API.

---

## 16. T38 implementation obligations

T38 may now implement the reusable location/URI encoder, but it must prove this
contract with host-independent fixtures before OSC 7 output integration begins.

At minimum T38 SHALL test:

- POSIX root and nested paths;
- Windows drive root/nested paths and drive-letter normalization;
- UNC authority mapping;
- spaces, literal `%`, `#`, `?`, and non-ASCII Unicode;
- literal escape-looking text such as `%20`;
- well-formed supplementary Unicode;
- malformed UTF-16 rejection;
- relative path rejection;
- invalid drive-relative and malformed UNC rejection;
- explicit authority validation;
- empty authority canonical local form;
- trailing/repeated separator preservation;
- exact 16384-byte URI boundary;
- one-byte-over rejection;
- identical output on Windows, Linux, and macOS for identical structured input;
- culture-independent output.

No OSC bytes are required in T38. That remains T39.

---

## 17. Gate T37

T37 is complete because the following are now written and frozen before
production OSC 7 implementation:

- protocol references;
- canonical OSC 7 wire form;
- `file:`-only URI policy;
- local/explicit authority behavior;
- POSIX path behavior;
- Windows drive behavior;
- UNC mapping;
- strict UTF-8 percent encoding;
- single-encoding rules;
- dot-segment and normalization behavior;
- 16384-byte resource limit;
- explicit privacy/disclosure policy;
- endpoint/support semantics;
- cancellation/output-ordering semantics;
- non-goals;
- T38 acceptance obligations.

The next tranche is **T38 — reusable URI/location encoder**.
