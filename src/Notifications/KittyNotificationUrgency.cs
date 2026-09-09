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
/// Identifies Kitty OSC 99 desktop-notification urgency.
/// </summary>
public enum KittyNotificationUrgency {
	/// <summary>
	/// Low urgency.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Normal urgency.
	/// </summary>
	Normal = 1,

	/// <summary>
	/// Critical urgency.
	/// </summary>
	Critical = 2
}
