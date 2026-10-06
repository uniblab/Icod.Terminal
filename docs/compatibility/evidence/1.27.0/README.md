# Icod.Terminal 1.27 live evidence

The generated [1.27 matrix](../../1.27.0.md) includes the reviewed Windows Terminal reports below. Every other lane and every unrecorded scenario remains NotRun.

## Reviewed Windows Terminal checkpoint — 2026-10-06

These source-launcher observations identify `Icod.Terminal 1.27.0-alpha.1`, source commit `a2bd1553765dee585607ea63de2ac6440f343fec`, Windows Terminal `1.24.11911.0`, and Windows `10.0.26200.9457`. No separate transport was recorded. The launcher was `samples\Icod.Terminal.Compatibility.Sample\Run-Environment-WindowsTerminal.cmd`; all three scenarios are revision 1.

| Report | Outcome | What was observed |
| --- | --- | --- |
| [Appearance query](windows-terminal-appearance-query.json) | Inconclusive | No conclusive appearance observation within the bounded scenario. The report does not distinguish timeout from another endpoint/environment limitation. |
| [Appearance reporting](windows-terminal-appearance-reporting.json) | Unavailable | An explicit private-mode state 0 or 4; no operator-induced report was observed. |
| [In-band resize](windows-terminal-in-band-resize.json) | Unavailable | An explicit private-mode state 0 or 4; no initial or changed resize report was observed. |

The submitted JSON was reviewed for matching source/version/scenario identity, bounded notes, and absence of raw input, replies, environment dumps, or host identity. It is retained unchanged. The submitted matrix was generated before these reports were accepted and contained only NotRun lanes; the checked-in matrix is regenerated from the reviewed JSON.

These observations establish an unavailable/missing reporting lane only. The standard unavailable note describes intended fallback behavior; it is not a live observation of ordinary input or native resize. Separate `input.text-key/v1` and `lifecycle.resize-suspend/v1` reports are still required. Positive appearance query/reporting and initial/changed in-band resize witnesses also remain missing, so stable acceptance is incomplete.

The source launcher does not consume the downloadable CI package. No local package/symbol hashes were supplied with these source observations. For reference, the automated candidate for this source tree passed all ten jobs in [workflow 37454866718](https://github.com/uniblab/Icod.Terminal/actions/runs/37454866718). Its packages embed synthetic merge source `8c00e13a9cbf5d3059f976725fc23a32930b4cf6`, whose tree matches the observed checkout:

| CI candidate file | SHA-256 |
| --- | --- |
| Icod.Terminal.1.27.0-alpha.1.nupkg | `fca9fccfe131aebbca24f8ec9b6846aeb4efdc463a285f93627bc7ad26be9950` |
| Icod.Terminal.1.27.0-alpha.1.snupkg | `b8482c5f6de46b8b4a9e3e3658b348a6bac6f1953ce9175c419512748ae9b0fb` |

## Collecting further evidence

Run the environment launchers at the exact candidate commit. Review reports before adding JSON here: require exact package version, source SHA, terminal/OS/transport versions, scenario revision 1, and bounded observations. Do not accept raw replies, escape bytes, keystrokes, environment dumps, or host identity. Record package and symbol SHA-256 hashes beside the reviewed source checkpoint.

Stable acceptance needs query.appearance, an operator-induced environment.appearance-reporting event, initial and changed environment.in-band-resize observations, and a missing/unavailable lane whose native input and resize still work. Preserve Unavailable separately from Inconclusive. Historical 1.26 evidence remains unchanged.
