namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T34 session-owned output ordering, flush, and close semantics.
/// </summary>
public sealed class TerminalSessionOutputOrderingTests {
	[Fact]
	public async Task TitleWaitsForApplicationWriteToFinish() {
		BlockingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		Task applicationWrite = session.WriteTextAsync( "A" ).AsTask();
		await output.FirstWriteStarted;

		Task titleWrite = session.SetWindowTitleAsync( "title" ).AsTask();
		await Task.Delay( 50 );

		Assert.False( titleWrite.IsCompleted );
		Assert.Equal( 1, output.MaximumConcurrentWrites );

		output.ReleaseFirstWrite();
		await applicationWrite;
		await titleWrite;

		Assert.Equal( 1, output.MaximumConcurrentWrites );
		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal( new byte[] { 0x41 }, output.Writes[ 0 ] );
		Assert.Equal(
			Convert.FromHexString( "1B5D323B7469746C651B5C" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task ApplicationWriteWaitsBehindControlOutputLease() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task applicationWrite = session.WriteTextAsync( "application" ).AsTask();
		await Task.Delay( 50 );

		Assert.False( applicationWrite.IsCompleted );
		Assert.Empty( output.Bytes );

		controlOutput.Dispose();
		await applicationWrite;

		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "application" ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task TitleWriteDoesNotFlushImplicitly() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		await session.SetTitleAsync( "title" );

		Assert.Equal( 0, output.FlushCount );
		await session.DisposeAsync();
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task DisposedSessionRejectsNewApplicationAndTitleOutput() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		await session.DisposeAsync();

		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.WriteTextAsync( "text" ).AsTask()
		);
		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.SetTitleAsync( "title" ).AsTask()
		);
		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.SetIconNameAsync( "icon" ).AsTask()
		);
		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.SetWindowTitleAsync( "window" ).AsTask()
		);

		Assert.Empty( output.Bytes );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
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
		internal List<byte> Bytes {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Bytes.AddRange( buffer.ToArray() );
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
			cancellationToken.ThrowIfCancellationRequested();
			int active = Interlocked.Increment( ref this.activeWrites );
			UpdateMaximum(
				ref this.maximumConcurrentWrites,
				active
			);

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

			return TerminalControlMutationResult.Success();
		}
	}
}
