namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T164 composition of the bounded OSC 9 safe-extension surface with retained terminal protocols.
/// </summary>
public sealed class TerminalOsc9SafeExtensionCompositionTests {
	[Fact]
	public async Task SafeOsc9ComposesWithRetainedSemanticOutputInOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		byte[] clipboardPayload = [ 0x01, 0x02 ];

		await session.SendNotificationAsync( "build complete" );
		await session.SetTitleAsync( "build" );
		await session.PublishCurrentLocationAsync(
			"/work/repo",
			TerminalLocationPathStyle.Posix
		);
		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\work\\repo"
		);
		await session.BeginPromptAsync();
		await session.BeginCommandInputAsync();
		await session.WriteHyperlinkAsync(
			"source",
			"https://example.com/source"
		);
		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			clipboardPayload
		);
		await session.SetPointerShapeAsync(
			TerminalPointerShape.Pointer
		);
		await session.SetPaletteColorAsync(
			2,
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		await session.SetDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground,
			new TerminalColor( 0x4444, 0x5555, 0x6666 )
		);
		await session.BeginCommandOutputAsync();
		await session.FinishCommandAsync( 0 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;build complete\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"build"
			),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			OscWriter.EncodeLocationFrame(
				"/work/repo",
				TerminalLocationPathKind.Posix
			),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\work\\repo\u001b\\" ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			transport.GetWrite( 4 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			transport.GetWrite( 5 )
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/source" ),
			transport.GetWrite( 6 )
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "source" ), transport.GetWrite( 7 ) );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			transport.GetWrite( 8 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				clipboardPayload
			),
			transport.GetWrite( 9 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			transport.GetWrite( 10 )
		);
		Assert.StartsWith(
			Encoding.ASCII.GetBytes( "\u001b]4;2;" ),
			transport.GetWrite( 11 )
		);
		Assert.StartsWith(
			Encoding.ASCII.GetBytes( "\u001b]10;" ),
			transport.GetWrite( 12 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			transport.GetWrite( 13 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
			transport.GetWrite( 14 )
		);
	}

	[Fact]
	public async Task SafeOsc9ComposesWithProgressAndSynchronizedOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalSynchronizedOutputLease synchronized =
			await session.AcquireSynchronizedOutputAsync();
		await session.SendNotificationAsync( "working" );
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync( 1, 2 );
		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\repo"
		);
		await progress.DisposeAsync();
		await synchronized.DisposeAsync();

		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			transport.GetWrite( 0 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;working\u001b\\" ),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 50 ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\repo\u001b\\" ),
			transport.GetWrite( 3 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			transport.GetWrite( 4 )
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.GetWrite( 5 )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task SafeOsc9ComposesWithActiveQueryRouter() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalDeviceStatus> query = session.QueryDeviceStatusAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Assert.False( query.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5n" ),
			transport.GetWrite( 0 )
		);

		await session.SendNotificationAsync( "query pending" );
		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\query"
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[0n" )
		);

		Assert.Equal( TerminalDeviceStatus.Ready, await query );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;query pending\u001b\\" ),
			transport.GetWrite( 1 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\query\u001b\\" ),
			transport.GetWrite( 2 )
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly List<byte[]> writes = [];
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();

		internal int FlushCount {
			get;
			private set;
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ];
			}
		}

		internal void Publish(
			byte[] frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			if ( !this.input.Writer.TryWrite( frame ) ) {
				throw new InvalidOperationException( "Unable to publish terminal input." );
			}
		}

		internal async Task WaitForWriteCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}
			for ( int attempt = 0; 200 > attempt; ++attempt ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}
				await Task.Delay( 5 ).ConfigureAwait( false );
			}
			throw new TimeoutException( "Expected terminal output was not observed." );
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] frame = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( frame.Length > buffer.Length ) {
				throw new InvalidOperationException( "Test input frame exceeds read buffer." );
			}
			frame.CopyTo( buffer );
			return frame.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
			}
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
						| TerminalControlCapabilities.LiveSize
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 80, 24 )
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
