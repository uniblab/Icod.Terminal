/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

/// <summary>
/// OSC 4/104 indexed-palette mutation, observation, reset, and scoped ownership for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalPaletteColorManager? paletteColorManager;

	internal TerminalPaletteColorManager PaletteColorManager {
		get {
			return this.paletteColorManager ??=
				new TerminalPaletteColorManager( this );
		}
	}

	/// <summary>
	/// Sets one indexed terminal-palette color using OSC 4.
	/// </summary>
	/// <param name="index">The palette index.</param>
	/// <param name="color">The normalized color to request.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing palette mutation.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal, session state is suspended, or scoped palette ownership is active.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// Successful completion proves complete OSC 4 emission only. It does not prove
	/// terminal support or visual application and does not update an authoritative cache.
	/// This remains an unscoped mutation. It is rejected while scoped palette ownership is
	/// active so it cannot invalidate an exact restoration contract. This operation does not flush.
	/// </remarks>
	public ValueTask SetPaletteColorAsync(
		byte index,
		TerminalColor color,
		CancellationToken cancellationToken = default
	) {
		return this.PaletteColorManager.WriteUnscopedAsync(
			TerminalOsc4Protocol.CreateSetRequest(
				index,
				color
			),
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
	/// <exception cref="InvalidOperationException">The output endpoint is not interactive, session state is suspended, or scoped palette ownership is active.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// The complete collection is validated and one complete OSC 4 frame is constructed
	/// before output commitment. This remains unscoped mutation and is rejected while any
	/// scoped palette-color lease is active. This operation does not flush.
	/// </remarks>
	public ValueTask SetPaletteColorsAsync(
		IReadOnlyList<TerminalPaletteColor> entries,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( entries );
		return this.PaletteColorManager.WriteUnscopedAsync(
			TerminalOsc4Protocol.CreateSetRequest( entries ),
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

	/// <summary>
	/// Acquires lifecycle-safe scoped ownership of one indexed terminal-palette color.
	/// </summary>
	/// <param name="index">The palette index to own.</param>
	/// <param name="color">The normalized color requested while this lease is effective.</param>
	/// <param name="queryTimeout">The finite timeout used to establish or refresh the external baseline.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>The acquired palette-color lease.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The query timeout is outside the supported terminal-query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support the required query/output operation or session state is suspended.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels acquisition.</exception>
	/// <exception cref="TimeoutException">The first-owner baseline query receives no correlated reply before the deadline.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed OSC 4 response.</exception>
	/// <remarks>
	/// The first owner for an index observes the real external color before any mutation.
	/// Later owners for that same index nest without re-querying. Owners are identity-aware
	/// and may be released out of order. Releasing the final owner explicitly replays the
	/// observed 16-bit baseline; OSC 104 is never used as a restoration substitute.
	///
	/// During managed suspend the external baseline is restored. After resume a fresh baseline
	/// is observed during the internal lifecycle observation window before retained ownership is
	/// reapplied. Timeout is not converted into a permanent unsupported result.
	/// </remarks>
	public ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
		byte index,
		TerminalColor color,
		TimeSpan queryTimeout,
		CancellationToken cancellationToken = default
	) {
		ValidatePaletteQueryTimeout( queryTimeout );
		cancellationToken.ThrowIfCancellationRequested();
		return this.PaletteColorManager.AcquireAsync(
			index,
			color,
			queryTimeout,
			cancellationToken
		);
	}

	/// <summary>
	/// Resets one indexed palette entry to terminal policy using OSC 104.
	/// </summary>
	/// <param name="index">The palette index to reset.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing reset emission.</returns>
	/// <remarks>
	/// This is a terminal-policy reset. It does not restore a color previously observed
	/// by this library and is rejected while scoped palette ownership is active.
	/// </remarks>
	public ValueTask ResetPaletteColorAsync(
		byte index,
		CancellationToken cancellationToken = default
	) {
		return this.PaletteColorManager.WriteUnscopedAsync(
			TerminalOsc104Protocol.CreateResetFrame( index ),
			cancellationToken
		);
	}

	/// <summary>
	/// Resets multiple distinct indexed palette entries to terminal policy in one OSC 104 frame.
	/// </summary>
	/// <param name="indices">One through 256 distinct palette indices.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing reset emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="indices"/> is null.</exception>
	/// <exception cref="ArgumentException">The collection is empty, too large, or contains a duplicate index.</exception>
	/// <remarks>
	/// All indices are validated before output commitment. This operation requests
	/// terminal-policy reset only, is not exact restoration, and is rejected while scoped
	/// palette ownership is active.
	/// </remarks>
	public ValueTask ResetPaletteColorsAsync(
		IReadOnlyList<byte> indices,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( indices );
		return this.PaletteColorManager.WriteUnscopedAsync(
			TerminalOsc104Protocol.CreateResetFrame( indices ),
			cancellationToken
		);
	}

	/// <summary>
	/// Resets the entire indexed palette to terminal policy using bare OSC 104.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing reset emission.</returns>
	/// <remarks>
	/// This emits bare OSC 104 and therefore requests the terminal's configured/default
	/// palette. It does not restore a library-observed palette snapshot and is rejected
	/// while scoped palette ownership is active.
	/// </remarks>
	public ValueTask ResetPaletteAsync(
		CancellationToken cancellationToken = default
	) {
		return this.PaletteColorManager.WriteUnscopedAsync(
			TerminalOsc104Protocol.CreateResetAllFrame(),
			cancellationToken
		);
	}

	private void InvalidatePaletteColorState() {
		this.paletteColorManager?.Invalidate();
	}

	private async ValueTask<Exception?> ClosePaletteColorStateAsync() {
		if ( this.paletteColorManager is null ) {
			return null;
		}

		try {
			await this.paletteColorManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
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
