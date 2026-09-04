namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the bounded T47 hyperlink operation participates in session-owned output ordering.
/// </summary>
public sealed class TerminalSessionHyperlinkOrderingTests {
	[Fact]
	public async Task HyperlinkWaitsForApplicationWriteToFinish() {
		BlockingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		Task applicationWrite = session.WriteTextAsync( "A" ).AsTask();
		await output.FirstWriteStarted;

		Task hyperlinkWrite = session.WriteHyperlinkAsync(
			"linked",
			"https://example.com/"
		).AsTask();
		await Task.Delay( 50 );

		Assert.False( hyperlinkWrite.IsCompleted );
		Assert.Equal( 1, output.MaximumConcurrentWrites );

		output.ReleaseFirstWrite();
		await applicationWrite;
		await hyperlinkWrite;

		Assert.Equal( 1, output.MaximumConcurrentWrites );
		Assert.Equal( 4, output.Writes.Count );
		Assert.Equal( new byte[] { 0x41 }, output.Writes[ 0 ] );
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "linked" ),
			output.Writes[ 2 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 3 ]
		);
	}

	[Fact]
	public async Task OtherSessionOutputWaitsForCompleteHyperlinkOperation() {
		BlockingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		Task hyperlinkWrite = session.WriteHyperlinkAsync(
			"linked",
			"https://example.com/"
		).AsTask();
		await output.FirstWriteStarted;

		Task titleWrite = session.SetWindowTitleAsync( "title" ).AsTask();
		Task locationWrite = session.PublishCurrentLocationAsync(
			"/usr/src",
			TerminalLocationPathStyle.Posix
		).AsTask();
		await Task.Delay( 50 );

		Assert.False( titleWrite.IsCompleted );
		Assert.False( locationWrite.IsCompleted );
		Assert.Equal( 1, output.MaximumConcurrentWrites );

		output.ReleaseFirstWrite();
		await hyperlinkWrite;
		await titleWrite;
		await locationWrite;

		Assert.Equal( 1, output.MaximumConcurrentWrites );
		Assert.Equal( 5, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "linked" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task HyperlinkWaitsBehindControlOutputLease() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task hyperlinkWrite = session.WriteHyperlinkAsync(
			"linked",
			"https://example.com/"
		).AsTask();
		await Task.Delay( 50 );

		Assert.False( hyperlinkWrite.IsCompleted );
		Assert.Empty( output.Bytes );

		controlOutput.Dispose();
		await hyperlinkWrite;

		Assert.Equal( 3, output.WriteCount );
	}

	[Fact]
	public async Task DisposedSessionRejectsHyperlinkWithoutWriting() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		await session.DisposeAsync();
		output.Reset();

		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.WriteHyperlinkAsync(
				"linked",
				"https://example.com/"
			).AsTask()
		);

		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.WriteCount );
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

		internal int WriteCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			this.Bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		internal void Reset() {
			this.Bytes.Clear();
			this.WriteCount = 0;
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
