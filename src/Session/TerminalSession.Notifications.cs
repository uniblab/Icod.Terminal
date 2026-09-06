namespace Icod.Terminal;

/// <summary>
/// Safe OSC 9 notification integration for <see cref="TerminalSession"/>.
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
	/// The message contains malformed Unicode, C0/C1/DEL controls, or exceeds the 0.16 notification payload bound.
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

	private async ValueTask WriteNotificationAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 9 desktop notifications require an interactive terminal output endpoint."
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
