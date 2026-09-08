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
/// Identifies the native filesystem path grammar used when publishing a terminal current location.
/// </summary>
public enum TerminalLocationPathStyle {
	/// <summary>
	/// A POSIX absolute path beginning with <c>/</c>.
	/// </summary>
	Posix = 0,

	/// <summary>
	/// A fully-qualified Windows drive path such as <c>C:\Development</c>.
	/// </summary>
	WindowsDrive = 1,

	/// <summary>
	/// A Windows UNC path such as <c>\\server\share\directory</c>.
	/// </summary>
	WindowsUnc = 2
}
