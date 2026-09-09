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
	internal static byte[] EncodeOsc1337SetMarkFrame() =>
		TerminalOsc1337ShellIntegrationEncoder.EncodeSetMarkFrame();

	internal static byte[] EncodeOsc1337CurrentDirectoryFrame(
		string currentDirectory
	) {
		ArgumentNullException.ThrowIfNull( currentDirectory );
		return TerminalOsc1337ShellIntegrationEncoder.EncodeCurrentDirectoryFrame(
			currentDirectory
		);
	}

	internal static byte[] EncodeOsc1337RemoteHostFrame(
		string userName,
		string hostName
	) {
		ArgumentNullException.ThrowIfNull( userName );
		ArgumentNullException.ThrowIfNull( hostName );
		return TerminalOsc1337ShellIntegrationEncoder.EncodeRemoteHostFrame(
			userName,
			hostName
		);
	}

	internal static byte[] EncodeOsc1337SetUserVariableFrame(
		string name,
		string value
	) {
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( value );
		return TerminalOsc1337ShellIntegrationEncoder.EncodeSetUserVariableFrame(
			name,
			value
		);
	}

	internal static byte[] EncodeOsc1337ShellIntegrationVersionFrame(
		int version,
		string shellName
	) {
		ArgumentNullException.ThrowIfNull( shellName );
		return TerminalOsc1337ShellIntegrationEncoder.EncodeShellIntegrationVersionFrame(
			version,
			shellName
		);
	}

	internal static byte[] EncodeOsc1337ClearCapturedOutputFrame() =>
		TerminalOsc1337ShellIntegrationEncoder.EncodeClearCapturedOutputFrame();

	internal static ValueTask WriteOsc1337FrameAsync(
		ITerminalOutput output,
		byte[] frame,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}
}
