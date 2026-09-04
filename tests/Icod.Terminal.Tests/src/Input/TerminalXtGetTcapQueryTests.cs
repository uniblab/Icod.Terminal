namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies the T26 public XTGETTCAP query family on the common transaction substrate.
/// </summary>
public sealed class TerminalXtGetTcapQueryTests {
	[Fact]
	public async Task PositiveKeyCapabilityPreservesExactByteValue() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"ku",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP+q6B75\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r6B75=1B5B41\u001b\\"
			)
		);
		TerminalCapabilityObservation observation = await query;
		IReadOnlyList<byte> value = Assert.IsAssignableFrom<IReadOnlyList<byte>>(
			observation.ValueBytes
		);

		Assert.Equal( "ku", observation.Name );
		Assert.True( observation.IsSupported );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[A" ),
			value.ToArray()
		);
	}

	[Fact]
	public async Task NegativeResponseIsTypedAsUnsupported() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP0+r\u001b\\" )
		);
		TerminalCapabilityObservation observation = await query;

		Assert.Equal( "TN", observation.Name );
		Assert.False( observation.IsSupported );
		Assert.Null( observation.ValueBytes );
	}

	[Fact]
	public async Task SupportedEmptyValueRemainsDistinctFromUnsupported() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1+r544E=\u001b\\" )
		);
		TerminalCapabilityObservation observation = await query;
		IReadOnlyList<byte> value = Assert.IsAssignableFrom<IReadOnlyList<byte>>(
			observation.ValueBytes
		);

		Assert.True( observation.IsSupported );
		Assert.Empty( value );
	}

	[Fact]
	public async Task EightBitDcsAndLowercaseHexAreAccepted() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			[
				0x90,
				(byte)'1',
				(byte)'+',
				(byte)'r',
				(byte)'4',
				(byte)'3',
				(byte)'6',
				(byte)'f',
				(byte)'=',
				(byte)'3',
				(byte)'2',
				(byte)'3',
				(byte)'5',
				(byte)'3',
				(byte)'6',
				0x9C
			]
		);
		TerminalCapabilityObservation observation = await query;
		IReadOnlyList<byte> value = Assert.IsAssignableFrom<IReadOnlyList<byte>>(
			observation.ValueBytes
		);

		Assert.True( observation.IsSupported );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "256" ),
			value.ToArray()
		);
	}

	[Fact]
	public async Task FragmentedResponseIsRoutedAcrossReads() {
		ManualMonotonicClock clock = new();
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock,
			TimeSpan.FromSeconds( 1 )
		);

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		byte[] response = Encoding.ASCII.GetBytes(
			"\u001bP1+r544E=787465726D2D323536636F6C6F72\u001b\\"
		);
		foreach ( byte value in response ) {
			transport.Publish( [ value ] );
		}

		TerminalCapabilityObservation observation = await query;
		IReadOnlyList<byte> valueBytes = Assert.IsAssignableFrom<IReadOnlyList<byte>>(
			observation.ValueBytes
		);

		Assert.True( observation.IsSupported );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "xterm-256color" ),
			valueBytes.ToArray()
		);
	}

	[Fact]
	public async Task OrdinaryInputRemainsLiveBeforeXtGetTcapResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		byte[] combined = Encoding.UTF8.GetBytes( "x" )
			.Concat(
				Encoding.ASCII.GetBytes(
					"\u001bP1+r436F=323536\u001b\\"
				)
			)
			.ToArray();
		transport.Publish( combined );

		TerminalEvent terminalEvent = await session.ReadEventAsync();
		TerminalInputEvent input = Assert.IsType<TerminalInputEvent>(
			terminalEvent.Input
		);
		TerminalCapabilityObservation observation = await query;

		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		Assert.Equal( new Rune( 'x' ), input.Character );
		Assert.True( observation.IsSupported );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public void CorrelatedMalformedHexAndDuplicatePairsFailDeterministically() {
		TerminalResponseFrame oddName = CreateFrame(
			"\u001bP1+r544=31\u001b\\"
		);
		TerminalResponseFrame invalidValue = CreateFrame(
			"\u001bP1+r544E=3G\u001b\\"
		);
		TerminalResponseFrame duplicate = CreateFrame(
			"\u001bP1+r544E=31;544E=32\u001b\\"
		);

		Assert.Throws<FormatException>(
			() => TerminalXtGetTcapProtocol.ParseResponse(
				"TN",
				oddName
			)
		);
		Assert.Throws<FormatException>(
			() => TerminalXtGetTcapProtocol.ParseResponse(
				"TN",
				invalidValue
			)
		);
		Assert.Throws<FormatException>(
			() => TerminalXtGetTcapProtocol.ParseResponse(
				"TN",
				duplicate
			)
		);
	}

	[Fact]
	public void CorrelatedMismatchedNameFailsDeterministically() {
		TerminalResponseFrame frame = CreateFrame(
			"\u001bP1+r436F=323536\u001b\\"
		);

		Assert.Throws<FormatException>(
			() => TerminalXtGetTcapProtocol.ParseResponse(
				"TN",
				frame
			)
		);
	}

	[Fact]
	public void OversizedDecodedValueIsRejected() {
		string encodedValue = new string(
			'0',
			checked(
				( TerminalXtGetTcapProtocol.MaximumCapabilityValueBytes + 1 ) * 2
			)
		);
		TerminalResponseFrame frame = CreateFrame(
			$"\u001bP1+r544E={encodedValue}\u001b\\"
		);

		Assert.Throws<FormatException>(
			() => TerminalXtGetTcapProtocol.ParseResponse(
				"TN",
				frame
			)
		);
	}

	[Fact]
	public async Task InvalidPublicNamesCannotEmitControlBytes() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.QueryLiveCapabilityAsync(
				"",
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentException>(
			() => session.QueryLiveCapabilityAsync(
				"bad\nname",
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentException>(
			() => session.QueryLiveCapabilityAsync(
				"café",
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.QueryLiveCapabilityAsync(
				new string(
					'a',
					TerminalXtGetTcapProtocol.MaximumCapabilityNameBytes + 1
				),
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task PunctuationNameIsHexEncodedRatherThanInjected() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"#2",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP+q2332\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP0+r\u001b\\" )
		);
		Assert.False( ( await query ).IsSupported );
	}

	[Fact]
	public void UnrelatedDcsFamiliesDoNotMatchXtGetTcapExpectation() {
		TerminalResponseFrame decrqss = CreateFrame(
			"\u001bP1$r0m\u001b\\"
		);
		TerminalResponseFrame xtGetXres = CreateFrame(
			"\u001bP1+R544E=787465726D\u001b\\"
		);

		Assert.False(
			TerminalXtGetTcapProtocol.ResponseMatcher.IsMatch( decrqss )
		);
		Assert.False(
			TerminalXtGetTcapProtocol.ResponseMatcher.IsMatch( xtGetXres )
		);
	}

	[Fact]
	public async Task CancellationRetainsLateXtGetTcapOwnership() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();

		Task<TerminalCapabilityObservation> first = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 ),
			cancellation.Token
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => first );

		Task<TerminalCapabilityObservation> second = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544E=787465726D\u001b\\"
			)
		);
		await WaitForWriteCountAsync( transport, 2 );

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r436F=323536\u001b\\"
			)
		);
		TerminalCapabilityObservation observation = await second;

		Assert.Equal( "Co", observation.Name );
		Assert.True( observation.IsSupported );
	}

	[Fact]
	public async Task TimeoutRetainsLateXtGetTcapOwnership() {
		ManualMonotonicClock clock = new();
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<TerminalCapabilityObservation> first = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		clock.Advance( TimeSpan.FromSeconds( 5 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => first );

		Task<TerminalCapabilityObservation> second = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544E=787465726D\u001b\\"
			)
		);
		await WaitForWriteCountAsync( transport, 2 );

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r436F=323536\u001b\\"
			)
		);
		Assert.True( ( await second ).IsSupported );
	}

	[Fact]
	public async Task DisposalTerminatesOutstandingXtGetTcapQuery() {
		XtGetTcapTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		try {
			Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
				"TN",
				TimeSpan.FromSeconds( 30 )
			).AsTask();
			await WaitForWriteCountAsync( transport, 1 );

			await session.DisposeAsync();

			await Assert.ThrowsAsync<ObjectDisposedException>( () => query );
		} finally {
			await session.DisposeAsync();
		}
	}

	private static TerminalResponseFrame CreateFrame(
		string wire
	) {
		ArgumentNullException.ThrowIfNull( wire );
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Dcs,
			Encoding.ASCII.GetBytes( wire )
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		XtGetTcapTransport transport,
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
		XtGetTcapTransport transport,
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

	private sealed class XtGetTcapTransport : ITerminalInput, ITerminalOutput {
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
