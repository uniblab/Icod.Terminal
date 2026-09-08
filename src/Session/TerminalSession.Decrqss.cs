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
/// Provides typed DEC Request Status String queries for a live terminal session.
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
