namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T103 terminal-progress ownership, precedence, restoration, and cleanup.
/// </summary>
public sealed class TerminalProgressManagerTests {
	[Fact]
	public async Task AcquisitionAloneEmitsNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressManager manager = session.ProgressManager;

		long owner = await manager.AcquireAsync( CancellationToken.None );

		Assert.True( 0 < owner );
		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		await manager.ReleaseAsync( owner );
		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task UnreportedNewerOwnerDoesNotMaskReportedLowerOwner() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressManager manager = session.ProgressManager;
		long lower = await manager.AcquireAsync( CancellationToken.None );
		long upper = await manager.AcquireAsync( CancellationToken.None );

		await manager.ReportAsync(
			lower,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				3,
				10
			),
			CancellationToken.None
		);
		await manager.ReportAsync(
			lower,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				4,
				10
			),
			CancellationToken.None
		);

		Assert.Equal( 2, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 30 ),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 40 ),
			output.GetWrite( 1 )
		);

		await manager.ReleaseAsync( upper );
		Assert.Equal( 2, output.WriteCount );
		await manager.ReleaseAsync( lower );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task NonControllingUpdateIsLogicalUntilControllerReleases() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressManager manager = session.ProgressManager;
		long outer = await manager.AcquireAsync( CancellationToken.None );
		await manager.ReportAsync(
			outer,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				3,
				10
			),
			CancellationToken.None
		);
		long inner = await manager.AcquireAsync( CancellationToken.None );
		await manager.ReportAsync(
			inner,
			TerminalProgressValue.CreateIndeterminate(),
			CancellationToken.None
		);

		await manager.ReportAsync(
			outer,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				4,
				10
			),
			CancellationToken.None
		);
		Assert.Equal( 2, output.WriteCount );

		await manager.ReleaseAsync( inner );
		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 40 ),
			output.GetWrite( 2 )
		);

		await manager.ReleaseAsync( outer );
		Assert.Equal( 4, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 3 )
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task OutOfOrderNonControllingReleaseIsSilent() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressManager manager = session.ProgressManager;
		long first = await manager.AcquireAsync( CancellationToken.None );
		await manager.ReportAsync(
			first,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Error,
				1,
				2
			),
			CancellationToken.None
		);
		long second = await manager.AcquireAsync( CancellationToken.None );
		await manager.ReportAsync(
			second,
			TerminalProgressValue.CreateIndeterminate(),
			CancellationToken.None
		);

		await manager.ReleaseAsync( first );
		Assert.Equal( 2, output.WriteCount );

		await manager.ReleaseAsync( second );
		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task SessionDisposalClearsActiveProgress() {
		RecordingOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressManager manager = session.ProgressManager;
		long owner = await manager.AcquireAsync( CancellationToken.None );
		await manager.ReportAsync(
			owner,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Attention,
				3,
				4
			),
			CancellationToken.None
		);

		await session.DisposeAsync();

		Assert.True( 2 <= output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( output.WriteCount - 1 )
		);
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
				"Size is not required by terminal-progress manager tests."
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
