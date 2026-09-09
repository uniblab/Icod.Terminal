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
/// Selects when a Kitty OSC 99 notification request should be honored.
/// </summary>
public enum KittyNotificationOccasion {
	/// <summary>
	/// Honor the request regardless of terminal-window focus or visibility.
	/// </summary>
	Always = 0,

	/// <summary>
	/// Honor the request only while the terminal window is unfocused.
	/// </summary>
	Unfocused = 1,

	/// <summary>
	/// Honor the request only while the terminal window is both unfocused and not visible.
	/// </summary>
	Invisible = 2
}
