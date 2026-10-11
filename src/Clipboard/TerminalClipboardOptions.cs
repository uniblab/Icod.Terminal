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

/// <summary>Sets independent local resource budgets for explicit clipboard operations.</summary>
/// <remarks>These limits do not negotiate or establish the terminal's clipboard capacity.</remarks>
public sealed class TerminalClipboardOptions {
	/// <summary>Gets the default decoded payload limit of 65,536 bytes.</summary>
	public static int DefaultMaximumBytes => 65_536;

	/// <summary>Gets the largest supported local decoded payload budget, 16 MiB.</summary>
	public static int SupportedMaximumBytes => 16_777_216;

	/// <summary>Gets or initializes the maximum decoded bytes returned by a clipboard read.</summary>
	public int MaximumReadBytes { get; init; } = 65_536;

	/// <summary>Gets or initializes the maximum bytes sent by a clipboard write.</summary>
	/// <remarks>Text is counted as strict UTF-8 bytes without a byte-order mark.</remarks>
	public int MaximumWriteBytes { get; init; } = 65_536;

	internal void Validate() {
		ValidateLimit( this.MaximumReadBytes, nameof( this.MaximumReadBytes ) );
		ValidateLimit( this.MaximumWriteBytes, nameof( this.MaximumWriteBytes ) );
	}

	internal static void ValidateLimit( int value, string name ) {
		if ( value < 1 || value > SupportedMaximumBytes ) {
			throw new ArgumentOutOfRangeException( name, value,
				$"Clipboard capacity must be between 1 and {SupportedMaximumBytes} decoded bytes." );
		}
	}
}
