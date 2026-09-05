namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T123 OSC 133 marker integration with TerminalSession output ordering.
/// </summary>
public sealed class TerminalSessionSemanticPromptTests {
	[Fact]
	public async Task MarkerWaitsForApplicationWriteToFinish() {
		BlockingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		Task applicationWrite = session.WriteTextAsync( "A" ).AsTask();
		await output.FirstWriteStarted;

		Task markerWrite = session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreatePromptStart()
		).AsTask();
		await Task.Delay( 50 );

		Assert.False( markerWrite.IsCompleted );
		Assert.Equal( 1, output.MaximumConcurrentWrites );

		output.ReleaseFirstWrite();
		await applicationWrite;
		await markerWrite;

		Assert.Equal( 1, output.MaximumConcurrentWrites );
		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "A" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task ApplicationWriteWaitsForMarkerToFinish() {
		BlockingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		Task markerWrite = session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandOutputStart()
		).AsTask();
		await output.FirstWriteStarted;

		Task applicationWrite = session.WriteTextAsync( "A" ).AsTask();
		await Task.Delay( 50 );

		Assert.False( applicationWrite.IsCompleted );
		Assert.Equal( 1, output.MaximumConcurrentWrites );

		output.ReleaseFirstWrite();
		await markerWrite;
		await applicationWrite;

		Assert.Equal( 1, output.MaximumConcurrentWrites );
		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "A" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task MarkerWaitsBehindControlOutputLease() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task markerWrite = session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandInputStart()
		).AsTask();
		await Task.Delay( 50 );

		Assert.False( markerWrite.IsCompleted );
		Assert.Empty( output.Writes );

		controlOutput.Dispose();
		await markerWrite;

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task MarkersRemainIndependentlyCallableInNoncanonicalOrder() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandOutputStart()
		);
		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreatePromptStart()
		);
		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandFinished( 0 )
		);
		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandInputStart()
		);
		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandAborted()
		);

		string[] expected = [
			"\u001b]133;C\u001b\\",
			"\u001b]133;A\u001b\\",
			"\u001b]133;D;0\u001b\\",
			"\u001b]133;B\u001b\\",
			"\u001b]133;D\u001b\\"
		];
		Assert.Equal( expected.Length, output.Writes.Count );
		for ( int index = 0; index < expected.Length; ++index ) {
			Assert.Equal(
				Encoding.ASCII.GetBytes( expected[ index ] ),
				output.Writes[ index ]
			);
		}
	}

	[Fact]
	public async Task MarkerCommitIsNoncancellableAndDoesNotFlush() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandFinished( 255 )
		);

		Assert.Single( output.Writes );
		Assert.Single( output.WriteCancellationTokens );
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );

		await session.DisposeAsync();
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task CancellationWhileWaitingForOutputEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();

		Task markerWrite = session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreatePromptStart(),
			cancellation.Token
		).AsTask();
		cancellation.Cancel();

		try {
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => markerWrite
			);
		} finally {
			controlOutput.Dispose();
		}

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task FailedMarkerDoesNotFabricateStateOrBlockLaterMarker() {
		FailOnceTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<IOException>(
			() => session.WriteSemanticPromptMarkerAsync(
				TerminalSemanticPromptMarker.CreateCommandOutputStart()
			).AsTask()
		);

		Assert.True( session.IsStateValid );
		await session.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandFinished( 5 )
		);

		Assert.Equal( 2, output.Attempts.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Attempts[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;5\u001b\\" ),
			output.Attempts[ 1 ]
		);
	}

	[Fact]
	public async Task NoninteractiveOutputIsRejected() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			output,
			outputIsTerminal: false
		);

		try {
			await Assert.ThrowsAsync<InvalidOperationException>(
				() => session.WriteSemanticPromptMarkerAsync(
					TerminalSemanticPromptMarker.CreatePromptStart()
				).AsTask()
			);
			Assert.Empty( output.Writes );
		} finally {
			await session.DisposeAsync();
		}
	}

	[Fact]
	public async Task DisposedSessionRejectsMarker() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		await session.DisposeAsync();

		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.WriteSemanticPromptMarkerAsync(
				TerminalSemanticPromptMarker.CreateCommandAborted()
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output,
		bool outputIsTerminal = true
	) {
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider( outputIsTerminal ),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				RequireInteractiveOutput = outputIsTerminal
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
			if ( 1 == Interlocked.Increment( ref this.writeCount ) ) {
				throw new IOException( "Synthetic OSC 133 write failure." );
			}

			return base.WriteAsync(
				buffer,
				cancellationToken
			);
		}
	}

	private sealed class BlockingTerminalOutput : ITerminalOutput {
		private readonly TaskCompletionSource firstWriteStarted = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource releaseFirstWrite = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private int activeWrites;
		private int maximumConcurrentWrites;
		private int writeCount;

		internal Task FirstWriteStarted {
			get {
				return this.firstWriteStarted.Task;
			}
		}

		internal int MaximumConcurrentWrites {
			get {
				return Volatile.Read( ref this.maximumConcurrentWrites );
			}
		}

		internal List<byte[]> Writes {
			get;
		} = [];

		internal void ReleaseFirstWrite() {
			this.releaseFirstWrite.TrySetResult();
		}

		public async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			int active = Interlocked.Increment( ref this.activeWrites );
			UpdateMaximum( ref this.maximumConcurrentWrites, active );

			try {
				int call = Interlocked.Increment( ref this.writeCount );
				if ( 1 == call ) {
					this.firstWriteStarted.TrySetResult();
					await this.releaseFirstWrite.Task.ConfigureAwait( false );
				}

				lock ( this.Writes ) {
					this.Writes.Add( buffer.ToArray() );
				}
			} finally {
				Interlocked.Decrement( ref this.activeWrites );
			}
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		private static void UpdateMaximum(
			ref int location,
			int candidate
		) {
			int current = Volatile.Read( ref location );
			while ( current < candidate ) {
				int observed = Interlocked.CompareExchange(
					ref location,
					candidate,
					current
				);
				if ( observed == current ) {
					return;
				}

				current = observed;
			}
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
		private readonly bool outputIsTerminal;

		internal RecordingTerminalControlProvider(
			bool outputIsTerminal
		) {
			this.outputIsTerminal = outputIsTerminal;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = !ReferenceEquals(
				endpoint,
				TerminalEndpoint.StandardOutput
			) || this.outputIsTerminal;
			TerminalPlatformKind? platform = isTerminal
				? TerminalPlatformKind.PosixTermios
				: null;
			TerminalControlCapabilities capabilities = isTerminal
				? TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
				: TerminalControlCapabilities.None;

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					platform,
					capabilities
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
