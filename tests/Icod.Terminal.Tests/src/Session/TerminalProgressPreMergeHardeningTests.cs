namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies final 0.10 terminal-progress release hardening contracts.
/// </summary>
public sealed class TerminalProgressPreMergeHardeningTests {
	[Fact]
	public async Task RedirectedOutputRejectsProgressAcquisitionWithoutEmission() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			outputIsTerminal: false
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.AcquireProgressAsync().AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task SessionDisposalClearsProgressBeforeSynchronizedOutputEnds() {
		RecordingOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			output,
			outputIsTerminal: true
		);
		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync(
			1,
			2
		);

		await session.DisposeAsync();

		int clearIndex = output.IndexOf(
			OscWriter.EncodeOsc9ProgressFrame(
				Osc9ProgressState.Clear,
				0
			)
		);
		int synchronizedEndIndex = output.IndexOf(
			CsiWriter.EncodeSynchronizedOutputEndFrame()
		);
		Assert.True( 0 <= clearIndex );
		Assert.True( clearIndex < synchronizedEndIndex );

		await progress.DisposeAsync();
		await synchronized.DisposeAsync();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingOutput output,
		bool outputIsTerminal
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider( outputIsTerminal ),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyInput(),
			output,
			new TerminalSessionOptions {
				RequireInteractiveOutput = outputIsTerminal,
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

		internal int IndexOf(
			byte[] expected
		) {
			ArgumentNullException.ThrowIfNull( expected );
			for ( int index = 0; index < this.writes.Count; ++index ) {
				if ( this.writes[ index ].AsSpan().SequenceEqual( expected ) ) {
					return index;
				}
			}
			return -1;
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
		private readonly bool outputIsTerminal;
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x00000007u
		);

		internal RecordingTerminalControlProvider(
			bool outputIsTerminal
		) {
			this.outputIsTerminal = outputIsTerminal;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = this.outputIsTerminal
				|| 1 != endpoint.FileDescriptor;
			if ( !isTerminal ) {
				return TerminalControlResult<TerminalEndpointObservation>.Available(
					new TerminalEndpointObservation(
						false,
						null,
						null,
						TerminalControlCapabilities.None
					)
				);
			}

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
				"Size is not required by terminal-progress hardening tests."
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
