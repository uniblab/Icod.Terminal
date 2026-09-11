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
/// Emits one already-committed persistent Kitty Graphics upload while the query manager
/// owns the session control-output gate and correlated response expectation.
/// </summary>
internal static class KittyGraphicsPersistentUploadTransaction {
	internal static async ValueTask WriteAsync(
		TerminalSession session,
		KittyRasterData raster,
		uint imageNumber
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( raster );
		if ( 0u == imageNumber ) {
			throw new ArgumentOutOfRangeException( nameof( imageNumber ) );
		}

		IEnumerable<ReadOnlyMemory<byte>> payloads =
			KittyGraphicsPersistentEncoder.EncodeUploadPayloads(
				raster,
				imageNumber
			);
		using IEnumerator<ReadOnlyMemory<byte>> enumerator = payloads.GetEnumerator();
		if ( !enumerator.MoveNext() ) {
			throw new InvalidOperationException(
				"The persistent Kitty Graphics encoder produced no payload for a non-empty raster."
			);
		}

		do {
			ReadOnlyMemory<byte> payload = enumerator.Current;
			if ( payload.IsEmpty ) {
				throw new InvalidOperationException(
					"The persistent Kitty Graphics encoder produced an empty payload after output commitment."
				);
			}

			byte[] frame = ApcWriter.EncodeFrame( payload.Span );
			await session.Output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );
		} while ( enumerator.MoveNext() );

		await session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
