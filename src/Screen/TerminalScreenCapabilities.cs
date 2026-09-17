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
		int indexedColorCount,
		bool supportsDirectRgb,
		bool supportsForegroundColor,
		bool supportsBackgroundColor,
		bool supportsDefaultColorRestoration,
		TerminalTextAttributes supportedAttributes,
		TerminalTextAttributes colorRestrictedAttributes,
		bool supportsAbsoluteCursorAddressing,
		bool supportsAlternateCharacterSet,
		bool supportsCursorHidden,
		bool supportsCursorNormal,
		bool supportsCursorVeryVisible
	) {
		this.IndexedColorCount = indexedColorCount;
		this.SupportsDirectRgb = supportsDirectRgb;
		this.SupportsForegroundColor = supportsForegroundColor;
		this.SupportsBackgroundColor = supportsBackgroundColor;
		this.SupportsDefaultColorRestoration = supportsDefaultColorRestoration;
		this.SupportedAttributes = supportedAttributes;
		this.ColorRestrictedAttributes = colorRestrictedAttributes;
		this.SupportsAbsoluteCursorAddressing = supportsAbsoluteCursorAddressing;
		this.SupportsAlternateCharacterSet = supportsAlternateCharacterSet;
		this.SupportsCursorHidden = supportsCursorHidden;
		this.SupportsCursorNormal = supportsCursorNormal;
		this.SupportsCursorVeryVisible = supportsCursorVeryVisible;
	}

	/// <summary>Gets the number of safely addressable indexed terminal colors.</summary>
	public int IndexedColorCount {
		get;
	}

	/// <summary>Gets whether the profile advertises direct RGB color semantics.</summary>
	public bool SupportsDirectRgb {
		get;
	}

	/// <summary>Gets whether a usable foreground-color selector is available.</summary>
	public bool SupportsForegroundColor {
		get;
	}

	/// <summary>Gets whether a usable background-color selector is available.</summary>
	public bool SupportsBackgroundColor {
		get;
	}

	/// <summary>Gets whether rendition can restore terminal-default colors.</summary>
	public bool SupportsDefaultColorRestoration {
		get;
	}

	/// <summary>Gets the text attributes with an advertised representation.</summary>
	public TerminalTextAttributes SupportedAttributes {
		get;
	}

	/// <summary>Gets supported attributes reported unavailable while color is active.</summary>
	public TerminalTextAttributes ColorRestrictedAttributes {
		get;
	}

	/// <summary>Gets whether absolute cursor positioning is available.</summary>
	public bool SupportsAbsoluteCursorAddressing {
		get;
	}

	/// <summary>Gets whether a complete alternate-character-set contract is advertised.</summary>
	public bool SupportsAlternateCharacterSet {
		get;
	}

	/// <summary>Gets whether the physical cursor can be hidden.</summary>
	public bool SupportsCursorHidden {
		get;
	}

	/// <summary>Gets whether normal physical cursor presentation is available.</summary>
	public bool SupportsCursorNormal {
		get;
	}

	/// <summary>Gets whether very-visible physical cursor presentation is available.</summary>
	public bool SupportsCursorVeryVisible {
		get;
	}

	/// <summary>Gets whether at least one indexed or direct color representation is available.</summary>
	public bool SupportsColor => 0 < this.IndexedColorCount || this.SupportsDirectRgb;

	/// <summary>Gets whether bold rendition is advertised.</summary>
	public bool SupportsBold => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Bold );

	/// <summary>Gets whether dim rendition is advertised.</summary>
	public bool SupportsDim => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Dim );

	/// <summary>Gets whether underline rendition is advertised.</summary>
	public bool SupportsUnderline => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Underline );

	/// <summary>Gets whether reverse-video rendition is advertised.</summary>
	public bool SupportsReverse => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Reverse );

	/// <summary>Gets whether standout rendition is advertised.</summary>
	public bool SupportsStandout => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Standout );

	/// <summary>Gets whether italic rendition is advertised.</summary>
	public bool SupportsItalic => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Italic );

	/// <summary>Gets whether blink rendition is advertised.</summary>
	public bool SupportsBlink => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Blink );

	/// <summary>Gets whether concealed rendition is advertised.</summary>
	public bool SupportsConceal => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Conceal );

	/// <summary>Gets whether strikeout rendition is advertised.</summary>
	public bool SupportsStrikeout => 0 != ( this.SupportedAttributes & TerminalTextAttributes.Strikeout );
}
