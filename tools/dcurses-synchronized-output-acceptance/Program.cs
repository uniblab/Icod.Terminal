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

RecordingOutput output = new();
RecordingTerminalControlProvider provider = new();
TerminalDescription terminal = new TerminalDescriptionBuilder(
	"dcurses-synchronized-output-acceptance"
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
curses.StandardScreen.Write( "X" );

int beforeRefresh = output.WriteCount;
TerminalSynchronizedOutputLease synchronized =
	await terminalSession.AcquireSynchronizedOutputAsync();
await curses.RefreshAsync();
await synchronized.DisposeAsync();

Require(
	beforeRefresh < output.WriteCount,
	"DCurses refresh produced no terminal output."
);
int beginIndex = output.IndexOf(
	CsiWriter.EncodeSynchronizedOutputBeginFrame()
);
int endIndex = output.IndexOf(
	CsiWriter.EncodeSynchronizedOutputEndFrame()
);
Require(
	0 <= beginIndex,
	"The synchronized-output begin frame was not emitted."
);
Require(
	beginIndex < endIndex,
	"The synchronized-output end frame did not follow the begin frame."
);
Require(
	1 < endIndex - beginIndex,
	"No DCurses refresh payload was emitted inside synchronized output."
);
Require(
	0 < output.FlushCount,
	"The synchronized-output final flush was not observed."
);

Console.WriteLine(
	"Icod.DCurses RefreshAsync synchronized-output acceptance passed."
);

internal sealed class EmptyInput : Icod.Terminal.ITerminalInput {
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

internal sealed class RecordingOutput : Icod.Terminal.ITerminalOutput {
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
			for ( int index = 0; index < this.writes.Count; index++ ) {
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
