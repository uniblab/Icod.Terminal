# T121 — OSC 133 Byte-Exact Writer

**Release:** `0.12.0`  
**Tranche:** `T121`  
**Development version:** `0.12.0-alpha.2`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T121 implements the specialized internal OSC 133 wire layer defined by the frozen T120 semantic-prompt contract.

No public OSC 133 API is introduced in this tranche.

---

## 2. Canonical frames

T121 emits only the portable T120 forms using seven-bit OSC and ST termination:

```text
ESC ] 133 ; A ESC \
ESC ] 133 ; B ESC \
ESC ] 133 ; C ESC \
ESC ] 133 ; D ESC \
ESC ] 133 ; D ; status ESC \
```

The marker letters remain internal wire details.

---

## 3. Semantic encoder entry points

The internal writer exposes semantic helpers for:

- prompt start;
- command-input start;
- command-output start;
- completed command with exit status;
- aborted command.

No raw marker character or arbitrary OSC 133 payload is accepted by the public surface.

---

## 4. Exit-status encoding

Completed commands accept a `byte` exit status and encode it as minimal ASCII decimal text.

Representative frozen encodings:

```text
0   -> D;0
9   -> D;9
10  -> D;10
99  -> D;99
100 -> D;100
255 -> D;255
```

Bare `D` remains a distinct abort/cancel marker and is never used as an alias for status `0`.

---

## 5. Commit semantics

Each internal write helper:

1. validates output;
2. observes caller cancellation before commit;
3. writes one complete OSC frame;
4. uses `CancellationToken.None` for the committed transport write;
5. performs no implicit flush.

T121 does not emit compensating OSC 133 markers after transport failure.

---

## 6. Tests

`Osc133SemanticPromptWriterTests` proves:

- byte-exact `A` framing;
- byte-exact `B` framing;
- byte-exact `C` framing;
- byte-exact bare `D` framing;
- bare `D` differs from `D;0`;
- decimal status encoding at one-, two-, and three-digit boundaries;
- maximum portable status `255`;
- exactly one committed write;
- committed write uses a non-cancellable token;
- no implicit flush;
- pre-cancelled calls emit nothing;
- null output is rejected.

---

## 7. T121 decision

The portable OSC 133 wire grammar is now isolated behind a byte-exact semantic writer. T122 may build the public/internal semantic marker model without exposing protocol letters or raw OSC construction.
