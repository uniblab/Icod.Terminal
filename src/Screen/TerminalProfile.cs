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

using System.Collections.ObjectModel;
using Icod.TermInfo;

/// <summary>Provides an immutable Terminal-owned semantic view of the selected terminal profile.</summary>
public sealed class TerminalProfile {
	private const int NoColorVideoStandout = 1;
	private const int NoColorVideoUnderline = 2;
	private const int NoColorVideoReverse = 4;
	private const int NoColorVideoBlink = 8;
	private const int NoColorVideoDim = 16;
	private const int NoColorVideoBold = 32;
	private const int NoColorVideoInvisible = 64;
	private const int NoColorVideoItalic = 32768;

	private TerminalProfile(
		string name,
		string? description,
		IReadOnlyList<string> aliases,
		TerminalScreenCapabilities screen
	) {
		this.Name = name;
		this.Description = description;
		this.Aliases = aliases;
		this.Screen = screen;
	}

	/// <summary>Gets the selected terminal profile name.</summary>
	public string Name {
		get;
	}

	/// <summary>Gets the optional human-readable profile description.</summary>
	public string? Description {
		get;
	}

	/// <summary>Gets immutable alternative names for the selected profile.</summary>
	public IReadOnlyList<string> Aliases {
		get;
	}

	/// <summary>Gets semantic screen capabilities derived from the selected profile.</summary>
	public TerminalScreenCapabilities Screen {
		get;
	}

	internal static TerminalProfile Create(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		TerminalColorSupport colors = TerminalColors.GetColorSupport( terminal );
		TerminalTextAttributes supportedAttributes = GetSupportedAttributes( terminal );
		bool alternateCharacterSet =
			null != terminal.GetString( StringCapability.AlternateCharacterSet )
			&& null != terminal.GetString( StringCapability.EnterAlternateCharacterSetMode )
			&& null != terminal.GetString( StringCapability.ExitAlternateCharacterSetMode );

		return new TerminalProfile(
			terminal.Name,
			terminal.Description,
			new ReadOnlyCollection<string>( terminal.Aliases.ToArray() ),
			new TerminalScreenCapabilities(
				colors.IndexedColorCount,
				TerminalColorModel.DirectRgb == colors.Model,
				colors.HasForegroundSelector,
				colors.HasBackgroundSelector,
				colors.HasOriginalColorPair,
				supportedAttributes,
				TranslateNoColorVideoMask( colors.NoColorVideoMask )
					& supportedAttributes,
				null != terminal.GetString( StringCapability.CursorAddress ),
				alternateCharacterSet,
				null != terminal.GetString( StringCapability.CursorInvisible ),
				null != terminal.GetString( StringCapability.CursorNormal ),
				null != terminal.GetString( StringCapability.CursorVeryVisible ),
				GetAdvertisement( terminal )
			)
		);
	}

	private static TerminalScreenAdvertisement GetAdvertisement( TerminalDescription terminal ) {
		TerminalScreenAdvertisement result = TerminalScreenAdvertisement.None;
		Add( TerminalScreenAdvertisement.CursorHome, StringCapability.CursorHome );
		Add( TerminalScreenAdvertisement.CursorRowAddressing, StringCapability.RowAddress );
		Add( TerminalScreenAdvertisement.CursorColumnAddressing, StringCapability.ColumnAddress );
		Add( TerminalScreenAdvertisement.CarriageReturn, StringCapability.CarriageReturn );
		Add( TerminalScreenAdvertisement.CursorUp, StringCapability.CursorUp, StringCapability.CursorUpOne );
		Add( TerminalScreenAdvertisement.CursorDown, StringCapability.CursorDown, StringCapability.CursorDownOne );
		Add( TerminalScreenAdvertisement.CursorLeft, StringCapability.CursorLeft, StringCapability.CursorLeftOne );
		Add( TerminalScreenAdvertisement.CursorRight, StringCapability.CursorRight, StringCapability.CursorRightOne );
		Add( TerminalScreenAdvertisement.ScrollRegion, StringCapability.ChangeScrollRegion );
		Add( TerminalScreenAdvertisement.EraseToEndOfLine, StringCapability.ClearToEndOfLine );
		Add( TerminalScreenAdvertisement.EraseToBeginningOfLine, StringCapability.ClearToBeginningOfLine );
		Add( TerminalScreenAdvertisement.EraseToEndOfScreen, StringCapability.ClearToEndOfScreen );
		Add( TerminalScreenAdvertisement.EraseScreen, StringCapability.ClearScreen );
		Add( TerminalScreenAdvertisement.InsertCharacters, StringCapability.InsertCharacters, StringCapability.InsertCharacter );
		Add( TerminalScreenAdvertisement.DeleteCharacters, StringCapability.DeleteCharacters, StringCapability.DeleteCharacter );
		Add( TerminalScreenAdvertisement.EraseCharacters, StringCapability.EraseCharacters );
		Add( TerminalScreenAdvertisement.InsertLines, StringCapability.InsertLines, StringCapability.InsertLine );
		Add( TerminalScreenAdvertisement.DeleteLines, StringCapability.DeleteLines, StringCapability.DeleteLine );
		Add( TerminalScreenAdvertisement.ScrollForward, StringCapability.ScrollForwardLines, StringCapability.ScrollForward );
		Add( TerminalScreenAdvertisement.ScrollReverse, StringCapability.ScrollReverseLines, StringCapability.ScrollReverse );
		return result;

		void Add( TerminalScreenAdvertisement operation, StringCapability primary, StringCapability? alternate = null ) {
			if ( null != terminal.GetString( primary )
				|| ( alternate.HasValue && null != terminal.GetString( alternate.Value ) ) ) {
				result |= operation;
			}
		}
	}
	private static TerminalTextAttributes GetSupportedAttributes(
		TerminalDescription terminal
	) {
		TerminalTextAttributes result = TerminalTextAttributes.None;
		if ( null != terminal.GetString( StringCapability.EnterBoldMode ) ) {
			result |= TerminalTextAttributes.Bold;
		}
		if ( null != terminal.GetString( StringCapability.EnterDimMode ) ) {
			result |= TerminalTextAttributes.Dim;
		}
		if ( null != terminal.GetString( StringCapability.EnterUnderlineMode ) ) {
			result |= TerminalTextAttributes.Underline;
		}
		if ( null != terminal.GetString( StringCapability.EnterReverseMode ) ) {
			result |= TerminalTextAttributes.Reverse;
		}
		if ( null != terminal.GetString( StringCapability.EnterStandoutMode ) ) {
			result |= TerminalTextAttributes.Standout;
		}
		if ( null != terminal.GetString( StringCapability.EnterItalicMode ) ) {
			result |= TerminalTextAttributes.Italic;
		}
		if ( null != terminal.GetString( StringCapability.EnterBlinkMode ) ) {
			result |= TerminalTextAttributes.Blink;
		}
		if ( null != terminal.GetString( StringCapability.EnterInvisibleMode ) ) {
			result |= TerminalTextAttributes.Conceal;
		}
		if ( terminal.TryGetExtendedString( "smxx", out _ ) ) {
			result |= TerminalTextAttributes.Strikeout;
		}
		return result;
	}

	private static TerminalTextAttributes TranslateNoColorVideoMask(
		int? mask
	) {
		if ( !mask.HasValue ) {
			return TerminalTextAttributes.None;
		}
		int value = mask.Value;
		TerminalTextAttributes result = TerminalTextAttributes.None;
		if ( 0 != ( value & NoColorVideoStandout ) ) {
			result |= TerminalTextAttributes.Standout;
		}
		if ( 0 != ( value & NoColorVideoUnderline ) ) {
			result |= TerminalTextAttributes.Underline;
		}
		if ( 0 != ( value & NoColorVideoReverse ) ) {
			result |= TerminalTextAttributes.Reverse;
		}
		if ( 0 != ( value & NoColorVideoBlink ) ) {
			result |= TerminalTextAttributes.Blink;
		}
		if ( 0 != ( value & NoColorVideoDim ) ) {
			result |= TerminalTextAttributes.Dim;
		}
		if ( 0 != ( value & NoColorVideoBold ) ) {
			result |= TerminalTextAttributes.Bold;
		}
		if ( 0 != ( value & NoColorVideoInvisible ) ) {
			result |= TerminalTextAttributes.Conceal;
		}
		if ( 0 != ( value & NoColorVideoItalic ) ) {
			result |= TerminalTextAttributes.Italic;
		}
		return result;
	}
}
