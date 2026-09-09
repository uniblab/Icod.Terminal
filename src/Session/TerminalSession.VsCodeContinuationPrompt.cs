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
/// Provides the typed VS Code OSC 633 continuation-prompt property.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Publishes the VS Code OSC 633 ContinuationPrompt property.
	/// </summary>
	/// <param name="continuationPrompt">
	/// The caller-supplied prompt text printed at the start of a continued multi-line input.
	/// </param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing property publication.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="continuationPrompt"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The prompt contains malformed Unicode or the encoded payload exceeds the 65,536-byte safety bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// The property value uses the same VS Code OSC 633 escaping rules as command-line and current-directory metadata.
	/// Empty prompt text is valid. This property does not enable or emit the unfinalized OSC 633 continuation markers.
	/// </remarks>
	public ValueTask PublishVsCodeContinuationPromptAsync(
		string continuationPrompt,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( continuationPrompt );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc633ContinuationPromptFrame(
			continuationPrompt
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}
}
