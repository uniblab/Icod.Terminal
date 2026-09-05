namespace Icod.Terminal.Tests.Session;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T85 truthful cursor-style restoration and strict-LIFO ownership.
/// </summary>
public sealed class TerminalSessionCursorStyleLeaseTests {
	[Fact]
	public async Task OutermostLeaseObservesSetsAndRestoresExactBaseline() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBar,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP$q q\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r3 q\u001b\\" )
		);
		TerminalCursorStyleLease lease = await acquire;

		Assert.Equal( TerminalCursorStyle.SteadyBar, lease.Style );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[6 q" ),
			transport.GetWrite( 1 )
		);

		await lease.DisposeAsync();

		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[3 q" ),
			transport.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task NestedLeaseUsesKnownOuterStyleAndRestoresInLifoOrder() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> outerAcquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBlock,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r4 q\u001b\\" )
		);
		TerminalCursorStyleLease outer = await outerAcquire;

		TerminalCursorStyleLease inner = await session.AcquireCursorStyleAsync(
			TerminalCursorStyle.BlinkingBar,
			TimeSpan.FromSeconds( 30 )
		);

		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[2 q" ),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5 q" ),
			transport.GetWrite( 2 )
		);

		await inner.DisposeAsync();
		await outer.DisposeAsync();

		Assert.Equal( 5, transport.WriteCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[2 q" ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[4 q" ),
			transport.GetWrite( 4 )
		);
	}

	[Fact]
	public async Task OutOfOrderReleaseFailsWithoutChangingPhysicalState() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> outerAcquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBlock,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r1 q\u001b\\" )
		);
		TerminalCursorStyleLease outer = await outerAcquire;
		TerminalCursorStyleLease inner = await session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyUnderline,
			TimeSpan.FromSeconds( 30 )
		);
		int beforeRelease = transport.WriteCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => outer.DisposeAsync().AsTask()
		);
		Assert.Equal( beforeRelease, transport.WriteCount );

		await inner.DisposeAsync();
		await outer.DisposeAsync();
	}

	[Fact]
	public async Task UnscopedMutationIsRejectedWhileLeaseIsActive() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBlock,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r1 q\u001b\\" )
		);
		TerminalCursorStyleLease lease = await acquire;
		int beforeSet = transport.WriteCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.SetCursorStyleAsync(
				TerminalCursorStyle.BlinkingUnderline
			).AsTask()
		);
		Assert.Equal( beforeSet, transport.WriteCount );

		await lease.DisposeAsync();
	}

	[Fact]
	public async Task UnsupportedObservationPreventsCursorStyleMutation() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBar,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP0$r\u001b\\" )
		);

		await Assert.ThrowsAsync<NotSupportedException>( () => acquire );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task SessionDisposalRestoresObservedBaselineAndReleasesLease() {
		DcsTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalCursorStyleLease? lease = null;
		try {
			Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
				TerminalCursorStyle.BlinkingBar,
				TimeSpan.FromSeconds( 30 )
			).AsTask();
			await transport.WaitForWriteCountAsync( 1 );
			transport.Publish(
				Encoding.ASCII.GetBytes( "\u001bP1$r2 q\u001b\\" )
			);
			lease = await acquire;

			await session.DisposeAsync();

			Assert.Equal(
				Encoding.ASCII.GetBytes( "\u001b[2 q" ),
				transport.GetWrite( -1 )
			);
			int afterDispose = transport.WriteCount;
			await lease.DisposeAsync();
			Assert.Equal( afterDispose, transport.WriteCount );
		} finally {
			if ( lease is not null ) {
				await lease.DisposeAsync();
			}
			await session.DisposeAsync();
		}
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
				int resolved = 0 > index
					? this.writes.Count + index
					: index;
				return this.writes[ resolved ].ToArray();
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
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

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
			byte[] bytes = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( bytes.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted input chunk exceeds the decoder read buffer."
				);
			}

			bytes.AsSpan().CopyTo( buffer.Span );
			return bytes.Length;
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
