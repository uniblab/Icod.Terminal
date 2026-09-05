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

Func<CancellationToken, ValueTask> beginPrompt = session.BeginPromptAsync;
Func<CancellationToken, ValueTask> beginInput = session.BeginCommandInputAsync;
Func<CancellationToken, ValueTask> beginOutput = session.BeginCommandOutputAsync;
Func<byte, CancellationToken, ValueTask> finish = session.FinishCommandAsync;
Func<CancellationToken, ValueTask> abort = session.AbortCommandAsync;
_ = beginPrompt;
_ = beginInput;
_ = beginOutput;
_ = finish;
_ = abort;

await session.BeginPromptAsync();
await session.BeginCommandInputAsync();
await session.BeginCommandOutputAsync();
await session.FinishCommandAsync( 0 );
await session.AbortCommandAsync();

byte[][] expected = [
	Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
	Encoding.ASCII.GetBytes( "\u001b]133;D\u001b\\" )
];
Require(
	expected.Length == output.WriteCount,
	$"Expected {expected.Length} OSC 133 writes; observed {output.WriteCount}."
);
for ( int index = 0; index < expected.Length; ++index ) {
	Require(
		expected[ index ].AsSpan().SequenceEqual( output.GetWrite( index ) ),
		$"Unexpected OSC 133 frame at index {index}."
	);
}
Require(
	0 == output.FlushCount,
	"OSC 133 semantic-prompt operations unexpectedly flushed output."
);
Require(
	output.WriteCancellationTokens.All( token => !token.CanBeCanceled ),
	"A committed OSC 133 package write retained caller cancellation."
);

Console.WriteLine(
	"Icod.Terminal package OSC 133 semantic-prompt smoke passed."
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
	private readonly List<CancellationToken> writeCancellationTokens = [];
	private int flushCount;

	internal int WriteCount {
		get {
			return this.writes.Count;
		}
	}

	internal IReadOnlyList<CancellationToken> WriteCancellationTokens {
		get {
			return this.writeCancellationTokens;
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
		this.writes.Add( buffer.ToArray() );
		this.writeCancellationTokens.Add( cancellationToken );
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
			"Size is not required by the package semantic-prompt smoke."
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
