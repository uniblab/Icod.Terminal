namespace Icod.Terminal;

using System.Runtime.ExceptionServices;

/// <summary>
/// Provides semantic OSC 8 hyperlink output for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Writes application text associated with one OSC 8 hyperlink.
	/// </summary>
	/// <param name="value">The application text to write while the hyperlink is active.</param>
	/// <param name="uri">A non-empty, absolute, already URI-encoded hyperlink target.</param>
	/// <param name="identifier">
	/// An optional OSC 8 hyperlink identifier containing only RFC 3986 unreserved ASCII characters.
	/// </param>
	/// <param name="cancellationToken">Cancellation observed before hyperlink transmission begins.</param>
	/// <returns>A value task representing the complete begin/text/end operation.</returns>
	/// <remarks>
	/// This method is the ordinary non-scoped OSC 8 operation for 0.6. It emits a
	/// validated hyperlink begin frame, writes <paramref name="value"/> through the
	/// session application encoding, and emits the canonical hyperlink close frame
	/// while holding the session-owned output serialization boundary.
	///
	/// Successful completion proves emission only. It does not prove that the
	/// terminal recognized OSC 8, rendered a hyperlink, or permits activation of
	/// the supplied URI. The library does not dereference or activate the URI.
	///
	/// If application-text output fails after the begin frame has succeeded, the
	/// session makes one best-effort close attempt before propagating the failure.
	/// If both the application write and cleanup fail, an <see cref="AggregateException"/>
	/// containing both failures is thrown.
	/// </remarks>
	public async ValueTask WriteHyperlinkAsync(
		string value,
		string uri,
		string? identifier = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( uri );
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 8 hyperlink operations require an interactive terminal output endpoint."
			);
		}

		byte[] beginFrame = OscWriter.EncodeHyperlinkBeginFrame(
			uri,
			identifier
		);
		byte[] textBytes = this.applicationEncoding.GetBytes( value );
		byte[] endFrame = OscWriter.EncodeHyperlinkEndFrame();
		cancellationToken.ThrowIfCancellationRequested();

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await this.Output.WriteAsync(
			beginFrame,
			CancellationToken.None
		).ConfigureAwait( false );

		try {
			await this.Output.WriteAsync(
				textBytes,
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception applicationFailure ) {
			try {
				await this.Output.WriteAsync(
					endFrame,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception cleanupFailure ) {
				throw new AggregateException(
					"Hyperlink text output and OSC 8 cleanup both failed.",
					applicationFailure,
					cleanupFailure
				);
			}

			ExceptionDispatchInfo.Capture( applicationFailure ).Throw();
			throw;
		}

		await this.Output.WriteAsync(
			endFrame,
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
