# Icod.Terminal 1.27 live evidence

The generated [1.27 matrix](../../1.27.0.md) includes the reviewed Windows Terminal and Kitty/WSL reports below. Every other lane and every unrecorded scenario remains NotRun.

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

## Reviewed Kitty/WSL checkpoint — 2026-10-06

These source-launcher observations identify `Icod.Terminal 1.27.0-alpha.1`, source commit `6c3bcdb5b209c30129bb23ec924f7ab031cd8542`, Kitty `0.32.2`, Ubuntu `24.04`, and WSL `2.6.1.0`. All three scenarios are revision 1. The recording shows a detached worktree in WSL's Linux filesystem, empty `git status --short`, and a completed `run-environment-kitty-wsl.sh` run. An earlier build was cancelled; the subsequent run produced these reports.

| Report | Outcome | What was observed |
| --- | --- | --- |
| [Appearance query](kitty-appearance-query.json) | Inconclusive | No conclusive appearance observation within the bounded scenario. |
| [Appearance reporting](kitty-appearance-reporting.json) | Unavailable | An explicit private-mode state 0 or 4. |
| [In-band resize](kitty-in-band-resize.json) | Unavailable | An explicit private-mode state 0 or 4. |

The JSON passed the same identity and bounded-content review as the Windows reports and is retained unchanged. This supplies the mediated WSL lane, with unavailable/missing observations. It does not supply positive feature witnesses or separate native input/resize observations. In particular, the standard unavailable note does not prove native fallback behavior.

The source launcher did not consume a CI package; no local package/symbol hashes accompanied these observations. The corresponding automated source candidate passed [workflow 37459014319](https://github.com/uniblab/Icod.Terminal/actions/runs/37459014319). Artifact `11411410642` has ZIP SHA-256 `04f84e9b421b035416e2137247defeef584a93a97da5eba3a3ce77313b42373b`. Its packages embed synthetic merge source `4d5acea4c8311051c27da9f7af96e1820d737ff9`, whose tree `7c7a3cf0fd06ca04ee4dfbe39c85590da38ddb03` matches the observed source checkout.

| CI candidate file | SHA-256 |
| --- | --- |
| Icod.Terminal.1.27.0-alpha.1.nupkg | `4f441d1148fb4fbeb1ce2bbbcb63b8984e8793ba121ea78ab009b61fa6480595` |
| Icod.Terminal.1.27.0-alpha.1.snupkg | `b708026670cb7f099f70a5804312ab9dea4152257a2988f44a7ffa9ce4784ec3` |

Kitty's [official changelog](https://sw.kovidgoyal.net/kitty/changelog/) places in-band resize support in 0.36.0 and dark/light appearance notification support in 0.38.1. Version 0.32.2 predates both additions. This explains the choice of a newer Kitty for the next live attempt; it does not qualify any untested version or guarantee an appearance change under WSLg.

Verification: all three JSON files compare byte-identically with the uploads; a second matrix render compares byte-identically with the checked-in matrix; fresh-package compatibility smoke against the corresponding CI package above passed on net8.0, net9.0, and net10.0. These checks validate evidence/schema and package consumption, not positive live terminal support.

## Reviewed Kitty 0.49.2 / WSL first run — 2026-10-06

The subsequent clean source run uses the same `6c3bcdb5b209c30129bb23ec924f7ab031cd8542` checkout, alpha.1, Ubuntu 24.04, WSL 2.6.1.0, and revision-1 scenarios, with Kitty upgraded to `0.49.2`. The screenshot shows empty Git status in the Linux worktree; the recording shows the completed query, bounded appearance wait, and operator-confirmed resize.

| Report | Outcome | What was observed |
| --- | --- | --- |
| [Appearance query](history/2026-10-06-kitty-0.49.2-first-run/kitty-0.49.2-appearance-query.json) | Pass | A bounded typed appearance observation. |
| [Appearance reporting](history/2026-10-06-kitty-0.49.2-first-run/kitty-0.49.2-appearance-reporting.json) | Inconclusive | Acquisition reached the change prompt, but no report arrived within the wait. The operator did not know how to change the palette; this does not establish unsupported reporting. |
| [In-band resize](history/2026-10-06-kitty-0.49.2-first-run/kitty-0.49.2-in-band-resize.json) | Pass | Initial and changed typed dimensions, with operator confirmation. Unknown-pixel behavior is not separately described by this report. |

All three JSON files passed identity/content review and are retained unchanged. The older 0.32.2 observations remain a separate exact-version environment. The matching source/CI artifact identity is the same as the previous Kitty checkpoint above. A positive appearance query and positive initial/changed resize witness now exist; an operator-induced appearance reporting witness and native input/resize fallback observations remain pending. The sample walkthrough supplies temporary color-change shortcuts for another appearance attempt.

Verification: original upload bytes and a second matrix render match; fresh-package compatibility smoke against the matching-source CI package passed on net8.0/net9.0/net10.0. The color-change walkthrough was checked against Kitty 0.49.2's `set-colors`, background-change callback, and appearance-reporting source; the next live attempt must still establish the event.

## Reviewed Kitty 0.49.2 / WSL successful rerun — 2026-10-06

The next clean Linux worktree run again identifies source `6c3bcdb5b209c30129bb23ec924f7ab031cd8542`, alpha.1, Kitty 0.49.2, Ubuntu 24.04, WSL 2.6.1.0, and scenario revision 1. The recording shows clean status, bounded query, appearance observation confirmation, and resize observation confirmation. The reviewed results are:

| Report | Outcome | What was observed |
| --- | --- | --- |
| [Appearance query](kitty-0.49.2-appearance-query.json) | Pass | A bounded typed appearance observation. |
| [Appearance reporting](kitty-0.49.2-appearance-reporting.json) | Pass | A typed Light report after the change prompt, with operator confirmation. This qualifies an operator-induced appearance/palette change; it does not establish automatic Windows-to-WSLg theme propagation. |
| [In-band resize](kitty-0.49.2-in-band-resize.json) | Pass | Initial and changed typed dimensions, with operator confirmation. |

The successful rerun supplies both selected feature witnesses. Native input/resize on an unavailable lane remains outstanding, so Task 9 is still partial and stable closure has not started. Package/source identity remains the matching-source CI checkpoint above; no local package hash was supplied.

Original JSON bytes are retained unchanged. The first run's Pass/Inconclusive/Pass JSON is retained unchanged in the linked `history/2026-10-06-kitty-0.49.2-first-run` directory. Matrix rendering reads only top-level JSON and rejects duplicate exact environment/scenario records; the successful rerun is the active representative for this exact Kitty version, and the history retains the earlier inconclusive result without rewriting it as success. Kitty 0.32.2 remains a separate active environment.

Verification: all six current/history JSON files compare byte-identically with their corresponding uploads; a repeated matrix render matches; fresh-package compatibility smoke against the matching-source CI package passed on net8.0/net9.0/net10.0. The next fallback commands passed shell syntax checking; their live results remain outstanding.

## Collecting further evidence

Run the environment launchers at the exact candidate commit. Review reports before adding JSON here: require exact package version, source SHA, terminal/OS/transport versions, scenario revision 1, and bounded observations. Do not accept raw replies, escape bytes, keystrokes, environment dumps, or host identity. Record package and symbol SHA-256 hashes beside the reviewed source checkpoint.

Stable acceptance needs query.appearance, an operator-induced environment.appearance-reporting event, initial and changed environment.in-band-resize observations, and a missing/unavailable lane whose native input and resize still work. Preserve Unavailable separately from Inconclusive. Historical 1.26 evidence remains unchanged.
