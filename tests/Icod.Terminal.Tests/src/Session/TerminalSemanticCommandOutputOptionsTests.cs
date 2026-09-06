namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T153 typed OSC 133 command-output metadata and public session integration.
/// </summary>
public sealed class TerminalSemanticCommandOutputOptionsTests {
	[Fact]
	public void DefaultOptionsRepresentNoCommandLineMetadata() {
		TerminalSemanticCommandOutputOptions options = default;

		Assert.Null( options.CommandLine );
	}

	[Fact]
	public async Task DefaultOptionsEmitExactlyThePortableBareCommandOutputFrame() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.BeginCommandOutputAsync(
			default( TerminalSemanticCommandOutputOptions )
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task EmptyCommandLinePublishesExplicitEmptyMetadata() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSemanticCommandOutputOptions options = new( string.Empty );

		await session.BeginCommandOutputAsync( options );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C;cmdline_url=\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task NonEmptyCommandLineUsesStrictUtf8PercentEncoding() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		const string commandLine = "echo café 😀; $HOME";
		TerminalSemanticCommandOutputOptions options = new( commandLine );

		await session.BeginCommandOutputAsync( options );

		Assert.Equal( commandLine, options.CommandLine );
		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;C;cmdline_url=echo%20caf%C3%A9%20%F0%9F%98%80%3B%20%24HOME\u001b\\"
			),
			output.Writes[ 0 ]
		);
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task IllFormedUtf16IsRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSemanticCommandOutputOptions options = new( "\ud800" );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.BeginCommandOutputAsync( options ).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task ExactMaximumPayloadIsAccepted() {
		const int prefixLength = 18;
		string commandLine = new(
			'a',
			TerminalOsc133ExtendedMetadataEncoder.MaximumPayloadLength - prefixLength
		);
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSemanticCommandOutputOptions options = new( commandLine );

		await session.BeginCommandOutputAsync( options );

		Assert.Single( output.Writes );
		Assert.Equal(
			TerminalOsc133ExtendedMetadataEncoder.MaximumPayloadLength + 4,
			output.Writes[ 0 ].Length
		);
	}

	[Fact]
	public async Task OneByteOverMaximumPayloadIsRejectedBeforeOutput() {
		const int prefixLength = 18;
		string commandLine = new(
			'a',
			TerminalOsc133ExtendedMetadataEncoder.MaximumPayloadLength - prefixLength + 1
		);
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSemanticCommandOutputOptions options = new( commandLine );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.BeginCommandOutputAsync( options ).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task PreCancelledCommandOutputEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		TerminalSemanticCommandOutputOptions options = new( "echo hello" );

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.BeginCommandOutputAsync(
				options,
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
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
