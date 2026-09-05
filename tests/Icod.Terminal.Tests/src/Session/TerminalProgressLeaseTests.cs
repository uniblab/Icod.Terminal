namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T104 public terminal-progress lease and session API.
/// </summary>
public sealed class TerminalProgressLeaseTests {
	[Fact]
	public async Task AcquisitionEmitsNothingUntilFirstReport() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await using TerminalProgressLease progress = await session.AcquireProgressAsync();

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task ReportsCallerStagesAsRoundedNormalProgress() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalProgressLease progress = await session.AcquireProgressAsync();

		await progress.ReportAsync( 1, 10 );
		await progress.ReportAsync( 2, 10 );

		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 10 ),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 20 ),
			output.GetWrite( 1 )
		);
	}

	[Fact]
	public async Task ReportsSemanticStatesAndIndeterminateProgress() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalProgressLease progress = await session.AcquireProgressAsync();

		await progress.ReportAsync(
			TerminalProgressState.Error,
			1,
			4
		);
		await progress.ReportAsync(
			TerminalProgressState.Attention,
			2,
			4
		);
		await progress.SetIndeterminateAsync();

		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Error, 25 ),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Attention, 50 ),
			output.GetWrite( 1 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Indeterminate, 0 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task NestedProgressRestoresLatestOuterLogicalValue() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease outer = await session.AcquireProgressAsync();
		TerminalProgressLease inner = await session.AcquireProgressAsync();

		await outer.ReportAsync( 3, 10 );
		await inner.SetIndeterminateAsync();
		await outer.ReportAsync( 4, 10 );

		Assert.Equal( 2, output.WriteCount );
		await inner.DisposeAsync();
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 40 ),
			output.GetWrite( 2 )
		);

		await outer.DisposeAsync();
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 3 )
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task OutOfOrderNonControllingDisposeIsSilent() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease first = await session.AcquireProgressAsync();
		TerminalProgressLease second = await session.AcquireProgressAsync();

		await first.ReportAsync( 1, 2 );
		await second.SetIndeterminateAsync();
		await first.DisposeAsync();
		Assert.Equal( 2, output.WriteCount );

		await second.DisposeAsync();
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task InvalidReportEmitsNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalProgressLease progress = await session.AcquireProgressAsync();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => progress.ReportAsync( 11, 10 ).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task SuccessfulDisposalIsIdempotentAndLaterReportIsRejected() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync( 1, 2 );
		await progress.DisposeAsync();
		int writes = output.WriteCount;

		await progress.DisposeAsync();
		Assert.Equal( writes, output.WriteCount );
		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => progress.ReportAsync( 2, 2 ).AsTask()
		);
	}

	[Fact]
	public async Task PreCancelledAcquisitionAndReportEmitNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.AcquireProgressAsync( cancellation.Token ).AsTask()
		);
		Assert.Equal( 0, output.WriteCount );

		await using TerminalProgressLease progress = await session.AcquireProgressAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => progress.ReportAsync(
				1,
				10,
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task LeaseDisposalAfterSessionDisposalEmitsNothing() {
		RecordingOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync( 1, 2 );

		await session.DisposeAsync();
		int writes = output.WriteCount;
		await progress.DisposeAsync();

		Assert.Equal( writes, output.WriteCount );
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
		private int flushCount;

		internal int WriteCount {
			get {
				return this.writes.Count;
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
			return this.writes[ index ].ToArray();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.writes.Add( buffer.ToArray() );
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
				"Size is not required by terminal-progress lease tests."
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
