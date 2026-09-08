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

using Icod.TermInfo;

/// <summary>
/// Acquires reversible host output-mode state needed for terminal protocol output.
/// </summary>
internal static class SystemTerminalOutputSetup {
	internal static IDisposable? Configure(
		ITerminalControlProvider controlProvider,
		TerminalEndpoint endpoint,
		TerminalEndpointObservation observation,
		bool configureOutput
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( observation );

		if ( !configureOutput
			|| !ReferenceEquals(
				controlProvider,
				SystemTerminalControlProvider.Instance
			)
			|| !OperatingSystem.IsWindows()
			|| !observation.IsTerminal
			|| ( TerminalPlatformKind.WindowsConsole != observation.Platform ) ) {
			return null;
		}

		TerminalStandardStream stream = endpoint.Kind switch {
			TerminalEndpointKind.FileDescriptor when 1 == endpoint.FileDescriptor =>
				TerminalStandardStream.Output,
			TerminalEndpointKind.FileDescriptor when 2 == endpoint.FileDescriptor =>
				TerminalStandardStream.Error,
			_ => throw new NotSupportedException(
				string.Concat(
					"Automatic Windows virtual-terminal output setup is available only for ",
					"process standard output or standard error. Set ConfigureOutput to false ",
					"only when a caller-owned endpoint is already configured."
				)
			)
		};

		IDisposable? lease = WindowsVirtualTerminal.TryEnableOutput( stream );
		if ( lease is null ) {
			throw new InvalidOperationException(
				string.Concat(
					"Windows virtual-terminal output processing could not be enabled for ",
					endpoint.DisplayName,
					"."
				)
			);
		}

		return lease;
	}
}
