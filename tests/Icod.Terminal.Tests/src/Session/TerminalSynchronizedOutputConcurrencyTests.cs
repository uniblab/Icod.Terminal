namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T96 synchronized-output ownership under concurrent acquisition and release.
/// </summary>
public sealed class TerminalSynchronizedOutputConcurrencyTests {
	[Fact]
	public async Task ConcurrentOwnersUseOnePhysicalBeginAndOnePhysicalEnd() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		const int ownerCount = 64;

		Task<TerminalSynchronizedOutputLease>[] acquisitions = Enumerable
			.Range( 0, ownerCount )
			.Select(
				_ => session.AcquireSynchronizedOutputAsync().AsTask()
			)
			.ToArray();
		TerminalSynchronizedOutputLease[] leases = await Task.WhenAll( acquisitions );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			output.GetWrite( 0 )
		);
		Assert.Equal( 0, output.FlushCount );

		Random random = new( 2026 );
		TerminalSynchronizedOutputLease[] shuffled = leases
			.OrderBy( _ => random.Next() )
			.ToArray();
		Task[] releases = shuffled
			.Select(
				lease => lease.DisposeAsync().AsTask()
			)
			.ToArray();
		await Task.WhenAll( releases );

		Assert.Equal( 2, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( 1 )
		);
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task RepeatedConcurrentRoundsDoNotAccumulatePhysicalOwnership() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		const int rounds = 12;
		const int ownerCount = 24;

		for ( int round = 0; round < rounds; round++ ) {
			TerminalSynchronizedOutputLease[] leases = await Task.WhenAll(
				Enumerable
					.Range( 0, ownerCount )
					.Select(
						_ => session.AcquireSynchronizedOutputAsync().AsTask()
					)
			);

			await Task.WhenAll(
				leases
					.Reverse()
					.Select(
						lease => lease.DisposeAsync().AsTask()
					)
			);
		}

		Assert.Equal( rounds * 2, output.WriteCount );
		Assert.Equal( rounds, output.FlushCount );
		for ( int round = 0; round < rounds; round++ ) {
			Assert.Equal(
				CsiWriter.EncodeSynchronizedOutputBeginFrame(),
				output.GetWrite( round * 2 )
			);
			Assert.Equal(
				CsiWriter.EncodeSynchronizedOutputEndFrame(),
				output.GetWrite( ( round * 2 ) + 1 )
			);
		}
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
		private readonly object sync = new();
		private readonly List<byte[]> writes = [];
		private int flushCount;

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
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
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
			}
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
				"Size is not required by synchronized-output concurrency tests."
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
