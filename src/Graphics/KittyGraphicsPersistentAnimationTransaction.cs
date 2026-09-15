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

internal sealed class KittyGraphicsPersistentAnimationAppendCommitment {
	private int committed;

	internal bool IsCommitted {
		get {
			return 0 != Volatile.Read( ref this.committed );
		}
	}

	internal void MarkCommitted() {
		Interlocked.Exchange(
			ref this.committed,
			1
		);
	}
}

internal static class KittyGraphicsPersistentAnimationTransaction {
	internal static async ValueTask WriteAsync(
		TerminalSession session,
		KittyRasterData raster,
		uint imageId,
		int gapMilliseconds,
		KittyGraphicsPersistentAnimationAppendCommitment commitment
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( raster );
		ArgumentNullException.ThrowIfNull( commitment );
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		if ( gapMilliseconds <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( gapMilliseconds ) );
		}

		IEnumerable<ReadOnlyMemory<byte>> payloads =
			KittyGraphicsPersistentAnimationEncoder.EncodeFramePayloads(
				raster,
				imageId,
				gapMilliseconds
			);
		using IEnumerator<ReadOnlyMemory<byte>> enumerator = payloads.GetEnumerator();
		if ( !enumerator.MoveNext() ) {
			throw new InvalidOperationException(
				"The persistent Kitty Graphics animation encoder produced no payload for a non-empty raster."
			);
		}

		bool first = true;
		do {
			ReadOnlyMemory<byte> payload = enumerator.Current;
			if ( payload.IsEmpty ) {
				throw new InvalidOperationException(
					"The persistent Kitty Graphics animation encoder produced an empty payload."
				);
			}

			byte[] frame = ApcWriter.EncodeFrame( payload.Span );
			await session.Output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );
			if ( first ) {
				commitment.MarkCommitted();
				first = false;
			}
		} while ( enumerator.MoveNext() );

		await session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
