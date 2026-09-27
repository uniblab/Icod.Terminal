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
/// <remarks>
/// Advertisement describes static representation presence, including empty or malformed sources.
/// It does not verify parameter-specific expansion, endpoint availability, or physical execution.
/// Use the screen planner for the actual request; another route may work when one is absent.
/// Inspection of these immutable facts never emits terminal traffic.
/// </remarks>
public readonly record struct TerminalScreenCapabilities {
	private readonly TerminalScreenAdvertisement advertisement;
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
		bool supportsCursorVeryVisible,
		TerminalScreenAdvertisement advertisement = TerminalScreenAdvertisement.None
	) {
		this.advertisement = advertisement;
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

	/// <summary>Gets whether a cursor-home representation is present in the static profile.</summary>
	public bool AdvertisesCursorHome => this.Advertises( TerminalScreenAdvertisement.CursorHome );

	/// <summary>Gets whether row-only cursor addressing is advertised.</summary>
	public bool AdvertisesCursorRowAddressing => this.Advertises( TerminalScreenAdvertisement.CursorRowAddressing );

	/// <summary>Gets whether column-only cursor addressing is advertised.</summary>
	public bool AdvertisesCursorColumnAddressing => this.Advertises( TerminalScreenAdvertisement.CursorColumnAddressing );

	/// <summary>Gets whether a carriage-return representation is advertised.</summary>
	public bool AdvertisesCarriageReturn => this.Advertises( TerminalScreenAdvertisement.CarriageReturn );

	/// <summary>Gets whether parameterized or single-step upward cursor movement is advertised.</summary>
	public bool AdvertisesCursorUp => this.Advertises( TerminalScreenAdvertisement.CursorUp );

	/// <summary>Gets whether parameterized or single-step downward cursor movement is advertised.</summary>
	public bool AdvertisesCursorDown => this.Advertises( TerminalScreenAdvertisement.CursorDown );

	/// <summary>Gets whether parameterized or single-step leftward cursor movement is advertised.</summary>
	public bool AdvertisesCursorLeft => this.Advertises( TerminalScreenAdvertisement.CursorLeft );

	/// <summary>Gets whether parameterized or single-step rightward cursor movement is advertised.</summary>
	public bool AdvertisesCursorRight => this.Advertises( TerminalScreenAdvertisement.CursorRight );

	/// <summary>Gets whether scrolling-region selection is advertised.</summary>
	public bool AdvertisesScrollRegion => this.Advertises( TerminalScreenAdvertisement.ScrollRegion );

	/// <summary>Reports whether a representation of the requested erase operation is present.</summary>
	/// <param name="kind">The semantic erase operation.</param>
	/// <returns>Whether the static profile advertises the operation, without expanding it.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The operation kind is unrecognized.</exception>
	public bool AdvertisesErase( TerminalScreenEraseKind kind ) => this.Advertises( kind switch {
		TerminalScreenEraseKind.ToEndOfLine => TerminalScreenAdvertisement.EraseToEndOfLine,
		TerminalScreenEraseKind.ToBeginningOfLine => TerminalScreenAdvertisement.EraseToBeginningOfLine,
		TerminalScreenEraseKind.ToEndOfScreen => TerminalScreenAdvertisement.EraseToEndOfScreen,
		TerminalScreenEraseKind.Screen => TerminalScreenAdvertisement.EraseScreen,
		_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
	} );

	/// <summary>Reports whether a representation of the requested character operation is present.</summary>
	/// <param name="kind">The semantic character operation.</param>
	/// <returns>Whether the static profile advertises an existing planner route for the operation.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The operation kind is unrecognized.</exception>
	public bool AdvertisesCharacterShift( TerminalScreenCharacterShiftKind kind ) => this.Advertises( kind switch {
		TerminalScreenCharacterShiftKind.Insert => TerminalScreenAdvertisement.InsertCharacters,
		TerminalScreenCharacterShiftKind.Delete => TerminalScreenAdvertisement.DeleteCharacters,
		TerminalScreenCharacterShiftKind.Erase => TerminalScreenAdvertisement.EraseCharacters,
		_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
	} );

	/// <summary>Reports whether a representation of the requested line operation is present.</summary>
	/// <param name="kind">The semantic line operation.</param>
	/// <returns>Whether the static profile advertises an existing planner route for the operation.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The operation kind is unrecognized.</exception>
	public bool AdvertisesLineShift( TerminalScreenLineShiftKind kind ) => this.Advertises( kind switch {
		TerminalScreenLineShiftKind.Insert => TerminalScreenAdvertisement.InsertLines,
		TerminalScreenLineShiftKind.Delete => TerminalScreenAdvertisement.DeleteLines,
		TerminalScreenLineShiftKind.ScrollForward => TerminalScreenAdvertisement.ScrollForward,
		TerminalScreenLineShiftKind.ScrollReverse => TerminalScreenAdvertisement.ScrollReverse,
		_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
	} );

	private bool Advertises( TerminalScreenAdvertisement operation ) => 0 != ( this.advertisement & operation );
}

[Flags]
internal enum TerminalScreenAdvertisement {
	None = 0,
	CursorHome = 1 << 0,
	CursorRowAddressing = 1 << 1,
	CursorColumnAddressing = 1 << 2,
	CarriageReturn = 1 << 3,
	CursorUp = 1 << 4,
	CursorDown = 1 << 5,
	CursorLeft = 1 << 6,
	CursorRight = 1 << 7,
	ScrollRegion = 1 << 8,
	EraseToEndOfLine = 1 << 9,
	EraseToBeginningOfLine = 1 << 10,
	EraseToEndOfScreen = 1 << 11,
	EraseScreen = 1 << 12,
	InsertCharacters = 1 << 13,
	DeleteCharacters = 1 << 14,
	EraseCharacters = 1 << 15,
	InsertLines = 1 << 16,
	DeleteLines = 1 << 17,
	ScrollForward = 1 << 18,
	ScrollReverse = 1 << 19
}
