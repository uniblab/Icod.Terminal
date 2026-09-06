namespace Icod.Terminal;

/// <summary>
/// Owns one session-managed indexed terminal-palette color request.
/// </summary>
/// <remarks>
/// Palette-color leases are identity-aware and may be disposed out of order. The newest
/// active owner for one palette index controls that physical color. Releasing the final
/// owner restores the exact color observed before the first owner mutated that index.
/// </remarks>
public sealed class TerminalPaletteColorLease : IAsyncDisposable {
	private readonly long ownerId;
	private readonly SemaphoreSlim operationGate = new( 1, 1 );
	private TerminalPaletteColorManager? owner;

	internal TerminalPaletteColorLease(
		TerminalPaletteColorManager owner,
		long ownerId,
		byte index,
		TerminalColor color
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}

		this.owner = owner;
		this.ownerId = ownerId;
		this.Index = index;
		this.Color = color;
	}

	/// <summary>Gets the palette index owned by this lease.</summary>
	public byte Index {
		get;
	}

	/// <summary>Gets the requested normalized color owned by this lease.</summary>
	public TerminalColor Color {
		get;
	}

	/// <summary>
	/// Releases this logical palette-color request.
	/// </summary>
	/// <returns>A value task representing asynchronous restoration.</returns>
	/// <remarks>
	/// Repeated successful disposal is idempotent. If restoration fails, ownership is
	/// retained so a later disposal attempt can retry cleanup.
	/// </remarks>
	public async ValueTask DisposeAsync() {
		await this.operationGate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			TerminalPaletteColorManager? currentOwner = this.owner;
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
