namespace Icod.Terminal;

/// <summary>
/// Public DECRQSS status-string queries introduced by the 0.3 query milestone.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Requests the current setting of one supported terminal control function
	/// using DEC Request Status String (DECRQSS).
	/// </summary>
	/// <param name="kind">The fixed control-function setting to request.</param>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>
	/// A typed DECRPSS response. An unsupported request is returned with
	/// <see cref="TerminalStatusStringResponse.IsSupported"/> set to
	/// <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The status-string kind or timeout is outside the supported query range.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The session endpoints cannot support an active terminal query.
	/// </exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">
	/// The terminal returns a correlated but malformed DECRPSS response.
	/// </exception>
	public async ValueTask<TerminalStatusStringResponse> QueryStatusStringAsync(
		TerminalStatusStringKind kind,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		ValidateStatusStringQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalDecrqssProtocol.CreateRequest( kind ),
			TerminalDecrqssProtocol.ResponseMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalDecrqssProtocol.ParseResponse(
			kind,
			frame
		);
	}

	private static void ValidateStatusStringQueryTimeout(
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
