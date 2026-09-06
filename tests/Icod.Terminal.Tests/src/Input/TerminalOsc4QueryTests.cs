namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies OSC 4 palette observation through the shared active-query router.
/// </summary>
public sealed class TerminalOsc4QueryTests {
	[Fact]
	public async Task QueryUsesCanonicalRequestAndReturnsTypedObservation() {
		Osc4Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> query = session.QueryPaletteColorAsync(
			17,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;17;?\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( 1, transport.FlushCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;17;rgb:1234/5678/9abc\u001b\\" )
		);

		Assert.Equal(
			new TerminalColor( 0x1234, 0x5678, 0x9abc ),
			await query
		);
	}

	[Fact]
	public async Task DifferentPaletteIndexDoesNotSatisfyOutstandingQuery() {
		Osc4Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> query = session.QueryPaletteColorAsync(
			7,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;8;rgb:ffff/0000/0000\u001b\\" )
		);
		await Task.Delay( 50 );
		Assert.False( query.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;7;rgb:0000/ffff/0000\u001b\\" )
		);
		Assert.Equal(
			new TerminalColor( 0x0000, 0xffff, 0x0000 ),
			await query
		);
	}

	[Fact]
	public async Task CorrelatedMalformedColorFailsDeterministically() {
		Osc4Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> query = session.QueryPaletteColorAsync(
			7,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;7;not-a-color\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task PreCancelledQueryWritesNothing() {
		Osc4Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.QueryPaletteColorAsync(
				1,
				TimeSpan.FromSeconds( 1 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.FlushCount );
	}

	[Fact]
	public async Task SingleMutationUsesSessionOutputAndDoesNotFlush() {
		Osc4Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await session.SetPaletteColorAsync(
			3,
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;3;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( 0, transport.FlushCount );
		Assert.False( transport.GetWriteCancellationToken( 0 ).CanBeCanceled );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		Osc4Transport transport
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
		Osc4Transport transport,
		int count
	) {
		ArgumentNullException.ThrowIfNull( transport );
		for ( int attempt = 0; attempt < 500; ++attempt ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}
		throw new TimeoutException( "The expected OSC 4 write was not observed." );
	}

	private sealed class Osc4Transport : ITerminalInput, ITerminalOutput {
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
				"Size is not used by OSC 4 query tests."
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
