using System.Text;
using Icod.Terminal;

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
await using TerminalSession session = await TerminalSession.OpenAsync(
	new RecordingTerminalControlProvider(),
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	new EmptyInput(),
	output,
	new TerminalSessionOptions {
		ConfigureOutput = false,
		ObserveLifecycleEvents = false
	}
);

TerminalProgressLease outer = await session.AcquireProgressAsync();
await outer.ReportAsync(
	1,
	10
);
TerminalProgressLease inner = await session.AcquireProgressAsync();
await outer.ReportAsync(
	2,
	10
);
await inner.SetIndeterminateAsync();
await outer.ReportAsync(
	TerminalProgressState.Attention,
	7,
	10
);
await inner.DisposeAsync();
await outer.DisposeAsync();
int writesAfterRelease = output.WriteCount;
await outer.DisposeAsync();

byte[][] expected = [
	Encoding.ASCII.GetBytes( "\u001b]9;4;1;10\u0007" ),
	Encoding.ASCII.GetBytes( "\u001b]9;4;1;20\u0007" ),
	Encoding.ASCII.GetBytes( "\u001b]9;4;3;0\u0007" ),
	Encoding.ASCII.GetBytes( "\u001b]9;4;4;70\u0007" ),
	Encoding.ASCII.GetBytes( "\u001b]9;4;0;0\u0007" )
];
Require(
	expected.Length == output.WriteCount,
	$"Expected {expected.Length} progress writes; observed {output.WriteCount}."
);
for ( int index = 0; index < expected.Length; ++index ) {
	Require(
		expected[ index ].AsSpan().SequenceEqual( output.GetWrite( index ) ),
		$"Unexpected terminal-progress frame at index {index}."
	);
}
Require(
	writesAfterRelease == output.WriteCount,
	"Repeated successful progress disposal emitted additional output."
);
Require(
	0 == output.FlushCount,
	"Terminal progress unexpectedly flushed output."
);

Console.WriteLine(
	"Icod.Terminal package terminal-progress smoke passed."
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
	private readonly List<byte[]> writes = [];
	private int flushCount;

	internal int WriteCount {
		get {
			return this.writes.Count;
		}
	}

	internal int FlushCount {
		get {
			return Volatile.Read( ref this.flushCount );
		}
	}

	internal byte[] GetWrite(
		int index
	) {
		return this.writes[ index ].ToArray();
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.writes.Add( buffer.ToArray() );
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
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
		TerminalConsoleDirection.Input,
		0x00000007u
	);

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.WindowsConsole,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Unsupported(
			"Size is not required by the package progress smoke."
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
