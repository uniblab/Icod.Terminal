namespace Icod.Terminal.Tests.Lifecycle;

using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Verifies ordered higher-layer lifecycle participation.</summary>
public sealed class TerminalSessionLifecycleParticipantTests {
	/// <summary>Verifies reverse preparation and forward resume ordering.</summary>
	[Fact]
	public async Task ParticipantsPrepareInReverseAndResumeInRegistrationOrder() {
		List<string> events = [];
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync( lifecycle );
		using IDisposable first = session.RegisterLifecycleParticipant(
			new RecordingParticipant( "first", events )
		);
		using IDisposable second = session.RegisterLifecycleParticipant(
			new RecordingParticipant( "second", events )
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal(
			new[] {
				"prepare:second",
				"prepare:first",
				"resume:first",
				"resume:second"
			},
			events
		);

		await session.DisposeAsync();
	}

	/// <summary>Verifies releasing a registration removes it from future suspend cycles.</summary>
	[Fact]
	public async Task DisposedRegistrationDoesNotParticipate() {
		List<string> events = [];
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync( lifecycle );
		IDisposable registration = session.RegisterLifecycleParticipant(
			new RecordingParticipant( "released", events )
		);
		registration.Dispose();
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Empty( events );
		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies that releasing a registration during preparation does not suppress
	/// the matching resume callback for the in-progress cycle.
	/// </summary>
	[Fact]
	public async Task PreparedParticipantStillResumesAfterRegistrationIsReleased() {
		List<string> events = [];
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync( lifecycle );
		IDisposable? registration = null;
		RecordingParticipant participant = new(
			"self-releasing",
			events,
			() => registration!.Dispose()
		);
		registration = session.RegisterLifecycleParticipant( participant );
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal(
			new[] {
				"prepare:self-releasing",
				"resume:self-releasing"
			},
			events
		);

		registration.Dispose();
		await session.DisposeAsync();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TestTerminalLifecycleSource lifecycle
	) {
		ArgumentNullException.ThrowIfNull( lifecycle );

		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			new TestTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
			}
		);
	}

	private sealed class RecordingParticipant : ITerminalSessionLifecycleParticipant {
		private readonly string name;
		private readonly IList<string> events;
		private readonly Action? onPrepare;

		internal RecordingParticipant(
			string name,
			IList<string> events,
			Action? onPrepare = null
		) {
			ArgumentException.ThrowIfNullOrWhiteSpace( name );
			ArgumentNullException.ThrowIfNull( events );

			this.name = name;
			this.events = events;
			this.onPrepare = onPrepare;
		}

		public ValueTask PrepareForTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.events.Add( "prepare:" + this.name );
			this.onPrepare?.Invoke();
			return ValueTask.CompletedTask;
		}

		public ValueTask ResumeAfterTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.events.Add( "resume:" + this.name );
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

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			if ( !this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) ) ) {
				throw new InvalidOperationException( "The test lifecycle queue rejected a signal." );
			}
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
		}
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

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
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
			return TerminalControlResult<TerminalSize>.Available( new TerminalSize( 80, 24 ) );
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
