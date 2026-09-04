namespace Icod.Terminal;

/// <summary>
/// Provides explicit OSC 52 clipboard and selection reads for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Explicitly requests bounded binary content from one terminal-managed clipboard or selection target.
	/// </summary>
	/// <param name="selection">The terminal-managed selection to query.</param>
	/// <param name="timeout">The caller-visible operation timeout, measured from transaction queueing.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The exact decoded selection bytes returned by the terminal.</returns>
	/// <remarks>
	/// <para>
	/// Calling this method is the explicit privacy opt-in for one OSC 52 clipboard
	/// read. A session never performs this query automatically during open, capability
	/// discovery, lifecycle handling, or disposal.
	/// </para>
	/// <para>
	/// The request is serialized through the session query transaction manager and
	/// flushed as part of query emission. The caller-visible timeout begins when the
	/// transaction is queued, so queueing and waiting for the control-output gate count
	/// against the requested deadline. If the deadline expires before emission commits,
	/// no request bytes are emitted.
	/// </para>
	/// <para>
	/// After an emitted request times out or the caller cancels, the transaction retains
	/// bounded late-response ownership so a delayed reply cannot satisfy a later query or
	/// leak into ordinary input. A structurally correlated OSC 52 response which reaches
	/// the complete-frame ceiling without terminating fails deterministically with
	/// <see cref="FormatException"/> and is drained through its OSC terminator before
	/// ordinary input decoding resumes.
	/// </para>
	/// <para>
	/// A successful result means a syntactically correlated OSC 52 response was
	/// received and decoded. Timeout alone does not prove that OSC 52 reads are unsupported;
	/// a terminal may disable or ignore clipboard queries by policy.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The selection or timeout is outside the supported contract.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible operation deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed or oversized OSC 52 response.</exception>
	public async ValueTask<byte[]> ReadClipboardAsync(
		TerminalClipboardSelection selection,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		TerminalOsc52Selection protocolSelection = ToOsc52Selection( selection );
		ValidateClipboardQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] request = OscWriter.EncodeOsc52QueryFrame( protocolSelection );
		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			request,
			TerminalOsc52Protocol.CreateResponseMatcher( protocolSelection ),
			timeout,
			cancellationToken
		).ConfigureAwait( false );

		return TerminalOsc52Protocol.ParsePayload(
			frame,
			protocolSelection
		);
	}

	private static void ValidateClipboardQueryTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal clipboard query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}
}
