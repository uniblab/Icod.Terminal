# C160 — Complete CSI Grammar Foundation

## Status

Implementation started for `Icod.Terminal 1.6.0`.

C160 formalizes one reusable internal CSI syntax layer over the N153 structural control-frame representation. It does not add a public raw CSI writer and does not assign operation-specific meaning to arbitrary CSI final bytes.

## Grammar boundary

The normalized CSI form is:

```text
7-bit: ESC [ <parameter bytes> <intermediate bytes> <final byte>
8-bit: CSI   <parameter bytes> <intermediate bytes> <final byte>
```

The syntax ranges are retained exactly:

```text
parameter bytes      0x30–0x3F
intermediate bytes   0x20–0x2F
final byte           0x40–0x7E
```

The family scanner and `TerminalControlFrameStructure` remain responsible for framing/order validity. C160 adds structured parameter/subparameter tokenization after a complete CSI frame has already been recognized.

## Private parameter bytes

Leading bytes in the private-use parameter range:

```text
0x3C–0x3F
```

are retained separately as `PrivateParameterBytes` while also remaining present in `RawParameterBytes`.

C160 does not assume that every private-use byte elsewhere in the parameter region is invalid. The syntax layer remains lossless; stricter interpretation belongs to the consuming dialect.

## Parameters and subparameters

After any leading private-use bytes, the remaining parameter data is tokenized structurally:

```text
;   parameter separator
:   subparameter separator
```

No numeric coercion occurs in C160.

This preserves distinctions such as:

```text
CSI c        no parameter bytes
CSI ;c       two empty parameter components
CSI 0c       explicit numeric text "0"
CSI :1c      one parameter with an empty first subparameter and "1" second subparameter
```

Whether an empty component means default, zero, inherited value, or invalid input is dialect-specific and belongs to C161 or the consuming protocol.

## Bounds

The initial internal limits are:

```text
raw parameter bytes       1024
parameter components        64
subparameters per parameter 64
```

These bounds are intentionally larger than current DA/DSR/CPR needs while preventing a recognized CSI prefix from becoming an unbounded allocation/tokenization surface.

## Relationship to N153

C160 does not replace `TerminalControlFrameStructure`.

```text
N151/N152 scanner
    -> complete CSI frame
    -> N153 TerminalControlFrameStructure
       parameter/intermediate/final regions
    -> C160 TerminalCsiSyntax
       private prefix + parameter/subparameter tokens
    -> C161+ dialect semantics
```

This keeps framing, syntax, and semantic interpretation separate.

## Compatibility

C160 is internal. Existing CSI query implementations have not yet been rerouted through it; that migration belongs to C161/C162 after the syntax layer is independently qualified.

No existing public output bytes or query semantics are intentionally changed by C160.

## Acceptance

C160 acceptance requires tests proving:

- 7-bit CSI syntax;
- 8-bit CSI syntax;
- private-use prefix retention;
- intermediate-byte retention;
- semicolon parameter tokenization;
- colon subparameter tokenization;
- empty component preservation;
- no-parameter versus empty-parameter distinction;
- lossless retention of otherwise uninterpreted parameter bytes;
- raw-parameter, parameter-count, and subparameter-count bounds.
