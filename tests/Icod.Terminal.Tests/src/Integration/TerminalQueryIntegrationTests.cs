namespace Icod.Terminal.Tests.Integration;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T27 integration of active queries with presentation, rich input,
/// lifecycle processing, and the unified event loop.
/// </summary>
public sealed class TerminalQueryIntegrationTests {
	private static readonly byte[] CursorPositionRequest =
		Encoding.ASCII.GetBytes( "\u001b[6n" );
	private static readonly byte[] DeviceStatusRequest =
		Encoding.ASCII.GetBytes( "\u001b[5n" );
	private static readonly byte[] SgrStatusStringRequest =
		Encoding.ASCII.GetBytes( "\u001bP$qm\u001b\\" );
	private static readonly byte[] TerminalNameRequest =
		Encoding.ASCII.GetBytes( "\u001bP+q544E\u001b\\" );

	[Fact]
	public async Task OpeningSessionDoesNotInterrogateTerminal() {
		IntegrationTransport transport = new();
		RecordingTerminalControlProvider provider = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			provider
		);

		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.ReadCount );
	}

	[Fact]
	public async Task QueriesRemainLiveWithPresentationRichInputAndApplicationInput() {
		IntegrationTransport transport = new();
		RecordingTerminalControlProvider provider = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			provider
		);
		(
			TerminalPresentationLease presentation,
			TerminalInputProtocolLease protocols
		) = await AcquireLeasesAsync( session );
		await using ( presentation ) {
			await using ( protocols ) {
				Task<TerminalCursorPosition> cursorQuery =
					session.QueryCursorPositionAsync(
						TimeSpan.FromSeconds( 30 )
					).AsTask();
				await WaitForWriteAsync(
					transport,
					CursorPositionRequest
				);

				Task<TerminalEvent> eventTask = session.ReadEventAsync().AsTask();
				transport.Publish(
					Encoding.UTF8.GetBytes( "x" )
				);
				TerminalEvent terminalEvent = await eventTask;
				TerminalInputEvent input = Assert.IsType<TerminalInputEvent>(
					terminalEvent.Input
				);

				Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
				Assert.Equal( TerminalInputEventKind.Text, input.Kind );
				Assert.Equal( new Rune( 'x' ), input.Character );
				Assert.False( cursorQuery.IsCompleted );

				transport.Publish(
					Encoding.ASCII.GetBytes( "\u001b[7;11R" )
				);
				TerminalCursorPosition cursor = await cursorQuery;

				Assert.Equal( 7, cursor.Row );
				Assert.Equal( 11, cursor.Column );

				Task<TerminalStatusStringResponse> statusQuery =
					session.QueryStatusStringAsync(
						TerminalStatusStringKind.SelectGraphicRendition,
						TimeSpan.FromSeconds( 30 )
					).AsTask();
				await WaitForWriteAsync(
					transport,
					SgrStatusStringRequest
				);
				transport.Publish(
					Encoding.ASCII.GetBytes(
						"\u001bP1$r0m\u001b\\"
					)
				);
				TerminalStatusStringResponse status = await statusQuery;

				Assert.True( status.IsSupported );
				Assert.Equal( "0m", status.StatusString );

				Task<TerminalCapabilityObservation> capabilityQuery =
					session.QueryLiveCapabilityAsync(
						"TN",
						TimeSpan.FromSeconds( 30 )
					).AsTask();
				await WaitForWriteAsync(
					transport,
					TerminalNameRequest
				);
				transport.Publish(
					Encoding.ASCII.GetBytes(
						"\u001bP1+r544E=787465726D\u001b\\"
					)
				);
				TerminalCapabilityObservation capability = await capabilityQuery;
				IReadOnlyList<byte> value = Assert.IsAssignableFrom<IReadOnlyList<byte>>(
					capability.ValueBytes
				);

				Assert.True( capability.IsSupported );
				Assert.Equal(
					Encoding.ASCII.GetBytes( "xterm" ),
					value.ToArray()
				);
				Assert.Equal( 1, transport.MaximumConcurrentReads );
			}
		}
	}

	[Fact]
	public async Task ResizeAndSuspendResumePreserveIntegratedQueryOwnership() {
		IntegrationTransport transport = new();
		RecordingTerminalControlProvider provider = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			provider,
			lifecycle
		);
		(
			TerminalPresentationLease presentation,
			TerminalInputProtocolLease protocols
		) = await AcquireLeasesAsync( session );
		await using ( presentation ) {
			await using ( protocols ) {
				using CancellationTokenSource waitTimeout = new(
					TimeSpan.FromSeconds( 5 )
				);

				Task<TerminalDeviceStatus> statusQuery =
					session.QueryDeviceStatusAsync(
						TimeSpan.FromSeconds( 30 )
					).AsTask();
				await WaitForWriteAsync(
					transport,
					DeviceStatusRequest
				);

				provider.Size = new TerminalSize( 132, 43 );
				lifecycle.Publish( TerminalLifecycleSignalKind.Resize );
				TerminalEvent resizeEvent = await session.ReadEventAsync(
					waitTimeout.Token
				);
				TerminalLifecycleEvent resize = Assert.IsType<TerminalLifecycleEvent>(
					resizeEvent.Lifecycle
				);

				Assert.Equal( TerminalEventKind.Lifecycle, resizeEvent.Kind );
				Assert.Equal( TerminalLifecycleEventKind.Resize, resize.Kind );
				Assert.Equal( new TerminalSize( 132, 43 ), resize.Size );
				Assert.False( statusQuery.IsCompleted );

				transport.Publish(
					Encoding.ASCII.GetBytes( "\u001b[0n" )
				);
				Assert.Equal(
					TerminalDeviceStatus.Ready,
					await statusQuery
				);

				Task<TerminalCapabilityObservation> interruptedQuery =
					session.QueryLiveCapabilityAsync(
						"TN",
						TimeSpan.FromSeconds( 30 )
					).AsTask();
				await WaitForWriteAsync(
					transport,
					TerminalNameRequest
				);

				lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
				TerminalEvent suspendingEvent = await session.ReadEventAsync(
					waitTimeout.Token
				);
				TerminalEvent resumedEvent = await session.ReadEventAsync(
					waitTimeout.Token
				);
				TerminalLifecycleEvent suspending =
					Assert.IsType<TerminalLifecycleEvent>(
						suspendingEvent.Lifecycle
					);
				TerminalLifecycleEvent resumed =
					Assert.IsType<TerminalLifecycleEvent>(
						resumedEvent.Lifecycle
					);

				Assert.Equal(
					TerminalLifecycleEventKind.Suspending,
					suspending.Kind
				);
				Assert.Equal(
					TerminalLifecycleEventKind.Resumed,
					resumed.Kind
				);
				Assert.True( session.IsStateValid );
				await Assert.ThrowsAsync<InvalidOperationException>(
					() => interruptedQuery
				);

				Task<TerminalCursorPosition> nextQuery =
					session.QueryCursorPositionAsync(
						TimeSpan.FromSeconds( 30 )
					).AsTask();

				transport.Publish(
					Encoding.ASCII.GetBytes(
						"\u001bP1+r544E=787465726D\u001b\\"
					)
				);
				await WaitForWriteAsync(
					transport,
					CursorPositionRequest
				);

				transport.Publish(
					Encoding.ASCII.GetBytes( "\u001b[9;13R" )
				);
				TerminalCursorPosition cursor = await nextQuery;

				Assert.Equal( 9, cursor.Row );
				Assert.Equal( 13, cursor.Column );
				Assert.Equal( 1, lifecycle.SuspendCount );
				Assert.Equal( 1, transport.MaximumConcurrentReads );
			}
		}
	}

	[Fact]
	public async Task CancellationAndTimeoutRetainOwnershipWithActiveLeases() {
		ManualMonotonicClock clock = new();
		IntegrationTransport transport = new();
		RecordingTerminalControlProvider provider = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			provider,
			monotonicClock: clock
		);
		(
			TerminalPresentationLease presentation,
			TerminalInputProtocolLease protocols
		) = await AcquireLeasesAsync( session );
		await using ( presentation ) {
			await using ( protocols ) {
				using CancellationTokenSource cancellation = new();

				Task<TerminalCursorPosition> cancelled =
					session.QueryCursorPositionAsync(
						TimeSpan.FromSeconds( 30 ),
						cancellation.Token
					).AsTask();
				await WaitForWriteAsync(
					transport,
					CursorPositionRequest
				);
				cancellation.Cancel();
				await Assert.ThrowsAnyAsync<OperationCanceledException>(
					() => cancelled
				);

				Task<TerminalStatusStringResponse> afterCancellation =
					session.QueryStatusStringAsync(
						TerminalStatusStringKind.SelectGraphicRendition,
						TimeSpan.FromSeconds( 30 )
					).AsTask();

				transport.Publish(
					Encoding.ASCII.GetBytes( "\u001b[3;4R" )
				);
				await WaitForWriteAsync(
					transport,
					SgrStatusStringRequest
				);
				transport.Publish(
					Encoding.ASCII.GetBytes(
						"\u001bP1$r0m\u001b\\"
					)
				);
				Assert.True( ( await afterCancellation ).IsSupported );

				Task<TerminalCapabilityObservation> timedOut =
					session.QueryLiveCapabilityAsync(
						"TN",
						TimeSpan.FromSeconds( 5 )
					).AsTask();
				await WaitForWriteAsync(
					transport,
					TerminalNameRequest
				);

				clock.Advance( TimeSpan.FromSeconds( 5 ) );
				await Assert.ThrowsAsync<TimeoutException>(
					() => timedOut
				);

				Task<TerminalDeviceStatus> afterTimeout =
					session.QueryDeviceStatusAsync(
						TimeSpan.FromSeconds( 30 )
					).AsTask();

				transport.Publish(
					Encoding.ASCII.GetBytes(
						"\u001bP1+r544E=787465726D\u001b\\"
					)
				);
				await WaitForWriteAsync(
					transport,
					DeviceStatusRequest
				);
				transport.Publish(
					Encoding.ASCII.GetBytes( "\u001b[0n" )
				);

				Assert.Equal(
					TerminalDeviceStatus.Ready,
					await afterTimeout
				);
				Assert.Equal( 1, transport.MaximumConcurrentReads );
			}
		}
	}

	[Fact]
	public async Task DisposalTerminatesOutstandingQueryWithActiveLeases() {
		IntegrationTransport transport = new();
		RecordingTerminalControlProvider provider = new();
		TestTerminalLifecycleSource lifecycle = new();
		TerminalSession session = await OpenSessionAsync(
			transport,
			provider,
			lifecycle
		);
		(
			TerminalPresentationLease presentation,
			TerminalInputProtocolLease protocols
		) = await AcquireLeasesAsync( session );

		try {
			Task<TerminalCapabilityObservation> query =
				session.QueryLiveCapabilityAsync(
					"TN",
					TimeSpan.FromSeconds( 30 )
				).AsTask();
			await WaitForWriteAsync(
				transport,
				TerminalNameRequest
			);

			await session.DisposeAsync();

			await Assert.ThrowsAsync<ObjectDisposedException>(
				() => query
			);
			Assert.Equal( 2, provider.SetModeCallCount );
			Assert.Equal( 1, transport.MaximumConcurrentReads );
		} finally {
			await protocols.DisposeAsync();
			await presentation.DisposeAsync();
			await session.DisposeAsync();
		}
	}

	private static async ValueTask<(
		TerminalPresentationLease Presentation,
		TerminalInputProtocolLease Protocols
	)> AcquireLeasesAsync(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );

		TerminalControlResult<TerminalPresentationLease> presentationResult =
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true,
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			);
		Assert.True(
			presentationResult.IsAvailable,
			presentationResult.Message
		);
		TerminalPresentationLease presentation =
			presentationResult.GetRequiredValue();

		try {
			TerminalControlResult<TerminalInputProtocolLease> protocolResult =
				await session.AcquireInputProtocolsAsync(
					new TerminalInputProtocolOptions {
						BracketedPaste = true,
						FocusReporting = true
					}
				);
			Assert.True(
				protocolResult.IsAvailable,
				protocolResult.Message
			);
			return (
				presentation,
				protocolResult.GetRequiredValue()
			);
		} catch {
			await presentation.DisposeAsync();
			throw;
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		IntegrationTransport transport,
		RecordingTerminalControlProvider provider,
		TestTerminalLifecycleSource? lifecycle = null,
		IMonotonicClock? monotonicClock = null
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( provider );

		TerminalDescription terminal = TerminalDatabase.BuiltIn.Load( "xterm" );
		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				InputMode = TerminalInputMode.CBreak,
				EchoInput = false,
				ConfigureOutput = false,
				TerminalOverride = terminal,
				LifecycleSource = lifecycle,
				MonotonicClock = monotonicClock ?? SystemMonotonicClock.Instance,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteAsync(
		IntegrationTransport transport,
		byte[] expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( expected );

		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);
		try {
			await transport.WaitForWriteAsync(
				expected,
				timeout.Token
			);
		} catch ( OperationCanceledException ) when ( timeout.IsCancellationRequested ) {
			Assert.True(
				transport.ContainsWrite( expected ),
				$"Expected terminal write {Convert.ToHexString( expected )} was not observed."
			);
		}
	}

	private sealed class IntegrationTransport : ITerminalInput, ITerminalOutput {
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
		private int readCount;

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal int ReadCount {
			get {
				return Volatile.Read( ref this.readCount );
			}
		}

		internal int MaximumConcurrentReads {
			get {
				return Volatile.Read( ref this.maximumConcurrentReads );
			}
		}

		internal bool ContainsWrite(
			byte[] expected
		) {
			ArgumentNullException.ThrowIfNull( expected );

			lock ( this.sync ) {
				return this.writes.Any(
					write => write.AsSpan().SequenceEqual( expected )
				);
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The integration terminal input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForWriteAsync(
			byte[] expected,
			CancellationToken cancellationToken = default
		) {
			ArgumentNullException.ThrowIfNull( expected );
			cancellationToken.ThrowIfCancellationRequested();

			while ( true ) {
				if ( this.ContainsWrite( expected ) ) {
					return;
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
			Interlocked.Increment( ref this.readCount );
			int active = Interlocked.Increment( ref this.activeReads );
			this.RecordMaximumConcurrentReads( active );
			try {
				byte[] bytes = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( bytes.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The integration input chunk exceeds the decoder read buffer."
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

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		internal RecordingTerminalControlProvider() {
			this.Baseline = TerminalModeSnapshot.CreatePosix(
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
		}

		internal TerminalModeSnapshot Baseline {
			get;
		}

		internal TerminalSize Size {
			get;
			set;
		} = new TerminalSize( 80, 24 );

		internal int SetModeCallCount {
			get;
			private set;
		}

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
			return TerminalControlResult<TerminalSize>.Available( this.Size );
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.Baseline
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

			++this.SetModeCallCount;
			return TerminalControlMutationResult.Success();
		}
	}

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal int SuspendCount {
			get;
			private set;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			if ( !this.signals.Writer.TryWrite(
				new TerminalLifecycleSignal( kind )
			) ) {
				throw new InvalidOperationException(
					"The integration lifecycle channel is closed."
				);
			}
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			++this.SuspendCount;
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}

			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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
}
