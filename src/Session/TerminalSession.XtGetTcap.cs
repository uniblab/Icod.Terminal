namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Public XTGETTCAP live-capability queries introduced by the 0.3 query milestone.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Requests one live terminal capability through xterm's XTGETTCAP protocol.
	/// </summary>
	/// <remarks>
	/// XTGETTCAP exposes selected special-key capabilities plus a small set of
	/// additional observations such as <c>Co</c>, <c>TN</c>, and <c>RGB</c>.
	/// The returned observation does not mutate this session's immutable
	/// <see cref="TerminalDescription"/>.
	/// </remarks>
	/// <param name="name">
	/// The printable non-space ASCII termcap/terminfo capability name to query.
	/// </param>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>
	/// A live capability observation. An unsupported name is returned with
	/// <see cref="TerminalCapabilityObservation.IsSupported"/> set to
	/// <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">The capability name is null.</exception>
	/// <exception cref="ArgumentException">
	/// The capability name is empty or contains a character outside the supported
	/// printable ASCII name grammar.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The capability name or timeout exceeds the supported query bounds.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The session endpoints cannot support an active terminal query.
	/// </exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">
	/// The terminal returns a correlated but malformed XTGETTCAP response.
	/// </exception>
	public async ValueTask<TerminalCapabilityObservation> QueryLiveCapabilityAsync(
		string name,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( name );
		TerminalXtGetTcapProtocol.ValidateCapabilityName( name );
		ValidateLiveCapabilityQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalXtGetTcapProtocol.CreateRequest( name ),
			TerminalXtGetTcapProtocol.ResponseMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalXtGetTcapProtocol.ParseResponse(
			name,
			frame
		);
	}

	private static void ValidateLiveCapabilityQueryTimeout(
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
