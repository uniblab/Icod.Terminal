namespace Icod.Terminal;

/// <summary>
/// Owns one session-managed OSC 8 hyperlink state.
/// </summary>
/// <remarks>
/// Hyperlink leases are strictly nested. The most recently acquired active lease
/// must be released first. Releasing an inner lease restores the immediately
/// preceding session-owned hyperlink; releasing the outermost lease emits the
/// canonical OSC 8 close frame.
/// </remarks>
public sealed class TerminalHyperlinkLease : IAsyncDisposable {
	private readonly object sync = new();
	private readonly long leaseId;
	private TerminalHyperlinkManager? owner;
	private Task? disposeTask;

	internal TerminalHyperlinkLease(
		TerminalHyperlinkManager owner,
		long leaseId,
		string uri,
		string? identifier
	) {
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentException.ThrowIfNullOrWhiteSpace( uri );

		this.owner = owner;
		this.leaseId = leaseId;
		this.Uri = uri;
		this.Identifier = identifier;
	}

	/// <summary>Gets the canonical URI text emitted for this hyperlink.</summary>
	public string Uri {
		get;
	}

	/// <summary>Gets the optional OSC 8 hyperlink identifier.</summary>
	public string? Identifier {
		get;
	}

	/// <summary>
	/// Releases this hyperlink state and restores the immediately preceding
	/// session-owned hyperlink, or closes OSC 8 state when this is the outermost lease.
	/// </summary>
	/// <returns>A value task representing asynchronous release.</returns>
	public ValueTask DisposeAsync() {
		lock ( this.sync ) {
			if ( this.owner is null ) {
				return ValueTask.CompletedTask;
			}

			this.disposeTask ??= this.DisposeCoreAsync();
			return new ValueTask( this.disposeTask );
		}
	}

	internal void MarkReleasedByOwner() {
		lock ( this.sync ) {
			this.owner = null;
			this.disposeTask = null;
		}
	}

	private async Task DisposeCoreAsync() {
		TerminalHyperlinkManager? currentOwner;
		lock ( this.sync ) {
			currentOwner = this.owner;
		}
		if ( currentOwner is null ) {
			return;
		}

		bool released = false;
		try {
			await currentOwner.ReleaseAsync( this.leaseId ).ConfigureAwait( false );
			released = true;
		} finally {
			lock ( this.sync ) {
				if ( released ) {
					this.owner = null;
				}
				this.disposeTask = null;
			}
		}
	}
}
