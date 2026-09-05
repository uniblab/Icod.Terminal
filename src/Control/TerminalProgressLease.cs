namespace Icod.Terminal;

/// <summary>
/// Owns one logical session-managed terminal-progress request.
/// </summary>
/// <remarks>
/// A progress lease may report determinate completed/total work or enter
/// indeterminate state. Nested leases are ordered by acquisition; the most
/// recently acquired reported lease controls physical terminal progress.
/// </remarks>
public sealed class TerminalProgressLease : IAsyncDisposable {
	private readonly long ownerId;
	private readonly SemaphoreSlim operationGate = new( 1, 1 );
	private TerminalProgressManager? owner;

	internal TerminalProgressLease(
		TerminalProgressManager owner,
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
	/// Reports normal determinate progress as completed work out of total work.
	/// </summary>
	/// <param name="completed">The nonnegative completed work count.</param>
	/// <param name="total">The positive total work count.</param>
	/// <param name="cancellationToken">Cancellation observed before update commit.</param>
	/// <returns>A value task representing the progress update.</returns>
	public ValueTask ReportAsync(
		long completed,
		long total,
		CancellationToken cancellationToken = default
	) {
		return this.ReportAsync(
			TerminalProgressState.Normal,
			completed,
			total,
			cancellationToken
		);
	}

	/// <summary>
	/// Reports determinate progress using the requested semantic rendering state.
	/// </summary>
	/// <param name="state">The semantic determinate progress state.</param>
	/// <param name="completed">The nonnegative completed work count.</param>
	/// <param name="total">The positive total work count.</param>
	/// <param name="cancellationToken">Cancellation observed before update commit.</param>
	/// <returns>A value task representing the progress update.</returns>
	public async ValueTask ReportAsync(
		TerminalProgressState state,
		long completed,
		long total,
		CancellationToken cancellationToken = default
	) {
		TerminalProgressValue value = TerminalProgressValue.CreateDeterminate(
			state,
			completed,
			total
		);
		cancellationToken.ThrowIfCancellationRequested();

		await this.operationGate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			TerminalProgressManager currentOwner = this.GetRequiredOwner();
			await currentOwner.ReportAsync(
				this.ownerId,
				value,
				cancellationToken
			).ConfigureAwait( false );
		} finally {
			this.operationGate.Release();
		}
	}

	/// <summary>
	/// Changes this logical progress owner to indeterminate progress.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before update commit.</param>
	/// <returns>A value task representing the progress update.</returns>
	public async ValueTask SetIndeterminateAsync(
		CancellationToken cancellationToken = default
	) {
		TerminalProgressValue value = TerminalProgressValue.CreateIndeterminate();
		cancellationToken.ThrowIfCancellationRequested();

		await this.operationGate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			TerminalProgressManager currentOwner = this.GetRequiredOwner();
			await currentOwner.ReportAsync(
				this.ownerId,
				value,
				cancellationToken
			).ConfigureAwait( false );
		} finally {
			this.operationGate.Release();
		}
	}

	/// <summary>
	/// Releases this logical terminal-progress request.
	/// </summary>
	/// <returns>A value task representing asynchronous release.</returns>
	/// <remarks>
	/// Successful repeated disposal is idempotent. If physical restoration or clear
	/// fails, ownership is retained so a later disposal attempt can retry cleanup.
	/// </remarks>
	public async ValueTask DisposeAsync() {
		await this.operationGate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			TerminalProgressManager? currentOwner = this.owner;
			if ( currentOwner is null ) {
				return;
			}

			await currentOwner.ReleaseAsync( this.ownerId ).ConfigureAwait( false );
			this.owner = null;
		} finally {
			this.operationGate.Release();
		}
	}

	private TerminalProgressManager GetRequiredOwner() {
		return this.owner
			?? throw new ObjectDisposedException( nameof( TerminalProgressLease ) );
	}
}
