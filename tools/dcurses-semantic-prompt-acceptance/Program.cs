/*
	Icod.Terminal.DCursesSemanticPromptAcceptance
	Downstream Icod.DCurses acceptance utility for Icod.Terminal integration contracts.
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
using System.Text;
using Icod.DCurses;
using Icod.Terminal;
using Icod.TermInfo;

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

byte[] promptFrame = Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" );
byte[] inputFrame = Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" );
byte[] outputFrame = Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" );
byte[] finishedFrame = Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" );
byte[] abortedFrame = Encoding.ASCII.GetBytes( "\u001b]133;D\u001b\\" );
byte[] extendedPromptFrame = Encoding.ASCII.GetBytes(
	"\u001b]133;A;redraw=0;special_key=1;k=s;click_events=2\u001b\\"
);
byte[] extendedOutputFrame = Encoding.ASCII.GetBytes(
	"\u001b]133;C;cmdline_url=printf%20caf%C3%A9%20%F0%9F%98%80\u001b\\"
);
byte[] extendedFinishedFrame = Encoding.ASCII.GetBytes( "\u001b]133;D;23\u001b\\" );
byte[] safeOsc9NotificationFrame = Encoding.ASCII.GetBytes(
	"\u001b]9;DCurses safe OSC 9 acceptance\u001b\\"
);
byte[] safeOsc9CurrentDirectoryFrame = Encoding.ASCII.GetBytes(
	"\u001b]9;9;C:\\work\\dcurses\u001b\\"
);
byte[] safeOsc9FinishedNotificationFrame = Encoding.ASCII.GetBytes(
	"\u001b]9;DCurses safe OSC 9 acceptance complete\u001b\\"
);
RecordingOutput output = new();
RecordingTerminalControlProvider provider = new();
TerminalDescription terminal = new TerminalDescriptionBuilder(
	"dcurses-semantic-prompt-acceptance"
)
	.SetString(
		StringCapability.CursorAddress,
		"<cup:%p1%d,%p2%d>"
	)
	.SetString(
		StringCapability.ExitAttributeMode,
		"<sgr0>"
	)
	.SetString(
		StringCapability.OriginalColorPair,
		"<op>"
	)
	.Build();

TerminalSession terminalSession = await TerminalSession.OpenAsync(
	provider,
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	new EmptyInput(),
	output,
	new TerminalSessionOptions {
		TerminalOverride = terminal,
		ConfigureOutput = false,
		ObserveLifecycleEvents = false
	}
);

CursesSessionOptions cursesOptions = new() {
	UseAlternateScreen = false,
	EnableKeypad = false,
	HideCursor = false
};
await using CursesSession curses = await CursesSession.OpenAsync(
	terminalSession,
	cursesOptions
);

await terminalSession.BeginPromptAsync();
curses.StandardScreen.Write( "prompt" );
await curses.RefreshAsync();

await terminalSession.BeginCommandInputAsync();
curses.StandardScreen.Write( " input" );
await curses.RefreshAsync();

await terminalSession.BeginCommandOutputAsync();
curses.StandardScreen.Write( " output" );
await curses.RefreshAsync();

await terminalSession.FinishCommandAsync( 0 );

await terminalSession.BeginPromptAsync();
curses.StandardScreen.Write( " next" );
await curses.RefreshAsync();
await terminalSession.BeginCommandInputAsync();
await terminalSession.AbortCommandAsync();

TerminalSemanticPromptOptions extendedPrompt = new(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
);
TerminalSemanticCommandOutputOptions extendedCommand = new(
	"printf café 😀"
);

await terminalSession.BeginPromptAsync( extendedPrompt );
curses.StandardScreen.Write( " extended" );
await curses.RefreshAsync();

await terminalSession.BeginCommandInputAsync();
curses.StandardScreen.Write( " command" );
await curses.RefreshAsync();

await terminalSession.BeginCommandOutputAsync( extendedCommand );
curses.StandardScreen.Write( " result" );
await curses.RefreshAsync();

await terminalSession.FinishCommandAsync( 23 );

await terminalSession.SendNotificationAsync(
	"DCurses safe OSC 9 acceptance"
);
curses.StandardScreen.Write( " notify" );
await curses.RefreshAsync();

await terminalSession.PublishWindowsCurrentDirectoryCompatibilityAsync(
	"C:\\work\\dcurses"
);
curses.StandardScreen.Write( " cwd" );
await curses.RefreshAsync();

await terminalSession.SendNotificationAsync(
	"DCurses safe OSC 9 acceptance complete"
);

int promptIndex = output.IndexOf(
	promptFrame,
	0
);
int inputIndex = output.IndexOf(
	inputFrame,
	promptIndex + 1
);
int commandOutputIndex = output.IndexOf(
	outputFrame,
	inputIndex + 1
);
int finishedIndex = output.IndexOf(
	finishedFrame,
	commandOutputIndex + 1
);
int nextPromptIndex = output.IndexOf(
	promptFrame,
	finishedIndex + 1
);
int nextInputIndex = output.IndexOf(
	inputFrame,
	nextPromptIndex + 1
);
int abortedIndex = output.IndexOf(
	abortedFrame,
	nextInputIndex + 1
);
int extendedPromptIndex = output.IndexOf(
	extendedPromptFrame,
	abortedIndex + 1
);
int extendedInputIndex = output.IndexOf(
	inputFrame,
	extendedPromptIndex + 1
);
int extendedOutputIndex = output.IndexOf(
	extendedOutputFrame,
	extendedInputIndex + 1
);
int extendedFinishedIndex = output.IndexOf(
	extendedFinishedFrame,
	extendedOutputIndex + 1
);
int safeOsc9NotificationIndex = output.IndexOf(
	safeOsc9NotificationFrame,
	extendedFinishedIndex + 1
);
int safeOsc9CurrentDirectoryIndex = output.IndexOf(
	safeOsc9CurrentDirectoryFrame,
	safeOsc9NotificationIndex + 1
);
int safeOsc9FinishedNotificationIndex = output.IndexOf(
	safeOsc9FinishedNotificationFrame,
	safeOsc9CurrentDirectoryIndex + 1
);

Require( 0 <= promptIndex, "The initial OSC 133 prompt marker was not emitted." );
Require( promptIndex < inputIndex, "The command-input marker did not follow the prompt marker." );
Require(
	1 < inputIndex - promptIndex,
	"No DCurses refresh payload was emitted between prompt and command-input markers."
);
Require( inputIndex < commandOutputIndex, "The command-output marker did not follow command input." );
Require(
	1 < commandOutputIndex - inputIndex,
	"No DCurses refresh payload was emitted between input and output markers."
);
Require( commandOutputIndex < finishedIndex, "The completion marker did not follow command output." );
Require(
	1 < finishedIndex - commandOutputIndex,
	"No DCurses refresh payload was emitted between output start and command completion."
);
Require( finishedIndex < nextPromptIndex, "The next prompt marker did not follow command completion." );
Require( nextPromptIndex < nextInputIndex, "The next command-input marker did not follow the next prompt." );
Require(
	1 < nextInputIndex - nextPromptIndex,
	"No DCurses refresh payload was emitted for the next prompt."
);
Require( nextInputIndex < abortedIndex, "The abort marker did not follow the second command-input marker." );

Require(
	abortedIndex < extendedPromptIndex,
	"The typed extended prompt marker did not follow the portable abort sequence."
);
Require(
	extendedPromptIndex < extendedInputIndex,
	"The command-input marker did not follow the typed extended prompt marker."
);
Require(
	1 < extendedInputIndex - extendedPromptIndex,
	"No DCurses refresh payload was emitted between the extended prompt and command-input markers."
);
Require(
	extendedInputIndex < extendedOutputIndex,
	"The typed cmdline_url command-output marker did not follow command input."
);
Require(
	1 < extendedOutputIndex - extendedInputIndex,
	"No DCurses refresh payload was emitted between extended command input and output markers."
);
Require(
	extendedOutputIndex < extendedFinishedIndex,
	"The extended command completion marker did not follow cmdline_url publication."
);
Require(
	1 < extendedFinishedIndex - extendedOutputIndex,
	"No DCurses refresh payload was emitted between extended output start and command completion."
);

Require(
	extendedFinishedIndex < safeOsc9NotificationIndex,
	"The safe OSC 9 notification did not follow the completed OSC 133 acceptance sequence."
);
Require(
	safeOsc9NotificationIndex < safeOsc9CurrentDirectoryIndex,
	"The OSC 9;9 current-directory compatibility frame did not follow the safe notification."
);
Require(
	1 < safeOsc9CurrentDirectoryIndex - safeOsc9NotificationIndex,
	"No DCurses refresh payload was emitted between the safe OSC 9 notification and OSC 9;9 compatibility frame."
);
Require(
	safeOsc9CurrentDirectoryIndex < safeOsc9FinishedNotificationIndex,
	"The closing safe OSC 9 notification did not follow OSC 9;9 compatibility publication."
);
Require(
	1 < safeOsc9FinishedNotificationIndex - safeOsc9CurrentDirectoryIndex,
	"No DCurses refresh payload was emitted between OSC 9;9 compatibility publication and the closing notification."
);

Console.WriteLine(
	"Icod.DCurses RefreshAsync OSC 133 and safe OSC 9 acceptance passed."
);

internal sealed class EmptyInput : ITerminalInput {
	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		_ = buffer;
		await Task.Delay(
			Timeout.InfiniteTimeSpan,
			cancellationToken
		).ConfigureAwait( false );
		return 0;
	}
}

internal sealed class RecordingOutput : ITerminalOutput {
	private readonly object sync = new();
	private readonly List<byte[]> writes = [];

	internal int IndexOf(
		byte[] expected,
		int startIndex
	) {
		ArgumentNullException.ThrowIfNull( expected );
		if ( 0 > startIndex ) {
			throw new ArgumentOutOfRangeException( nameof( startIndex ) );
		}
		lock ( this.sync ) {
			for ( int index = startIndex; index < this.writes.Count; ++index ) {
				if ( this.writes[ index ].AsSpan().SequenceEqual( expected ) ) {
					return index;
				}
			}
		}
		return -1;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		lock ( this.sync ) {
			this.writes.Add( buffer.ToArray() );
		}
		return ValueTask.CompletedTask;
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

internal sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
		0,
		0,
		0,
		0x0002UL,
		new byte[ 32 ],
		0,
		32,
		0,
		new TerminalSpeed( 13, 9600 ),
		new TerminalSpeed( 13, 9600 )
	);

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.LiveSize
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Available(
			new TerminalSize( 80, 24 )
		);
	}

	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalModeSnapshot>.Available(
			this.baseline
		);
	}

	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( mode );
		if ( !Enum.IsDefined( timing ) ) {
			throw new ArgumentOutOfRangeException( nameof( timing ) );
		}
		return TerminalControlMutationResult.Success();
	}
}
