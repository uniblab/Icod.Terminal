namespace Icod.Terminal;

/// <summary>
/// Owns one logical session-managed synchronized-output request.
/// </summary>
/// <remarks>
/// Multiple synchronized-output leases share one terminal-side mode-2026 request.
/// The first logical owner emits the begin frame and the final logical owner emits
/// the end frame followed by the synchronized-output flush boundary. Lease lifetime
/// does not prove that the terminal implements or continues honoring mode 2026.
/// </remarks>
public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable {
	private readonly object sync = new();
	private readonly long ownerId;
	private TerminalSynchronizedOutputManager? owner;
	private Task? disposeTask;

	internal TerminalSynchronizedOutputLease(
		TerminalSynchronizedOutputManager owner,
		long ownerId
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}

		this.owner = owner;
		this.ownerId = ownerId;
	}

	/// <summary>
	/// Releases this logical synchronized-output request.
	/// </summary>
	/// <returns>A value task representing asynchronous release.</returns>
	/// <remarks>
	/// Non-final release performs no terminal output. Final release emits the
	/// synchronized-output end frame and flushes once. If final release fails, the
	/// lease remains owned so a later disposal attempt can retry cleanup.
	/// </remarks>
	public async ValueTask DisposeAsync() {
		Task? task;
		lock ( this.sync ) {
			if ( this.owner is null ) {
				return;
			}

			this.disposeTask ??= this.DisposeCoreAsync();
			task = this.disposeTask;
		}

		try {
			await task.ConfigureAwait( false );
		} finally {
			lock ( this.sync ) {
				if ( ReferenceEquals( this.disposeTask, task ) ) {
					this.disposeTask = null;
				}
			}
		}
	}

	private async Task DisposeCoreAsync() {
		TerminalSynchronizedOutputManager? currentOwner;
		lock ( this.sync ) {
			currentOwner = this.owner;
		}
		if ( currentOwner is null ) {
			return;
		}

		await currentOwner.ReleaseAsync( this.ownerId ).ConfigureAwait( false );
		lock ( this.sync ) {
			this.owner = null;
		}
	}
}
