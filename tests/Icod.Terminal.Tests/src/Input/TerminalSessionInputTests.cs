namespace Icod.Terminal.Tests.Input;

using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T08 session event-loop contract without using process terminal state.
/// </summary>
public sealed class TerminalSessionInputTests {
	[Fact]
	public async Task ReadEventDecodesTerminfoKeyFromSessionTerminal() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "session-key" )
			.SetString(
				StringCapability.KeyCursorRight,
				"\u001b[C"
			)
			.Build();
		ScriptedTerminalInput input = new(
			[
				[ 0x1B, 0x5B, 0x43 ]
			]
		);
		await using TerminalSession session = await OpenSessionAsync(
			input,
			terminal
		);

		TerminalEvent terminalEvent = await session.ReadEventAsync();

		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		TerminalInputEvent inputEvent = Assert.IsType<TerminalInputEvent>( terminalEvent.Input );
		Assert.Equal( TerminalKey.Right, inputEvent.Key );
	}

	[Fact]
	public async Task SessionHonorsConfiguredPasteChunkSize() {
		TerminalDescription terminal = new TerminalDescriptionBuilder(
			"session-paste"
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
		ScriptedTerminalInput input = new(
			[
				System.Text.Encoding.Latin1.GetBytes(
					"\u001b[200~abcd\u001b[201~"
				)
			]
		);
		await using TerminalSession session = await OpenSessionAsync(
			input,
			terminal,
			decoderOptions: new TerminalInputDecoderOptions {
				PasteChunkBytes = 2
			}
		);

		TerminalInputEvent begin = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);
		TerminalInputEvent firstData = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);
		TerminalInputEvent secondData = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);
		TerminalInputEvent end = Assert.IsType<TerminalInputEvent>(
			( await session.ReadEventAsync() ).Input
		);

		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>( begin.Paste ).Phase
		);
		Assert.Equal(
			"ab",
			Assert.IsType<TerminalPasteEvent>( firstData.Paste ).Text
		);
		Assert.Equal(
			"cd",
			Assert.IsType<TerminalPasteEvent>( secondData.Paste ).Text
		);
		Assert.Equal(
			TerminalPastePhase.End,
			Assert.IsType<TerminalPasteEvent>( end.Paste ).Phase
		);
	}

	[Fact]
	public async Task TimeoutDoesNotCancelPendingTerminalRead() {
		DeferredTerminalInput input = new();
		await using TerminalSession session = await OpenSessionAsync( input );

		TerminalEvent timedOut = await session.ReadEventAsync(
			TimeSpan.FromMilliseconds( 10 )
		);
		input.Release( [ (byte)'x' ] );
		TerminalEvent inputEvent = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 1 )
		);

		Assert.Equal( TerminalEventKind.Timeout, timedOut.Kind );
		Assert.Equal( TerminalEventKind.Input, inputEvent.Kind );
		TerminalInputEvent decoded = Assert.IsType<TerminalInputEvent>( inputEvent.Input );
		Assert.Equal( new System.Text.Rune( 'x' ), decoded.Character );
		Assert.Equal( 1, input.ReadCount );
	}

	[Fact]
	public async Task CallerCancellationIsEventAndPreservesPendingRead() {
		DeferredTerminalInput input = new();
		await using TerminalSession session = await OpenSessionAsync( input );
		using CancellationTokenSource cancellation = new();
		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 10 ) );

		TerminalEvent cancelled = await session.ReadEventAsync( cancellation.Token );
		input.Release( [ (byte)'y' ] );
		TerminalEvent inputEvent = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 1 )
		);

		Assert.Equal( TerminalEventKind.Cancelled, cancelled.Kind );
		Assert.Equal( TerminalEventKind.Input, inputEvent.Kind );
		TerminalInputEvent decoded = Assert.IsType<TerminalInputEvent>( inputEvent.Input );
		Assert.Equal( new System.Text.Rune( 'y' ), decoded.Character );
		Assert.Equal( 1, input.ReadCount );
	}

	[Fact]
	public async Task RelativeTimeoutIncludesTimeWaitingForReadGate() {
		DeferredTerminalInput input = new();
		await using TerminalSession session = await OpenSessionAsync( input );

		Task<TerminalEvent> firstRead = session.ReadEventAsync().AsTask();
		while ( 0 == input.ReadCount ) {
			await Task.Yield();
		}

		TerminalEvent secondRead = await session.ReadEventAsync(
			TimeSpan.FromMilliseconds( 10 )
		);

		Assert.Equal( TerminalEventKind.Timeout, secondRead.Kind );
		Assert.Equal( 1, input.ReadCount );

		input.Release( [ (byte)'z' ] );
		TerminalEvent firstEvent = await firstRead;
		Assert.Equal( TerminalEventKind.Input, firstEvent.Kind );
	}

	[Fact]
	public async Task ExpiredDeadlineReturnsTimeoutWithoutStartingSecondRead() {
		DeferredTerminalInput input = new();
		await using TerminalSession session = await OpenSessionAsync( input );

		TerminalEvent first = await session.ReadEventAsync( TimeSpan.Zero );
		TerminalEvent second = await session.ReadEventAsync(
			DateTimeOffset.UtcNow - TimeSpan.FromSeconds( 1 )
		);

		Assert.Equal( TerminalEventKind.Timeout, first.Kind );
		Assert.Equal( TerminalEventKind.Timeout, second.Kind );
		Assert.Equal( 1, input.ReadCount );
	}

	[Fact]
	public async Task ResizeLifecycleEventWakesUnifiedReader() {
		TestLifecycleSource lifecycleSource = new();
		NeverTerminalInput input = new();
		await using TerminalSession session = await OpenSessionAsync(
			input,
			lifecycleSource: lifecycleSource
		);

		Task<TerminalEvent> readTask = session.ReadEventAsync(
			TimeSpan.FromSeconds( 1 )
		).AsTask();
		lifecycleSource.Publish( TerminalLifecycleSignalKind.Resize );
		TerminalEvent terminalEvent = await readTask;

		Assert.Equal( TerminalEventKind.Lifecycle, terminalEvent.Kind );
		TerminalLifecycleEvent lifecycle = Assert.IsType<TerminalLifecycleEvent>(
			terminalEvent.Lifecycle
		);
		Assert.Equal( TerminalLifecycleEventKind.Resize, lifecycle.Kind );
	}

	[Fact]
	public async Task EndOfInputIsRepresentedAsInputEvent() {
		await using TerminalSession session = await OpenSessionAsync(
			new ScriptedTerminalInput( [] )
		);

		TerminalEvent terminalEvent = await session.ReadEventAsync();

		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		TerminalInputEvent inputEvent = Assert.IsType<TerminalInputEvent>( terminalEvent.Input );
		Assert.Equal( TerminalInputEventKind.EndOfInput, inputEvent.Kind );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalInput input,
		TerminalDescription? terminal = null,
		ITerminalLifecycleSource? lifecycleSource = null,
		TerminalInputDecoderOptions? decoderOptions = null
	) {
		ArgumentNullException.ThrowIfNull( input );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			input,
			new TestTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = terminal ?? TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycleSource,
				InputDecoderOptions = decoderOptions ?? new TerminalInputDecoderOptions()
			}
		);
	}

	private sealed class ScriptedTerminalInput : ITerminalInput {
		private readonly Queue<byte[]> chunks;

		internal ScriptedTerminalInput(
			IEnumerable<byte[]> chunks
		) {
			ArgumentNullException.ThrowIfNull( chunks );
			this.chunks = new Queue<byte[]>(
				chunks.Select( static value => value.ToArray() )
			);
		}

		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( 0 == this.chunks.Count ) {
				return ValueTask.FromResult( 0 );
			}

			byte[] chunk = this.chunks.Dequeue();
			chunk.AsSpan().CopyTo( buffer.Span );
			return ValueTask.FromResult( chunk.Length );
		}
	}

	private sealed class DeferredTerminalInput : ITerminalInput {
		private readonly TaskCompletionSource<byte[]> completion = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private int readCount;

		internal int ReadCount {
			get {
				return Volatile.Read( ref this.readCount );
			}
		}

		internal void Release(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			this.completion.TrySetResult( bytes.ToArray() );
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			Interlocked.Increment( ref this.readCount );
			byte[] bytes = await this.completion.Task.WaitAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( bytes.Length > buffer.Length ) {
				throw new InvalidOperationException( "The deferred input does not fit the read buffer." );
			}

			bytes.AsSpan().CopyTo( buffer.Span );
			return bytes.Length;
		}
	}

	private sealed class NeverTerminalInput : ITerminalInput {
		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}
	}

	private sealed class TestTerminalOutput : ITerminalOutput {
		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
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

	private sealed class TestLifecycleSource : ITerminalLifecycleSource {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) );
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
		}
	}
}
