namespace Icod.Terminal;

/// <summary>
/// OSC 4 indexed-palette mutation and observation for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Sets one indexed terminal-palette color using OSC 4.
	/// </summary>
	/// <param name="index">The palette index.</param>
	/// <param name="color">The normalized color to request.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing palette mutation.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// Successful completion proves complete OSC 4 emission only. It does not prove
	/// terminal support or visual application and does not update an authoritative cache.
	/// This operation does not flush.
	/// </remarks>
	public ValueTask SetPaletteColorAsync(
		byte index,
		TerminalColor color,
		CancellationToken cancellationToken = default
	) {
		byte[] frame = TerminalOsc4Protocol.CreateSetRequest(
			index,
			color
		);
		return this.WritePaletteFrameAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Sets multiple distinct indexed terminal-palette colors in one OSC 4 frame.
	/// </summary>
	/// <param name="entries">One through 256 distinct indexed colors.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing palette mutation.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
	/// <exception cref="ArgumentException">The collection is empty, too large, or contains a duplicate index.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// The complete collection is validated and one complete OSC 4 frame is constructed
	/// before output commitment. This operation does not flush.
	/// </remarks>
	public ValueTask SetPaletteColorsAsync(
		IReadOnlyList<TerminalPaletteColor> entries,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( entries );
		byte[] frame = TerminalOsc4Protocol.CreateSetRequest( entries );
		return this.WritePaletteFrameAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries one indexed terminal-palette color using OSC 4.
	/// </summary>
	/// <param name="index">The palette index.</param>
	/// <param name="timeout">The caller-visible finite query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's query.</param>
	/// <returns>The normalized color explicitly reported for the requested palette index.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported terminal-query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">No correlated reply arrives before the deadline.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed OSC 4 response.</exception>
	/// <remarks>
	/// A successful query is an observation for this transaction only. Timeout is not
	/// interpreted as proof that OSC 4 is unsupported and no result is cached as authoritative state.
	/// </remarks>
	public async ValueTask<TerminalColor> QueryPaletteColorAsync(
		byte index,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidatePaletteQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalOsc4Protocol.CreateQueryRequest( index ),
			TerminalOsc4Protocol.CreateResponseMatcher( index ),
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalOsc4Protocol.ParseObservation(
			frame,
			index
		);
	}

	private async ValueTask WritePaletteFrameAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 4 palette mutation requires an interactive terminal output endpoint."
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

	private static void ValidatePaletteQueryTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal palette query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}
}
