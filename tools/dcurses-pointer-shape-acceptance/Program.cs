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

byte[] pointerFrame = Encoding.ASCII.GetBytes( "\u001b]22;pointer\u001b\\" );
byte[] waitFrame = Encoding.ASCII.GetBytes( "\u001b]22;wait\u001b\\" );
byte[] resetFrame = Encoding.ASCII.GetBytes( "\u001b]22;\u001b\\" );
RecordingOutput output = new();
RecordingTerminalControlProvider provider = new();
TerminalDescription terminal = new TerminalDescriptionBuilder(
	"dcurses-pointer-shape-acceptance"
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
TerminalPointerShapeLease outer = await terminalSession.AcquirePointerShapeAsync(
	TerminalPointerShape.Pointer
);

curses.StandardScreen.Write( "A" );
await curses.RefreshAsync();

TerminalPointerShapeLease inner = await terminalSession.AcquirePointerShapeAsync(
	TerminalPointerShape.Wait
);
curses.StandardScreen.Write( "B" );
await curses.RefreshAsync();

await inner.DisposeAsync();
curses.StandardScreen.Write( "C" );
await curses.RefreshAsync();

await outer.DisposeAsync();

int initialPointerIndex = output.IndexOf(
	pointerFrame,
	0
);
int waitIndex = output.IndexOf(
	waitFrame,
	initialPointerIndex + 1
);
int restoredPointerIndex = output.IndexOf(
	pointerFrame,
	waitIndex + 1
);
int resetIndex = output.IndexOf(
	resetFrame,
	restoredPointerIndex + 1
);
Require( 0 <= initialPointerIndex, "The initial pointer shape frame was not emitted." );
Require( initialPointerIndex < waitIndex, "The nested wait pointer shape did not follow the initial pointer shape." );
Require(
	1 < waitIndex - initialPointerIndex,
	"No DCurses refresh payload was emitted while the outer pointer shape was active."
);
Require(
	waitIndex < restoredPointerIndex,
	"Disposing the nested pointer lease did not restore the outer pointer shape."
);
Require(
	1 < restoredPointerIndex - waitIndex,
	"No DCurses refresh payload was emitted while the nested pointer shape was active."
);
Require(
	restoredPointerIndex < resetIndex,
	"The final terminal-policy reset did not follow outer pointer restoration."
);
Require(
	1 < resetIndex - restoredPointerIndex,
	"No DCurses refresh payload was emitted after restoring the outer pointer shape."
);

Console.WriteLine(
	"Icod.DCurses RefreshAsync terminal pointer-shape acceptance passed."
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
