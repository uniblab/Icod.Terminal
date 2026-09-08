/*
	Icod.Terminal.SemanticPrompt.Sample
	Sample application demonstrating Icod.Terminal SemanticPrompt features.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

// The original portable OSC 133 A/B/C/D path remains unchanged.
await session.BeginPromptAsync();
await session.WriteTextAsync(
	"demo> "
);

await session.BeginCommandInputAsync();
await session.WriteTextAsync(
	"echo hello\r\n"
);

await session.BeginCommandOutputAsync();
await session.WriteTextAsync(
	"hello\r\n"
);

await session.FinishCommandAsync( 0 );

// 0.15 adds typed prompt metadata. Emit extended fields only when the
// application/shell integration intentionally wants those semantics.
TerminalSemanticPromptOptions promptOptions = new(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
);

await session.BeginPromptAsync( promptOptions );
await session.WriteTextAsync(
	"continue> "
);
await session.BeginCommandInputAsync();
await session.WriteTextAsync(
	"printf 'café 😀'\r\n"
);

// Command-line publication is explicit. Do not publish text that may contain
// credentials, tokens, private paths, or other sensitive information unless
// that disclosure is appropriate for the application.
TerminalSemanticCommandOutputOptions commandOptions = new(
	"printf 'café 😀'"
);
await session.BeginCommandOutputAsync( commandOptions );
await session.WriteTextAsync(
	"café 😀\r\n"
);
await session.FinishCommandAsync( 0 );

// Abort remains a bare D marker with no status.
await session.BeginPromptAsync();
await session.WriteTextAsync(
	"demo> "
);
await session.BeginCommandInputAsync();
await session.WriteTextAsync(
	"cancelled command^C\r\n"
);
await session.AbortCommandAsync();

await session.WriteTextAsync(
	"OSC 133 semantic-prompt sample complete.\r\n"
);
