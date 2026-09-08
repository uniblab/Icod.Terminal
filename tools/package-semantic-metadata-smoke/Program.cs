/*
	Icod.Terminal.PackageSemanticMetadataSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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

TerminalSemanticPromptOptions prompt = new(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
);
TerminalSemanticPromptOptions defaultPrompt = default;
TerminalSemanticCommandOutputOptions command = new(
	"printf café 😀"
);
TerminalSemanticCommandOutputOptions defaultCommand = default;

Func<TerminalSession, TerminalSemanticPromptOptions, CancellationToken, ValueTask> beginPrompt =
	BindBeginPrompt;
Func<TerminalSession, TerminalSemanticCommandOutputOptions, CancellationToken, ValueTask> beginCommandOutput =
	BindBeginCommandOutput;

_ = prompt.Kind;
_ = prompt.ResizeBehavior;
_ = prompt.UseSpecialCursorKey;
_ = prompt.ClickMode;
_ = defaultPrompt;
_ = command.CommandLine;
_ = defaultCommand;
_ = beginPrompt;
_ = beginCommandOutput;

Console.WriteLine( "Icod.Terminal 0.15 OSC 133 extended semantic metadata package API smoke passed." );

static ValueTask BindBeginPrompt(
	TerminalSession session,
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginPromptAsync(
		options,
		cancellationToken
	);
}

static ValueTask BindBeginCommandOutput(
	TerminalSession session,
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginCommandOutputAsync(
		options,
		cancellationToken
	);
}
