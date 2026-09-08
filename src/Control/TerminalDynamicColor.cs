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
/// Identifies one semantic xterm dynamic-color slot supported by Icod.Terminal.
/// </summary>
public enum TerminalDynamicColor {
	/// <summary>The terminal's default text foreground color.</summary>
	DefaultForeground,
	/// <summary>The terminal's default text background color.</summary>
	DefaultBackground,
	/// <summary>The terminal's text-cursor color.</summary>
	TextCursor,
	/// <summary>The terminal's mouse-pointer foreground color.</summary>
	MouseForeground,
	/// <summary>The terminal's mouse-pointer background color.</summary>
	MouseBackground,
	/// <summary>The terminal's highlight background color.</summary>
	HighlightBackground,
	/// <summary>The terminal's highlight foreground color.</summary>
	HighlightForeground
}
