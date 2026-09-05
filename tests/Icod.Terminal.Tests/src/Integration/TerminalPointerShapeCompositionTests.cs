namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T116 terminal pointer-shape composition with existing semantic output, input protocols, and queries.
/// </summary>
public sealed class TerminalPointerShapeCompositionTests {
	[Fact]
	public async Task PointerShapeComposesWithExistingSemanticFamiliesInOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			CreateCompositionTerminal(),
			transport
		);
		TerminalPointerShapeLease pointer = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Pointer
		);
		byte[] clipboardPayload = [ 0x01, 0x02 ];

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
					AlternateScreen = true,
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		await presentation.DisposeAsync();

		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync(
			1,
			2
		);
		await progress.DisposeAsync();

		TerminalInputProtocolLease protocols = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			)
		).GetRequiredValue();
		await protocols.DisposeAsync();
		await pointer.DisposeAsync();

		byte[][] expected = [
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			Encoding.UTF8.GetBytes( "A" ),
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"T"
			),
			OscWriter.EncodeLocationFrame(
				"/tmp",
				TerminalLocationPathKind.Posix
			),
			OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/" ),
			Encoding.UTF8.GetBytes( "H" ),
			OscWriter.EncodeHyperlinkEndFrame(),
			OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				clipboardPayload
			),
			CsiWriter.EncodeCursorStyleFrame( 4 ),
			Encoding.Latin1.GetBytes( "<A+>" ),
			Encoding.Latin1.GetBytes( "<C0>" ),
			Encoding.Latin1.GetBytes( "<C1>" ),
			Encoding.Latin1.GetBytes( "<A->" ),
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 50 ),
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			Encoding.Latin1.GetBytes( "<P+>" ),
			Encoding.Latin1.GetBytes( "<P->" ),
			OscWriter.EncodeOsc22PointerShapeFrame( null )
		];
		Assert.Equal( expected.Length, transport.WriteCount );
		for ( int index = 0; index < expected.Length; ++index ) {
			Assert.Equal(
				expected[ index ],
				transport.GetWrite( index )
			);
		}
		Assert.Equal( 4, transport.FlushCount );
	}

	[Fact]
	public async Task NestedPointerShapeComposesWithSynchronizedOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);
		TerminalPointerShapeLease outer = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Pointer
		);
		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();
		TerminalPointerShapeLease inner = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Wait
		);

		await session.WriteTextAsync( "X" );
		await inner.DisposeAsync();
		await synchronized.DisposeAsync();
		await outer.DisposeAsync();

		byte[][] expected = [
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			OscWriter.EncodeOsc22PointerShapeFrame( "wait" ),
			Encoding.UTF8.GetBytes( "X" ),
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			OscWriter.EncodeOsc22PointerShapeFrame( null )
		];
		Assert.Equal( expected.Length, transport.WriteCount );
		for ( int index = 0; index < expected.Length; ++index ) {
			Assert.Equal(
				expected[ index ],
				transport.GetWrite( index )
			);
		}
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task ActiveQueryCompletesWhilePointerShapeIsOwned() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			transport
		);
		TerminalPointerShapeLease pointer = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Crosshair
		);

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

		await session.WriteTextAsync( "Q" );
		await pointer.DisposeAsync();

		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "crosshair" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "Q" ), transport.GetWrite( 2 ) );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			transport.GetWrite( 3 )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	private static TerminalDescription CreateCompositionTerminal() {
		return new TerminalDescriptionBuilder( "pointer-composition" )
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				"<A+>"
			)
			.SetString(
				StringCapability.ExitCursorAddressingMode,
				"<A->"
			)
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
					if ( expected <= this.writes.Count ) {
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
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
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
				"Size is not used by terminal pointer-shape composition tests."
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
