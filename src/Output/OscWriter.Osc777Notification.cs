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

internal static partial class OscWriter {
	/// <summary>
	/// Encodes one complete canonical urxvt-style OSC 777 desktop-notification frame.
	/// </summary>
	internal static byte[] EncodeOsc777NotificationFrame(
		string title,
		string message
	) {
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( message );
		return TerminalOsc777NotificationEncoder.EncodeFrame(
			title,
			message
		);
	}

	/// <summary>
	/// Validates and emits one complete OSC 777 desktop-notification frame through one output write.
	/// </summary>
	internal static ValueTask WriteOsc777NotificationAsync(
		ITerminalOutput output,
		string title,
		string message,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( message );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc777NotificationFrame(
			title,
			message
		);
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}
}
