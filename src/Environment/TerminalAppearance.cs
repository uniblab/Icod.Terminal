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

/// <summary>Identifies the terminal's reported appearance preference.</summary>
public enum TerminalAppearance {
	/// <summary>No current appearance observation is available.</summary>
	Unknown = 0,

	/// <summary>The terminal reports a dark appearance preference.</summary>
	Dark = 1,

	/// <summary>The terminal reports a light appearance preference.</summary>
	Light = 2
}

/// <summary>Represents one terminal-reported appearance or palette observation.</summary>
public sealed class TerminalAppearanceEvent {
	internal TerminalAppearanceEvent(
		TerminalAppearance appearance
	) {
		if ( appearance is not TerminalAppearance.Dark and not TerminalAppearance.Light ) {
			throw new ArgumentOutOfRangeException(
				nameof( appearance ),
				appearance,
				"A terminal appearance event must report dark or light."
			);
		}

		this.Appearance = appearance;
	}

	/// <summary>Gets the terminal-reported dark or light appearance.</summary>
	public TerminalAppearance Appearance {
		get;
	}
}
