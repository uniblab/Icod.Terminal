namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies the T24 public CSI query family on the T23 transaction substrate.
/// </summary>
public sealed class TerminalCsiQueryTests {
	[Fact]
	public async Task PrimaryDeviceAttributesUsesTypedSevenBitTransaction() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPrimaryDeviceAttributes> query = session.QueryPrimaryDeviceAttributesAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[c" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[?65;1;2;4;6c" )
		);

		TerminalPrimaryDeviceAttributes result = await query;

		Assert.Equal( 65, result.DeviceCode );
		Assert.Equal(
			new[] { 1, 2, 4, 6 },
			result.Attributes
		);
		Assert.True( result.HasAttribute( 4 ) );
		Assert.False( result.HasAttribute( 9 ) );
	}

	[Fact]
	public async Task SecondaryDeviceAttributesUsesTypedThreeParameterResponse() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalSecondaryDeviceAttributes> query = session.QuerySecondaryDeviceAttributesAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[>c" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[>0;411;0c" )
		);

		TerminalSecondaryDeviceAttributes result = await query;

		Assert.Equal( 0, result.TerminalTypeCode );
		Assert.Equal( 411, result.FirmwareVersion );
		Assert.Equal( 0, result.OptionCode );
	}

	[Theory]
	[InlineData( 0, TerminalDeviceStatus.Ready )]
	[InlineData( 1, TerminalDeviceStatus.BusyRequestAgain )]
	[InlineData( 2, TerminalDeviceStatus.BusyReportFollows )]
	[InlineData( 3, TerminalDeviceStatus.MalfunctionRequestAgain )]
	[InlineData( 4, TerminalDeviceStatus.MalfunctionReportFollows )]
	public async Task DeviceStatusPreservesProtocolDefinedStatus(
		int wireStatus,
		TerminalDeviceStatus expected
	) {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalDeviceStatus> query = session.QueryDeviceStatusAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5n" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b[{wireStatus}n" )
		);

		Assert.Equal( expected, await query );
	}

	[Fact]
	public async Task CursorPositionIsOneBasedAndUsesStandardCpr() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorPosition> query = session.QueryCursorPositionAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[6n" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[24;80R" )
		);

		TerminalCursorPosition position = await query;

		Assert.Equal( 24, position.Row );
		Assert.Equal( 80, position.Column );
	}

	[Fact]
	public async Task EightBitCsiResponseIsAcceptedForStandardCpr() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorPosition> query = session.QueryCursorPositionAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			[
				0x9B,
				(byte)'3',
				(byte)';',
				(byte)'7',
				(byte)'R'
			]
		);

		TerminalCursorPosition position = await query;

		Assert.Equal( 3, position.Row );
		Assert.Equal( 7, position.Column );
	}

	[Fact]
	public async Task CorrelatedMalformedCprFailsDeterministically() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorPosition> query = session.QueryCursorPositionAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[0;80R" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task CorrelatedNonDecimalCprFailsDeterministically() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCursorPosition> query = session.QueryCursorPositionAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[2:3R" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task CorrelatedMalformedSecondaryDaFailsDeterministically() {
		CsiTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalSecondaryDeviceAttributes> query = session.QuerySecondaryDeviceAttributesAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[>0;411c" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public void ParserRejectsOversizedAndExcessiveParameters() {
		TerminalResponseFrame oversized = new(
			TerminalResponseFrameKind.Csi,
			Encoding.ASCII.GetBytes( "\u001b[1000001;1R" )
		);
		string excessiveParameters = string.Join(
			';',
			Enumerable.Repeat(
				"1",
				TerminalCsiQueryProtocol.MaximumParameterCount + 1
			)
		);
		TerminalResponseFrame excessive = new(
			TerminalResponseFrameKind.Csi,
			Encoding.ASCII.GetBytes( $"\u001b[?65;{excessiveParameters}c" )
		);

		Assert.Throws<FormatException>(
			() => TerminalCsiQueryProtocol.ParseCursorPosition( oversized )
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiQueryProtocol.ParsePrimaryDeviceAttributes( excessive )
		);
	}

	[Fact]
	public async Task CprShapedFunctionKeyRemainsApplicationInputWithoutQuery() {
		CsiTransport transport = new();
		TerminalDescription terminal = new TerminalDescriptionBuilder( "t24-ambiguous-key" )
			.SetString(
				StringCapability.KeyF3,
				"\u001b[1;2R"
			)
			.Build();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			terminal
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[1;2R" )
		);
		TerminalInputEvent input = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);

		Assert.Equal( TerminalInputEventKind.Key, input.Kind );
		Assert.Equal( TerminalKey.Function, input.Key );
		Assert.Equal( 3, input.FunctionKeyNumber );
		Assert.Equal( TerminalKeyModifiers.Shift, input.Modifiers );
	}

	[Fact]
	public async Task RichApplicationInputRemainsOrderedWhileCprIsPending() {
		CsiTransport transport = new();
		TerminalDescription terminal = CreateRichInputTerminal();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			terminal
		);

		Task<TerminalCursorPosition> query = session.QueryCursorPositionAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish( Encoding.UTF8.GetBytes( "x" ) );
		TerminalInputEvent text = await ReadInputAsync( session );
		Assert.Equal( TerminalInputEventKind.Text, text.Kind );
		Assert.Equal( new Rune( 'x' ), text.Character );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[A" ) );
		TerminalInputEvent key = await ReadInputAsync( session );
		Assert.Equal( TerminalInputEventKind.Key, key.Kind );
		Assert.Equal( TerminalKey.Up, key.Key );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[I" ) );
		TerminalInputEvent focus = await ReadInputAsync( session );
		Assert.Equal( TerminalInputEventKind.Focus, focus.Kind );
		Assert.Equal(
			TerminalFocusState.Focused,
			Assert.IsType<TerminalFocusEvent>( focus.Focus ).State
		);

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[<0;3;4M" ) );
		TerminalInputEvent mouse = await ReadInputAsync( session );
		TerminalMouseEvent mouseEvent = Assert.IsType<TerminalMouseEvent>( mouse.Mouse );
		Assert.Equal( TerminalInputEventKind.Mouse, mouse.Kind );
		Assert.Equal( 2, mouseEvent.Column );
		Assert.Equal( 3, mouseEvent.Row );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[200~" ) );
		TerminalInputEvent pasteBegin = await ReadInputAsync( session );
		Assert.Equal( TerminalInputEventKind.Paste, pasteBegin.Kind );
		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>( pasteBegin.Paste ).Phase
		);

		transport.Publish( Encoding.UTF8.GetBytes( "p" ) );
		TerminalInputEvent pasteData = await ReadInputAsync( session );
		TerminalPasteEvent data = Assert.IsType<TerminalPasteEvent>( pasteData.Paste );
		Assert.Equal( TerminalPastePhase.Data, data.Phase );
		Assert.Equal( "p", data.Text );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[201~" ) );
		TerminalInputEvent pasteEnd = await ReadInputAsync( session );
		Assert.Equal(
			TerminalPastePhase.End,
			Assert.IsType<TerminalPasteEvent>( pasteEnd.Paste ).Phase
		);

		Assert.False( query.IsCompleted );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[9;10R" ) );
		TerminalCursorPosition position = await query;

		Assert.Equal( 9, position.Row );
		Assert.Equal( 10, position.Column );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	private static async Task<TerminalInputEvent> ReadInputAsync(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		TerminalEvent terminalEvent = await session.ReadEventAsync();
		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		return Assert.IsType<TerminalInputEvent>( terminalEvent.Input );
	}

	private static TerminalDescription CreateRichInputTerminal() {
		return new TerminalDescriptionBuilder( "t24-rich-input" )
			.SetString(
				StringCapability.KeyCursorUp,
				"\u001b[A"
			)
			.SetString(
				StringCapability.KeyMouse,
				"\u001b[<"
			)
			.SetExtendedString(
				"kxIN",
				"\u001b[I"
			)
			.SetExtendedString(
				"kxOUT",
				"\u001b[O"
			)
			.SetExtendedString(
				"PS",
				"\u001b[200~"
			)
			.SetExtendedString(
				"PE",
				"\u001b[201~"
			)
			.Build();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		CsiTransport transport,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal ?? TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = SystemMonotonicClock.Instance,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		CsiTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		for ( int attempt = 0; attempt < 10_000; attempt++ ) {
			if ( expected <= transport.WriteCount ) {
				return;
			}
			await Task.Yield();
		}

		Assert.True(
			expected <= transport.WriteCount,
			$"Expected at least {expected} terminal writes, observed {transport.WriteCount}."
		);
	}

	private sealed class CsiTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly List<byte[]> writes = [];
		private int activeReads;
		private int maximumConcurrentReads;

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal int MaximumConcurrentReads {
			get {
				return Volatile.Read( ref this.maximumConcurrentReads );
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
					"The scripted terminal input channel is closed."
				);
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			int active = Interlocked.Increment( ref this.activeReads );
			this.RecordMaximumConcurrentReads( active );
			try {
				byte[] bytes = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( bytes.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The scripted CSI input chunk exceeds the decoder read buffer."
					);
				}

				bytes.AsSpan().CopyTo( buffer.Span );
				return bytes.Length;
			} finally {
				Interlocked.Decrement( ref this.activeReads );
			}
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
			return ValueTask.CompletedTask;
		}

		private void RecordMaximumConcurrentReads(
			int active
		) {
			while ( true ) {
				int observed = Volatile.Read( ref this.maximumConcurrentReads );
				if ( active <= observed ) {
					return;
				}
				if ( observed == Interlocked.CompareExchange(
					ref this.maximumConcurrentReads,
					active,
					observed
				) ) {
					return;
				}
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
			return TerminalControlResult<TerminalSize>.Unavailable(
				"No scripted live size."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
