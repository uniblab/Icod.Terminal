# C161 — Typed CSI Parameter Semantics

## Status

Implemented; Staging validation pending.

C161 adds bounded semantic conversion above the C160 `TerminalCsiSyntax` grammar without collapsing syntax distinctions or assigning one global CSI defaulting rule.

## Component states

A numeric component has exactly one of three states:

```text
Omitted
Empty
Numeric
```

These states are intentionally different.

For example:

```text
CSI m
    parameter 0 -> Omitted

CSI ;m
    parameter 0 -> Empty
    parameter 1 -> Empty

CSI 0m
    parameter 0 -> Numeric(0)
```

A dialect may later decide that omitted or empty means a particular default, but the shared semantic layer does not make that decision globally.

## Numeric conversion

Decimal conversion is:

- ASCII-decimal only;
- non-negative;
- overflow-safe;
- bounded by a caller-selected reviewed maximum;
- deterministic on malformed bytes.

The default reviewed maximum is:

```text
1,000,000
```

Individual protocols may impose a smaller maximum.

## Dialect-policy helpers

The shared layer provides helpers for dialects that require:

- no private parameter bytes;
- one exact private parameter prefix;
- no intermediate bytes;
- one expected final byte;
- no colon subparameters;
- all present parameters to contain explicit decimal values;
- a protocol-specific maximum parameter count.

These helpers are validation policy above C160. They do not alter the retained raw syntax.

## Existing CSI query migration

`TerminalCsiQueryProtocol` now parses DA/DSR/CPR responses through:

```text
TerminalControlFrameStructure
    -> TerminalCsiSyntax
    -> TerminalCsiParameterSemantics
    -> query-specific interpretation
```

The existing query-specific rules remain unchanged:

- Primary DA requires `?`, final `c`, no intermediates, at least one numeric parameter;
- Secondary DA requires `>`, final `c`, no intermediates, exactly three numeric parameters;
- standard DSR requires no private prefix, final `n`, no intermediates, one numeric status from 0 through 4;
- CPR requires no private prefix, final `R`, no intermediates, exactly two positive numeric parameters;
- all four reject empty numeric parameters and colon subparameters;
- the historical query parameter-count ceiling remains 32;
- the historical query numeric ceiling remains 1,000,000.

No emitted request bytes change.

## Compatibility

C161 is internal. It adds no public API and no generic CSI writer.

The migration removes the old query-specific decimal loop while preserving the released behavior and errors of the existing public query methods.

## Relationship to C162

C161 completes the reusable syntax-plus-semantic stack needed by C162.

C162 will audit and migrate the remaining existing CSI emitters/parsers, including DEC private modes, focus, paste, mouse, synchronized output, Kitty keyboard negotiation, and DECSCUSR, without changing released bytes.
