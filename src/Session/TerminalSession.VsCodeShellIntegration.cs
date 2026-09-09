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
/// Provides the typed VS Code OSC 633 shell-integration output surface.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Emits the VS Code OSC 633 prompt-start marker.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask BeginVsCodePromptAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633PromptStartFrame(),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the VS Code OSC 633 command-input-start/prompt-end marker.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask BeginVsCodeCommandInputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633CommandInputStartFrame(),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the VS Code OSC 633 pre-execution/command-output-start marker.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask BeginVsCodeCommandOutputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633CommandOutputStartFrame(),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the VS Code OSC 633 command-finished marker with an explicit exit code.
	/// </summary>
	/// <param name="exitCode">The shell or process exit code to publish as signed decimal text.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask FinishVsCodeCommandAsync(
		int exitCode,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633CommandFinishedFrame( exitCode ),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the bare VS Code OSC 633 command-finished marker for an empty, cancelled, or otherwise status-less command region.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask AbortVsCodeCommandAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633CommandAbortedFrame(),
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes the exact command line using the VS Code OSC 633 E sequence.
	/// </summary>
	/// <param name="commandLine">The caller-supplied command line.</param>
	/// <param name="nonce">Optional VS Code shell-integration nonce used by the terminal to authenticate command metadata.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing command-line publication.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="commandLine"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The command line contains malformed Unicode, the nonce is empty/unsafe/too long, or the encoded payload exceeds the 65,536-byte safety bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// The command line is serialized according to the VS Code OSC 633 escaping rules: backslash is doubled,
	/// semicolon and ASCII characters U+0000 through U+0020 are encoded as <c>\xAB</c>. Publication is explicit
	/// caller intent; Icod.Terminal does not inspect shell history, process arguments, or redact secrets.
	/// </remarks>
	public ValueTask PublishVsCodeCommandLineAsync(
		string commandLine,
		string? nonce = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( commandLine );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc633CommandLineFrame(
			commandLine,
			nonce
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes a current working directory using the typed VS Code OSC 633 Cwd property.
	/// </summary>
	/// <param name="currentDirectory">The caller-supplied current working directory.</param>
	/// <param name="nonce">Optional VS Code shell-integration nonce used by current VS Code builds to mark the update trusted.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing current-directory publication.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="currentDirectory"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The value is empty or malformed Unicode, the nonce is empty/unsafe/too long, or the encoded payload exceeds the 65,536-byte safety bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This is a vendor-specific shell-integration property and does not replace the portable OSC 7
	/// <see cref="PublishCurrentLocationAsync(string, TerminalLocationPathStyle, string?, CancellationToken)"/> API.
	/// The library does not read or normalize the process current directory automatically.
	/// </remarks>
	public ValueTask PublishVsCodeCurrentDirectoryAsync(
		string currentDirectory,
		string? nonce = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( currentDirectory );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc633CurrentDirectoryFrame(
			currentDirectory,
			nonce
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes the VS Code OSC 633 IsWindows property.
	/// </summary>
	/// <param name="isWindows"><see langword="true"/> for a Windows PTY/backend; otherwise <see langword="false"/>.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing property publication.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask PublishVsCodeIsWindowsAsync(
		bool isWindows,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633IsWindowsFrame( isWindows ),
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes the VS Code OSC 633 HasRichCommandDetection property.
	/// </summary>
	/// <param name="hasRichCommandDetection">
	/// <see langword="true"/> when the caller can place A, B, E, C, and D at the protocol-defined boundaries; otherwise <see langword="false"/>.
	/// </param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing property publication.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask PublishVsCodeRichCommandDetectionAsync(
		bool hasRichCommandDetection,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteVsCodeShellIntegrationAsync(
			OscWriter.EncodeOsc633RichCommandDetectionFrame( hasRichCommandDetection ),
			cancellationToken
		);
	}

	private async ValueTask WriteVsCodeShellIntegrationAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 633 VS Code shell integration requires an interactive terminal output endpoint."
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
