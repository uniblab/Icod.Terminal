using System.Text;
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

await session.SetPointerShapeAsync(
	TerminalPointerShape.Default
);
await session.ResetPointerShapeAsync();

TerminalPointerShapeLease outer = await session.AcquirePointerShapeAsync(
	TerminalPointerShape.Pointer
);
TerminalPointerShapeLease middle = await session.AcquirePointerShapeAsync(
	TerminalPointerShape.Text
);
TerminalPointerShapeLease inner = await session.AcquirePointerShapeAsync(
	TerminalPointerShape.Wait
);
await middle.DisposeAsync();
int writesAfterNoncontrollingRelease = output.WriteCount;
await inner.DisposeAsync();
await outer.DisposeAsync();
int writesAfterRelease = output.WriteCount;
await outer.DisposeAsync();

Func<TimeSpan, CancellationToken, ValueTask<TerminalPointerShapeObservation>> currentQuery =
	session.QueryCurrentPointerShapeAsync;
Func<TimeSpan, CancellationToken, ValueTask<TerminalPointerShapeObservation>> defaultQuery =
	session.QueryDefaultPointerShapeAsync;
Func<TimeSpan, CancellationToken, ValueTask<TerminalPointerShapeObservation>> grabbedQuery =
	session.QueryGrabbedPointerShapeAsync;
Func<TerminalPointerShape, TimeSpan, CancellationToken, ValueTask<bool>> supportQuery =
	session.QueryPointerShapeSupportAsync;
_ = currentQuery;
_ = defaultQuery;
_ = grabbedQuery;
_ = supportQuery;

byte[][] expected = [
	Encoding.ASCII.GetBytes( "\u001b]22;default\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]22;\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]22;pointer\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]22;text\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]22;wait\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]22;pointer\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]22;\u001b\\" )
];
Require(
	expected.Length == output.WriteCount,
	$"Expected {expected.Length} pointer-shape writes; observed {output.WriteCount}."
);
Require(
	5 == writesAfterNoncontrollingRelease,
	"Out-of-order disposal of a non-controlling pointer owner emitted output."
);
for ( int index = 0; index < expected.Length; ++index ) {
	Require(
		expected[ index ].AsSpan().SequenceEqual( output.GetWrite( index ) ),
		$"Unexpected pointer-shape frame at index {index}."
	);
}
Require(
	writesAfterRelease == output.WriteCount,
	"Repeated successful pointer-shape disposal emitted additional output."
);
Require(
	0 == output.FlushCount,
	"Pointer-shape operations unexpectedly flushed output."
);

Console.WriteLine(
	"Icod.Terminal package pointer-shape smoke passed."
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
			"Size is not required by the package pointer-shape smoke."
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
