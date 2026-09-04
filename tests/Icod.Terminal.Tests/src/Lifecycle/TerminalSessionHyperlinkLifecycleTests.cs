namespace Icod.Terminal.Tests.Lifecycle;

using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies that session-owned OSC 8 hyperlink state is physically neutralized
/// across suspension while logical lease ownership survives for deterministic re-entry.
/// </summary>
public sealed class TerminalSessionHyperlinkLifecycleTests {
	[Fact]
	public async Task ActiveHyperlinkClosesForSuspendAndReentersAfterResume() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		TerminalHyperlinkLease lease = await session.AcquireHyperlinkAsync(
			"https://example.com/active",
			"active"
		);
		byte[] begin = output.Writes[ 0 ];

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 1 ]
		);
		Assert.Equal( begin, output.Writes[ 2 ] );
		Assert.True( session.IsStateValid );

		await lease.DisposeAsync();
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ ^1 ]
		);
	}

	[Fact]
	public async Task NestedHyperlinkResumeRestoresInnermostState() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
			"https://example.com/outer",
			"outer"
		);
		TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
			"https://example.com/inner",
			"inner"
		);
		byte[] outerBegin = output.Writes[ 0 ];
		byte[] innerBegin = output.Writes[ 1 ];

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal( 4, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 2 ]
		);
		Assert.Equal( innerBegin, output.Writes[ 3 ] );

		await inner.DisposeAsync();
		Assert.Equal( outerBegin, output.Writes[ 4 ] );
		await outer.DisposeAsync();
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 5 ]
		);
	}

	[Fact]
	public async Task SuspendWithoutHyperlinkWritesNoOscEightState() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task RepeatedSuspendResumeCyclesEmitOneCloseAndBeginPerCycle() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		await using TerminalHyperlinkLease lease = await session.AcquireHyperlinkAsync(
			"https://example.com/cycle",
			"cycle"
		);
		byte[] begin = output.Writes[ 0 ];

		for ( int cycle = 0; cycle < 2; ++cycle ) {
			lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
			_ = await session.ReadLifecycleEventAsync( timeout.Token );
			_ = await session.ReadLifecycleEventAsync( timeout.Token );
		}

		Assert.Equal( 5, output.Writes.Count );
		Assert.Equal( OscWriter.EncodeHyperlinkEndFrame(), output.Writes[ 1 ] );
		Assert.Equal( begin, output.Writes[ 2 ] );
		Assert.Equal( OscWriter.EncodeHyperlinkEndFrame(), output.Writes[ 3 ] );
		Assert.Equal( begin, output.Writes[ 4 ] );
	}

	[Fact]
	public async Task FailedHyperlinkReentryLeavesSessionStateInvalid() {
		FailingTerminalOutput output = new( 3 );
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		_ = await session.AcquireHyperlinkAsync(
			"https://example.com/fail-resume"
		);
		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		await WaitUntilAsync(
			() => session.TerminationToken.IsCancellationRequested,
			timeout.Token
		);
		Assert.False( session.IsStateValid );

		await Assert.ThrowsAnyAsync<Exception>(
			() => session.DisposeAsync().AsTask()
		);
	}

	private static async Task WaitUntilAsync(
		Func<bool> predicate,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( predicate );
		while ( !predicate() ) {
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Delay(
				10,
				cancellationToken
			);
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output,
		TestTerminalLifecycleSource lifecycle
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( lifecycle );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FailingTerminalOutput : ITerminalOutput {
		private readonly HashSet<int> failingWrites;
		private int writeCount;

		internal FailingTerminalOutput(
			params int[] failingWrites
		) {
			ArgumentNullException.ThrowIfNull( failingWrites );
			this.failingWrites = [ .. failingWrites ];
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			int call = Interlocked.Increment( ref this.writeCount );
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
				"Size is not required by hyperlink lifecycle tests."
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
