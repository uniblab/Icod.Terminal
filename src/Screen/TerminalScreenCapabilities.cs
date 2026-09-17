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

/// <summary>Describes semantic screen capabilities of one selected terminal profile.</summary>
public readonly record struct TerminalScreenCapabilities {
	internal TerminalScreenCapabilities(
		bool supportsAbsoluteCursorAddressing,
		bool supportsBold,
		bool supportsAlternateCharacterSet
	) {
		this.SupportsAbsoluteCursorAddressing = supportsAbsoluteCursorAddressing;
		this.SupportsBold = supportsBold;
		this.SupportsAlternateCharacterSet = supportsAlternateCharacterSet;
	}

	/// <summary>Gets whether absolute cursor positioning is available.</summary>
	public bool SupportsAbsoluteCursorAddressing {
		get;
	}

	/// <summary>Gets whether bold rendition is advertised.</summary>
	public bool SupportsBold {
		get;
	}

	/// <summary>Gets whether a complete alternate-character-set contract is advertised.</summary>
	public bool SupportsAlternateCharacterSet {
		get;
	}
}
