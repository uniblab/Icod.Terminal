namespace Icod.Terminal;

/// <summary>
/// Public CSI device, status, and cursor queries introduced by the 0.3 query milestone.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Requests Primary Device Attributes from the attached terminal.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The typed Primary Device Attributes response.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Primary Device Attributes response.</exception>
	public async ValueTask<TerminalPrimaryDeviceAttributes> QueryPrimaryDeviceAttributesAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.PrimaryDeviceAttributesRequest,
			TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParsePrimaryDeviceAttributes( frame );
	}

	/// <summary>
	/// Requests Secondary Device Attributes from the attached terminal.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The typed Secondary Device Attributes response.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Secondary Device Attributes response.</exception>
	public async ValueTask<TerminalSecondaryDeviceAttributes> QuerySecondaryDeviceAttributesAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.SecondaryDeviceAttributesRequest,
			TerminalCsiQueryProtocol.SecondaryDeviceAttributesMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParseSecondaryDeviceAttributes( frame );
	}

	/// <summary>
	/// Requests the standard ECMA-48 Device Status Report from the attached terminal.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The status reported by the terminal.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Device Status Report response.</exception>
	public async ValueTask<TerminalDeviceStatus> QueryDeviceStatusAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.DeviceStatusRequest,
			TerminalCsiQueryProtocol.DeviceStatusMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParseDeviceStatus( frame );
	}

	/// <summary>
	/// Requests the standard ECMA-48 Cursor Position Report from the attached terminal.
	/// </summary>
	/// <remarks>
	/// <see cref="TerminalCursorPosition.Row"/> and
	/// <see cref="TerminalCursorPosition.Column"/> are one-based, matching the CPR wire protocol.
	/// </remarks>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The one-based cursor position reported by the terminal.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Cursor Position Report response.</exception>
	public async ValueTask<TerminalCursorPosition> QueryCursorPositionAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.CursorPositionRequest,
			TerminalCsiQueryProtocol.CursorPositionMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParseCursorPosition( frame );
	}

	private static void ValidateCsiQueryTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}
}
