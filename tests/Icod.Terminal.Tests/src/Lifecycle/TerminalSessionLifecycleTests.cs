namespace Icod.Terminal.Tests.Lifecycle;

using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies live-size and lifecycle-session behavior without installing process
/// signal handlers or mutating the process terminal.
/// </summary>
public sealed class TerminalSessionLifecycleTests {
	/// <summary>Verifies that live size prefers the observed output endpoint.</summary>
	[Fact]
	public async Task GetsLiveSizeFromObservedOutputEndpoint() {
		RecordingTerminalControlProvider provider = new();
		provider.Size = new TerminalSize( 132, 43 );
		TerminalSession session = await OpenSessionAsync( provider );

		TerminalControlResult<TerminalSize> result = session.GetSize();

		Assert.True( result.IsAvailable );
		Assert.Equal( new TerminalSize( 132, 43 ), result.GetRequiredValue() );
		Assert.Same( TerminalEndpoint.StandardOutput, provider.LastSizeEndpoint );

		await session.DisposeAsync();
	}

	/// <summary>Verifies that input is used when only it advertises live dimensions.</summary>
	[Fact]
	public async Task FallsBackToInputForLiveSize() {
		RecordingTerminalControlProvider provider = new() {
			OutputObservation = CreateTerminalObservation(
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			)
		};
		provider.Size = new TerminalSize( 90, 31 );
		TerminalSession session = await OpenSessionAsync( provider );

		TerminalControlResult<TerminalSize> result = session.GetSize();

		Assert.True( result.IsAvailable );
		Assert.Equal( new TerminalSize( 90, 31 ), result.GetRequiredValue() );
		Assert.Same( TerminalEndpoint.StandardInput, provider.LastSizeEndpoint );

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies that resize wakes a waiting loop, duplicate dimensions are suppressed,
	/// and interrupt delivery remains ordered after the coalesced resize.
	/// </summary>
	[Fact]
	public async Task ResizeWakesWaiterAndSuppressesDuplicateDimensions() {
		RecordingTerminalControlProvider provider = new();
		provider.Size = new TerminalSize( 100, 40 );
		TestTerminalLifecycleSource lifecycle = new();
		TerminalSession session = await OpenSessionAsync( provider, lifecycle );
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resize );
		TerminalLifecycleEvent resize = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal( TerminalLifecycleEventKind.Resize, resize.Kind );
		Assert.Equal( new TerminalSize( 100, 40 ), resize.Size );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resize );
		lifecycle.Publish( TerminalLifecycleSignalKind.Interrupt );
		TerminalLifecycleEvent next = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal( TerminalLifecycleEventKind.Interrupt, next.Kind );
		Assert.True( session.TerminationToken.IsCancellationRequested );

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies intercepted suspension restores baseline state before suspension and
	/// resume reapplies the requested session mode before publishing the resume event.
	/// </summary>
	[Fact]
	public async Task SuspendAndResumeRestoreAndReenterSessionState() {
		RecordingTerminalControlProvider provider = new();
		provider.Size = new TerminalSize( 120, 50 );
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TestTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			provider,
			lifecycle,
			output
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		Assert.Single( provider.SetModeCalls );
		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );

		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync( timeout.Token );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( new TerminalSize( 120, 50 ), resumed.Size );
		Assert.Equal( 1, lifecycle.SuspendCount );
		Assert.True( session.IsStateValid );
		Assert.Equal( 3, provider.SetModeCalls.Count );
		Assert.Same( provider.Baseline, provider.SetModeCalls[ 1 ].Mode );
		Assert.NotSame( provider.Baseline, provider.SetModeCalls[ 2 ].Mode );
		Assert.Equal( 1, output.FlushCount );

		await session.DisposeAsync();

		Assert.Equal( 4, provider.SetModeCalls.Count );
		Assert.Same( provider.Baseline, provider.SetModeCalls[ 3 ].Mode );
		Assert.Equal( 2, output.FlushCount );
	}

	/// <summary>
	/// Verifies a resume notification without a preceding catchable suspend still
	/// invalidates and reapplies session-owned state.
	/// </summary>
	[Fact]
	public async Task ExternalResumeReappliesSessionState() {
		RecordingTerminalControlProvider provider = new();
		TestTerminalLifecycleSource lifecycle = new();
		TerminalSession session = await OpenSessionAsync( provider, lifecycle );
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.True( session.IsStateValid );
		Assert.Equal( 2, provider.SetModeCalls.Count );

		await session.DisposeAsync();
		Assert.Equal( 3, provider.SetModeCalls.Count );
	}

	/// <summary>
	/// Verifies custom terminal-control providers do not implicitly install
	/// process-wide lifecycle observation.
	/// </summary>
	[Fact]
	public async Task CustomProviderDoesNotAutomaticallyInstallLifecycleSource() {
		RecordingTerminalControlProvider provider = new();
		TerminalSession session = await OpenSessionAsync( provider );

		Assert.False( session.SupportsLifecycleEvents );
		Assert.Throws<NotSupportedException>(
			() => session.ReadLifecycleEventAsync()
		);

		await session.DisposeAsync();
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalControlProvider provider,
		TestTerminalLifecycleSource? lifecycle = null,
		TestTerminalOutput? output = null
	) {
		ArgumentNullException.ThrowIfNull( provider );

		return await TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output ?? new TestTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
			}
		);
	}

	private static TerminalEndpointObservation CreateTerminalObservation(
		TerminalControlCapabilities capabilities
	) {
		return new TerminalEndpointObservation(
			true,
			null,
			TerminalPlatformKind.PosixTermios,
			capabilities
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
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
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
			this.InputObservation = CreateTerminalObservation(
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.LiveSize
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			);
			this.OutputObservation = this.InputObservation;
		}

		internal TerminalModeSnapshot Baseline {
			get;
		}

		internal TerminalEndpointObservation InputObservation {
			get;
			set;
		}

		internal TerminalEndpointObservation OutputObservation {
			get;
			set;
		}

		internal TerminalSize Size {
			get;
			set;
		} = new TerminalSize( 80, 24 );

		internal TerminalEndpoint? LastSizeEndpoint {
			get;
			private set;
		}

		internal List<SetModeCall> SetModeCalls {
			get;
		} = [];

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
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
			this.LastSizeEndpoint = endpoint;
			return TerminalControlResult<TerminalSize>.Available( this.Size );
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.Baseline );
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
			return TerminalControlMutationResult.Success();
		}
	}

	private readonly record struct SetModeCall(
		TerminalModeSnapshot Mode,
		TerminalModeApplyTiming Timing
	);
}
