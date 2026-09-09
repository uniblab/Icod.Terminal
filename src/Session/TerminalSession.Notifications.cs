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
/// Safe semantic desktop-notification integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Emits one legacy OSC 9 desktop-notification request with bounded, validated text.
	/// </summary>
	/// <param name="message">The notification message. Empty text is allowed.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing notification emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The message contains malformed Unicode, C0/C1/DEL controls, or exceeds the 4,096-byte OSC payload bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// Successful completion proves only that the complete OSC 9 request was written. It does not prove
	/// that a desktop notification was displayed. Terminal/user policy may suppress the request. This
	/// operation does not flush and does not perform terminal-brand detection or notification capability probing.
	/// The complete frame is validated and encoded before waiting for the shared session output gate.
	/// </remarks>
	public ValueTask SendNotificationAsync(
		string message,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( message );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc9NotificationFrame( message );
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteNotificationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Emits one urxvt-style OSC 777 desktop-notification request with separate title and message fields.
	/// </summary>
	/// <param name="title">The notification title. Empty text is allowed.</param>
	/// <param name="message">The notification body/message. Empty text is allowed.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing notification emission.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="title"/> or <paramref name="message"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// A field contains malformed Unicode, a semicolon, C0/C1/DEL controls, or the complete encoded OSC payload exceeds 4,096 bytes.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This method emits exactly <c>OSC 777;notify;&lt;title&gt;;&lt;message&gt; ST</c> using strict UTF-8 and
	/// canonical ST termination. OSC 777 defines no interoperable escaping grammar for semicolon-delimited fields,
	/// so semicolons are rejected rather than rewritten. Successful completion proves only complete frame emission;
	/// terminal/user policy may suppress display. The library does not infer OSC 777 support from terminal identity.
	/// </remarks>
	public ValueTask SendTitledNotificationAsync(
		string title,
		string message,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( message );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc777NotificationFrame(
			title,
			message
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteNotificationAsync(
			frame,
			cancellationToken
		);
	}

	private async ValueTask WriteNotificationAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Desktop notification protocols require an interactive terminal output endpoint."
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
