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
/// Typed Kitty OSC 99 desktop-notification operations for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Emits one bounded Kitty OSC 99 notification, automatically chunking title, body, and optional icon data.
	/// </summary>
	/// <param name="title">The notification title. May be empty when <paramref name="body"/> is non-empty.</param>
	/// <param name="body">The notification body. May be empty when <paramref name="title"/> is non-empty.</param>
	/// <param name="options">Optional Kitty notification metadata and update/icon policy.</param>
	/// <param name="cancellationToken">Cancellation observed before the first notification frame is committed.</param>
	/// <returns>A value task representing complete notification-frame emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="title"/> or <paramref name="body"/> is null.</exception>
	/// <exception cref="ArgumentException">The request contains invalid or oversized Kitty notification data.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option enum or expiration value is outside its supported range.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before the first frame is committed.</exception>
	/// <remarks>
	/// Title/body payloads are encoded as strict UTF-8 followed by RFC 4648 Base64 and are chunked to the
	/// Kitty protocol's 4096 encoded-byte payload limit. Reusing <see cref="KittyNotificationOptions.Identifier"/>
	/// requests notification replacement/update when the terminal supports that behavior. If multiple frames are
	/// required and no identifier is supplied, the library creates an internal safe identifier solely to associate
	/// those chunks. Successful completion proves emission, not terminal support or desktop display.
	/// </remarks>
	public ValueTask SendKittyNotificationAsync(
		string title,
		string body = "",
		KittyNotificationOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( body );
		cancellationToken.ThrowIfCancellationRequested();
		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			title,
			body,
			options
		);
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteKittyNotificationFramesAsync(
			frames,
			cancellationToken
		);
	}

	/// <summary>
	/// Requests closure of a previously identified Kitty OSC 99 notification.
	/// </summary>
	/// <param name="identifier">The notification identifier used when the notification was sent.</param>
	/// <param name="cancellationToken">Cancellation observed before the close request is committed.</param>
	/// <returns>A value task representing close-request emission.</returns>
	/// <exception cref="ArgumentException">The identifier is empty, reserved, oversized, or contains invalid characters.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before commit.</exception>
	public ValueTask CloseKittyNotificationAsync(
		string identifier,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( identifier );
		cancellationToken.ThrowIfCancellationRequested();
		byte[] frame = OscWriter.EncodeOsc99CloseFrame( identifier );
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteNotificationAsync(
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Explicitly queries Kitty OSC 99 desktop-notification capabilities.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's query wait.</param>
	/// <returns>A typed capability observation reported by the terminal.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed OSC 99 support response.</exception>
	/// <remarks>
	/// This is an explicit active query. A timeout is not converted into a claim that OSC 99 is unsupported.
	/// The generated query identifier is internal and unique for response correlation/multiplexer routing.
	/// </remarks>
	public async ValueTask<KittyNotificationSupport> QueryKittyNotificationSupportAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		string identifier = TerminalOsc99Protocol.CreateQueryIdentifier();
		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalOsc99Protocol.CreateSupportQueryRequest( identifier ),
			TerminalOsc99Protocol.CreateSupportResponseMatcher( identifier ),
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalOsc99Protocol.ParseSupportResponse(
			frame,
			identifier
		);
	}

	/// <summary>
	/// Explicitly queries which identified Kitty OSC 99 notifications remain alive.
	/// </summary>
	/// <param name="timeout">The caller-visible response timeout.</param>
	/// <param name="cancellationToken">Cancellation for the caller's query wait.</param>
	/// <returns>The bounded list of terminal-reported live notification identifiers.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is outside the supported query range.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The response deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated malformed OSC 99 alive response.</exception>
	public async ValueTask<IReadOnlyList<string>> QueryKittyAliveNotificationsAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		string identifier = TerminalOsc99Protocol.CreateQueryIdentifier();
		TerminalResponseFrame frame = await this.ExecuteQueryAsync(
			TerminalOsc99Protocol.CreateAliveQueryRequest( identifier ),
			TerminalOsc99Protocol.CreateAliveResponseMatcher( identifier ),
			timeout,
			cancellationToken
		).ConfigureAwait( false );
		return TerminalOsc99Protocol.ParseAliveResponse(
			frame,
			identifier
		);
	}

	private async ValueTask WriteKittyNotificationFramesAsync(
		byte[][] frames,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frames );
		if ( 0 == frames.Length ) {
			throw new ArgumentException(
				"A Kitty OSC 99 notification requires at least one encoded frame.",
				nameof( frames )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Kitty OSC 99 desktop notifications require an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		foreach ( byte[] frame in frames ) {
			ArgumentNullException.ThrowIfNull( frame );
			await this.Output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}
}
