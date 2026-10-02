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
/// Provides semantic pixel-geometry observations for graphics consumers.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>Queries one terminal-window pixel-dimension observation.</summary>
	/// <remarks>
	/// The observation is not cached as authoritative terminal state. A timeout does
	/// not imply that the operation is unsupported. A correlated malformed response
	/// fails rather than fabricating dimensions.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeout"/> is not positive.</exception>
	/// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
	/// <exception cref="TimeoutException">Thrown when no matching response arrives before <paramref name="timeout"/>.</exception>
	/// <exception cref="FormatException">Thrown when a correlated response is malformed.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the session cannot perform an active query.</exception>
	public async ValueTask<TerminalPixelDimensions> QueryTerminalPixelDimensionsAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiGeometryProtocol.TerminalPixelSizeRequest,
			TerminalCsiGeometryProtocol.TerminalPixelSizeMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiGeometryProtocol.ParseTerminalPixelSize( frame );
	}

	/// <summary>Queries one character-cell pixel-dimension observation.</summary>
	/// <remarks>
	/// The observation is not cached as authoritative terminal state. A timeout does
	/// not imply that the operation is unsupported. A correlated malformed response
	/// fails rather than fabricating dimensions.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeout"/> is not positive.</exception>
	/// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
	/// <exception cref="TimeoutException">Thrown when no matching response arrives before <paramref name="timeout"/>.</exception>
	/// <exception cref="FormatException">Thrown when a correlated response is malformed.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the session cannot perform an active query.</exception>
	public async ValueTask<TerminalPixelDimensions> QueryCellPixelDimensionsAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ValidateCsiQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalCsiGeometryProtocol.CellPixelSizeRequest,
			TerminalCsiGeometryProtocol.CellPixelSizeMatcher,
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalCsiGeometryProtocol.ParseCellPixelSize( frame );
	}
}
