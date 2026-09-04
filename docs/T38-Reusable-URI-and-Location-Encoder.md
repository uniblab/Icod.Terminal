# T38 — Reusable URI and Location Encoder

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Tranche:** T38 — reusable URI/location encoder  
**Development version:** `0.5.0-alpha.3`  
**Predecessor:** T37 — OSC 7 contract and reference freeze  
**Status:** Implemented; OSC emission remains deferred to T39

---

## 1. Purpose

T38 implements the deterministic native-filesystem-location to RFC 8089
`file:` URI encoder required by the T37 contract.

This tranche intentionally performs **no terminal I/O**. It proves URI/path
semantics independently before OSC selector 7 framing is introduced in T39.

---

## 2. Internal model

T38 adds an internal path grammar discriminator:

```csharp
internal enum TerminalLocationPathKind {
    Posix,
    WindowsDrive,
    WindowsUnc
}
```

and a reusable internal encoder:

```csharp
TerminalLocationUriEncoder.EncodeFileUri(
    path,
    pathKind,
    authority
);
```

The path kind is explicit rather than inferred from `OperatingSystem`, directory
separators, or the CI host. Identical structured input therefore has identical
output on Windows, Linux, and macOS.

The type remains internal in T38. T40 will decide the least-regrettable public
semantic representation after the encoder has implementation evidence.

---

## 3. Deterministic forms

The encoder implements the T37 canonical forms.

### POSIX

```text
/                      -> file:///
/usr/local/src         -> file:///usr/local/src
/home/a/My Project     -> file:///home/a/My%20Project
```

### Windows drive

```text
C:\                    -> file:///C:/
c:\Temp                -> file:///C:/Temp
C:\Development\Icod   -> file:///C:/Development/Icod
```

Drive letters normalize to uppercase. Backslash and slash are recognized as
Windows path separators and emitted as URI `/` separators.

### UNC

```text
\\server\share\dir -> file://server/share/dir
```

The UNC server becomes the URI authority and the share becomes the first path
segment. An independent explicit authority is rejected for UNC input.

---

## 4. UTF-8 and percent encoding

The encoder validates well-formed Unicode and encodes path segments with strict
UTF-8.

Only RFC 3986 unreserved ASCII bytes are emitted literally:

```text
A-Z a-z 0-9 - . _ ~
```

Every other path-data byte is emitted as `%HH` with uppercase hexadecimal.
Structural path separators remain `/`; the Windows drive colon remains the
single structural drive delimiter.

Examples:

```text
space -> %20
%     -> %25
#     -> %23
?     -> %3F
é     -> %C3%A9
猫    -> %E7%8C%AB
😀    -> %F0%9F%98%80
```

Native path text resembling an existing escape is encoded as native data:

```text
%20 -> %2520
```

No URI decoding step exists.

---

## 5. Preservation versus normalization

The encoder is not a filesystem canonicalizer.

It preserves:

- `.` and `..` path segments;
- repeated separators;
- trailing separators;
- path case other than Windows drive-letter normalization;
- Unicode normalization form.

It performs no filesystem I/O, existence check, symlink resolution, mount
resolution, or current-directory lookup.

---

## 6. Authority validation

T38 implements the narrow 0.5 authority contract:

- ASCII DNS names;
- IPv4 literals;
- bracketed IPv6 literals;
- explicit `localhost`.

It rejects:

- empty explicit authorities;
- userinfo;
- passwords/`@` forms;
- ports;
- unbracketed IPv6;
- path/query/fragment delimiters;
- malformed DNS labels;
- non-ASCII/IDNA host names in 0.5.

The encoder never discovers a host name from the environment or operating
system.

---

## 7. Invalid native forms

T38 rejects:

- relative POSIX paths;
- empty POSIX paths;
- Windows drive-relative paths such as `C:foo`;
- legacy `C|` drive forms;
- rooted Windows paths without a drive or UNC server;
- malformed UNC paths lacking a server or share;
- Windows device/extended namespace paths beginning `\\?\` or `\\.\`;
- malformed UTF-16;
- unknown path-kind values.

---

## 8. Resource bound

The T37 maximum is implemented as:

```csharp
TerminalLocationUriEncoder.MaximumEncodedUriByteCount == 16384
```

Because the resulting `file:` URI is ASCII-only after percent encoding, its
managed string length equals its encoded byte length.

The limit includes the complete URI payload (`file://`, authority, path, and
percent escapes) and excludes future OSC framing bytes.

Tests prove exact-boundary acceptance and one-byte-over rejection.

---

## 9. Test evidence

`TerminalLocationUriEncoderTests` covers:

- POSIX root/nested paths;
- spaces and reserved-looking characters;
- literal percent-escape-looking input;
- BMP and supplementary Unicode;
- dot-segment/repeated/trailing separator preservation;
- Windows drive roots/nested paths and drive normalization;
- UNC authority mapping;
- DNS, IPv4, and bracketed IPv6 authorities;
- invalid path grammars;
- invalid authorities;
- malformed Unicode;
- exact 16384-byte and one-byte-over resource cases;
- culture independence, including Turkish casing;
- unknown path-kind rejection.

The tests provide explicit path grammar values and therefore do not depend on
the host OS's path parser.

---

## 10. T38 gate

T38 is complete when the repository matrix proves the new fixtures on Windows,
Linux, and macOS.

No OSC 7 bytes are emitted by T38. The next tranche is:

**T39 — OSC 7 writer integration**, which will place this already-validated URI
payload inside the existing canonical OSC transport and prove complete-frame,
zero-partial-output, cancellation, and no-implicit-flush behavior.
