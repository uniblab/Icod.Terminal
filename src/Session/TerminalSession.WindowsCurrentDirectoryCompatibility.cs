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

/// <summary>
/// Provides explicit OSC 9;9 Windows-current-directory compatibility publication.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Publishes one caller-supplied Windows filesystem path using the OSC 9;9 compatibility form.
	/// </summary>
	/// <param name="windowsPath">The Windows filesystem path to publish.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing compatibility-hint emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="windowsPath"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The path is empty, contains malformed Unicode or C0/C1/DEL controls, or exceeds the 32,768-byte OSC payload bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This is an explicitly secondary Windows Terminal/ConEmu compatibility mechanism. The portable
	/// <see cref="PublishCurrentLocationAsync(string, TerminalLocationPathStyle, string?, CancellationToken)"/>
	/// OSC 7 API remains the preferred current-location protocol. This method never translates a POSIX path,
	/// inspects the process current directory, probes terminal identity, or emits OSC 7 automatically.
	/// The complete frame is validated and encoded before waiting for the shared session output gate.
	/// </remarks>
	public ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
		string windowsPath,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( windowsPath );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame(
			windowsPath
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteWindowsCurrentDirectoryCompatibilityAsync(
			frame,
			cancellationToken
		);
	}

	private async ValueTask WriteWindowsCurrentDirectoryCompatibilityAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 9;9 current-directory compatibility publication requires an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await this.Output.WriteAsync(
			frame,
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
