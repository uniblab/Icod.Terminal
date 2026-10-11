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

internal static class TerminalClipboardLimits {
	internal static int GetEncodedLength( int decodedBytes ) {
		if ( decodedBytes < 0 || decodedBytes > TerminalClipboardOptions.SupportedMaximumBytes )
			throw new ArgumentOutOfRangeException( nameof( decodedBytes ) );
		return checked( 4 * ( ( decodedBytes + 2 ) / 3 ) );
	}

	internal static int GetResponseFrameLimit( int maximumDecodedBytes ) {
		TerminalClipboardOptions.ValidateLimit( maximumDecodedBytes, nameof( maximumDecodedBytes ) );
		return checked( GetEncodedLength( maximumDecodedBytes ) + 16 );
	}
}
