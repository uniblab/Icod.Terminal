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
