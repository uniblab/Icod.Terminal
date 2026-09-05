namespace Icod.Terminal.Tests.Session;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies cursor-style lease acquisition failure recovery and no-mutation boundaries.
/// </summary>
public sealed class TerminalSessionCursorStyleFailureTests {
	[Fact]
	public async Task FailedAcquisitionWriteRestoresObservedBaselineBeforeRethrow() {
		ScriptedTransport transport = new( 2 );
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBar,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.PublishCursorStyle( 3 );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => acquire
		);

		Assert.Equal( "Synthetic output failure 2.", exception.Message );
		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 6 ),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 3 ),
			transport.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task FailedAcquisitionAndFailedRecoveryRetainBaselineForSessionDisposal() {
		ScriptedTransport transport = new( 2, 3 );
		TerminalSession session = await OpenSessionAsync( transport );
		try {
			Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
				TerminalCursorStyle.BlinkingBar,
				TimeSpan.FromSeconds( 30 )
			).AsTask();
			await transport.WaitForWriteCountAsync( 1 );
			transport.PublishCursorStyle( 2 );

			AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
				() => acquire
			);
			Assert.Equal( 2, exception.InnerExceptions.Count );
			Assert.Equal( 3, transport.WriteCount );
			Assert.Equal(
				CsiWriter.EncodeCursorStyleFrame( 2 ),
				transport.GetWrite( 2 )
			);

			await session.DisposeAsync();

			Assert.Equal( 4, transport.WriteCount );
			Assert.Equal(
				CsiWriter.EncodeCursorStyleFrame( 2 ),
				transport.GetWrite( 3 )
			);
		} finally {
			await session.DisposeAsync();
		}
	}

	[Fact]
	public async Task MalformedBaselineObservationDoesNotEmitStyleMutation() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyUnderline,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.PublishCursorStyle( 7 );

		await Assert.ThrowsAsync<FormatException>( () => acquire );
		Assert.Equal( 1, transport.WriteCount );
		Assert.Equal( 0, transport.CursorStyleMutationCount );
	}

	[Fact]
	public async Task CancellationDuringBaselineObservationDoesNotEmitStyleMutation() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();

		Task<TerminalCursorStyleLease> acquire = session.AcquireCursorStyleAsync(
			TerminalCursorStyle.SteadyBlock,
			TimeSpan.FromSeconds( 30 ),
			cancellation.Token
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => acquire );
		Assert.Equal( 0, transport.CursorStyleMutationCount );
	}

	[Fact]
	public async Task ZeroTimeoutDoesNotEmitStyleMutation() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<TimeoutException>(
			() => session.AcquireCursorStyleAsync(
				TerminalCursorStyle.BlinkingUnderline,
				TimeSpan.Zero
			).AsTask()
		);

		Assert.Equal( 0, transport.CursorStyleMutationCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
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

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private readonly HashSet<int> failingWrites;
		private readonly SemaphoreSlim writeSignal = new( 0 );

		internal ScriptedTransport(
			params int[] failingWrites
		) {
			ArgumentNullException.ThrowIfNull( failingWrites );
			this.failingWrites = [ .. failingWrites ];
		}

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
		}

		internal int CursorStyleMutationCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count(
						static value => 5 == value.Length
							&& 0x1B == value[ 0 ]
							&& (byte)'[' == value[ 1 ]
							&& (byte)' ' == value[ 3 ]
							&& (byte)'q' == value[ 4 ]
					);
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

		internal void PublishCursorStyle(
			int parameter
		) {
			this.input.Writer.TryWrite(
				Encoding.ASCII.GetBytes(
					$"\u001bP1$r{parameter} q\u001b\\"
				)
			);
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
			int call;
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
				call = this.writes.Count;
			}
			this.writeSignal.Release();
			if ( this.failingWrites.Contains( call ) ) {
				return ValueTask.FromException(
					new IOException( $"Synthetic output failure {call}." )
				);
			}
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
				"Size is not used by cursor-style failure tests."
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
