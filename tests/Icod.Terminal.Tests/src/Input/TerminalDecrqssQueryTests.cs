namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies the T25 public DECRQSS query family on the common transaction substrate.
/// </summary>
public sealed class TerminalDecrqssQueryTests {
	[Theory]
	[InlineData( TerminalStatusStringKind.SelectGraphicRendition, "m" )]
	[InlineData( TerminalStatusStringKind.ConformanceLevel, "\"p" )]
	[InlineData( TerminalStatusStringKind.CursorStyle, " q" )]
	[InlineData( TerminalStatusStringKind.CharacterProtection, "\"q" )]
	[InlineData( TerminalStatusStringKind.ScrollingRegion, "r" )]
	[InlineData( TerminalStatusStringKind.LeftRightMargins, "s" )]
	[InlineData( TerminalStatusStringKind.LinesPerPage, "t" )]
	[InlineData( TerminalStatusStringKind.ColumnsPerPage, "$|" )]
	[InlineData( TerminalStatusStringKind.ActiveStatusDisplay, "$}" )]
	[InlineData( TerminalStatusStringKind.StatusLineType, "$~" )]
	[InlineData( TerminalStatusStringKind.AttributeChangeExtent, "*x" )]
	[InlineData( TerminalStatusStringKind.LinesPerScreen, "*|" )]
	public void FixedStatusStringKindsMapToBoundedIdentifiers(
		TerminalStatusStringKind kind,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			TerminalDecrqssProtocol.GetRequestIdentifier( kind )
		);
		Assert.True(
			TerminalDecrqssProtocol.MaximumRequestIdentifierBytes
				>= expected.Length
		);
	}

	[Fact]
	public async Task PositiveSgrResponseUsesTypedSevenBitTransaction() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.SelectGraphicRendition,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP$qm\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r0;4;7m\u001b\\" )
		);
		TerminalStatusStringResponse response = await query;

		Assert.Equal(
			TerminalStatusStringKind.SelectGraphicRendition,
			response.Kind
		);
		Assert.True( response.IsSupported );
		Assert.Equal( "0;4;7m", response.StatusString );
	}

	[Fact]
	public async Task NegativeResponseIsTypedAsUnsupported() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.CursorStyle,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP0$r\u001b\\" )
		);
		TerminalStatusStringResponse response = await query;

		Assert.Equal( TerminalStatusStringKind.CursorStyle, response.Kind );
		Assert.False( response.IsSupported );
		Assert.Null( response.StatusString );
	}

	[Fact]
	public async Task EightBitDcsAndStringTerminatorAreAccepted() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.CursorStyle,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			[
				0x90,
				(byte)'1',
				(byte)'$',
				(byte)'r',
				(byte)'1',
				(byte)' ',
				(byte)'q',
				0x9C
			]
		);
		TerminalStatusStringResponse response = await query;

		Assert.True( response.IsSupported );
		Assert.Equal( "1 q", response.StatusString );
	}

	[Fact]
	public async Task FragmentedDecrpssResponseIsRoutedAcrossReads() {
		ManualMonotonicClock clock = new();
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock,
			TimeSpan.FromSeconds( 1 )
		);

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.ScrollingRegion,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		byte[] response = Encoding.ASCII.GetBytes(
			"\u001bP1$r1;24r\u001b\\"
		);
		foreach ( byte value in response ) {
			transport.Publish( [ value ] );
		}

		TerminalStatusStringResponse result = await query;

		Assert.True( result.IsSupported );
		Assert.Equal( "1;24r", result.StatusString );
	}

	[Fact]
	public async Task OrdinaryInputRemainsLiveBeforeDcsResponse() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.SelectGraphicRendition,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		byte[] combined = Encoding.UTF8.GetBytes( "x" )
			.Concat(
				Encoding.ASCII.GetBytes( "\u001bP1$r0m\u001b\\" )
			)
			.ToArray();
		transport.Publish( combined );

		TerminalEvent terminalEvent = await session.ReadEventAsync();
		TerminalInputEvent input = Assert.IsType<TerminalInputEvent>(
			terminalEvent.Input
		);
		TerminalStatusStringResponse response = await query;

		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		Assert.Equal( new Rune( 'x' ), input.Character );
		Assert.Equal( "0m", response.StatusString );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task CorrelatedMalformedValidityFailsDeterministically() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.SelectGraphicRendition,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP2$r0m\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task CorrelatedWrongStatusStringFailsDeterministically() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
			TerminalStatusStringKind.CursorStyle,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r0m\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public void UnrelatedDcsFamilyDoesNotMatchDecrqssExpectation() {
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Dcs,
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544e=787465726d\u001b\\"
			)
		);

		Assert.False(
			TerminalDecrqssProtocol.ResponseMatcher.IsMatch( frame )
		);
	}

	[Fact]
	public void OversizedPositiveStatusStringIsRejected() {
		string payload = new string(
			'0',
			TerminalDecrqssProtocol.MaximumStatusStringBytes
		) + "m";
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Dcs,
			Encoding.ASCII.GetBytes(
				$"\u001bP1$r{payload}\u001b\\"
			)
		);

		Assert.Throws<FormatException>(
			() => TerminalDecrqssProtocol.ParseResponse(
				TerminalStatusStringKind.SelectGraphicRendition,
				frame
			)
		);
	}

	[Fact]
	public async Task InvalidPublicKindCannotEmitControlBytes() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.QueryStatusStringAsync(
				(TerminalStatusStringKind)int.MaxValue,
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task CancellationRetainsLateDcsOwnership() {
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();

		Task<TerminalStatusStringResponse> first = session.QueryStatusStringAsync(
			TerminalStatusStringKind.SelectGraphicRendition,
			TimeSpan.FromSeconds( 30 ),
			cancellation.Token
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => first );

		Task<TerminalStatusStringResponse> second = session.QueryStatusStringAsync(
			TerminalStatusStringKind.ScrollingRegion,
			TimeSpan.FromSeconds( 30 )
		).AsTask();

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r0m\u001b\\" )
		);
		await WaitForWriteCountAsync( transport, 2 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r1;24r\u001b\\" )
		);
		TerminalStatusStringResponse response = await second;

		Assert.Equal( "1;24r", response.StatusString );
	}

	[Fact]
	public async Task TimeoutRetainsLateDcsOwnership() {
		ManualMonotonicClock clock = new();
		DcsTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<TerminalStatusStringResponse> first = session.QueryStatusStringAsync(
			TerminalStatusStringKind.SelectGraphicRendition,
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		clock.Advance( TimeSpan.FromSeconds( 5 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => first );

		Task<TerminalStatusStringResponse> second = session.QueryStatusStringAsync(
			TerminalStatusStringKind.ScrollingRegion,
			TimeSpan.FromSeconds( 30 )
		).AsTask();

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r0m\u001b\\" )
		);
		await WaitForWriteCountAsync( transport, 2 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r1;24r\u001b\\" )
		);
		TerminalStatusStringResponse response = await second;

		Assert.Equal( "1;24r", response.StatusString );
	}

	[Fact]
	public async Task DisposalTerminatesOutstandingDcsQuery() {
		DcsTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		try {
			Task<TerminalStatusStringResponse> query = session.QueryStatusStringAsync(
				TerminalStatusStringKind.SelectGraphicRendition,
				TimeSpan.FromSeconds( 30 )
			).AsTask();
			await WaitForWriteCountAsync( transport, 1 );

			await session.DisposeAsync();

			await Assert.ThrowsAsync<ObjectDisposedException>( () => query );
		} finally {
			await session.DisposeAsync();
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		DcsTransport transport,
		IMonotonicClock? monotonicClock = null,
		TimeSpan? escapeSequenceTimeout = null
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
				MonotonicClock = monotonicClock ?? SystemMonotonicClock.Instance,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = escapeSequenceTimeout ?? TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		DcsTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		using CancellationTokenSource timeout = new();
		timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
		try {
			await transport.WaitForWriteCountAsync(
				expected,
				timeout.Token
			);
		} catch ( OperationCanceledException ) when ( timeout.IsCancellationRequested ) {
			Assert.True(
				expected <= transport.WriteCount,
				$"Expected at least {expected} terminal writes, observed {transport.WriteCount}."
			);
		}
	}

	private sealed class DcsTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim writeSignal = new( 0 );

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

		internal async ValueTask WaitForWriteCountAsync(
			int expected,
			CancellationToken cancellationToken = default
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}
			cancellationToken.ThrowIfCancellationRequested();

			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}

				await this.writeSignal.WaitAsync(
					cancellationToken
				).ConfigureAwait( false );
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
						"The scripted input chunk exceeds the decoder read buffer."
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
			this.writeSignal.Release();
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

	private sealed class ManualMonotonicClock : IMonotonicClock {
		private readonly object sync = new();
		private readonly List<DelayWaiter> waiters = [];
		private long timestamp;

		public long GetTimestamp() {
			lock ( this.sync ) {
				return this.timestamp;
			}
		}

		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) {
			return TimeSpan.FromTicks(
				endingTimestamp - startingTimestamp
			);
		}

		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			if ( TimeSpan.Zero > delay ) {
				throw new ArgumentOutOfRangeException( nameof( delay ) );
			}
			cancellationToken.ThrowIfCancellationRequested();
			if ( TimeSpan.Zero == delay ) {
				return ValueTask.CompletedTask;
			}

			return new ValueTask(
				this.DelayCoreAsync(
					delay,
					cancellationToken
				)
			);
		}

		internal void Advance(
			TimeSpan elapsed
		) {
			if ( TimeSpan.Zero > elapsed ) {
				throw new ArgumentOutOfRangeException( nameof( elapsed ) );
			}

			List<DelayWaiter> due;
			lock ( this.sync ) {
				this.timestamp = checked(
					this.timestamp + elapsed.Ticks
				);
				due = this.waiters
					.Where( waiter => waiter.DueTimestamp <= this.timestamp )
					.ToList();
			}

			foreach ( DelayWaiter waiter in due ) {
				waiter.Completion.TrySetResult();
			}
		}

		private async Task DelayCoreAsync(
			TimeSpan delay,
			CancellationToken cancellationToken
		) {
			DelayWaiter waiter;
			lock ( this.sync ) {
				waiter = new DelayWaiter(
					checked( this.timestamp + delay.Ticks )
				);
				this.waiters.Add( waiter );
			}

			using CancellationTokenRegistration registration = cancellationToken.Register(
				static state => {
					var tuple = (Tuple<TaskCompletionSource, CancellationToken>)state!;
					tuple.Item1.TrySetCanceled( tuple.Item2 );
				},
				Tuple.Create(
					waiter.Completion,
					cancellationToken
				)
			);

			try {
				await waiter.Completion.Task.ConfigureAwait( false );
			} finally {
				lock ( this.sync ) {
					this.waiters.Remove( waiter );
				}
			}
		}

		private sealed class DelayWaiter {
			internal DelayWaiter(
				long dueTimestamp
			) {
				this.DueTimestamp = dueTimestamp;
			}

			internal long DueTimestamp {
				get;
			}

			internal TaskCompletionSource Completion {
				get;
			} = new(
				TaskCreationOptions.RunContinuationsAsynchronously
			);
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
