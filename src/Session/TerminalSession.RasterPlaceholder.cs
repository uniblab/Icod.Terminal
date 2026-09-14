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
/// Provides typed current-cursor output for semantic raster-placeholder cells.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Writes one semantic raster-placeholder cell at the terminal's current text cursor position.
	/// </summary>
	/// <param name="cell">The opaque semantic placeholder cell.</param>
	/// <param name="cancellationToken">Cancellation observed before placeholder output commits.</param>
	/// <returns>A value task representing the write operation.</returns>
	public ValueTask WriteRasterPlaceholderCellAsync(
		TerminalRasterPlaceholderCell cell,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();
		throw new NotSupportedException(
			"Raster placeholder cell output is not yet enabled by the current implementation tranche."
		);
	}

	/// <summary>
	/// Writes semantic raster-placeholder cells in caller-supplied order beginning at the terminal's
	/// current text cursor position.
	/// </summary>
	/// <param name="cells">The ordered semantic placeholder cells to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before placeholder output commits.</param>
	/// <returns>A value task representing the write operation.</returns>
	public ValueTask WriteRasterPlaceholderCellsAsync(
		ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();
		throw new NotSupportedException(
			"Raster placeholder cell output is not yet enabled by the current implementation tranche."
		);
	}
}
