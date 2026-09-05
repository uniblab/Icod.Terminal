namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T92 synchronized-output logical ownership and physical transitions.
/// </summary>
public sealed class TerminalSynchronizedOutputManagerTests {
	[Fact]
	public async Task MultipleOwnersUseOneBeginAndOneFinalEnd() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		var manager = new TerminalSynchronizedOutputManager( session );

		long ownerA = await manager.AcquireAsync( CancellationToken.None );
		long ownerB = await manager.AcquireAsync( CancellationToken.None );
		long ownerC = await manager.AcquireAsync( CancellationToken.None );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			output.GetWrite( 0 )
		);
		Assert.Equal( 0, output.FlushCount );

		await manager.ReleaseAsync( ownerB );
		await manager.ReleaseAsync( ownerC );
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		await manager.ReleaseAsync( ownerA );
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( 1 )
		);
		Assert.Equal( 1, output.FlushCount );

		await manager.CloseAsync();
	}

	[Fact]
	public async Task OutOfOrderReleaseIsIdentityAware() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		var manager = new TerminalSynchronizedOutputManager( session );

		long ownerA = await manager.AcquireAsync( CancellationToken.None );
		long ownerB = await manager.AcquireAsync( CancellationToken.None );
		long ownerC = await manager.AcquireAsync( CancellationToken.None );

		await manager.ReleaseAsync( ownerA );
		await manager.ReleaseAsync( ownerC );
		Assert.Equal( 1, output.WriteCount );

		await manager.ReleaseAsync( ownerB );
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 1, output.FlushCount );

		await manager.CloseAsync();
	}

	[Fact]
	public async Task FinalReleaseFailureRetainsOwnerForRetry() {
		RecordingOutput output = new( 2 );
		await using TerminalSession session = await OpenSessionAsync( output );
		var manager = new TerminalSynchronizedOutputManager( session );

		long owner = await manager.AcquireAsync( CancellationToken.None );

		await Assert.ThrowsAsync<IOException>(
			() => manager.ReleaseAsync( owner ).AsTask()
		);
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		output.ClearFailures();
		await manager.ReleaseAsync( owner );
		Assert.Equal( 3, output.WriteCount );
		Assert.Equal( 1, output.FlushCount );

		await manager.CloseAsync();
	}

	[Fact]
	public async Task PreCancelledFirstAcquisitionEmitsNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		var manager = new TerminalSynchronizedOutputManager( session );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => manager.AcquireAsync( cancellation.Token ).AsTask()
		);
		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		await manager.CloseAsync();
	}

	[Fact]
	public async Task FailedFirstBeginAttemptsImmediateCleanup() {
		RecordingOutput output = new( 1 );
		await using TerminalSession session = await OpenSessionAsync( output );
		var manager = new TerminalSynchronizedOutputManager( session );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => manager.AcquireAsync( CancellationToken.None ).AsTask()
		);
		Assert.Equal( "Synthetic output failure 1.", exception.Message );
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( 1 )
		);
		Assert.Equal( 1, output.FlushCount );

		await manager.CloseAsync();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class EmptyInput : ITerminalInput {
		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			_ = buffer;
			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}
	}

	private sealed class RecordingOutput : ITerminalOutput {
		private readonly List<byte[]> writes = [];
		private readonly HashSet<int> failingWrites;

		internal RecordingOutput(
			params int[] failingWrites
		) {
			ArgumentNullException.ThrowIfNull( failingWrites );
			this.failingWrites = [ .. failingWrites ];
		}

		internal int WriteCount {
			get {
				return this.writes.Count;
			}
		}

		internal int FlushCount {
			get;
			private set;
		}

		internal byte[] GetWrite(
			int index
		) {
			return this.writes[ index ].ToArray();
		}

		internal void ClearFailures() {
			this.failingWrites.Clear();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.writes.Add( buffer.ToArray() );
			int call = this.writes.Count;
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
			this.FlushCount++;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x00000007u
		);

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.WindowsConsole,
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
				"Size is not required by synchronized-output manager tests."
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
