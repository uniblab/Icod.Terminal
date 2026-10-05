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
/// Provides bounded terminal-environment queries and reporting ownership.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Requests one dark or light appearance observation from the attached terminal.
	/// </summary>
	/// <remarks>
	/// This method emits only the appearance query and does not enable ongoing
	/// appearance reporting. Solicited and unsolicited appearance reports share the
	/// same wire grammar, so a response is an observation rather than proof of unique
	/// causal correlation with this request.
	/// </remarks>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The terminal-reported <see cref="TerminalAppearance.Dark"/> or <see cref="TerminalAppearance.Light"/> value.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">No correlated reply arrives before the deadline.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed or unknown appearance value.</exception>
	public async ValueTask<TerminalAppearance> QueryAppearanceAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalEnvironmentProtocol.AppearanceQueryRequest,
			TerminalEnvironmentProtocol.AppearanceReportMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalEnvironmentProtocol.ParseAppearance( frame );
	}
}
