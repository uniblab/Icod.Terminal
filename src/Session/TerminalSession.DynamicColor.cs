namespace Icod.Terminal;

/// <summary>
/// Selected xterm dynamic-color mutation, observation, and reset for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Sets one selected dynamic terminal color.
	/// </summary>
	/// <param name="kind">The semantic dynamic-color identity.</param>
	/// <param name="color">The normalized color to request.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing the mutation.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not part of the selected 0.13 contract.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <remarks>
	/// Successful completion proves complete emission only and does not update an authoritative
	/// terminal-color cache. OSC 10–12 are the common/core tier; OSC 13/14/17/19 are the
	/// extended xterm tier and may have lower interoperability across terminal implementations.
	/// This is an unscoped mutation: the session does not capture, own, or automatically
	/// restore the prior value during invalidation, suspend/resume, or disposal.
	/// </remarks>
	public ValueTask SetDynamicColorAsync(
		TerminalDynamicColor kind,
		TerminalColor color,
		CancellationToken cancellationToken = default
	) {
		byte[] frame = TerminalDynamicColorProtocol.CreateSetRequest(
			kind,
			color
		);
		return this.WriteDynamicColorFrameAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries one selected dynamic terminal color.
	/// </summary>
	/// <param name="kind">The semantic dynamic-color identity.</param>
	/// <param name="timeout">The caller-visible finite query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's query.</param>
	/// <returns>The normalized color explicitly reported for the requested identity.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The color identity or timeout is outside the supported contract.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="TimeoutException">No correlated reply arrives before the deadline.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed dynamic-color response.</exception>
	/// <remarks>
	/// A successful query is an observation for this transaction only. Timeout is not interpreted
	/// as permanent lack of support and the result is not cached as authoritative state. Extended
	/// xterm-tier identities may be unsupported by terminals that implement only OSC 10–12.
	/// </remarks>
	public async ValueTask<TerminalColor> QueryDynamicColorAsync(
		TerminalDynamicColor kind,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateDynamicColorQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalDynamicColorProtocol.CreateQueryRequest( kind ),
			TerminalDynamicColorProtocol.CreateResponseMatcher( kind ),
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalDynamicColorProtocol.ParseObservation(
			frame,
			kind
		);
	}

	/// <summary>
	/// Resets one selected dynamic terminal color to terminal policy/default.
	/// </summary>
	/// <param name="kind">The semantic dynamic-color identity.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing the reset request.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not part of the selected 0.13 contract.</exception>
	/// <remarks>
	/// Reset is not exact restoration of a previously observed value. This operation performs no query.
	/// </remarks>
	public ValueTask ResetDynamicColorAsync(
		TerminalDynamicColor kind,
		CancellationToken cancellationToken = default
	) {
		byte[] frame = TerminalDynamicColorProtocol.CreateResetRequest( kind );
		return this.WriteDynamicColorFrameAsync(
			frame,
			cancellationToken
		);
	}

	private async ValueTask WriteDynamicColorFrameAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Dynamic-color mutation/reset requires an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();
		await this.Output.WriteAsync(
			frame,
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private static void ValidateDynamicColorQueryTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A dynamic-color query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}
}
