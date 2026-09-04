namespace Icod.Terminal;

/// <summary>
/// Provides semantic OSC 8 hyperlink output for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalHyperlinkManager? hyperlinkManager;

	private TerminalHyperlinkManager HyperlinkManager {
		get {
			return this.hyperlinkManager ??= new TerminalHyperlinkManager( this );
		}
	}

	/// <summary>
	/// Acquires one session-owned OSC 8 hyperlink scope.
	/// </summary>
	/// <param name="uri">A non-empty, absolute, already URI-encoded hyperlink target.</param>
	/// <param name="identifier">
	/// An optional OSC 8 hyperlink identifier containing only RFC 3986 unreserved ASCII characters.
	/// </param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A value task containing the acquired hyperlink lease.</returns>
	/// <remarks>
	/// Hyperlink leases are strictly nested. Releasing the innermost lease restores
	/// the immediately preceding session-owned hyperlink. Releasing the outermost
	/// lease emits the canonical OSC 8 close frame. Cleanup is not caller-cancellable.
	/// Active logical scopes survive managed terminal suspend/resume: physical OSC 8
	/// state is closed before suspension and the innermost active scope is re-emitted
	/// after successful session-state re-entry.
	/// </remarks>
	public ValueTask<TerminalHyperlinkLease> AcquireHyperlinkAsync(
		string uri,
		string? identifier = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( uri );
		cancellationToken.ThrowIfCancellationRequested();

		return this.HyperlinkManager.AcquireAsync(
			uri,
			identifier,
			cancellationToken
		);
	}

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
	/// This bounded convenience operation uses the same persistent ownership manager
	/// as <see cref="AcquireHyperlinkAsync(string,string?,CancellationToken)"/>. The
	/// session owns one serialized begin/text/end sequence, and a failed close leaves
	/// the hyperlink recorded for later retry or final session-disposal cleanup.
	///
	/// Successful completion proves emission only. It does not prove that the
	/// terminal recognized OSC 8, rendered a hyperlink, or permits activation of
	/// the supplied URI. The library does not dereference or activate the URI.
	/// </remarks>
	public ValueTask WriteHyperlinkAsync(
		string value,
		string uri,
		string? identifier = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( uri );
		cancellationToken.ThrowIfCancellationRequested();

		return this.HyperlinkManager.WriteBoundedAsync(
			value,
			uri,
			identifier,
			cancellationToken
		);
	}

	private async ValueTask<Exception?> CloseHyperlinkStateAsync() {
		if ( this.hyperlinkManager is null ) {
			return null;
		}

		try {
			await this.hyperlinkManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
