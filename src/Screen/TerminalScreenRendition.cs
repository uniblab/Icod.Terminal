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

/// <summary>Identifies terminal-independent text rendition attributes.</summary>
[Flags]
public enum TerminalTextAttributes {
	/// <summary>No special rendition attribute.</summary>
	None = 0,

	/// <summary>Bold or intense rendition.</summary>
	Bold = 1,

	/// <summary>Dim rendition.</summary>
	Dim = 2,

	/// <summary>Underline rendition.</summary>
	Underline = 4,

	/// <summary>Reverse-video rendition.</summary>
	Reverse = 8,

	/// <summary>Standout rendition or a safe semantic equivalent.</summary>
	Standout = 16,

	/// <summary>Italic rendition.</summary>
	Italic = 32,

	/// <summary>Blinking rendition.</summary>
	Blink = 64,

	/// <summary>Concealed or invisible rendition.</summary>
	Conceal = 128,

	/// <summary>Strikeout rendition.</summary>
	Strikeout = 256
}

/// <summary>Identifies the semantic representation of a screen color.</summary>
public enum TerminalScreenColorKind {
	/// <summary>Use the terminal's default color.</summary>
	Default,

	/// <summary>Use an indexed terminal color.</summary>
	Indexed,

	/// <summary>Use an explicit RGB color.</summary>
	Rgb
}

/// <summary>Represents a terminal-independent screen color request.</summary>
public readonly record struct TerminalScreenColor {
	private TerminalScreenColor(
		TerminalScreenColorKind kind,
		int? index,
		byte? red,
		byte? green,
		byte? blue
	) {
		this.Kind = kind;
		this.Index = index;
		this.Red = red;
		this.Green = green;
		this.Blue = blue;
	}

	/// <summary>Gets the terminal-default color.</summary>
	public static TerminalScreenColor Default => default;

	/// <summary>Gets the semantic color representation.</summary>
	public TerminalScreenColorKind Kind {
		get;
	}

	/// <summary>Gets the indexed color number when this is an indexed color.</summary>
	public int? Index {
		get;
	}

	/// <summary>Gets the red component when this is an RGB color.</summary>
	public byte? Red {
		get;
	}

	/// <summary>Gets the green component when this is an RGB color.</summary>
	public byte? Green {
		get;
	}

	/// <summary>Gets the blue component when this is an RGB color.</summary>
	public byte? Blue {
		get;
	}

	/// <summary>Gets whether this color requests the terminal default.</summary>
	public bool IsDefault => TerminalScreenColorKind.Default == this.Kind;

	/// <summary>Creates a non-negative indexed-color request.</summary>
	public static TerminalScreenColor Indexed(
		int index
	) {
		if ( 0 > index ) {
			throw new ArgumentOutOfRangeException( nameof( index ) );
		}
		return new TerminalScreenColor(
			TerminalScreenColorKind.Indexed,
			index,
			null,
			null,
			null
		);
	}

	/// <summary>Creates a direct RGB color request.</summary>
	public static TerminalScreenColor Rgb(
		byte red,
		byte green,
		byte blue
	) {
		return new TerminalScreenColor(
			TerminalScreenColorKind.Rgb,
			null,
			red,
			green,
			blue
		);
	}
}

/// <summary>Describes terminal-independent screen text rendition.</summary>
public readonly record struct TerminalScreenRendition {
	private const TerminalTextAttributes KnownAttributes =
		TerminalTextAttributes.Bold
		| TerminalTextAttributes.Dim
		| TerminalTextAttributes.Underline
		| TerminalTextAttributes.Reverse
		| TerminalTextAttributes.Standout
		| TerminalTextAttributes.Italic
		| TerminalTextAttributes.Blink
		| TerminalTextAttributes.Conceal
		| TerminalTextAttributes.Strikeout;

	/// <summary>Initializes terminal-independent screen rendition.</summary>
	public TerminalScreenRendition(
		TerminalScreenColor foreground,
		TerminalScreenColor background,
		TerminalTextAttributes attributes = TerminalTextAttributes.None
	) {
		if ( 0 != ( attributes & ~KnownAttributes ) ) {
			throw new ArgumentOutOfRangeException( nameof( attributes ) );
		}
		this.Foreground = foreground;
		this.Background = background;
		this.Attributes = attributes;
	}

	/// <summary>Gets terminal-default colors with no attributes.</summary>
	public static TerminalScreenRendition Default => default;

	/// <summary>Gets the foreground color.</summary>
	public TerminalScreenColor Foreground {
		get;
	}

	/// <summary>Gets the background color.</summary>
	public TerminalScreenColor Background {
		get;
	}

	/// <summary>Gets the text attributes.</summary>
	public TerminalTextAttributes Attributes {
		get;
	}

	/// <summary>Gets whether this is terminal-default rendition.</summary>
	public bool IsDefault => default == this;
}
