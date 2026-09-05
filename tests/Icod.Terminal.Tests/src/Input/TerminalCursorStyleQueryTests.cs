namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T84 typed cursor-style query on the existing DECRQSS transaction substrate.
/// </summary>
public sealed class TerminalCursorStyleQueryTests {
	[Theory]
	[InlineData( "\u001bP1$r q\u001b\\", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "\u001bP1$r0 q\u001b\\", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "\u001bP1$r1 q\u001b\\", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "\u001bP1$r2 q\u001b\\", TerminalCursorStyle.SteadyBlock )]
	[InlineData( "\u001bP1$r3 q\u001b\\", TerminalCursorStyle.BlinkingUnderline )]
	[InlineData( "\u001bP1$r4 q\u001b\\", TerminalCursorStyle.SteadyUnderline )]
	[InlineData( "\u001bP1$r5 q\u001b\\", TerminalCursorStyle.BlinkingBar )]
	[InlineData( "\u001bP1$r6 q\u001b\\", TerminalCursorStyle.SteadyBar )]
	[InlineData( "\u001bP1$r0006 q\u001b\\", TerminalCursorStyle.SteadyBar )]
	public async Task PositiveResponseReturnsTypedObservation(
		string responseText,
		TerminalCursorStyle expectedStyle
	) {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleObservation> query = session.QueryCursorStyleAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP$q q\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish( Encoding.ASCII.GetBytes( responseText ) );
		TerminalCursorStyleObservation observation = await query;

		Assert.True( observation.IsSupported );
		Assert.Equal( expectedStyle, observation.Style );
	}

	[Fact]
	public async Task NegativeResponseReturnsUnsupportedObservation() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleObservation> query = session.QueryCursorStyleAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP0$r\u001b\\" )
		);
		TerminalCursorStyleObservation observation = await query;

		Assert.False( observation.IsSupported );
		Assert.Null( observation.Style );
	}

	[Theory]
	[InlineData( "\u001bP1$r7 q\u001b\\" )]
	[InlineData( "\u001bP1$r2;3 q\u001b\\" )]
	[InlineData( "\u001bP1$r?2 q\u001b\\" )]
	[InlineData( "\u001bP1$r2 x\u001b\\" )]
	public async Task MalformedOrUnknownPositiveStateFailsDeterministically(
		string responseText
	) {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleObservation> query = session.QueryCursorStyleAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( responseText ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task PreCancelledQueryEmitsNothing() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.QueryCursorStyleAsync(
				TimeSpan.FromSeconds( 30 ),
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task ZeroTimeoutPropagatesTimeout() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<TimeoutException>(
			() => session.QueryCursorStyleAsync(
				TimeSpan.Zero
			).AsTask()
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		DcsTransport transport
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
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		DcsTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);
		await transport.WaitForWriteCountAsync(
			expected,
			timeout.Token
		);
	}

	private sealed class DcsTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private byte[]? pending;
		private int pendingOffset;

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected,
			CancellationToken cancellationToken
		) {
			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}

				await this.writeSignal.WaitAsync(
					cancellationToken
				).ConfigureAwait( false );
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
			this.pending.AsSpan(
				this.pendingOffset,
				count
			).CopyTo( buffer.Span );
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
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
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
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
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
}
