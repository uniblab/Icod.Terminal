namespace Icod.Terminal;

/// <summary>
/// Pointer-shape operations and ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalPointerShapeManager? pointerShapeManager;

	internal TerminalPointerShapeManager PointerShapeManager {
		get {
			return this.pointerShapeManager ??=
				new TerminalPointerShapeManager( this );
		}
	}

	/// <summary>
	/// Sets the terminal mouse-pointer shape using OSC 22 without creating a scoped owner.
	/// </summary>
	/// <param name="shape">The semantic pointer shape to request.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the pointer-shape emission.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The pointer shape is not recognized.</exception>
	/// <exception cref="InvalidOperationException">
	/// The output endpoint is not interactive, terminal state is suspended, a scoped
	/// pointer owner is active, or pointer-state recovery remains pending.
	/// </exception>
	/// <exception cref="OperationCanceledException">The caller cancels before commit.</exception>
	/// <remarks>
	/// Successful completion proves only that the complete OSC 22 frame was emitted.
	/// It does not prove that the terminal supports or visually applied the requested
	/// shape. This operation does not automatically query support and does not flush.
	/// </remarks>
	public ValueTask SetPointerShapeAsync(
		TerminalPointerShape shape,
		CancellationToken cancellationToken = default
	) {
		_ = TerminalPointerShapeCodec.GetWireName( shape );
		cancellationToken.ThrowIfCancellationRequested();
		return this.PointerShapeManager.SetAsync(
			shape,
			cancellationToken
		);
	}

	/// <summary>
	/// Resets OSC 22 pointer shape to terminal policy without creating a scoped owner.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the reset emission.</returns>
	/// <exception cref="InvalidOperationException">
	/// The output endpoint is not interactive, terminal state is suspended, a scoped
	/// pointer owner is active, or pointer-state recovery remains pending.
	/// </exception>
	/// <exception cref="OperationCanceledException">The caller cancels before commit.</exception>
	/// <remarks>
	/// This emits the empty OSC 22 payload. It is distinct from requesting
	/// <see cref="TerminalPointerShape.Default"/>.
	/// </remarks>
	public ValueTask ResetPointerShapeAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.PointerShapeManager.ResetAsync( cancellationToken );
	}

	/// <summary>
	/// Acquires one session-managed OSC 22 pointer-shape owner.
	/// </summary>
	/// <param name="shape">The semantic pointer shape to own.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A value task containing the acquired pointer-shape lease.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The pointer shape is not recognized.</exception>
	/// <exception cref="InvalidOperationException">
	/// The output endpoint is not interactive, terminal state is suspended, or pointer
	/// cleanup/state recovery remains pending.
	/// </exception>
	/// <exception cref="OperationCanceledException">The caller cancels acquisition.</exception>
	/// <remarks>
	/// Acquisition immediately emits the requested semantic shape. Nested owners are
	/// identity-aware and may be disposed out of order. Final release emits the empty
	/// OSC 22 terminal-policy reset; it does not claim restoration of an unknown
	/// external pre-lease pointer shape.
	/// </remarks>
	public async ValueTask<TerminalPointerShapeLease> AcquirePointerShapeAsync(
		TerminalPointerShape shape,
		CancellationToken cancellationToken = default
	) {
		_ = TerminalPointerShapeCodec.GetWireName( shape );
		cancellationToken.ThrowIfCancellationRequested();
		TerminalPointerShapeManager manager = this.PointerShapeManager;
		long ownerId = await manager.AcquireAsync(
			shape,
			cancellationToken
		).ConfigureAwait( false );
		return new TerminalPointerShapeLease(
			manager,
			ownerId,
			shape
		);
	}

	/// <summary>
	/// Explicitly queries the current Kitty-compatible OSC 22 application pointer shape.
	/// </summary>
	/// <param name="timeout">The caller-visible query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>
	/// A pointer-shape observation. <see cref="TerminalPointerShapeObservation.HasShape"/>
	/// is false when the terminal explicitly reports that no application pointer shape is set.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported terminal-query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed or unknown OSC 22 response.</exception>
	/// <remarks>
	/// This query is explicit and performs no support inference. A timeout is not treated
	/// as proof that OSC 22 is unsupported. An explicit no-shape result is distinct from
	/// <see cref="TerminalPointerShape.Default"/>.
	/// </remarks>
	public ValueTask<TerminalPointerShapeObservation> QueryCurrentPointerShapeAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		return this.QueryPointerShapeAsync(
			TerminalOsc22Protocol.CreateCurrentQueryRequest(),
			timeout,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries the terminal's Kitty-compatible OSC 22 default pointer shape.
	/// </summary>
	/// <param name="timeout">The caller-visible query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>A pointer-shape observation containing the terminal-reported default shape when present.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported terminal-query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed or unknown OSC 22 response.</exception>
	/// <remarks>
	/// This query is explicit and performs no support inference. Timeout remains an
	/// unanswered query rather than being converted into an unsupported result.
	/// </remarks>
	public ValueTask<TerminalPointerShapeObservation> QueryDefaultPointerShapeAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		return this.QueryPointerShapeAsync(
			TerminalOsc22Protocol.CreateDefaultQueryRequest(),
			timeout,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries the terminal's Kitty-compatible OSC 22 grabbed pointer shape.
	/// </summary>
	/// <param name="timeout">The caller-visible query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>A pointer-shape observation containing the terminal-reported grabbed shape when present.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported terminal-query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed or unknown OSC 22 response.</exception>
	/// <remarks>
	/// This query is explicit and performs no support inference. Timeout remains an
	/// unanswered query rather than being converted into an unsupported result.
	/// </remarks>
	public ValueTask<TerminalPointerShapeObservation> QueryGrabbedPointerShapeAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		return this.QueryPointerShapeAsync(
			TerminalOsc22Protocol.CreateGrabbedQueryRequest(),
			timeout,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries whether the terminal reports support for one semantic OSC 22 pointer shape.
	/// </summary>
	/// <param name="shape">The semantic pointer shape to query.</param>
	/// <param name="timeout">The caller-visible query timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns><see langword="true"/> for an explicit support reply of 1; otherwise <see langword="false"/> for 0.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The shape or timeout is outside the supported contract.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed OSC 22 response.</exception>
	/// <remarks>
	/// Only an explicit reply of 0 is treated as unsupported. Timeout is not proof that
	/// OSC 22 or the requested semantic shape is unsupported.
	/// </remarks>
	public async ValueTask<bool> QueryPointerShapeSupportAsync(
		TerminalPointerShape shape,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		byte[] request = TerminalOsc22Protocol.CreateSupportQueryRequest( shape );
		ValidatePointerShapeQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			request,
			TerminalOsc22Protocol.CreateResponseMatcher(),
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalOsc22Protocol.ParseSingleShapeSupport( frame );
	}

	private async ValueTask<TerminalPointerShapeObservation> QueryPointerShapeAsync(
		byte[] request,
		TimeSpan timeout,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( request );
		ValidatePointerShapeQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			request,
			TerminalOsc22Protocol.CreateResponseMatcher(),
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return new TerminalPointerShapeObservation(
			TerminalOsc22Protocol.ParseShapeObservation( frame )
		);
	}

	private void InvalidatePointerShapeState() {
		this.pointerShapeManager?.Invalidate();
	}

	private async ValueTask<Exception?> ClosePointerShapeStateAsync() {
		if ( this.pointerShapeManager is null ) {
			return null;
		}

		try {
			await this.pointerShapeManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}

	private static void ValidatePointerShapeQueryTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal pointer-shape query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}
}
