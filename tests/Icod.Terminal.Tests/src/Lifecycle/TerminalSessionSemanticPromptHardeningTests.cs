namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T125 lifecycle, failure, cancellation, and disposal hardening for
/// the public OSC 133 semantic-prompt surface.
/// </summary>
public sealed class TerminalSessionSemanticPromptHardeningTests {
	[Fact]
	public async Task CancellationWhileQueuedForSessionOutputEmitsNothing() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);
		IDisposable outputLease = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();

		Task markerWrite = session.BeginPromptAsync(
			cancellation.Token
		).AsTask();
		cancellation.Cancel();

		try {
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => markerWrite
			);
		} finally {
			outputLease.Dispose();
		}

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task CancellationAfterCommitDoesNotCancelCommittedFrame() {
		RecordingTerminalControlProvider provider = new();
		BlockingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);
		using CancellationTokenSource cancellation = new();

		Task markerWrite = session.BeginCommandOutputAsync(
			cancellation.Token
		).AsTask();
		await output.FirstWriteStarted;

		try {
			cancellation.Cancel();
			Assert.False( markerWrite.IsCompleted );
			Assert.Single( output.WriteCancellationTokens );
			Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		} finally {
			output.ReleaseWrite();
		}

		await markerWrite;
		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 0 ]
		);

		await session.DisposeAsync();
	}

	[Fact]
	public async Task FailedCommittedMarkerDoesNotCompensateAndLaterCallRemainsAvailable() {
		RecordingTerminalControlProvider provider = new();
		FailOnceTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);

		await Assert.ThrowsAsync<IOException>(
			() => session.BeginCommandOutputAsync().AsTask()
		);

		Assert.Single( output.Attempts );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Attempts[ 0 ]
		);
		Assert.True( session.IsStateValid );

		await session.FinishCommandAsync( 17 );

		Assert.Equal( 2, output.Attempts.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;17\u001b\\" ),
			output.Attempts[ 1 ]
		);
		Assert.All(
			output.WriteCancellationTokens,
			token => Assert.False( token.CanBeCanceled )
		);
	}

	[Fact]
	public async Task DisposalDoesNotFinishOrAbortAndRepeatedDisposalEmitsNothing() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);

		await session.BeginCommandOutputAsync();
		Assert.Single( output.Writes );

		await session.DisposeAsync();
		await session.DisposeAsync();

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task SuspendAndResumeDoNotEmitOrReplayMarkers() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			provider,
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		await session.BeginCommandOutputAsync();
		Assert.Single( output.Writes );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.True( session.IsStateValid );
		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 0 ]
		);

		await session.FinishCommandAsync( 3 );
		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;3\u001b\\" ),
			output.Writes[ 1 ]
		);

		await session.DisposeAsync();
		Assert.Equal( 2, output.Writes.Count );
	}

	[Fact]
	public async Task LifecycleReentryFailureDoesNotFabricateSemanticHistory() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new();
		TerminalSession session = await OpenSessionAsync(
			provider,
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
		TaskCompletionSource terminationObserved = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		using CancellationTokenRegistration registration = session.TerminationToken.Register(
			terminationObserved.SetResult
		);

		await session.BeginPromptAsync();
		provider.FailSetModeCall = provider.SetModeCallCount + 1;
		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		await terminationObserved.Task.WaitAsync( timeout.Token );

		Assert.False( session.IsStateValid );
		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			output.Writes[ 0 ]
		);

		await session.DisposeAsync();
		Assert.Single( output.Writes );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalControlProvider provider,
		ITerminalOutput output,
		TestTerminalLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			provider,
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

	private class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public virtual ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FailOnceTerminalOutput : RecordingTerminalOutput {
		private int writeCount;

		internal List<byte[]> Attempts {
			get;
		} = [];

		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Attempts.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			if ( 1 == Interlocked.Increment( ref this.writeCount ) ) {
				throw new IOException( "Synthetic committed OSC 133 write failure." );
			}

			this.Writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}
	}

	private sealed class BlockingTerminalOutput : ITerminalOutput {
		private readonly TaskCompletionSource firstWriteStarted = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource releaseWrite = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		internal Task FirstWriteStarted {
			get {
				return this.firstWriteStarted.Task;
			}
		}

		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal void ReleaseWrite() {
			this.releaseWrite.TrySetResult();
		}

		public async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			this.firstWriteStarted.TrySetResult();
			await this.releaseWrite.Task.ConfigureAwait( false );
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

		internal int FailSetModeCall {
			get;
			set;
		}

		internal int SetModeCallCount {
			get;
			private set;
		}

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

			int call = ++this.SetModeCallCount;
			if ( call == this.FailSetModeCall ) {
				return TerminalControlMutationResult.Unavailable(
					"Synthetic terminal mode re-entry failure."
				);
			}

			return TerminalControlMutationResult.Success();
		}
	}
}
