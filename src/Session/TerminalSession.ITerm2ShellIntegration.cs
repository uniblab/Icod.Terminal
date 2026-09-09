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
/// Typed iTerm2 OSC 1337 shell-integration and semantic-history metadata.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Records an iTerm2 semantic-history mark at the current terminal position.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing complete frame emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask SetITerm2MarkAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc1337SetMarkFrame();
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteITerm2ShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes an explicit current directory to iTerm2 semantic history through OSC 1337.
	/// </summary>
	/// <param name="currentDirectory">The caller-supplied current directory. The library does not read or normalize process state.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing complete frame emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="currentDirectory"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The directory is empty, contains malformed Unicode or framing controls, or exceeds the OSC 1337 payload bound.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask PublishITerm2CurrentDirectoryAsync(
		string currentDirectory,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( currentDirectory );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc1337CurrentDirectoryFrame(
			currentDirectory
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteITerm2ShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes an explicit user and host name to iTerm2 semantic history through OSC 1337.
	/// </summary>
	/// <param name="userName">The caller-supplied user name.</param>
	/// <param name="hostName">The caller-supplied fully-qualified host name.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing complete frame emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="hostName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A field is empty, malformed, contains a protocol delimiter/control, or exceeds its bound.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask PublishITerm2RemoteHostAsync(
		string userName,
		string hostName,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( userName );
		ArgumentNullException.ThrowIfNull( hostName );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc1337RemoteHostFrame(
			userName,
			hostName
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteITerm2ShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Sets one iTerm2 user variable using strict UTF-8 text encoded as the protocol-defined Base64 value.
	/// </summary>
	/// <param name="name">The user-variable key.</param>
	/// <param name="value">The user-variable value. Empty text is allowed.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing complete frame emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The name/value is malformed or the resulting payload exceeds the OSC 1337 bound.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	public ValueTask SetITerm2UserVariableAsync(
		string name,
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( value );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc1337SetUserVariableFrame(
			name,
			value
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteITerm2ShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Publishes the caller's iTerm2 shell-integration script version and shell identity.
	/// </summary>
	/// <param name="version">The non-negative integration-script version.</param>
	/// <param name="shellName">The shell name, such as <c>bash</c>, <c>zsh</c>, or <c>fish</c>.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing complete frame emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="shellName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The shell name is empty or not a bounded ASCII shell identifier.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// iTerm2 may use this value to decide whether to offer a shell-integration upgrade. The library does not discover
	/// the installed shell script, infer a version, modify shell startup files, or install shell integration.
	/// </remarks>
	public ValueTask PublishITerm2ShellIntegrationVersionAsync(
		int version,
		string shellName,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( shellName );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc1337ShellIntegrationVersionFrame(
			version,
			shellName
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteITerm2ShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Clears iTerm2's current semantic-history captured-output record.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing complete frame emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This is an explicit destructive metadata operation. It does not clear the visible screen or ordinary scrollback,
	/// and it is never emitted automatically by session open, resume, or disposal.
	/// </remarks>
	public ValueTask ClearITerm2CapturedOutputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc1337ClearCapturedOutputFrame();
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteITerm2ShellIntegrationAsync(
			frame,
			cancellationToken
		);
	}

	private async ValueTask WriteITerm2ShellIntegrationAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"iTerm2 OSC 1337 semantic metadata requires an interactive terminal output endpoint."
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
