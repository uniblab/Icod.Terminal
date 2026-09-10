# E190 Reference Snapshot

This short companion records the protocol facts reviewed for the 1.9.0 unsolicited semantic-event contract freeze.

Primary authority:

`https://sw.kovidgoyal.net/kitty/desktop-notifications/`

Reviewed facts for the 1.9 scope:

- `a=report` requests activation/button interaction reports;
- `c=1` requests close reports;
- `p=buttons` carries button labels;
- button labels are UTF-8 strings separated by U+2028 LINE SEPARATOR;
- button responses use one-based button numbers;
- activation, button, close, and `untracked` close-tracking reports are the accepted unsolicited subset;
- the existing support query remains the source for advertised notification feature support;
- unsolicited reports remain terminal-controlled input and are not authenticated by identifier correlation.

The complete architectural contract is `docs/E190-Unsolicited-Semantic-Event-Contract-and-Reference-Freeze.md`.
