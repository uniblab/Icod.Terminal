namespace Icod.Terminal;

/// <summary>
/// Internal Kitty progressive-keyboard negotiation support.
/// </summary>
public sealed partial class TerminalSession {
	private static TimeSpan KittyKeyboardProbeTimeout {
		get;
	} = TimeSpan.FromSeconds( 1 );

	internal async ValueTask<bool> ProbeKittyKeyboardSupportAfterResumeAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();

		bool useLifecycleObservation;
		lock ( this.queryTransactionSync ) {
			useLifecycleObservation = this.queryTransactionsSuspended;
		}

		if ( !useLifecycleObservation ) {
			return await this.ProbeKittyKeyboardSupportAsync(
				lifecycleObservation: false,
				cancellationToken
			).ConfigureAwait( false );
		}

		this.BeginLifecycleObservationQueryWindow();
		try {
			return await this.ProbeKittyKeyboardSupportAsync(
				lifecycleObservation: true,
				cancellationToken
			).ConfigureAwait( false );
		} finally {
			this.EndLifecycleObservationQueryWindow();
		}
	}

	internal async ValueTask<bool> ProbeKittyKeyboardSupportAsync(
		bool lifecycleObservation,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalInputCoordinator coordinator = this.GetInputCoordinator();
		KittyKeyboardFlagsProbe probe = coordinator.RegisterKittyKeyboardFlagsProbe();

		try {
			byte[] request = [
				0x1b,
				(byte)'[',
				(byte)'?',
				(byte)'u',
				0x1b,
				(byte)'[',
				(byte)'c'
			];

			try {
				_ = lifecycleObservation
					? await this.ExecuteLifecycleObservationQueryAsync(
						request,
						TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
						KittyKeyboardProbeTimeout,
						cancellationToken
					).ConfigureAwait( false )
					: await this.ExecuteQueryAsync(
						request,
						TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
						KittyKeyboardProbeTimeout,
						cancellationToken
					).ConfigureAwait( false )
					;
			} catch ( TimeoutException ) {
				return false;
			}

			return probe.Flags.HasValue;
		} finally {
			coordinator.RemoveKittyKeyboardFlagsProbe( probe );
		}
	}
}
