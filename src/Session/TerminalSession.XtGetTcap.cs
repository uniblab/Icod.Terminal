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

using Icod.TermInfo;

/// <summary>
/// Provides typed XTGETTCAP live-capability queries for a live terminal session.
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
			TerminalXtGetTcapProtocol.CreateResponseMatcher( name ),
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
