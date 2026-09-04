using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;

static void Require(
	bool condition,
	string message
) {
	ArgumentException.ThrowIfNullOrWhiteSpace( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

ClipboardTransport transport = new();
await using TerminalSession session = await TerminalSession.OpenAsync(
	new PackageTerminalControlProvider(),
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	transport,
	transport,
	new TerminalSessionOptions {
		ConfigureOutput = false,
		ObserveLifecycleEvents = false,
		TerminalOverride = TerminalProfiles.Dumb
	}
);

await session.WriteClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	"package"
);
Require(
	Encoding.ASCII.GetBytes( "\u001b]52;c;cGFja2FnZQ==\u001b\\" )
		.SequenceEqual( transport.GetWrite( 0 ) ),
	"The package-only OSC 52 write emitted unexpected bytes."
);
Require(
	0 == transport.FlushCount,
	"OSC 52 writes must not flush implicitly."
);

Task<byte[]> query = session.ReadClipboardAsync(
	TerminalClipboardSelection.Primary,
	TimeSpan.FromSeconds( 5 )
).AsTask();
await transport.WaitForWriteCountAsync( 2 );
Require(
	Encoding.ASCII.GetBytes( "\u001b]52;p;?\u001b\\" )
		.SequenceEqual( transport.GetWrite( 1 ) ),
	"The package-only OSC 52 query emitted unexpected bytes."
);
Require(
	1 == transport.FlushCount,
	"OSC 52 queries must flush the conversational request."
);
transport.Publish(
	Encoding.ASCII.GetBytes( "\u001b]52;p;AAEC/w==\u001b\\" )
);
byte[] payload = await query;
Require(
	new byte[] { 0, 1, 2, 255 }.SequenceEqual( payload ),
	"The package-only OSC 52 query returned unexpected decoded bytes."
);

Console.WriteLine( "Icod.Terminal package clipboard smoke test passed." );

internal sealed class ClipboardTransport : ITerminalInput, ITerminalOutput {
	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private readonly List<byte[]> writes = [];
	private readonly SemaphoreSlim writeSignal = new( 0 );
	private byte[]? pending;
	private int pendingOffset;
	private int flushCount;

	internal int FlushCount {
		get {
			return Volatile.Read( ref this.flushCount );
		}
	}

	internal byte[] GetWrite(
		int index
	) {
		lock ( this.writes ) {
			return this.writes[ index ].ToArray();
		}
	}

	internal void Publish(
		byte[] bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
			throw new InvalidOperationException( "The package clipboard input channel is closed." );
		}
	}

	internal async ValueTask WaitForWriteCountAsync(
		int count,
		CancellationToken cancellationToken = default
	) {
		if ( 0 > count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}

		while ( true ) {
			lock ( this.writes ) {
				if ( count <= this.writes.Count ) {
					return;
				}
			}
			await this.writeSignal.WaitAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( this.pending is null ) {
			this.pending = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			this.pendingOffset = 0;
		}

		int count = Math.Min(
			buffer.Length,
			this.pending.Length - this.pendingOffset
		);
		this.pending.AsSpan( this.pendingOffset, count ).CopyTo( buffer.Span );
		this.pendingOffset += count;
		if ( this.pendingOffset == this.pending.Length ) {
			this.pending = null;
			this.pendingOffset = 0;
		}
		return count;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		lock ( this.writes ) {
			this.writes.Add( buffer.ToArray() );
		}
		this.writeSignal.Release();
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
		TerminalControlCapabilities capabilities = TerminalControlCapabilities.Attachment;
		if ( TerminalEndpointKind.FileDescriptor == endpoint.Kind
			&& 0 == endpoint.FileDescriptor ) {
			capabilities |= TerminalControlCapabilities.ModeRead
				| TerminalControlCapabilities.ModeWrite;
		}

		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.WindowsConsole,
				capabilities
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Unsupported(
			"Size is not required by the package clipboard smoke."
		);
	}

	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
