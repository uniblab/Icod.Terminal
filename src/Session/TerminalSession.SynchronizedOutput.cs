namespace Icod.Terminal;

/// <summary>
/// Synchronized-output ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalSynchronizedOutputManager? synchronizedOutputManager;

	internal TerminalSynchronizedOutputManager SynchronizedOutputManager {
		get {
			return this.synchronizedOutputManager ??=
				new TerminalSynchronizedOutputManager( this );
		}
	}

	/// <summary>
	/// Acquires one logical synchronized-output request using DEC private mode 2026.
	/// </summary>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A lease owning one logical synchronized-output request.</returns>
	/// <remarks>
	/// The first active lease emits <c>CSI ? 2026 h</c>. Nested logical leases share
	/// the same physical terminal mode and emit no additional begin frame. The final
	/// active lease emits <c>CSI ? 2026 l</c> and flushes once on release.
	/// Successful acquisition proves only that the required begin frame was emitted,
	/// or that an existing logical synchronized-output owner was joined. It does not
	/// prove that the terminal recognizes or continues honoring mode 2026.
	/// </remarks>
	public async ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalSynchronizedOutputManager manager = this.SynchronizedOutputManager;
		long ownerId = await manager.AcquireAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new TerminalSynchronizedOutputLease(
			manager,
			ownerId
		);
	}

	private async ValueTask<Exception?> CloseSynchronizedOutputStateAsync() {
		if ( this.synchronizedOutputManager is null ) {
			return null;
		}

		try {
			await this.synchronizedOutputManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
