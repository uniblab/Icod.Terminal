namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T23 query transaction ownership without using a physical terminal.
/// </summary>
public sealed class TerminalQueryTransactionTests {
	private static readonly byte[] ResponseOne = Encoding.Latin1.GetBytes(
		"\u001b[1;2R"
	);
	private static readonly byte[] ResponseTwo = Encoding.Latin1.GetBytes(
		"\u001b[3;4R"
	);

	[Fact]
	public async Task OrdinaryReadDemandDoesNotPredrainAnotherTransportRead() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		transport.Publish( Encoding.UTF8.GetBytes( "x" ) );
		transport.Publish( Encoding.UTF8.GetBytes( "y" ) );

		TerminalInputEvent first = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);
		await YieldSeveralTimesAsync();

		Assert.Equal( new Rune( 'x' ), first.Character );
		Assert.Equal( 1, transport.ReadCount );

		TerminalInputEvent second = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);
		Assert.Equal( new Rune( 'y' ), second.Character );
		Assert.Equal( 2, transport.ReadCount );
	}

	[Fact]
	public async Task QueryProgressesWithoutApplicationRead() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish( ResponseOne );
		TerminalResponseFrame frame = await query;

		Assert.Equal( ResponseOne, frame.Bytes.ToArray() );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task ApplicationInputRemainsLiveWhileQueryIsPending() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Task<TerminalEvent> applicationRead = session.ReadEventAsync().AsTask();
		transport.Publish( Encoding.UTF8.GetBytes( "x" ) );
		TerminalEvent terminalEvent = await applicationRead;

		Assert.False( query.IsCompleted );
		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		Assert.Equal(
			new Rune( 'x' ),
			Assert.IsType<TerminalInputEvent>( terminalEvent.Input ).Character
		);

		transport.Publish( ResponseOne );
		await query;
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task CancellationBeforeEmissionWritesNothing() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.ExecuteQueryAsync(
				Encoding.ASCII.GetBytes( "Q1" ),
				new ExactResponseMatcher( ResponseOne ),
				TimeSpan.FromSeconds( 30 ),
				cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task QueuedCancellationDoesNotEmitSecondRequest() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<TerminalResponseFrame> first = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		using CancellationTokenSource cancellation = new();
		Task<TerminalResponseFrame> second = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q2" ),
			new ExactResponseMatcher( ResponseTwo ),
			TimeSpan.FromSeconds( 30 ),
			cancellation.Token
		).AsTask();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => second );
		transport.Publish( ResponseOne );
		await first;
		await YieldSeveralTimesAsync();

		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task QueuedTimeoutDoesNotEmitSecondRequest() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<TerminalResponseFrame> first = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Task<TerminalResponseFrame> second = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q2" ),
			new ExactResponseMatcher( ResponseTwo ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		clock.Advance( TimeSpan.FromSeconds( 5 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => second );

		transport.Publish( ResponseOne );
		await first;
		await YieldSeveralTimesAsync();

		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task TimedOutQueryRetainsWireSlotUntilLateResponseIsConsumed() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<TerminalResponseFrame> first = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 10 ),
			TimeSpan.FromSeconds( 10 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		clock.Advance( TimeSpan.FromSeconds( 10 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => first );

		Task<TerminalResponseFrame> second = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q2" ),
			new ExactResponseMatcher( ResponseTwo ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 1, transport.WriteCount );

		transport.Publish( ResponseOne );
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish( ResponseTwo );
		TerminalResponseFrame frame = await second;

		Assert.Equal( ResponseTwo, frame.Bytes.ToArray() );
	}

	[Fact]
	public async Task LateResponseOwnershipExpiryReleasesWireSlot() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<TerminalResponseFrame> first = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 10 ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		clock.Advance( TimeSpan.FromSeconds( 10 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => first );

		Task<TerminalResponseFrame> second = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q2" ),
			new ExactResponseMatcher( ResponseTwo ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 1, transport.WriteCount );

		clock.Advance( TimeSpan.FromSeconds( 5 ) );
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish( ResponseTwo );
		TerminalResponseFrame frame = await second;

		Assert.Equal( ResponseTwo, frame.Bytes.ToArray() );
	}

	[Fact]
	public async Task CancellationAfterEmissionConsumesLateResponse() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		using CancellationTokenSource cancellation = new();
		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 ),
			TimeSpan.FromSeconds( 10 ),
			cancellation.Token
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => query );
		transport.Publish( ResponseOne );
		transport.Publish( Encoding.UTF8.GetBytes( "z" ) );

		TerminalEvent terminalEvent = await session.ReadEventAsync();
		Assert.Equal(
			new Rune( 'z' ),
			Assert.IsType<TerminalInputEvent>( terminalEvent.Input ).Character
		);
	}

	[Fact]
	public async Task BufferedResponseShapedInputCannotSatisfyLaterQuery() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		TerminalDescription terminal = new TerminalDescriptionBuilder( "stale-cpr" )
			.SetString(
				StringCapability.KeyF3,
				"\u001b[1;2R"
			)
			.Build();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock,
			terminal
		);

		transport.Publish(
			Encoding.UTF8.GetBytes( "x" )
				.Concat( ResponseOne )
				.ToArray()
		);
		TerminalEvent firstInput = await session.ReadEventAsync();
		Assert.Equal(
			new Rune( 'x' ),
			Assert.IsType<TerminalInputEvent>( firstInput.Input ).Character
		);

		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		await YieldSeveralTimesAsync();
		Assert.False( query.IsCompleted );

		transport.Publish( ResponseOne );
		await query;

		TerminalInputEvent staleInput = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);
		Assert.Equal( TerminalKey.Function, staleInput.Key );
		Assert.Equal( 3, staleInput.FunctionKeyNumber );
		Assert.Equal( TerminalKeyModifiers.Shift, staleInput.Modifiers );
	}

	[Fact]
	public async Task QueryEmissionSharesSessionControlOutputGate() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);
		try {
			Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
				Encoding.ASCII.GetBytes( "Q1" ),
				new ExactResponseMatcher( ResponseOne ),
				TimeSpan.FromSeconds( 30 )
			).AsTask();
			await YieldSeveralTimesAsync();
			Assert.Equal( 0, transport.WriteCount );

			controlOutput.Dispose();
			controlOutput = EmptyDisposable.Instance;
			await WaitForWriteCountAsync( transport, 1 );
			transport.Publish( ResponseOne );
			await query;
		} finally {
			controlOutput.Dispose();
		}
	}

	[Fact]
	public async Task QueryReleasesControlOutputGateAfterRequestEmission() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Task<IDisposable> controlOutput = session.AcquireControlOutputAsync(
			CancellationToken.None
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.True( controlOutput.IsCompletedSuccessfully );

		using ( await controlOutput ) {
		}
		transport.Publish( ResponseOne );
		await query;
	}

	[Fact]
	public async Task QueryCannotCreateTransactionManagerWhileSessionIsSuspended() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		session.SuspendQueryTransactions();
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.ExecuteQueryAsync(
				Encoding.ASCII.GetBytes( "Q1" ),
				new ExactResponseMatcher( ResponseOne ),
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );

		session.ResumeQueryTransactions();
		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( ResponseOne );
		await query;
	}

	[Fact]
	public async Task SuspensionInvalidatesCallerButRetainsLateOwnership() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<TerminalResponseFrame> first = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 ),
			TimeSpan.FromSeconds( 10 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		session.SuspendQueryTransactions();
		await Assert.ThrowsAsync<InvalidOperationException>( () => first );
		session.ResumeQueryTransactions();

		Task<TerminalResponseFrame> second = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q2" ),
			new ExactResponseMatcher( ResponseTwo ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 1, transport.WriteCount );

		transport.Publish( ResponseOne );
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish( ResponseTwo );
		await second;
	}

	[Fact]
	public async Task WindowsConsoleDirectionAliasesRemainAConversationalPair() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		await using TerminalSession session = await OpenWindowsSessionAsync(
			transport,
			clock
		);

		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( ResponseOne );

		Assert.Equal( ResponseOne, ( await query ).Bytes.ToArray() );
	}

	[Fact]
	public async Task DisposalTerminatesOutstandingWireOwnership() {
		ManualMonotonicClock clock = new();
		QueryTransport transport = new();
		TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<TerminalResponseFrame> query = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 ),
			TimeSpan.FromSeconds( 10 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Task disposal = session.DisposeAsync().AsTask();
		await Assert.ThrowsAsync<ObjectDisposedException>( () => query );
		await disposal;
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		QueryTransport transport,
		IMonotonicClock monotonicClock,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( monotonicClock );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal ?? TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = monotonicClock,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static ValueTask<TerminalSession> OpenWindowsSessionAsync(
		QueryTransport transport,
		IMonotonicClock monotonicClock
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( monotonicClock );

		return TerminalSession.OpenAsync(
			new WindowsAliasTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = monotonicClock,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		QueryTransport transport,
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

	private static async Task YieldSeveralTimesAsync() {
		for ( int count = 0; count < 32; count++ ) {
			await Task.Yield();
		}
	}

	private sealed class ExactResponseMatcher : ITerminalResponseMatcher {
		private readonly byte[] expected;

		internal ExactResponseMatcher(
			byte[] expected
		) {
			ArgumentNullException.ThrowIfNull( expected );
			this.expected = expected.ToArray();
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Csi;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TerminalResponseFrameKind.Csi == frame.Kind
				&& frame.Bytes.Span.SequenceEqual( this.expected )
			;
		}
	}

	private sealed class QueryTransport : ITerminalInput, ITerminalOutput {
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
		private int readCount;

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

		internal int ReadCount {
			get {
				return Volatile.Read( ref this.readCount );
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
			Interlocked.Increment( ref this.readCount );
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
			return TerminalControlResult<TerminalSize>.Unavailable( "No scripted live size." );
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

	private sealed class WindowsAliasTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline =
			TerminalModeSnapshot.CreateWindowsConsole(
				TerminalConsoleDirection.Input,
				0
			);

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			bool input = TerminalEndpointKind.FileDescriptor == endpoint.Kind
				&& 0 == endpoint.FileDescriptor;
			TerminalControlCapabilities capabilities =
				TerminalControlCapabilities.Attachment
				| TerminalControlCapabilities.Pathname
				| TerminalControlCapabilities.ModeRead
				| TerminalControlCapabilities.ModeWrite;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					input ? "CONIN$" : "CONOUT$",
					TerminalPlatformKind.WindowsConsole,
					capabilities
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unavailable(
				"No scripted Windows live size."
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

	private sealed class EmptyDisposable : IDisposable {
		internal static EmptyDisposable Instance {
			get;
		} = new();

		private EmptyDisposable() {
		}

		public void Dispose() {
		}
	}
}
