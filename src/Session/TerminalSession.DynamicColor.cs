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
/// Selected xterm dynamic-color mutation, observation, reset, and scoped ownership
/// for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalDynamicColorManager? dynamicColorManager;

	internal TerminalDynamicColorManager DynamicColorManager {
		get {
			return this.dynamicColorManager ??=
				new TerminalDynamicColorManager( this );
		}
	}

	/// <summary>
	/// Sets one selected dynamic terminal color without creating a scoped owner.
	/// </summary>
	public ValueTask SetDynamicColorAsync(
		TerminalDynamicColor kind,
		TerminalColor color,
		CancellationToken cancellationToken = default
	) {
		byte[] frame = TerminalDynamicColorProtocol.CreateSetRequest(
			kind,
			color
		);
		return this.DynamicColorManager.WriteUnscopedAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Acquires lifecycle-safe ownership of one selected dynamic terminal color.
	/// </summary>
	/// <param name="kind">The semantic dynamic-color identity.</param>
	/// <param name="color">The normalized color to own while the lease is active.</param>
	/// <param name="queryTimeout">The finite timeout used to observe the exact external baseline.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>The acquired dynamic-color lease.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The color identity or timeout is outside the supported contract.</exception>
	/// <exception cref="InvalidOperationException">The session cannot perform the required query or is suspended.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels acquisition.</exception>
	/// <exception cref="TimeoutException">The baseline observation times out.</exception>
	/// <exception cref="FormatException">The correlated baseline response is malformed.</exception>
	/// <remarks>
	/// The first owner for an identity queries and stores the exact current terminal color before
	/// mutation. Nested owners are identity-aware and may be disposed out of order. Releasing the
	/// final owner explicitly replays the observed baseline; OSC 110-114/117/119 reset controls are
	/// never used as a substitute for exact restoration. Managed suspend restores the external
	/// baseline, and resume re-observes a fresh lifecycle-epoch baseline before retained ownership
	/// is reapplied.
	/// </remarks>
	public ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
		TerminalDynamicColor kind,
		TerminalColor color,
		TimeSpan queryTimeout,
		CancellationToken cancellationToken = default
	) {
		_ = TerminalDynamicColorProtocol.CreateSetRequest( kind, color );
		ValidateDynamicColorQueryTimeout( queryTimeout );
		cancellationToken.ThrowIfCancellationRequested();
		return this.DynamicColorManager.AcquireAsync(
			kind,
			color,
			queryTimeout,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries one selected dynamic terminal color.
	/// </summary>
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
	/// Resets one selected dynamic terminal color to terminal policy/default without creating
	/// a scoped owner.
	/// </summary>
	public ValueTask ResetDynamicColorAsync(
		TerminalDynamicColor kind,
		CancellationToken cancellationToken = default
	) {
		byte[] frame = TerminalDynamicColorProtocol.CreateResetRequest( kind );
		return this.DynamicColorManager.WriteUnscopedAsync(
			frame,
			cancellationToken
		);
	}

	private void InvalidateDynamicColorState() {
		this.dynamicColorManager?.Invalidate();
	}

	private async ValueTask<Exception?> CloseDynamicColorStateAsync() {
		if ( this.dynamicColorManager is null ) {
			return null;
		}

		try {
			await this.dynamicColorManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
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
