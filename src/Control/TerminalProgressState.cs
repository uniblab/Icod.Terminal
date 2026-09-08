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
/// Identifies one semantic determinate terminal-progress rendering state.
/// </summary>
public enum TerminalProgressState {
	/// <summary>Normal/default determinate progress.</summary>
	Normal,

	/// <summary>Determinate progress indicating an error condition.</summary>
	Error,

	/// <summary>
	/// Determinate progress requesting vendor-defined attention rendering.
	/// </summary>
	/// <remarks>
	/// Windows Terminal describes OSC 9;4 wire state 4 as warning while ConEmu
	/// describes it as paused. <see cref="Attention"/> is the neutral semantic name.
	/// </remarks>
	Attention
}
