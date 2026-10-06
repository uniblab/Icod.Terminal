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
	private readonly TerminalEnvironmentReportingManager environmentReportingManager;

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

	/// <summary>
	/// Acquires one independently owned request for unsolicited appearance reports.
	/// </summary>
	/// <param name="timeout">The caller-visible private-mode query timeout.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>
	/// An available result containing the reporting lease, or an unavailable result
	/// when the terminal explicitly reports that mode 2031 cannot be enabled.
	/// </returns>
	/// <remarks>
	/// Acquisition observes the mode with DECRQM before changing it. Overlapping
	/// owners share one captured baseline; releasing the last owner disables reporting
	/// only when this session enabled a previously reset mode.
	/// </remarks>
	public async ValueTask<TerminalControlResult<TerminalAppearanceReportingLease>>
		AcquireAppearanceReportingAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken = default
		) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		using IDisposable composition = await this.AcquireStateCompositionAsync(
			cancellationToken
		).ConfigureAwait( false );
		this.ThrowIfStateAcquisitionUnavailable();
		return await this.environmentReportingManager.AcquireAppearanceAsync(
			timeout,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Acquires one independently owned request for in-band resize reports.
	/// </summary>
	/// <param name="timeout">The caller-visible private-mode query timeout.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>
	/// An available result containing the reporting lease, or an unavailable result
	/// when the terminal explicitly reports that mode 2048 cannot be enabled.
	/// </returns>
	/// <remarks>
	/// Acquisition enables or re-enables the mode so the terminal requests an
	/// immediate report. That report remains on the authoritative semantic input path.
	/// Native lifecycle and synchronous geometry APIs retain their own provenance.
	/// </remarks>
	public async ValueTask<TerminalControlResult<TerminalInBandResizeReportingLease>>
		AcquireInBandResizeReportingAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken = default
		) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();
		using IDisposable composition = await this.AcquireStateCompositionAsync(
			cancellationToken
		).ConfigureAwait( false );
		this.ThrowIfStateAcquisitionUnavailable();
		return await this.environmentReportingManager.AcquireInBandResizeAsync(
			timeout,
			cancellationToken
		).ConfigureAwait( false );
	}
}
