namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T106 terminal-progress composition with existing semantic output and queries.
/// </summary>
public sealed class TerminalProgressCompositionTests {
	[Fact]
	public async Task ProgressComposesWithExistingSemanticOutputInOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			transport
		);
		byte[] clipboardPayload = [ 0x01, 0x02 ];
		TerminalProgressLease progress = await session.AcquireProgressAsync();

		await progress.ReportAsync( 1, 10 );
		await session.WriteTextAsync( "A" );
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
		await progress.SetIndeterminateAsync();
		await progress.ReportAsync(
			TerminalProgressState.Attention,
			9,
			10
		);
		await progress.DisposeAsync();

		Assert.Equal( 14, transport.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 10 ),
			transport.Writes[ 0 ]
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "A" ), transport.Writes[ 1 ] );
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"T"
			),
			transport.Writes[ 2 ]
		);
		Assert.Equal(
			OscWriter.EncodeLocationFrame(
				"/tmp",
				TerminalLocationPathKind.Posix
			),
			transport.Writes[ 3 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/" ),
			transport.Writes[ 4 ]
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "H" ), transport.Writes[ 5 ] );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			transport.Writes[ 6 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				clipboardPayload
			),
			transport.Writes[ 7 ]
		);
		Assert.Equal(
			CsiWriter.EncodeCursorStyleFrame( 4 ),
			transport.Writes[ 8 ]
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<C0>" ),
			transport.Writes[ 9 ]
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<C1>" ),
			transport.Writes[ 10 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Indeterminate, 0 ),
			transport.Writes[ 11 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Attention, 90 ),
			transport.Writes[ 12 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			transport.Writes[ 13 ]
		);
		Assert.Equal( 2, transport.FlushCount );
	}

	[Fact]
	public async Task ProgressComposesWithSynchronizedOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync( 1, 10 );
		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();

		await progress.ReportAsync( 2, 10 );
		await session.WriteTextAsync( "X" );
		await progress.SetIndeterminateAsync();
		await synchronized.DisposeAsync();
		await progress.DisposeAsync();

		Assert.Equal( 7, transport.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 10 ),
			transport.Writes[ 0 ]
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 20 ),
			transport.Writes[ 2 ]
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "X" ), transport.Writes[ 3 ] );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Indeterminate, 0 ),
			transport.Writes[ 4 ]
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.Writes[ 5 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			transport.Writes[ 6 ]
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task ActiveQueryCompletesWhileProgressIsOwned() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync( 1, 4 );

		Task<TerminalDeviceStatus> query = session.QueryDeviceStatusAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5n" ),
			transport.GetWrite( 1 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[0n" )
		);
		Assert.Equal( TerminalDeviceStatus.Ready, await query );

		await progress.ReportAsync( 2, 4 );
		await progress.DisposeAsync();

		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 25 ),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 50 ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			transport.GetWrite( 3 )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	private static TerminalDescription CreatePresentationTerminal() {
		return new TerminalDescriptionBuilder( "progress-composition" )
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
				"Size is not used by terminal-progress composition tests."
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
