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
	internal static byte[][] EncodeOsc99NotificationFrames(
		string title,
		string body,
		KittyNotificationOptions? options
	) {
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( body );
		return TerminalOsc99NotificationEncoder.EncodeNotificationFrames(
			title,
			body,
			options
		);
	}

	internal static byte[] EncodeOsc99CloseFrame(
		string identifier
	) {
		ArgumentNullException.ThrowIfNull( identifier );
		return TerminalOsc99NotificationEncoder.EncodeCloseFrame( identifier );
	}

	internal static async ValueTask WriteOsc99NotificationAsync(
		ITerminalOutput output,
		string title,
		string body,
		KittyNotificationOptions? options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( body );
		cancellationToken.ThrowIfCancellationRequested();
		byte[][] frames = EncodeOsc99NotificationFrames(
			title,
			body,
			options
		);
		cancellationToken.ThrowIfCancellationRequested();

		foreach ( byte[] frame in frames ) {
			await output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}

	internal static ValueTask WriteOsc99CloseAsync(
		ITerminalOutput output,
		string identifier,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( identifier );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = EncodeOsc99CloseFrame( identifier );
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}
}
