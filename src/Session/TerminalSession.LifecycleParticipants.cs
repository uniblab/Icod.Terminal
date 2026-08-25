namespace Icod.Terminal;

/// <summary>
/// Higher-layer lifecycle-participant registration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object lifecycleParticipantSync = new();
	private readonly List<LifecycleParticipantRegistration> lifecycleParticipants = [];

	private IReadOnlyList<ITerminalSessionLifecycleParticipant>? suspendedLifecycleParticipants;

	/// <summary>
	/// Registers higher-layer terminal state which must participate in suspend/resume ordering.
	/// </summary>
	/// <param name="participant">The lifecycle participant.</param>
	/// <returns>
	/// A registration token. Disposing it prevents future suspend preparations. If the participant
	/// has already prepared for the current suspend cycle, its matching resume callback is retained.
	/// </returns>
	public IDisposable RegisterLifecycleParticipant(
		ITerminalSessionLifecycleParticipant participant
	) {
		ArgumentNullException.ThrowIfNull( participant );

		LifecycleParticipantRegistration registration = new(
			this,
			participant
		);
		lock ( this.lifecycleParticipantSync ) {
			this.lifecycleParticipants.Add( registration );
		}

		return registration;
	}

	private async ValueTask PrepareLifecycleParticipantsAsync() {
		IReadOnlyList<ITerminalSessionLifecycleParticipant> participants =
			this.SnapshotLifecycleParticipants();
		List<ITerminalSessionLifecycleParticipant> prepared = [];

		for ( int index = participants.Count - 1; 0 <= index; --index ) {
			ITerminalSessionLifecycleParticipant participant = participants[ index ];
			prepared.Add( participant );
			try {
				await participant.PrepareForTerminalSuspendAsync(
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				prepared.Reverse();
				this.suspendedLifecycleParticipants = prepared;
				throw;
			}
		}

		prepared.Reverse();
		this.suspendedLifecycleParticipants = prepared;
	}

	private async ValueTask ResumeLifecycleParticipantsAsync() {
		IReadOnlyList<ITerminalSessionLifecycleParticipant> participants =
			this.suspendedLifecycleParticipants
			?? this.SnapshotLifecycleParticipants();
		this.suspendedLifecycleParticipants = null;

		List<Exception> exceptions = [];
		foreach ( ITerminalSessionLifecycleParticipant participant in participants ) {
			try {
				await participant.ResumeAfterTerminalSuspendAsync(
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception e ) {
				exceptions.Add( e );
			}
		}

		Exception? exception = BuildRestorationException( exceptions );
		if ( exception is not null ) {
			throw exception;
		}
	}

	private IReadOnlyList<ITerminalSessionLifecycleParticipant> SnapshotLifecycleParticipants() {
		lock ( this.lifecycleParticipantSync ) {
			if ( 0 == this.lifecycleParticipants.Count ) {
				return [];
			}

			ITerminalSessionLifecycleParticipant[] participants =
				new ITerminalSessionLifecycleParticipant[ this.lifecycleParticipants.Count ];
			for ( int index = 0; index < participants.Length; ++index ) {
				participants[ index ] = this.lifecycleParticipants[ index ].Participant;
			}

			return participants;
		}
	}

	private void UnregisterLifecycleParticipant(
		LifecycleParticipantRegistration registration
	) {
		ArgumentNullException.ThrowIfNull( registration );

		lock ( this.lifecycleParticipantSync ) {
			_ = this.lifecycleParticipants.Remove( registration );
		}
	}

	private sealed class LifecycleParticipantRegistration : IDisposable {
		private TerminalSession? owner;

		internal LifecycleParticipantRegistration(
			TerminalSession owner,
			ITerminalSessionLifecycleParticipant participant
		) {
			ArgumentNullException.ThrowIfNull( owner );
			ArgumentNullException.ThrowIfNull( participant );

			this.owner = owner;
			this.Participant = participant;
		}

		internal ITerminalSessionLifecycleParticipant Participant {
			get;
		}

		public void Dispose() {
			TerminalSession? currentOwner = Interlocked.Exchange(
				ref this.owner,
				null
			);
			currentOwner?.UnregisterLifecycleParticipant( this );
		}
	}
}
