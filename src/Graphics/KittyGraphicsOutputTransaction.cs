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
/// Emits one A183 Kitty Graphics direct-transfer sequence as a committed,
/// serialized multi-frame APC transaction.
/// </summary>
internal static class KittyGraphicsOutputTransaction {
	internal static async ValueTask WriteAsync(
		TerminalSession session,
		KittyRasterData raster,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( raster );
		cancellationToken.ThrowIfCancellationRequested();

		IEnumerable<ReadOnlyMemory<byte>> payloads =
			KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster );
		using IEnumerator<ReadOnlyMemory<byte>> enumerator = payloads.GetEnumerator();
		if ( !enumerator.MoveNext() ) {
			throw new InvalidOperationException(
				"The Kitty Graphics direct encoder produced no payload for a non-empty raster."
			);
		}

		ReadOnlyMemory<byte> firstPayload = enumerator.Current;
		if ( firstPayload.IsEmpty ) {
			throw new InvalidOperationException(
				"The Kitty Graphics direct encoder produced an empty first payload."
			);
		}
		byte[] firstFrame = ApcWriter.EncodeFrame( firstPayload.Span );

		using IDisposable outputLease = await session.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await session.Output.WriteAsync(
			firstFrame,
			CancellationToken.None
		).ConfigureAwait( false );

		while ( enumerator.MoveNext() ) {
			ReadOnlyMemory<byte> payload = enumerator.Current;
			if ( payload.IsEmpty ) {
				throw new InvalidOperationException(
					"The Kitty Graphics direct encoder produced an empty continuation payload after output commitment."
				);
			}
			byte[] frame = ApcWriter.EncodeFrame( payload.Span );
			await session.Output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );
		}

		await session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
