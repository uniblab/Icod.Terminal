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
	/// result when the selected terminal cannot establish the required protocol contract.
	/// </returns>
	public async ValueTask<TerminalControlResult<TerminalInputProtocolLease>> AcquireInputProtocolsAsync(
		TerminalInputProtocolOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		if ( options.KeyboardReportingMode.HasValue ) {
			bool kittySupported = await this.ProbeKittyKeyboardSupportAsync(
				lifecycleObservation: false,
				cancellationToken
			).ConfigureAwait( false );
			if ( !kittySupported ) {
				return TerminalControlResult<TerminalInputProtocolLease>.Unavailable(
					"The terminal did not establish Kitty progressive keyboard protocol support."
				);
			}
		}

		return await this.inputProtocolManager.AcquireAsync(
			options,
			cancellationToken
		).ConfigureAwait( false );
	}

	private void InvalidateInputProtocolState() {
		this.inputProtocolManager.Invalidate();
	}

	private ValueTask SuspendInputProtocolStateAsync() {
		return this.inputProtocolManager.SuspendAsync();
	}

	private async ValueTask ResumeInputProtocolStateAsync() {
		this.BeginLifecycleObservationQueryWindow();
		try {
			await this.inputProtocolManager.ReenterAsync().ConfigureAwait( false );
		} finally {
			this.EndLifecycleObservationQueryWindow();
		}
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
