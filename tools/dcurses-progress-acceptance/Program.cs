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

byte[] stage10 = Encoding.ASCII.GetBytes( "\u001b]9;4;1;10\u0007" );
byte[] stage20 = Encoding.ASCII.GetBytes( "\u001b]9;4;1;20\u0007" );
byte[] indeterminate = Encoding.ASCII.GetBytes( "\u001b]9;4;3;0\u0007" );
byte[] attention70 = Encoding.ASCII.GetBytes( "\u001b]9;4;4;70\u0007" );
byte[] clear = Encoding.ASCII.GetBytes( "\u001b]9;4;0;0\u0007" );
RecordingOutput output = new();
RecordingTerminalControlProvider provider = new();
TerminalDescription terminal = new TerminalDescriptionBuilder(
	"dcurses-progress-acceptance"
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
TerminalProgressLease progress = await terminalSession.AcquireProgressAsync();

await progress.ReportAsync(
	1,
	10
);
curses.StandardScreen.Write( "A" );
await curses.RefreshAsync();

await progress.ReportAsync(
	2,
	10
);
await progress.SetIndeterminateAsync();
curses.StandardScreen.Write( "B" );
await curses.RefreshAsync();

await progress.ReportAsync(
	TerminalProgressState.Attention,
	7,
	10
);
await progress.DisposeAsync();

int stage10Index = output.IndexOf( stage10 );
int stage20Index = output.IndexOf( stage20 );
int indeterminateIndex = output.IndexOf( indeterminate );
int attentionIndex = output.IndexOf( attention70 );
int clearIndex = output.IndexOf( clear );
Require( 0 <= stage10Index, "The 10 percent progress frame was not emitted." );
Require( stage10Index < stage20Index, "The 20 percent progress frame did not follow 10 percent." );
Require(
	1 < stage20Index - stage10Index,
	"No DCurses refresh payload was emitted between stage 1 and stage 2 progress."
);
Require(
	stage20Index < indeterminateIndex,
	"Indeterminate progress did not follow stage 2 progress."
);
Require(
	indeterminateIndex < attentionIndex,
	"Attention progress did not follow indeterminate progress."
);
Require(
	1 < attentionIndex - indeterminateIndex,
	"No DCurses refresh payload was emitted while indeterminate progress was active."
);
Require(
	attentionIndex < clearIndex,
	"The terminal progress clear frame did not follow the final attention state."
);

Console.WriteLine(
	"Icod.DCurses RefreshAsync terminal-progress acceptance passed."
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
	private int flushCount;

	internal int WriteCount {
		get {
			lock ( this.sync ) {
				return this.writes.Count;
			}
		}
	}

	internal int FlushCount {
		get {
			return Volatile.Read( ref this.flushCount );
		}
	}

	internal int IndexOf(
		byte[] expected
	) {
		ArgumentNullException.ThrowIfNull( expected );
		lock ( this.sync ) {
			for ( int index = 0; index < this.writes.Count; ++index ) {
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
		Interlocked.Increment( ref this.flushCount );
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
