namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies session ownership, rollback, cancellation, and idempotent restoration
/// without touching the process terminal.
/// </summary>
public sealed class TerminalSessionTests {
	/// <summary>
	/// Verifies that a successful POSIX session applies semantic state, exposes
	/// borrowed services, flushes, and restores the exact baseline once.
	/// </summary>
	[Fact]
	public async Task OpensAndRestoresPosixSessionExactlyOnce() {
		TerminalModeSnapshot baseline = CreatePosixBaseline();
		RecordingTerminalControlProvider provider = new( baseline );
		TestTerminalInput input = new();
		TestTerminalOutput output = new();
		TerminalEndpoint inputEndpoint = TerminalEndpoint.ForFileDescriptor( 10 );
		TerminalEndpoint outputEndpoint = TerminalEndpoint.ForFileDescriptor( 11 );

		TerminalSession session = await TerminalSession.OpenAsync(
			provider,
			inputEndpoint,
			outputEndpoint,
			input,
			output,
			new TerminalSessionOptions {
				InputMode = TerminalInputMode.CBreak,
				EchoInput = false
			}
		);

		Assert.Same( input, session.Input );
		Assert.Same( output, session.Output );
		Assert.Same( inputEndpoint, session.InputEndpoint );
		Assert.Same( outputEndpoint, session.OutputEndpoint );
		Assert.True( session.IsInteractive );
		Assert.True( session.IsStateValid );
		Assert.Single( provider.SetModeCalls );
		Assert.Equal(
			TerminalModeApplyTiming.AfterOutputDrained,
			provider.SetModeCalls[ 0 ].Timing
		);
		Assert.NotSame( baseline, provider.SetModeCalls[ 0 ].Mode );

		await session.DisposeAsync();

		Assert.False( session.IsStateValid );
		Assert.Equal( 1, output.FlushCount );
		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
		Assert.Equal(
			TerminalModeApplyTiming.AfterOutputDrained,
			provider.SetModeCalls[ 1 ].Timing
		);

		await session.DisposeAsync();

		Assert.Equal( 1, output.FlushCount );
		Assert.Equal( 2, provider.SetModeCalls.Count );
	}

	/// <summary>Verifies that Windows restoration uses immediate console-mode application.</summary>
	[Fact]
	public async Task RestoresWindowsConsoleModeImmediately() {
		TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x0007U
		);
		RecordingTerminalControlProvider provider = new(
			baseline,
			TerminalPlatformKind.WindowsConsole
		);
		TerminalSession session = await OpenTestSessionAsync( provider );

		await session.DisposeAsync();

		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Equal(
			TerminalModeApplyTiming.Immediately,
			provider.SetModeCalls[ 0 ].Timing
		);
		Assert.Equal(
			TerminalModeApplyTiming.Immediately,
			provider.SetModeCalls[ 1 ].Timing
		);
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
	}

	/// <summary>
	/// Verifies that required redirected output is rejected before mode capture or mutation.
	/// </summary>
	[Fact]
	public async Task RejectsRequiredNoninteractiveOutputBeforeMutation() {
		RecordingTerminalControlProvider provider = new( CreatePosixBaseline() ) {
			OutputObservation = CreateNonterminalObservation()
		};
		TestTerminalOutput output = new();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => TerminalSession.OpenAsync(
				provider,
				TerminalEndpoint.StandardInput,
				TerminalEndpoint.StandardOutput,
				new TestTerminalInput(),
				output
			).AsTask()
		);

		Assert.Equal( 0, provider.GetModeCount );
		Assert.Empty( provider.SetModeCalls );
		Assert.Equal( 0, output.FlushCount );
	}

	/// <summary>
	/// Verifies that a caller may deliberately combine interactive input with redirected output.
	/// </summary>
	[Fact]
	public async Task AllowsNoninteractiveOutputWhenExplicitlyConfigured() {
		RecordingTerminalControlProvider provider = new( CreatePosixBaseline() ) {
			OutputObservation = CreateNonterminalObservation()
		};
		TerminalSession session = await TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			new TestTerminalOutput(),
			new TerminalSessionOptions {
				RequireInteractiveOutput = false
			}
		);

		Assert.False( session.IsInteractive );
		Assert.True( session.InputObservation.IsTerminal );
		Assert.False( session.OutputObservation.IsTerminal );

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies rollback when semantic mode application reports a controlled failure.
	/// </summary>
	[Fact]
	public async Task RestoresBaselineAfterApplyFailure() {
		TerminalModeSnapshot baseline = CreatePosixBaseline();
		RecordingTerminalControlProvider provider = new( baseline );
		provider.SetModeResults.Enqueue(
			TerminalControlMutationResult.Failed( "apply failed" )
		);
		provider.SetModeResults.Enqueue( TerminalControlMutationResult.Success() );

		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => OpenTestSessionAsync( provider ).AsTask()
		);

		Assert.Contains( "apply failed", exception.Message );
		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
	}

	/// <summary>Verifies rollback when the provider throws during semantic mode application.</summary>
	[Fact]
	public async Task RestoresBaselineAfterApplyException() {
		TerminalModeSnapshot baseline = CreatePosixBaseline();
		RecordingTerminalControlProvider provider = new( baseline ) {
			ThrowOnSetModeCall = 1,
			SetModeException = new InvalidOperationException( "provider exploded" )
		};

		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => OpenTestSessionAsync( provider ).AsTask()
		);

		Assert.Equal( "provider exploded", exception.Message );
		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
	}

	/// <summary>
	/// Verifies cancellation observed after a successful mutation still restores the baseline.
	/// </summary>
	[Fact]
	public async Task RestoresBaselineWhenInitializationIsCancelledAfterApply() {
		TerminalModeSnapshot baseline = CreatePosixBaseline();
		RecordingTerminalControlProvider provider = new( baseline );
		using CancellationTokenSource cancellation = new();
		provider.SetModeCallback = callNumber => {
			if ( 1 == callNumber ) {
				cancellation.Cancel();
			}
		};

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => TerminalSession.OpenAsync(
				provider,
				TerminalEndpoint.StandardInput,
				TerminalEndpoint.StandardOutput,
				new TestTerminalInput(),
				new TestTerminalOutput(),
				cancellationToken: cancellation.Token
			).AsTask()
		);

		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
	}

	/// <summary>
	/// Verifies that an initialization error and a restoration error are both preserved.
	/// </summary>
	[Fact]
	public async Task AggregatesInitializationAndRestorationFailures() {
		RecordingTerminalControlProvider provider = new( CreatePosixBaseline() );
		provider.SetModeResults.Enqueue(
			TerminalControlMutationResult.Failed( "apply failed" )
		);
		provider.SetModeResults.Enqueue(
			TerminalControlMutationResult.Failed( "restore failed" )
		);

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => OpenTestSessionAsync( provider ).AsTask()
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
		Assert.Contains( "apply failed", exception.InnerExceptions[ 0 ].Message );
		Assert.Contains( "restore failed", exception.InnerExceptions[ 1 ].Message );
		Assert.Equal( 2, provider.SetModeCalls.Count );
	}

	/// <summary>
	/// Verifies explicit invalidation records that out-of-band activity may have
	/// replaced the session's configured terminal state.
	/// </summary>
	[Fact]
	public async Task ExplicitInvalidationMarksSessionStateInvalid() {
		RecordingTerminalControlProvider provider = new( CreatePosixBaseline() );
		TerminalSession session = await OpenTestSessionAsync( provider );
		Assert.True( session.IsStateValid );

		session.InvalidateState();

		Assert.False( session.IsStateValid );
		await session.DisposeAsync();
		Assert.Equal( 2, provider.SetModeCalls.Count );
	}

	/// <summary>Verifies cancellation before opening performs no endpoint observation or mutation.</summary>
	[Fact]
	public async Task HonorsCancellationBeforeInitialization() {
		RecordingTerminalControlProvider provider = new( CreatePosixBaseline() );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => TerminalSession.OpenAsync(
				provider,
				TerminalEndpoint.StandardInput,
				TerminalEndpoint.StandardOutput,
				new TestTerminalInput(),
				new TestTerminalOutput(),
				cancellationToken: cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, provider.ObserveCount );
		Assert.Equal( 0, provider.GetModeCount );
		Assert.Empty( provider.SetModeCalls );
	}

	/// <summary>Verifies undefined input-mode options are rejected before native operations.</summary>
	[Fact]
	public async Task RejectsUndefinedSessionInputMode() {
		RecordingTerminalControlProvider provider = new( CreatePosixBaseline() );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => TerminalSession.OpenAsync(
				provider,
				TerminalEndpoint.StandardInput,
				TerminalEndpoint.StandardOutput,
				new TestTerminalInput(),
				new TestTerminalOutput(),
				new TerminalSessionOptions {
					InputMode = (TerminalInputMode)int.MaxValue
				}
			).AsTask()
		);

		Assert.Equal( 0, provider.ObserveCount );
		Assert.Empty( provider.SetModeCalls );
	}

	private static async ValueTask<TerminalSession> OpenTestSessionAsync(
		RecordingTerminalControlProvider provider
	) {
		ArgumentNullException.ThrowIfNull( provider );

		return await TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			new TestTerminalOutput()
		);
	}

	private static TerminalModeSnapshot CreatePosixBaseline() {
		return TerminalModeSnapshot.CreatePosix(
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

	private static TerminalEndpointObservation CreateTerminalObservation(
		TerminalPlatformKind platform
	) {
		return new TerminalEndpointObservation(
			true,
			null,
			platform,
			TerminalControlCapabilities.Attachment
				| TerminalControlCapabilities.ModeRead
				| TerminalControlCapabilities.ModeWrite
		);
	}

	private static TerminalEndpointObservation CreateNonterminalObservation() {
		return new TerminalEndpointObservation(
			false,
			null,
			null,
			TerminalControlCapabilities.None
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class TestTerminalOutput : ITerminalOutput {
		internal int FlushCount {
			get;
			private set;
		}

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
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline;
		private readonly TerminalPlatformKind platform;

		internal RecordingTerminalControlProvider(
			TerminalModeSnapshot baseline,
			TerminalPlatformKind? platform = null
		) {
			ArgumentNullException.ThrowIfNull( baseline );
			this.baseline = baseline;
			this.platform = platform ?? baseline.Platform;
			this.InputObservation = CreateTerminalObservation( this.platform );
			this.OutputObservation = CreateTerminalObservation( this.platform );
		}

		internal TerminalEndpointObservation InputObservation {
			get;
			set;
		}

		internal TerminalEndpointObservation OutputObservation {
			get;
			set;
		}

		internal Queue<TerminalControlMutationResult> SetModeResults {
			get;
		} = new();

		internal List<SetModeCall> SetModeCalls {
			get;
		} = [];

		internal int ObserveCount {
			get;
			private set;
		}

		internal int GetModeCount {
			get;
			private set;
		}

		internal int? ThrowOnSetModeCall {
			get;
			set;
		}

		internal Exception? SetModeException {
			get;
			set;
		}

		internal Action<int>? SetModeCallback {
			get;
			set;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			++this.ObserveCount;

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				ReferenceEquals( endpoint, TerminalEndpoint.StandardOutput )
					? this.OutputObservation
					: this.InputObservation
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			++this.GetModeCount;
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

			this.SetModeCalls.Add( new SetModeCall( mode, timing ) );
			int callNumber = this.SetModeCalls.Count;
			this.SetModeCallback?.Invoke( callNumber );

			if ( callNumber == this.ThrowOnSetModeCall ) {
				throw this.SetModeException
					?? new InvalidOperationException( "Injected SetMode failure." );
			}

			return 0 < this.SetModeResults.Count
				? this.SetModeResults.Dequeue()
				: TerminalControlMutationResult.Success();
		}
	}

	private readonly record struct SetModeCall(
		TerminalModeSnapshot Mode,
		TerminalModeApplyTiming Timing
	);
}
