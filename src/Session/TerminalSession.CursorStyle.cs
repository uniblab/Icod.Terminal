namespace Icod.Terminal;

/// <summary>
/// Provides semantic DECSCUSR cursor-style operations for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalCursorStyleManager? cursorStyleManager;

	private TerminalCursorStyleManager CursorStyleManager {
		get {
			return this.cursorStyleManager ??= new TerminalCursorStyleManager( this );
		}
	}

	/// <summary>
	/// Sets the terminal text-cursor style using DECSCUSR.
	/// </summary>
	/// <param name="style">The semantic cursor style to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the cursor-style emission.</returns>
	/// <remarks>
	/// <para>
	/// Successful completion means the complete DECSCUSR frame was emitted to the
	/// interactive terminal output endpoint. It does not prove that the terminal
	/// recognized or applied the requested style.
	/// </para>
	/// <para>
	/// Cursor style is independent of cursor visibility. This operation does not
	/// show or hide the cursor and does not acquire any presentation-state lease.
	/// </para>
	/// <para>
	/// Bar styles use the xterm DECSCUSR extension. The library does not infer
	/// support from terminal identity and does not substitute another style when a
	/// terminal ignores an emitted request.
	/// </para>
	/// <para>
	/// Unscoped mutation is unavailable while a cursor-style lease is active so the
	/// lease stack can preserve exact restoration semantics.
	/// </para>
	/// </remarks>
	public ValueTask SetCursorStyleAsync(
		TerminalCursorStyle style,
		CancellationToken cancellationToken = default
	) {
		_ = TerminalCursorStyleCodec.GetParameter( style );
		cancellationToken.ThrowIfCancellationRequested();
		return this.CursorStyleManager.SetAsync(
			style,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries the current terminal text-cursor style through DECRQSS.
	/// </summary>
	/// <param name="timeout">The caller-visible query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>
	/// A typed cursor-style observation. An explicit negative DECRQSS response is
	/// returned with <see cref="TerminalCursorStyleObservation.IsSupported"/> set
	/// to <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The timeout is outside the supported terminal-query range.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The session endpoints cannot support an active terminal query.
	/// </exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">
	/// The terminal returns a correlated malformed response or a positive cursor-style
	/// value outside the frozen semantic set.
	/// </exception>
	/// <remarks>
	/// This operation reuses the existing DECRQSS <c>SP q</c> transaction path. It
	/// does not probe automatically during session open, lifecycle handling, or
	/// disposal, and it does not cache support state.
	/// </remarks>
	public async ValueTask<TerminalCursorStyleObservation> QueryCursorStyleAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		TerminalStatusStringResponse response = await this.QueryStatusStringAsync(
			TerminalStatusStringKind.CursorStyle,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		if ( !response.IsSupported ) {
			return new TerminalCursorStyleObservation(
				isSupported: false,
				style: null
			);
		}

		TerminalCursorStyle style = TerminalCursorStyleCodec.ParseStatusString(
			response.StatusString!
		);
		return new TerminalCursorStyleObservation(
			isSupported: true,
			style
		);
	}

	/// <summary>
	/// Acquires one reversible session-owned cursor-style request.
	/// </summary>
	/// <param name="style">The cursor style to own while the lease is active.</param>
	/// <param name="timeout">
	/// The caller-visible timeout used only when the outermost lease must observe the
	/// terminal's current cursor style before mutation.
	/// </param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A value task containing the acquired cursor-style lease.</returns>
	/// <exception cref="NotSupportedException">
	/// The terminal explicitly reports that DECRQSS cursor-style observation is unsupported.
	/// </exception>
	/// <remarks>
	/// The outermost acquisition first observes the real terminal cursor style and
	/// performs no mutation unless that observation succeeds. Nested acquisitions are
	/// strictly LIFO and restore the immediately preceding session-owned style. The
	/// outermost release restores the observed pre-lease style exactly; it never uses
	/// DECSCUSR parameter 0, parameter 1, or xterm parameter 7 as a guessed reset.
	/// </remarks>
	public ValueTask<TerminalCursorStyleLease> AcquireCursorStyleAsync(
		TerminalCursorStyle style,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		_ = TerminalCursorStyleCodec.GetParameter( style );
		cancellationToken.ThrowIfCancellationRequested();
		return this.CursorStyleManager.AcquireAsync(
			style,
			timeout,
			cancellationToken
		);
	}

	private async ValueTask<Exception?> CloseCursorStyleStateAsync() {
		if ( this.cursorStyleManager is null ) {
			return null;
		}

		try {
			await this.cursorStyleManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
