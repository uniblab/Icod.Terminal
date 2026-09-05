namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the frozen T124 public OSC 133 semantic-prompt API.
/// </summary>
public sealed class TerminalSessionSemanticPromptPublicApiTests {
	[Fact]
	public async Task PublicSemanticOperationsEmitCanonicalFrames() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.BeginPromptAsync();
		await session.BeginCommandInputAsync();
		await session.BeginCommandOutputAsync();
		await session.FinishCommandAsync( 255 );
		await session.AbortCommandAsync();

		Assert.Equal( 5, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 2 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;255\u001b\\" ),
			output.Writes[ 3 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D\u001b\\" ),
			output.Writes[ 4 ]
		);
		Assert.All(
			output.WriteCancellationTokens,
			token => Assert.False( token.CanBeCanceled )
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task SuccessfulCompletionRemainsDistinctFromAbort() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.FinishCommandAsync( 0 );
		await session.AbortCommandAsync();

		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D\u001b\\" ),
			output.Writes[ 1 ]
		);
		Assert.NotEqual(
			output.Writes[ 0 ],
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task PublicOperationsRemainIndependentlyCallable() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.AbortCommandAsync();
		await session.BeginCommandOutputAsync();
		await session.BeginPromptAsync();
		await session.FinishCommandAsync( 7 );
		await session.BeginCommandInputAsync();

		Assert.Equal( 5, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			output.Writes[ 2 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;7\u001b\\" ),
			output.Writes[ 3 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			output.Writes[ 4 ]
		);
	}

	[Fact]
	public async Task PreCancelledPublicOperationEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.BeginPromptAsync(
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task DisposedSessionRejectsPublicOperation() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		await session.DisposeAsync();

		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.AbortCommandAsync().AsTask()
		);

		Assert.Empty( output.Writes );
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

		public ValueTask WriteAsync(
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
