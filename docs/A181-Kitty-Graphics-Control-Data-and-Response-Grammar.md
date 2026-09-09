# A181 — Kitty Graphics Control-Data and Response Grammar

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A181  
**Status:** implementation starting

## Purpose

A181 defines the first typed Kitty Graphics dialect layer above the generic APC construction and structural parsing foundation completed by A180.

The protocol form used by Kitty Graphics is:

```text
ESC _ G <control-data> ; <payload-or-response-message> ESC \
```

A180 owns only the outer APC framing. A181 owns the `G` dialect marker, the reviewed control-data grammar required for capability probing, and strict parsing of correlated Kitty Graphics response messages.

A181 does **not** implement general image Base64 chunking, raster conversion, committed multi-frame output, live query registration, or backend routing. Those remain A182–A186 concerns.

## Reference

The reference protocol is the current Kitty terminal graphics protocol:

`https://sw.kovidgoyal.net/kitty/graphics-protocol/`

The protocol defines control data as comma-separated `key=value` pairs and defines its integer fields as 32-bit values. The support-probe example uses a non-zero image id and one RGB24 pixel:

```text
ESC _ G i=31,s=1,v=1,a=q,t=d,f=24;AAAA ESC \
```

followed later, in A185, by Primary DA as the synchronization barrier.

## Generated support-query payload

A181 freezes the application payload for the one-pixel direct RGB24 query as:

```text
Gi=<image-id>,s=1,v=1,a=q,t=d,f=24;AAAA
```

where:

- `i` is a caller-internal correlation value from `1` through `4294967295`;
- `s=1` and `v=1` describe one pixel;
- `a=q` selects the query action so the dummy image is not retained as normal image state;
- `t=d` selects direct transmission;
- `f=24` selects RGB24;
- `AAAA` is Base64 for one black RGB pixel (`00 00 00`).

The key order above is canonical for the Icod support query and matches the protocol's documented support-test example. A185 will own generation of collision-resistant/non-zero query ids and composition with Primary DA.

## Response framing and dialect marker

A181 parses already-framed APC responses through `TerminalControlFrameStructure`; it does not create another string scanner.

A response must:

- be an APC frame;
- have normalized valid APC framing, including reviewed seven-bit/eight-bit inbound forms;
- contain an application payload beginning with ASCII `G`;
- contain exactly one first `;` boundary between control data and the response message;
- contain non-empty control data and a non-empty response message.

The response message is not Base64. It is printable ASCII and is either:

```text
OK
```

or a protocol error string such as:

```text
ENOENT:<detail>
```

A181 classifies only exact `OK` as success. Other structurally valid printable response messages are retained as errors without inventing an error-code taxonomy prematurely.

## Control-data parsing

A181 response control data uses these rules:

- comma-separated fields;
- every field is exactly one ASCII letter key, `=`, and a non-empty printable ASCII value;
- empty fields are invalid;
- duplicate keys are invalid, including duplicate unknown keys;
- recognized numeric fields use unsigned decimal syntax with no sign and no leading/trailing whitespace;
- numeric overflow beyond `UInt32.MaxValue` is invalid;
- recognized response fields are `i`, `I`, and `p`;
- unknown structurally valid letter keys are accepted and ignored for forward compatibility;
- a correlated response for the A181/A185 probe must contain non-zero `i`.

`I` and `p` are retained when present because the protocol may include image-number and placement identity in acknowledgements. A181 does not expose a generic metadata dictionary.

## Bounds

A181 bounds both control data and response-message text independently to **1024 bytes**.

The existing response framer remains an outer bound. These dialect limits provide defense in depth and keep future matcher/correlation work independent of terminal-controlled message length.

The generated one-pixel support query is far below both A180's 8192-byte small APC frame ceiling and the future A183 direct-transfer payload limit.

## Public API boundary

A181 introduces no public API.

In particular it does not add:

- a public Kitty Graphics command object;
- a public arbitrary `key=value` dictionary;
- a raw APC/Kitty writer;
- public image ids or placement ids;
- public protocol response objects;
- a second input reader.

The public raster API remains the 1.7 `TerminalRasterImage` / `DisplayRasterAsync(...)` contract.

## Tests

A181 regression coverage must include:

- exact documented support-query payload for id `31`;
- `UInt32.MaxValue` query id;
- query id zero rejection;
- canonical APC composition of the support query;
- successful seven-bit `Gi=31;OK` response;
- successful eight-bit APC/C1-ST response normalization;
- printable protocol error retention;
- optional `I` and `p` parsing;
- forward-compatible unknown-key acceptance;
- duplicate-key rejection;
- missing/non-zero `i` enforcement;
- unsigned-decimal overflow, sign, and non-decimal rejection;
- malformed key/value fields;
- non-printable/non-ASCII response-message rejection;
- control/message bound enforcement;
- public API fingerprint unchanged from 1.7.

## Acceptance rule

A181 is accepted only on an exact PR head passing Windows, Linux, macOS, package candidate/public-API freeze, all four package shards, and the validated package artifact.

PR #46 remains draft after A181; A182–A189 remain required for release closure.
