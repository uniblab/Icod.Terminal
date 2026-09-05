namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T94 synchronized-output composition with existing semantic output and queries.
/// </summary>
public sealed class TerminalSynchronizedOutputCompositionTests {
	[Fact]
	public async Task SynchronizedOutputBracketsExistingSemanticOutputInOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			transport
		);
		byte[] clipboardPayload = [ 0x01, 0x02 ];

		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();
		await session.WriteTextAsync( "A" );
		await session.WriteTerminalStringAsync( "<TI>" );
		await session.SetTitleAsync( "T" );
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

		TerminalPresentationLease presentation = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		await presentation.DisposeAsync();
		await synchronized.DisposeAsync();

		Assert.Equal( 13, transport.Writes.Count );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			transport.Writes[ 0 ]
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "A" ), transport.Writes[ 1 ] );
		Assert.Equal( Encoding.Latin1.GetBytes( "<TI>" ), transport.Writes[ 2 ] );
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"T"
			),
			transport.Writes[ 3 ]
		);
		Assert.Equal(
			OscWriter.EncodeLocationFrame(
				"/tmp",
				TerminalLocationPathKind.Posix
			),
			transport.Writes[ 4 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/"
			),
			transport.Writes[ 5 ]
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "H" ), transport.Writes[ 6 ] );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			transport.Writes[ 7 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				clipboardPayload
			),
			transport.Writes[ 8 ]
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 4 ),
			transport.Writes[ 9 ]
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<C0>" ),
			transport.Writes[ 10 ]
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<C1>" ),
			transport.Writes[ 11 ]
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.Writes[ 12 ]
		);
		Assert.Equal( 3, transport.FlushCount );
	}

	[Fact]
	public async Task ActiveQueryCompletesInsideSynchronizedOutputLease() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);
		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();

		Task<TerminalDeviceStatus> query = session.QueryDeviceStatusAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 2 );

		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5n" ),
			transport.GetWrite( 1 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[0n" )
		);
		Assert.Equal( TerminalDeviceStatus.Ready, await query );

		await synchronized.DisposeAsync();
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.GetWrite( 2 )
		);
		Assert.Equal( 2, transport.FlushCount );
	}

	private static TerminalDescription CreatePresentationTerminal() {
		return new TerminalDescriptionBuilder( "synchronized-output-composition" )
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
				"Size is not used by synchronized-output composition tests."
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
