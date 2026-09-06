namespace Icod.Terminal;

/// <summary>
/// Owns one session-managed dynamic terminal-color request.
/// </summary>
/// <remarks>
/// Dynamic-color leases are identity-aware and may be disposed out of order. The
/// newest active owner for one semantic dynamic-color slot controls the physical
/// terminal value. Releasing the final owner restores the exact color observed
/// before the first owner in the current lifecycle epoch.
/// </remarks>
public sealed class TerminalDynamicColorLease : IAsyncDisposable {
	private readonly long ownerId;
	private readonly SemaphoreSlim operationGate = new( 1, 1 );
	private TerminalDynamicColorManager? owner;

	internal TerminalDynamicColorLease(
		TerminalDynamicColorManager owner,
		long ownerId,
		TerminalDynamicColor kind,
		TerminalColor color
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}
		_ = TerminalDynamicColorProtocol.CreateSetRequest( kind, color );

		this.owner = owner;
		this.ownerId = ownerId;
		this.Kind = kind;
		this.Color = color;
	}

	/// <summary>Gets the semantic dynamic-color identity owned by this lease.</summary>
	public TerminalDynamicColor Kind {
		get;
	}

	/// <summary>Gets the normalized color requested by this lease.</summary>
	public TerminalColor Color {
		get;
	}

	/// <summary>
	/// Releases this logical dynamic-color request and restores the next active owner
	/// or the exact externally observed baseline.
	/// </summary>
	/// <returns>A value task representing asynchronous restoration.</returns>
	/// <remarks>
	/// Repeated successful disposal is idempotent. If physical restoration fails,
	/// ownership is retained so a later disposal attempt can retry cleanup.
	/// </remarks>
	public async ValueTask DisposeAsync() {
		await this.operationGate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			TerminalDynamicColorManager? currentOwner = this.owner;
			if ( currentOwner is null ) {
				return;
			}

			await currentOwner.ReleaseAsync( this.ownerId ).ConfigureAwait( false );
			this.owner = null;
		} finally {
			this.operationGate.Release();
		}
	}

	internal void MarkReleasedByOwner() {
		this.owner = null;
	}
}
