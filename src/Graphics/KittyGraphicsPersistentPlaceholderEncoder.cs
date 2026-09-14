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

using System.Globalization;
using System.Text;

/// <summary>
/// Encodes one acknowledged Kitty Graphics virtual placement for Unicode-placeholder rendering.
/// </summary>
internal static class KittyGraphicsPersistentPlaceholderEncoder {
	internal static ReadOnlyMemory<byte> EncodePlacementPayload(
		uint imageId,
		uint placementId,
		int columns,
		int rows
	) {
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		if ( placementId is 0u or > TerminalPersistentRasterRegistry.MaximumPlaceholderPlacementId ) {
			throw new ArgumentOutOfRangeException( nameof( placementId ) );
		}
		if ( columns is < 1 or > TerminalRasterPlaceholderOptions.MaximumExtent ) {
			throw new ArgumentOutOfRangeException( nameof( columns ) );
		}
		if ( rows is < 1 or > TerminalRasterPlaceholderOptions.MaximumExtent ) {
			throw new ArgumentOutOfRangeException( nameof( rows ) );
		}

		return Encoding.ASCII.GetBytes(
			"Ga=p,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",p="
			+ placementId.ToString( CultureInfo.InvariantCulture )
			+ ",C=1,U=1,c="
			+ columns.ToString( CultureInfo.InvariantCulture )
			+ ",r="
			+ rows.ToString( CultureInfo.InvariantCulture )
		);
	}
}
