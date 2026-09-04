namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T57 explicit OSC 52 clipboard-read API on the shared query substrate.
/// </summary>
public sealed class TerminalOsc52QueryTests {
	[Theory]
	[InlineData( TerminalClipboardSelection.Clipboard, 'c' )]
	[InlineData( TerminalClipboardSelection.Primary, 'p' )]
	[InlineData( TerminalClipboardSelection.Secondary, 'q' )]
	[InlineData( TerminalClipboardSelection.Select, 's' )]
	public async Task ExplicitReadUsesCanonicalQueryAndReturnsDecodedBytes(
		TerminalClipboardSelection selection,
		char selector
	) {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			selection,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]52;{selector};?\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( 1, transport.FlushCount );

		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]52;{selector};AAECA/8=\u001b\\" )
		);

		Assert.Equal(
			new byte[] { 0, 1, 2, 3, 255 },
			await query
		);
	}

	[Fact]
	public async Task BelTerminatedResponseIsAccepted() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;c;SGVsbG8=\u0007" )
		);

		Assert.Equal( Encoding.ASCII.GetBytes( "Hello" ), await query );
	}

	[Fact]
	public async Task C1ResponseIsAccepted() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Select,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			[
				0x9D,
				(byte)'5',
				(byte)'2',
				(byte)';',
				(byte)'s',
				(byte)';',
				(byte)'Z',
				(byte)'g',
				(byte)'=',
				(byte)'=',
				0x9C
			]
		);

		Assert.Equal( new byte[] { (byte)'f' }, await query );
	}

	[Theory]
	[InlineData( "\u001b]52;c;SGVsbG8_\u001b\\" )]
	[InlineData( "\u001b]52;c;Zh==\u001b\\" )]
	public async Task CorrelatedMalformedResponseFailsDeterministically(
		string response
	) {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.Latin1.GetBytes( response ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task WrongSelectionDoesNotSatisfyQuery() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromMilliseconds( 50 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;p;SGVsbG8=\u001b\\" )
		);

		await Assert.ThrowsAsync<TimeoutException>( () => query );
	}

	[Fact]
	public async Task CancellationBeforeEmissionWritesNothing() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.ReadClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				TimeSpan.FromSeconds( 1 ),
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.FlushCount );
	}

	[Fact]
	public async Task RedirectedOutputIsRejectedWithoutQueryEmission() {
		Osc52Transport transport = new();
		RecordingTerminalControlProvider provider = new() {
			OutputIsTerminal = false
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			provider,
			new TerminalSessionOptions {
				RequireInteractiveOutput = false,
				TerminalOverride = TerminalProfiles.Dumb
			}
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.ReadClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				TimeSpan.FromSeconds( 1 )
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.FlushCount );
	}

	[Fact]
	public async Task InvalidSelectionAndTimeoutAreRejectedBeforeOutput() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.ReadClipboardAsync(
				(TerminalClipboardSelection)int.MaxValue,
				TimeSpan.FromSeconds( 1 )
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.ReadClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				TimeSpan.FromMilliseconds( -1 )
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		Osc52Transport transport,
		RecordingTerminalControlProvider? provider = null,
		TerminalSessionOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			provider ?? new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			options
			?? new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		Osc52Transport transport,
		int count
	) {
		ArgumentNullException.ThrowIfNull( transport );

		for ( int attempt = 0; attempt < 500; attempt++ ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}

		throw new TimeoutException( "The expected terminal query write was not observed." );
	}

	private sealed class Osc52Transport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private byte[]? pending;
		private int pendingOffset;
		private int flushCount;

		internal int WriteCount {
			get {
				lock ( this.writes ) {
					return this.writes.Count;
				}
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
			lock ( this.writes ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException( "The test input channel is closed." );
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

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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

		internal bool OutputIsTerminal {
			get;
			init;
		} = true;

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = !ReferenceEquals(
				endpoint,
				TerminalEndpoint.StandardOutput
			)
				|| this.OutputIsTerminal;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					isTerminal ? TerminalPlatformKind.PosixTermios : null,
					isTerminal
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
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
}
