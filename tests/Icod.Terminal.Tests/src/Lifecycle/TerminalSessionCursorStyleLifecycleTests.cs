namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T86 cursor-style lease behavior across managed suspend and resume.
/// </summary>
public sealed class TerminalSessionCursorStyleLifecycleTests {
	[Fact]
	public async Task ActiveCursorStyleLeaseRestoresBaselineForSuspendAndReentersAfterResume() {
		DcsTransport transport = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBar,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1, timeout.Token );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r3 q\u001b\\" )
		);
		TerminalCursorStyleLease lease = await acquire;
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 6 ),
			transport.GetWrite( 1 )
		);

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( 4, transport.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 3 ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 6 ),
			transport.GetWrite( 3 )
		);
		Assert.True( session.IsStateValid );

		await lease.DisposeAsync();
		Assert.Equal( 5, transport.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 3 ),
			transport.GetWrite( 4 )
		);
	}

	[Fact]
	public async Task ReleasingLeaseWhileSuspendedEmitsNoAdditionalCursorStyleFrame() {
		DcsTransport transport = new();
		TestTerminalLifecycleSource lifecycle = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyUnderline,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1, timeout.Token );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r2 q\u001b\\" )
		);
		TerminalCursorStyleLease lease = await acquire;

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 2 ),
			transport.GetWrite( 2 )
		);

		await lease.DisposeAsync();
		Assert.Equal( 3, transport.WriteCount );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( 3, transport.WriteCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		DcsTransport transport,
		TestTerminalLifecycleSource lifecycle
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( lifecycle );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle,
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
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

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
			byte[] bytes = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
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

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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
				"Size is not used by cursor-style lifecycle tests."
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
