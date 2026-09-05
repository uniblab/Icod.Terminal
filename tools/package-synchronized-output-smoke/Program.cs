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
	new PackageTerminalControlProvider(),
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	new EmptyInput(),
	output,
	new TerminalSessionOptions {
		TerminalOverride = TerminalProfiles.Dumb,
		ConfigureOutput = false,
		ObserveLifecycleEvents = false
	}
);

TerminalSynchronizedOutputLease outer =
	await session.AcquireSynchronizedOutputAsync();
TerminalSynchronizedOutputLease inner =
	await session.AcquireSynchronizedOutputAsync();

Require(
	1 == output.WriteCount,
	"Nested package synchronized-output acquisition emitted more than one begin frame."
);
Require(
	output.ContainsWrite( "\u001b[?2026h" ),
	"The package synchronized-output lease did not emit the canonical begin frame."
);

await inner.DisposeAsync();
Require(
	1 == output.WriteCount,
	"Non-final package synchronized-output release emitted terminal output."
);
Require(
	0 == output.FlushCount,
	"Non-final package synchronized-output release flushed output."
);

await session.WriteTextAsync( "package synchronized output" );
await outer.DisposeAsync();

Require(
	output.ContainsWrite( "\u001b[?2026l" ),
	"The package synchronized-output lease did not emit the canonical end frame."
);
Require(
	1 == output.FlushCount,
	"Final package synchronized-output release did not contribute exactly one flush."
);

await outer.DisposeAsync();
Require(
	1 == output.FlushCount,
	"Successful synchronized-output lease disposal was not idempotent."
);

Console.WriteLine( "Icod.Terminal synchronized-output package smoke test passed." );

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

	internal bool ContainsWrite(
		string text
	) {
		ArgumentNullException.ThrowIfNull( text );
		byte[] expected = Encoding.ASCII.GetBytes( text );
		return this.writes.Any(
			value => value.AsSpan().SequenceEqual( expected )
		);
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

internal sealed class PackageTerminalControlProvider : ITerminalControlProvider {
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
			"Size is not required by the synchronized-output package smoke."
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
