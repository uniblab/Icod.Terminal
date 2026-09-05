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

Console.WriteLine(
	"Icod.DCurses RefreshAsync OSC 133 semantic-prompt acceptance passed."
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
