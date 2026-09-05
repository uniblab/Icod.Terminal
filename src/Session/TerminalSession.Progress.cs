namespace Icod.Terminal;

/// <summary>
/// Terminal-progress ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalProgressManager? progressManager;

	internal TerminalProgressManager ProgressManager {
		get {
			return this.progressManager ??=
				new TerminalProgressManager( this );
		}
	}

	/// <summary>
	/// Acquires one logical terminal-progress owner using OSC 9;4.
	/// </summary>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A value task containing the acquired progress lease.</returns>
	/// <exception cref="InvalidOperationException">
	/// The output endpoint is not an interactive terminal, the session state is
	/// suspended, or progress cleanup remains unresolved.
	/// </exception>
	/// <exception cref="OperationCanceledException">Acquisition is cancelled.</exception>
	/// <remarks>
	/// Acquisition itself emits no progress frame. The lease begins affecting terminal
	/// progress only after a successful report or indeterminate-state update. Successful
	/// progress emission does not prove that the terminal implements OSC 9;4.
	/// </remarks>
	public async ValueTask<TerminalProgressLease> AcquireProgressAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalProgressManager manager = this.ProgressManager;
		long ownerId = await manager.AcquireAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new TerminalProgressLease(
			manager,
			ownerId
		);
	}

	private async ValueTask<Exception?> CloseProgressStateAsync() {
		if ( this.progressManager is null ) {
			return null;
		}

		try {
			await this.progressManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
