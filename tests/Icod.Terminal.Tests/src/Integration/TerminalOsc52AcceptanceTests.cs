namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T58 OSC 52 integration, security, ownership, and compatibility acceptance.
/// </summary>
public sealed class TerminalOsc52AcceptanceTests {
	[Fact]
	public async Task CallerBytesCannotInjectOscFraming() {
		AcceptanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		byte[] payload = [
			0x00,
			0x07,
			0x1B,
			(byte)']',
			(byte)'5',
			(byte)'2',
			(byte)';',
			(byte)'?',
			0x1B,
			(byte)'\\',
			0xFF
		];

		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			payload
		);

		Assert.Equal( 1, transport.WriteCount );
		byte[] frame = transport.GetWrite( 0 );
		Assert.True(
			frame.AsSpan( 0, 7 ).SequenceEqual(
				Encoding.ASCII.GetBytes( "\u001b]52;c;" )
			)
		);
		Assert.Equal( 0x1B, frame[ ^2 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );

		ReadOnlySpan<byte> encoded = frame.AsSpan( 7, frame.Length - 9 );
		Assert.DoesNotContain( (byte)0x07, encoded.ToArray() );
		Assert.DoesNotContain( (byte)0x1B, encoded.ToArray() );
		Assert.Equal(
			payload,
			TerminalOsc52PayloadCodec.Decode( encoded )
		);
	}

	[Fact]
	public async Task ClipboardComposesWithPriorSemanticOutputFamiliesAndQueries() {
		AcceptanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await session.WriteTextAsync( "A" );
		await session.SetWindowTitleAsync( "title" );
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
			"C"
		);

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 8 );

		Assert.Equal( Encoding.UTF8.GetBytes( "A" ), transport.GetWrite( 0 ) );
		AssertStartsWith( transport.GetWrite( 1 ), "\u001b]2;" );
		AssertStartsWith( transport.GetWrite( 2 ), "\u001b]7;" );
		AssertStartsWith( transport.GetWrite( 3 ), "\u001b]8;" );
		Assert.Equal( Encoding.UTF8.GetBytes( "H" ), transport.GetWrite( 4 ) );
		AssertStartsWith( transport.GetWrite( 5 ), "\u001b]8;" );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]52;c;Qw==\u001b\\" ),
			transport.GetWrite( 6 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]52;c;?\u001b\\" ),
			transport.GetWrite( 7 )
		);
		Assert.Equal( 1, transport.FlushCount );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;c;Ug==\u001b\\" )
		);
		Assert.Equal( new byte[] { (byte)'R' }, await query );
	}

	[Fact]
	public async Task UnrelatedOscTrafficCannotSatisfyClipboardQuery() {
		AcceptanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Primary,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]51;p;bm9pc2U=\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;c;d3Jvbmc=\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;p;cmlnaHQ=\u001b\\" )
		);

		Assert.Equal( Encoding.ASCII.GetBytes( "right" ), await query );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task TimedOutClipboardQueryOwnsLateResponseBeforeNextQuery() {
		ManualMonotonicClock clock = new();
		AcceptanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<byte[]> first = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		clock.Advance( TimeSpan.FromSeconds( 5 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => first );

		Task<byte[]> second = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 1, transport.WriteCount );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;c;b2xk\u001b\\" )
		);
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;c;bmV3\u001b\\" )
		);

		Assert.Equal( Encoding.ASCII.GetBytes( "new" ), await second );
	}

	[Fact]
	public async Task PublicReadAcceptsMaximumPayloadAcrossFragmentation() {
		AcceptanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		byte[] payload = new byte[ TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes ];
		for ( int index = 0; index < payload.Length; index++ ) {
			payload[ index ] = (byte)( ( index * 37 ) & 0xFF );
		}
		string encoded = TerminalOsc52PayloadCodec.Encode( payload );
		byte[] response = Encoding.ASCII.GetBytes(
			$"\u001b]52;s;{encoded}\u001b\\"
		);

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Select,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		foreach ( byte[] chunk in Split( response, 257 ) ) {
			transport.Publish( chunk );
		}

		Assert.Equal( payload, await query );
	}

	[Fact]
	public void DeterministicPayloadPropertyMatrixRoundTrips() {
		Random random = new( 0x52 );
		int[] lengths = [
			0,
			1,
			2,
			3,
			4,
			7,
			31,
			255,
			1024,
			4095,
			4096,
			4097,
			16_383,
			32_768,
			65_535,
			65_536
		];

		foreach ( int length in lengths ) {
			byte[] payload = new byte[ length ];
			random.NextBytes( payload );
			string encoded = TerminalOsc52PayloadCodec.Encode( payload );
			Assert.Equal(
				TerminalOsc52PayloadCodec.GetEncodedLength( length ),
				encoded.Length
			);
			Assert.Equal(
				payload,
				TerminalOsc52PayloadCodec.Decode(
					Encoding.ASCII.GetBytes( encoded )
				)
			);
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		AcceptanceTransport transport,
		IMonotonicClock? monotonicClock = null
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
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		AcceptanceTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);
		await transport.WaitForWriteCountAsync(
			expected,
			timeout.Token
		);
	}

	private static void AssertStartsWith(
		byte[] actual,
		string expectedPrefix
	) {
		ArgumentNullException.ThrowIfNull( actual );
		ArgumentNullException.ThrowIfNull( expectedPrefix );
		Assert.True(
			actual.AsSpan().StartsWith(
				Encoding.ASCII.GetBytes( expectedPrefix )
			)
		);
	}

	private static IEnumerable<byte[]> Split(
		byte[] bytes,
		int size
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 0 >= size ) {
			throw new ArgumentOutOfRangeException( nameof( size ) );
		}

		for ( int offset = 0; offset < bytes.Length; offset += size ) {
			int length = Math.Min( size, bytes.Length - offset );
			yield return bytes.AsSpan( offset, length ).ToArray();
		}
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int count = 0; count < 32; count++ ) {
			await Task.Yield();
		}
	}

	private sealed class AcceptanceTransport : ITerminalInput, ITerminalOutput {
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
		private byte[]? pending;
		private int pendingOffset;
		private int activeReads;
		private int maximumConcurrentReads;
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
			CancellationToken cancellationToken
		) {
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
			RecordMaximum(
				ref this.maximumConcurrentReads,
				active
			);
			try {
				if ( this.pending is null ) {
					this.pending = await this.input.Reader.ReadAsync(
						cancellationToken
					).ConfigureAwait( false );
					this.pendingOffset = 0;
				}

				int count = Math.Min(
					buffer.Length,
					this.pending.Length - this.pendingOffset
				);
				this.pending.AsSpan(
					this.pendingOffset,
					count
				).CopyTo( buffer.Span );
				this.pendingOffset += count;
				if ( this.pendingOffset == this.pending.Length ) {
					this.pending = null;
					this.pendingOffset = 0;
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
			Interlocked.Increment( ref this.flushCount );
			return ValueTask.CompletedTask;
		}

		private static void RecordMaximum(
			ref int location,
			int candidate
		) {
			int current = Volatile.Read( ref location );
			while ( current < candidate ) {
				int observed = Interlocked.CompareExchange(
					ref location,
					candidate,
					current
				);
				if ( observed == current ) {
					return;
				}
				current = observed;
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
