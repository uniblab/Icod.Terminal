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
/// Emits one persistent Kitty Graphics placement as a committed single-frame transaction.
/// </summary>
internal static class KittyGraphicsPersistentPlacementTransaction {
	internal static async ValueTask WriteAsync(
		TerminalSession session,
		uint imageId,
		uint placementId,
		TerminalRasterPlacementOptions? options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		options?.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		using IDisposable outputLease = await session.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await WriteCoreAsync(
			session,
			imageId,
			placementId,
			options
		).ConfigureAwait( false );
	}

	internal static async ValueTask WriteCoreAsync(
		TerminalSession session,
		uint imageId,
		uint placementId,
		TerminalRasterPlacementOptions? options
	) {
		ArgumentNullException.ThrowIfNull( session );
		options?.Validate();

		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId,
			placementId,
			options
		);
		byte[] frame = ApcWriter.EncodeFrame( payload.Span );

		await session.Output.WriteAsync(
			frame,
			CancellationToken.None
		).ConfigureAwait( false );
		await session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
