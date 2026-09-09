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
/// Provides typed CSI device, status, and cursor queries for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Requests Primary Device Attributes from the attached terminal.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The typed Primary Device Attributes response.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Primary Device Attributes response.</exception>
	public async ValueTask<TerminalPrimaryDeviceAttributes> QueryPrimaryDeviceAttributesAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.PrimaryDeviceAttributesRequest,
			TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		TerminalPrimaryDeviceAttributes attributes =
			TerminalCsiQueryProtocol.ParsePrimaryDeviceAttributes( frame );
		this.RecordPrimaryDeviceAttributesCapabilityEvidence( attributes );
		return attributes;
	}

	/// <summary>
	/// Requests Secondary Device Attributes from the attached terminal.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The typed Secondary Device Attributes response.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Secondary Device Attributes response.</exception>
	public async ValueTask<TerminalSecondaryDeviceAttributes> QuerySecondaryDeviceAttributesAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.SecondaryDeviceAttributesRequest,
			TerminalCsiQueryProtocol.SecondaryDeviceAttributesMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParseSecondaryDeviceAttributes( frame );
	}

	/// <summary>
	/// Requests the standard ECMA-48 Device Status Report from the attached terminal.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The status reported by the terminal.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Device Status Report response.</exception>
	public async ValueTask<TerminalDeviceStatus> QueryDeviceStatusAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.DeviceStatusRequest,
			TerminalCsiQueryProtocol.DeviceStatusMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParseDeviceStatus( frame );
	}

	/// <summary>
	/// Requests the standard ECMA-48 Cursor Position Report from the attached terminal.
	/// </summary>
	/// <remarks>
	/// <see cref="TerminalCursorPosition.Row"/> and
	/// <see cref="TerminalCursorPosition.Column"/> are one-based, matching the CPR wire protocol.
	/// </remarks>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The one-based cursor position reported by the terminal.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed Cursor Position Report response.</exception>
	public async ValueTask<TerminalCursorPosition> QueryCursorPositionAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiQueryProtocol.CursorPositionRequest,
			TerminalCsiQueryProtocol.CursorPositionMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiQueryProtocol.ParseCursorPosition( frame );
	}

	private static void ValidateCsiQueryTimeout(
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
