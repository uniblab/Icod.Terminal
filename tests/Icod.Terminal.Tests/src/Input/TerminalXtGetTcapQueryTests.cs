using System.Text;
using System.Threading.Channels;
using Icod.Timing;
using Icod.TermInfo;
using Xunit;

namespace Icod.Terminal.Tests.Input;

/// <summary>
/// Verifies live XTGETTCAP query emission, parsing, routing, bounds, and ownership.
/// </summary>
public sealed class TerminalXtGetTcapQueryTests {
	[Fact]
	public async Task QueryLiveCapabilityEmitsExactRequestAndParsesPositiveResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP+q544E\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544E=787465726D2D323536636F6C6F72\u001b\\"
			)
		);
		TerminalCapabilityObservation observation = await query;

		Assert.Equal( "TN", observation.Name );
		Assert.True( observation.IsSupported );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "xterm-256color" ),
			observation.ValueBytes.ToArray()
		);
	}

	[Fact]
	public async Task QueryLiveCapabilityParsesNegativeResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"RGB",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP+q524742\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP0+r\u001b\\" )
		);
		TerminalCapabilityObservation observation = await query;

		Assert.Equal( "RGB", observation.Name );
		Assert.False( observation.IsSupported );
		Assert.True( observation.ValueBytes.IsEmpty );
	}

	[Fact]
	public async Task QueryLiveCapabilityAcceptsEightBitDcsResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			new byte[] {
				0x90,
				(byte)'1',
				(byte)'+',
				(byte)'r',
				(byte)'4',
				(byte)'3',
				(byte)'6',
				(byte)'F',
				(byte)'=',
				(byte)'3',
				(byte)'2',
				(byte)'3',
				(byte)'5',
				(byte)'3',
				(byte)'3',
				0x9C
			}
		);
		TerminalCapabilityObservation observation = await query;

		Assert.Equal( "Co", observation.Name );
		Assert.True( observation.IsSupported );
		Assert.Equal( Encoding.ASCII.GetBytes( "253" ), observation.ValueBytes.ToArray() );
	}

	[Fact]
	public async Task QueryLiveCapabilitySerializesOverlappingQueries() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> first = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		Task<TerminalCapabilityObservation> second = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();

		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal( 1, transport.WriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544E=787465726D\u001b\\"
			)
		);
		Assert.True( ( await first ).IsSupported );

		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r436F=323536\u001b\\"
			)
		);
		Assert.True( ( await second ).IsSupported );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task QueryLiveCapabilityAllowsBufferedApplicationInputBeforeResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		transport.Publish( Encoding.ASCII.GetBytes( "x" ) );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544E=787465726D\u001b\\"
			)
		);
		TerminalCapabilityObservation observation = await query;
		Assert.True( observation.IsSupported );

		TerminalEvent terminalEvent = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 1 )
		);
		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		Assert.Equal( 'x', terminalEvent.Input!.Character );
	}

	[Fact]
	public async Task QueryLiveCapabilityDoesNotStealUnrelatedDcsInput() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1$r0m\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r544E=787465726D\u001b\\"
			)
		);
		Assert.True( ( await query ).IsSupported );

		TerminalEvent first = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 1 )
		);
		Assert.Equal( TerminalEventKind.Input, first.Kind );
	}

	[Fact]
	public async Task QueryLiveCapabilityRejectsMismatchedReturnedName() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r436F=323536\u001b\\"
			)
		);
		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task QueryLiveCapabilityRejectsMalformedHex() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001bP1+r544E=GG\u001b\\" )
		);
		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task QueryLiveCapabilityRejectsOversizedCorrelatedResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> query = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		string oversized = "\u001bP1+r544E="
			+ new string( '4', TerminalResponseFramer.DefaultMaximumFrameBytes )
			+ "\u001b\\";
		transport.Publish( Encoding.ASCII.GetBytes( oversized ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task QueryLiveCapabilityResynchronizesAfterOversizedCorrelatedResponse() {
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalCapabilityObservation> first = session.QueryLiveCapabilityAsync(
			"TN",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		string oversized = "\u001bP1+r544E="
			+ new string( '4', TerminalResponseFramer.DefaultMaximumFrameBytes )
			+ "\u001b\\";
		transport.Publish( Encoding.ASCII.GetBytes( oversized ) );
		await Assert.ThrowsAsync<FormatException>( () => first );

		Task<TerminalCapabilityObservation> second = session.QueryLiveCapabilityAsync(
			"Co",
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001bP1+r436F=323536\u001b\\"
			)
		);
		Assert.True( ( await second ).IsSupported );
	}

	[Fact]
	public async Task QueryLiveCapabilitySupportsPrintablePunctuationName() {
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
		ManualMonotonicClock clock = new();
		XtGetTcapTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
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

		Assert.Equal( 1, transport.WriteCount );
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
				throw new InvalidOperationException( "Unable to publish terminal input." );
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected,
			CancellationToken cancellationToken
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			while ( this.WriteCount < expected ) {
				await this.writeSignal.WaitAsync(
					cancellationToken
				).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				return 0;
			}

			int active = Interlocked.Increment( ref this.activeReads );
			UpdateMaximum(
				ref this.maximumConcurrentReads,
				active
			);
			try {
				byte[] next = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				int count = Math.Min( buffer.Length, next.Length );
				next.AsSpan( 0, count ).CopyTo( buffer.Span );
				if ( count < next.Length ) {
					byte[] remainder = next[ count.. ];
					if ( !this.input.Writer.TryWrite( remainder ) ) {
						throw new InvalidOperationException(
							"Unable to republish terminal input remainder."
						);
					}
				}
				return count;
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

		private static void UpdateMaximum(
			ref int target,
			int candidate
		) {
			while ( true ) {
				int observed = Volatile.Read( ref target );
				if ( candidate <= observed ) {
					return;
				}

				if ( observed == Interlocked.CompareExchange(
					ref target,
					candidate,
					observed
				) ) {
					return;
				}
			}
		}
	}
}
