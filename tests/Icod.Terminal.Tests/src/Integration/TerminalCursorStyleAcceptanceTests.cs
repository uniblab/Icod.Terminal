namespace Icod.Terminal.Tests.Integration;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T86 cursor-style integration with prior semantic output and presentation state.
/// </summary>
public sealed class TerminalCursorStyleAcceptanceTests {
	[Fact]
	public async Task CursorStyleAndVisibilityRemainOrthogonal() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		TerminalPresentationLease visibility = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();

		await session.SetCursorStyleAsync(
			TerminalCursorStyle.SteadyUnderline
		);
		await visibility.DisposeAsync();

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<C0>" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 4 ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<C1>" ),
			output.Writes[ 2 ]
		);
		Assert.Equal( 2, output.FlushCount );
	}

	[Fact]
	public async Task CursorStyleComposesWithAllPriorSemanticOutputFamilies() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		await session.WriteTextAsync( "A" );
		await session.SetTitleAsync( "T0" );
		await session.SetIconNameAsync( "T1" );
		await session.SetWindowTitleAsync( "T2" );
		await session.PublishCurrentLocationAsync(
			"/tmp",
			TerminalLocationPathStyle.Posix
		);
		await session.WriteHyperlinkAsync(
			"H",
			"https://example.com/"
		);
		await session.SetCursorStyleAsync(
			TerminalCursorStyle.BlinkingBar
		);
		byte[] clipboardPayload = [ 0x01, 0x02 ];
		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			clipboardPayload
		);

		Assert.Equal( 10, output.Writes.Count );
		Assert.Equal( Encoding.UTF8.GetBytes( "A" ), output.Writes[ 0 ] );
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"T0"
			),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconName,
				"T1"
			),
			output.Writes[ 2 ]
		);
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.WindowTitle,
				"T2"
			),
			output.Writes[ 3 ]
		);
		Assert.Equal(
			OscWriter.EncodeLocationFrame(
				"/tmp",
				TerminalLocationPathKind.Posix
			),
			output.Writes[ 4 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/"
			),
			output.Writes[ 5 ]
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "H" ), output.Writes[ 6 ] );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 7 ]
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 5 ),
			output.Writes[ 8 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				clipboardPayload
			),
			output.Writes[ 9 ]
		);
		Assert.Equal( 0, output.FlushCount );
	}

	private static TerminalDescription CreatePresentationTerminal() {
		return new TerminalDescriptionBuilder( "cursor-style-acceptance" )
			.SetString(
				StringCapability.CursorInvisible,
				"<C0>"
			)
			.SetString(
				StringCapability.CursorNormal,
				"<C1>"
			)
			.SetString(
				StringCapability.CursorVeryVisible,
				"<C2>"
			)
			.Build();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal,
		RecordingTerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false
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

		internal int FlushCount {
			get;
			private set;
		}

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
				"Size is not used by cursor-style acceptance tests."
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
