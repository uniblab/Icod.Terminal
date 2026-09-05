namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T126 OSC 133 composition with existing terminal output, leases,
/// presentation, rich input, synchronized output, progress, and active queries.
/// </summary>
public sealed class TerminalSemanticPromptCompositionTests {
	[Fact]
	public async Task SemanticPromptComposesWithExistingSemanticOutputInOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			CreateCompositionTerminal(),
			transport
		);
		byte[] clipboardPayload = [ 0x01, 0x02 ];

		await session.BeginPromptAsync();
		await session.WriteTextAsync( "P" );
		await session.SetTitleAsync( "T" );
		await session.BeginCommandInputAsync();
		await session.PublishCurrentLocationAsync(
			"/tmp",
			TerminalLocationPathStyle.Posix
		);
		await session.WriteHyperlinkAsync(
			"H",
			"https://example.com/"
		);
		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			clipboardPayload
		);
		await session.SetCursorStyleAsync(
			TerminalCursorStyle.SteadyUnderline
		);
		await session.SetPointerShapeAsync(
			TerminalPointerShape.Pointer
		);

		TerminalPresentationLease presentation = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		TerminalInputProtocolLease protocols = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			)
		).GetRequiredValue();

		await session.BeginCommandOutputAsync();
		await protocols.DisposeAsync();
		await presentation.DisposeAsync();
		await session.FinishCommandAsync( 0 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "P" ), transport.GetWrite( 1 ) );
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"T"
			),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			OscWriter.EncodeLocationFrame(
				"/tmp",
				TerminalLocationPathKind.Posix
			),
			transport.GetWrite( 4 )
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/" ),
			transport.GetWrite( 5 )
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "H" ), transport.GetWrite( 6 ) );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			transport.GetWrite( 7 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				clipboardPayload
			),
			transport.GetWrite( 8 )
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 4 ),
			transport.GetWrite( 9 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			transport.GetWrite( 10 )
		);
		Assert.Equal( Encoding.Latin1.GetBytes( "<C0>" ), transport.GetWrite( 11 ) );
		Assert.Equal( Encoding.Latin1.GetBytes( "<P+>" ), transport.GetWrite( 12 ) );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			transport.GetWrite( 13 )
		);
		Assert.Equal( Encoding.Latin1.GetBytes( "<P->" ), transport.GetWrite( 14 ) );
		Assert.Equal( Encoding.Latin1.GetBytes( "<C1>" ), transport.GetWrite( 15 ) );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
			transport.GetWrite( 16 )
		);
		Assert.Equal( 4, transport.FlushCount );
	}

	[Fact]
	public async Task SemanticPromptComposesWithProgressAndSynchronizedOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);

		await session.BeginPromptAsync();
		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();
		await session.BeginCommandInputAsync();
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync( 1, 2 );
		await session.BeginCommandOutputAsync();
		await session.FinishCommandAsync( 7 );
		await progress.DisposeAsync();
		await synchronized.DisposeAsync();

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 50 ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			transport.GetWrite( 4 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;7\u001b\\" ),
			transport.GetWrite( 5 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			transport.GetWrite( 6 )
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.GetWrite( 7 )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task SemanticPromptComposesWithActiveQuery() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);

		await session.BeginPromptAsync();
		Task<TerminalDeviceStatus> query = session.QueryDeviceStatusAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.False( query.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5n" ),
			transport.GetWrite( 1 )
		);

		await session.BeginCommandInputAsync();
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[0n" )
		);
		Assert.Equal( TerminalDeviceStatus.Ready, await query );
		await session.BeginCommandOutputAsync();
		await session.FinishCommandAsync( 0 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
			transport.GetWrite( 4 )
		);
	}

	private static TerminalDescription CreateCompositionTerminal() {
		return new TerminalDescriptionBuilder( "semantic-prompt-composition" )
			.SetString( StringCapability.CursorInvisible, "<C0>" )
			.SetString( StringCapability.CursorNormal, "<C1>" )
			.SetString( StringCapability.CursorVeryVisible, "<C2>" )
			.SetExtendedString( "BE", "<P+>" )
			.SetExtendedString( "BD", "<P->" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "PE", "\u001b[201~" )
			.Build();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal,
		RecordingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly SemaphoreSlim writeSignal = new( 0 );

		internal List<byte[]> Writes {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.Writes[ index ].ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected
		) {
			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.Writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] bytes = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			bytes.AsSpan().CopyTo( buffer.Span );
			return bytes.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
				this.Writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
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
				"Size is not used by semantic-prompt composition tests."
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
