namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies composition between bounded and explicitly scoped OSC 8 output.
/// </summary>
public sealed class TerminalSessionHyperlinkCompositionTests {
	[Fact]
	public async Task BoundedHyperlinkInsideOuterScopeRestoresOuterState() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await using ( TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
			"https://example.com/outer",
			"outer"
		) ) {
			await session.WriteTextAsync( "A" );
			await session.WriteHyperlinkAsync(
				"B",
				"https://example.com/inner",
				"inner"
			);
			await session.WriteTextAsync( "C" );
		}

		Assert.Equal( 7, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/outer",
				"outer"
			),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "A" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/inner",
				"inner"
			),
			output.Writes[ 2 ]
		);
		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "B" ),
			output.Writes[ 3 ]
		);
		Assert.Equal( output.Writes[ 0 ], output.Writes[ 4 ] );
		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "C" ),
			output.Writes[ 5 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 6 ]
		);
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

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
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
				"Size is not required by hyperlink composition tests."
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
