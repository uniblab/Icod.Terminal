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

internal sealed class KittyGraphicsPersistentAnimationEmissionState {
	private int hasStarted;
	private int outputCommitted;

	internal bool HasStarted {
		get {
			return 0 != Volatile.Read( ref this.hasStarted );
		}
	}

	internal bool OutputCommitted {
		get {
			return 0 != Volatile.Read( ref this.outputCommitted );
		}
	}

	internal void MarkStarted() {
		Interlocked.Exchange(
			ref this.hasStarted,
			1
		);
	}

	internal void MarkOutputCommitted() {
		this.MarkStarted();
		Interlocked.Exchange(
			ref this.outputCommitted,
			1
		);
	}
}

internal static class KittyGraphicsPersistentAnimationTransaction {
	internal static ValueTask WriteControlAsync(
		TerminalSession session,
		ReadOnlyMemory<byte> payload,
		KittyGraphicsPersistentAnimationEmissionState emissionState
	) {
		if ( payload.IsEmpty ) {
			throw new ArgumentException(
				"A persistent Kitty Graphics animation control payload cannot be empty.",
				nameof( payload )
			);
		}
		return WritePayloadsAsync(
			session,
			[ payload ],
			emissionState
		);
	}

	internal static ValueTask WriteFrameEditAsync(
		TerminalSession session,
		KittyRasterData raster,
		uint imageId,
		uint frameNumber,
		int destinationX,
		int destinationY,
		KittyGraphicsPersistentAnimationEmissionState emissionState
	) {
		IEnumerable<ReadOnlyMemory<byte>> payloads =
			KittyGraphicsPersistentAnimationEncoder.EncodeFrameEditPayloads(
				raster, imageId, frameNumber, destinationX, destinationY
			);
		return WritePayloadsAsync( session, payloads, emissionState );
	}

	internal static async ValueTask WriteAsync(
		TerminalSession session,
		KittyRasterData raster,
		uint imageId,
		int gapMilliseconds,
		KittyGraphicsPersistentAnimationEmissionState emissionState
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( raster );
		ArgumentNullException.ThrowIfNull( emissionState );
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
		await WritePayloadsAsync( session, payloads, emissionState ).ConfigureAwait( false );
	}

	private static async ValueTask WritePayloadsAsync(
		TerminalSession session,
		IEnumerable<ReadOnlyMemory<byte>> payloads,
		KittyGraphicsPersistentAnimationEmissionState emissionState
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( payloads );
		ArgumentNullException.ThrowIfNull( emissionState );
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
				emissionState.MarkStarted();
				first = false;
			}
		} while ( enumerator.MoveNext() );

		await session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		emissionState.MarkOutputCommitted();
	}
}
