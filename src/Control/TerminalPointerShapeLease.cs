namespace Icod.Terminal;

/// <summary>
/// Owns one session-managed terminal mouse-pointer shape request.
/// </summary>
/// <remarks>
/// Pointer-shape leases are identity-aware and may be disposed out of order. The
/// newest active owner controls physical pointer shape. Releasing the final owner
/// resets OSC 22 pointer state to terminal policy rather than claiming restoration
/// of an unknown pre-lease external pointer shape.
/// </remarks>
public sealed class TerminalPointerShapeLease : IAsyncDisposable {
	private readonly long ownerId;
	private readonly SemaphoreSlim operationGate = new( 1, 1 );
	private TerminalPointerShapeManager? owner;

	internal TerminalPointerShapeLease(
		TerminalPointerShapeManager owner,
		long ownerId,
		TerminalPointerShape shape
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}
		_ = TerminalPointerShapeCodec.GetWireName( shape );

		this.owner = owner;
		this.ownerId = ownerId;
		this.Shape = shape;
	}

	/// <summary>Gets the semantic pointer shape owned by this lease.</summary>
	public TerminalPointerShape Shape {
		get;
	}

	/// <summary>
	/// Releases this logical pointer-shape request.
	/// </summary>
	/// <returns>A value task representing asynchronous restoration or final reset.</returns>
	/// <remarks>
	/// Successful repeated disposal is idempotent. If physical restoration or reset
	/// fails, ownership is retained so a later disposal attempt can retry cleanup.
	/// </remarks>
	public async ValueTask DisposeAsync() {
		await this.operationGate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			TerminalPointerShapeManager? currentOwner = this.owner;
			if ( currentOwner is null ) {
				return;
			}

			await currentOwner.ReleaseAsync( this.ownerId ).ConfigureAwait( false );
			this.owner = null;
		} finally {
			this.operationGate.Release();
		}
	}
}
