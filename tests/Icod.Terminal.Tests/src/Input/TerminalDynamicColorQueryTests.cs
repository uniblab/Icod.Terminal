namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies common dynamic-color mutation, observation, and reset.
/// </summary>
public sealed class TerminalDynamicColorQueryTests {
	[Theory]
	[InlineData( TerminalDynamicColor.DefaultForeground, 10, 110 )]
	[InlineData( TerminalDynamicColor.DefaultBackground, 11, 111 )]
	[InlineData( TerminalDynamicColor.TextCursor, 12, 112 )]
	public async Task CommonColorsUseCanonicalSetQueryAndResetFrames(
		TerminalDynamicColor kind,
		int osc,
		int resetOsc
	) {
		DynamicColorTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalColor requested = new( 0x1234, 0x5678, 0x9abc );

		await session.SetDynamicColorAsync(
			kind,
			requested
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:1234/5678/9abc\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.False( transport.GetWriteCancellationToken( 0 ).CanBeCanceled );
		Assert.Equal( 0, transport.FlushCount );

		Task<TerminalColor> query = session.QueryDynamicColorAsync(
			kind,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};?\u001b\\" ),
			transport.GetWrite( 1 )
		);
		Assert.Equal( 1, transport.FlushCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:abcd/0123/ffff\u001b\\" )
		);
		Assert.Equal(
			new TerminalColor( 0xabcd, 0x0123, 0xffff ),
			await query
		);

		await session.ResetDynamicColorAsync( kind );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{resetOsc}\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task DifferentDynamicColorReplyDoesNotSatisfyOutstandingQuery() {
		DynamicColorTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> query = session.QueryDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]11;rgb:ffff/0000/0000\u001b\\" )
		);
		await Task.Delay( 50 );
		Assert.False( query.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]10;rgb:0000/ffff/0000\u001b\\" )
		);
		Assert.Equal(
			new TerminalColor( 0x0000, 0xffff, 0x0000 ),
			await query
		);
	}

	[Fact]
	public async Task CorrelatedMalformedDynamicColorFailsDeterministically() {
		DynamicColorTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> query = session.QueryDynamicColorAsync(
			TerminalDynamicColor.TextCursor,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]12;not-a-color\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task ExtendedIdentityIsRejectedBeforeOutputInT134() {
		DynamicColorTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.SetDynamicColorAsync(
				TerminalDynamicColor.MouseForeground,
				new TerminalColor( 1, 2, 3 )
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.QueryDynamicColorAsync(
				TerminalDynamicColor.HighlightForeground,
				TimeSpan.FromSeconds( 1 )
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task BelTerminatedCommonColorReplyIsAccepted() {
		DynamicColorTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalColor> query = session.QueryDynamicColorAsync(
			TerminalDynamicColor.DefaultBackground,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]11;#123456\u0007" )
		);

		Assert.Equal(
			new TerminalColor( 0x1200, 0x3400, 0x5600 ),
			await query
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		DynamicColorTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		DynamicColorTransport transport,
		int count
	) {
		for ( int attempt = 0; attempt < 500; ++attempt ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}
		throw new TimeoutException( "The expected dynamic-color write was not observed." );
	}

	private sealed class DynamicColorTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private readonly List<CancellationToken> writeTokens = [];
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

		internal int FlushCount => Volatile.Read( ref this.flushCount );

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.writes ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal CancellationToken GetWriteCancellationToken(
			int index
		) {
			lock ( this.writes ) {
				return this.writeTokens[ index ];
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
				this.writeTokens.Add( cancellationToken );
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
			0, 0, 0, 0x0002UL, new byte[ 32 ], 0, 32, 0,
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
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by dynamic-color query tests."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			return TerminalControlMutationResult.Success();
		}
	}
}
