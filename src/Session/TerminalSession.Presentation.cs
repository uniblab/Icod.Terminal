namespace Icod.Terminal;

/// <summary>
/// Reversible presentation-state ownership for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private readonly TerminalPresentationManager presentationManager;

	/// <summary>
	/// Acquires one reversible set of terminal presentation-state requirements.
	/// </summary>
	/// <param name="options">The presentation state required while the lease is active.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>
	/// An available result containing the acquired lease, or a controlled unavailable
	/// result when the selected terminal does not advertise the required capabilities.
	/// </returns>
	public ValueTask<TerminalControlResult<TerminalPresentationLease>> AcquirePresentationAsync(
		TerminalPresentationOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		return this.presentationManager.AcquireAsync(
			options,
			cancellationToken
		);
	}

	private void InvalidatePresentationState() {
		this.presentationManager.Invalidate();
	}

	private ValueTask SuspendPresentationStateAsync() {
		return this.presentationManager.SuspendAsync();
	}

	private ValueTask ResumePresentationStateAsync() {
		return this.presentationManager.ReenterAsync();
	}

	private async ValueTask<Exception?> ClosePresentationStateAsync() {
		try {
			await this.presentationManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
