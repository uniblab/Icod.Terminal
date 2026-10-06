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
/// Owns one request for terminal-reported in-band resize observations.
/// </summary>
public sealed class TerminalInBandResizeReportingLease :
	IAsyncDisposable,
	ITerminalEnvironmentReportingLease {
	private readonly SemaphoreSlim operationGate = new( 1, 1 );
	private TerminalEnvironmentReportingManager? owner;

	internal TerminalInBandResizeReportingLease(
		TerminalEnvironmentReportingManager owner,
		long ownerId
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}
		this.owner = owner;
		this.OwnerId = ownerId;
	}

	internal long OwnerId {
		get;
	}

	/// <summary>Releases this in-band resize-reporting request.</summary>
	/// <returns>A value task representing asynchronous restoration.</returns>
	/// <remarks>
	/// Repeated successful disposal is idempotent. A failed restoration retains
	/// ownership so a later disposal attempt can retry cleanup.
	/// </remarks>
	public async ValueTask DisposeAsync() {
		await this.operationGate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			TerminalEnvironmentReportingManager? currentOwner = this.owner;
			if ( currentOwner is null ) {
				return;
			}
			using IDisposable composition = await currentOwner.AcquireCompositionAsync(
				CancellationToken.None
			).ConfigureAwait( false );
			await currentOwner.ReleaseAsync(
				TerminalEnvironmentReportingKind.InBandResize,
				this.OwnerId
			).ConfigureAwait( false );
			this.owner = null;
		} finally {
			this.operationGate.Release();
		}
	}

	void ITerminalEnvironmentReportingLease.MarkReleasedByOwner() {
		this.owner = null;
	}
}
