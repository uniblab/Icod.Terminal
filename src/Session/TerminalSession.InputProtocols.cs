namespace Icod.Terminal;

/// <summary>
/// Reversible rich-input protocol ownership for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private readonly TerminalInputProtocolManager inputProtocolManager;

	/// <summary>
	/// Acquires one reversible set of rich-input protocol reporting requirements.
	/// </summary>
	/// <param name="options">The input protocols required while the lease is active.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>
	/// An available result containing the acquired lease, or a controlled unavailable
	/// result when the selected terminal does not advertise the required protocol contract.
	/// </returns>
	public ValueTask<TerminalControlResult<TerminalInputProtocolLease>> AcquireInputProtocolsAsync(
		TerminalInputProtocolOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		return this.inputProtocolManager.AcquireAsync(
			options,
			cancellationToken
		);
	}

	private void InvalidateInputProtocolState() {
		this.inputProtocolManager.Invalidate();
	}

	private ValueTask SuspendInputProtocolStateAsync() {
		return this.inputProtocolManager.SuspendAsync();
	}

	private ValueTask ResumeInputProtocolStateAsync() {
		return this.inputProtocolManager.ReenterAsync();
	}

	private async ValueTask<Exception?> CloseInputProtocolStateAsync() {
		try {
			await this.inputProtocolManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
