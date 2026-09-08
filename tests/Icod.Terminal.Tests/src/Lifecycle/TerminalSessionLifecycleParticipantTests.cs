/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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

	/// <summary>
	/// Verifies that ordinary higher-layer lifecycle participants cannot issue public
	/// terminal queries while the session is still inside resume re-entry.
	/// </summary>
	[Fact]
	public async Task PublicQueriesRemainUnavailableDuringParticipantResume() {
		List<string> events = [];
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync( lifecycle );
		using IDisposable registration = session.RegisterLifecycleParticipant(
			new QueryAttemptParticipant(
				session,
				events
			)
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal(
			new[] {
				"prepare",
				"resume-query-rejected"
			},
			events
		);

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies that session-owned observed participants receive an internal query window
	/// before ordinary participant resume while public queries remain unavailable.
	/// </summary>
	[Fact]
	public async Task ObservedParticipantRefreshRunsBeforeOrdinaryResumeWithOnlyInternalQueryAccess() {
		List<string> events = [];
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync( lifecycle );
		using IDisposable observed = session.RegisterCoreLifecycleParticipant(
			new ObservedParticipant(
				session,
				events
			)
		);
		using IDisposable ordinary = session.RegisterLifecycleParticipant(
			new RecordingParticipant( "ordinary", events )
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );
		_ = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal(
			new[] {
				"prepare:ordinary",
				"prepare:observed",
				"observe:public-query-rejected",
				"observe:internal-query-ran",
				"resume:observed",
				"resume:ordinary"
			},
			events
		);

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

	private sealed class QueryAttemptParticipant : ITerminalSessionLifecycleParticipant {
		private readonly TerminalSession session;
		private readonly IList<string> events;

		internal QueryAttemptParticipant(
			TerminalSession session,
			IList<string> events
		) {
			ArgumentNullException.ThrowIfNull( session );
			ArgumentNullException.ThrowIfNull( events );

			this.session = session;
			this.events = events;
		}

		public ValueTask PrepareForTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.events.Add( "prepare" );
			return ValueTask.CompletedTask;
		}

		public async ValueTask ResumeAfterTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			try {
				_ = await this.session.QueryPaletteColorAsync(
					0,
					TimeSpan.FromMilliseconds( 100 ),
					cancellationToken
				);
				this.events.Add( "resume-query-unexpectedly-succeeded" );
			} catch ( InvalidOperationException ) {
				this.events.Add( "resume-query-rejected" );
			}
		}
	}

	private sealed class ObservedParticipant : ITerminalObservedLifecycleParticipant {
		private readonly TerminalSession session;
		private readonly IList<string> events;

		internal ObservedParticipant(
			TerminalSession session,
			IList<string> events
		) {
			ArgumentNullException.ThrowIfNull( session );
			ArgumentNullException.ThrowIfNull( events );

			this.session = session;
			this.events = events;
		}

		public ValueTask PrepareForTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.events.Add( "prepare:observed" );
			return ValueTask.CompletedTask;
		}

		public async ValueTask RefreshAfterTerminalResumeAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();

			try {
				_ = await this.session.QueryPaletteColorAsync(
					0,
					TimeSpan.Zero,
					cancellationToken
				);
				this.events.Add( "observe:public-query-unexpectedly-succeeded" );
			} catch ( InvalidOperationException ) {
				this.events.Add( "observe:public-query-rejected" );
			}

			try {
				_ = await this.session.ExecuteLifecycleObservationQueryAsync(
					TerminalOsc4Protocol.CreateQueryRequest( 0 ),
					TerminalOsc4Protocol.CreateResponseMatcher( 0 ),
					TimeSpan.Zero,
					cancellationToken
				);
			} catch ( TimeoutException ) {
			} finally {
				this.events.Add( "observe:internal-query-ran" );
			}
		}

		public ValueTask ResumeAfterTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.events.Add( "resume:observed" );
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
		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			_ = buffer;
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
