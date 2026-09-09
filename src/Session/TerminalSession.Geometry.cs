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
/// Provides internal semantic pixel-geometry observations for later graphics backends.
/// </summary>
public sealed partial class TerminalSession {
	internal async ValueTask<TerminalPixelSize> QueryTerminalPixelSizeAsync(
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

	internal async ValueTask<TerminalPixelSize> QueryCellPixelSizeAsync(
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
