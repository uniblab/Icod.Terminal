namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T137 color-control composition with existing session output and active-query traffic.
/// </summary>
public sealed class TerminalColorCompositionTests {
	[Fact]
	public async Task ColorMutationComposesWithExistingSemanticOutputInOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await session.BeginPromptAsync();
		await session.SetPaletteColorAsync(
			5,
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		await session.SetDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground,
			new TerminalColor( 0x4444, 0x5555, 0x6666 )
		);
		await session.WriteTextAsync( "X" );
		await session.ResetDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground
		);
		await session.ResetPaletteColorAsync( 5 );
		await session.FinishCommandAsync( 0 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]10;rgb:4444/5555/6666\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			Encoding.UTF8.GetBytes( "X" ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]110\u001b\\" ),
			transport.GetWrite( 4 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104;5\u001b\\" ),
			transport.GetWrite( 5 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
			transport.GetWrite( 6 )
		);
		Assert.Equal( 0, transport.FlushCount );
	}

	[Fact]
	public async Task ActiveColorObservationAllowsSerializedControlOutputWhileAwaitingReply() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> query = session.QueryPaletteColorAsync(
			7,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;7;?\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.False( query.IsCompleted );

		await session.SetDynamicColorAsync(
			TerminalDynamicColor.TextCursor,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc )
		);
		await session.BeginCommandOutputAsync();

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;7;rgb:1234/5678/9abc\u001b\\" )
		);
		Assert.Equal(
			new TerminalColor( 0x1234, 0x5678, 0x9abc ),
			await query
		);

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]12;rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task PaletteAndDynamicObservationsRemainExactlyCorrelatedAcrossSequentialQueries() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalColor> paletteQuery = session.QueryPaletteColorAsync(
			42,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]10;rgb:ffff/0000/0000\u001b\\" )
		);
		await Task.Delay( 50 );
		Assert.False( paletteQuery.IsCompleted );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;42;rgb:0101/0202/0303\u001b\\" )
		);
		Assert.Equal(
			new TerminalColor( 0x0101, 0x0202, 0x0303 ),
			await paletteQuery
		);

		Task<TerminalColor> dynamicQuery = session.QueryDynamicColorAsync(
			TerminalDynamicColor.HighlightBackground,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;42;rgb:ffff/ffff/ffff\u001b\\" )
		);
		await Task.Delay( 50 );
		Assert.False( dynamicQuery.IsCompleted );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]17;rgb:0/8/f\u001b\\" )
		);
		Assert.Equal(
			new TerminalColor( 0x0000, 0x8888, 0xffff ),
			await dynamicQuery
		);
		Assert.Equal( 2, transport.FlushCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTransport transport
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
				ObserveLifecycleEvents = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly List<byte[]> writes = [];
		private byte[]? pending;
		private int pendingOffset;
		private int flushCount;

		internal int FlushCount => Volatile.Read( ref this.flushCount );

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
				throw new InvalidOperationException( "The scripted input channel is closed." );
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected
		) {
			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync(
					timeout.Token
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
				"Size is not used by T137 color composition tests."
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
