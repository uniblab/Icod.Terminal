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
/// Provides backend-neutral raster display for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Displays one immutable raw raster through a verified graphics backend.
	/// </summary>
	/// <remarks>
	/// Version 1.7 implements this semantic operation through Sixel. The method
	/// does not expose Sixel commands, palette registers, or backend selection.
	/// A later backend such as Kitty Graphics can implement the same operation
	/// without changing the caller's raster contract.
	/// </remarks>
	/// <param name="image">The owned backend-neutral raster image.</param>
	/// <param name="cancellationToken">
	/// Cancellation observed before graphics output commits. After the first
	/// control-string byte commits, cancellation cannot truncate the frame.
	/// </param>
	/// <returns>
	/// A successful mutation result when the raster was emitted, an unavailable
	/// result when no verified raster backend is available, or an unsupported
	/// result when the selected backend cannot preserve the supplied raster semantics.
	/// </returns>
	public async ValueTask<TerminalControlMutationResult> DisplayRasterAsync(
		TerminalRasterImage image,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( image );
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		TerminalSemanticBackendResolution resolution = this.ResolveSemanticBackend(
			TerminalSemanticOperation.RasterGraphics
		);
		if ( !IsVerifiedRasterBackend( resolution ) ) {
			_ = await this.ProbeSixelSupportAsync(
				cancellationToken
			).ConfigureAwait( false );
			cancellationToken.ThrowIfCancellationRequested();
			resolution = this.ResolveSemanticBackend(
				TerminalSemanticOperation.RasterGraphics
			);
		}

		if ( !resolution.SelectedCandidate.HasValue
			|| TerminalCapabilitySupportState.Verified != resolution.State ) {
			return TerminalControlMutationResult.Unavailable(
				"No verified raster graphics backend is available for this terminal session."
			);
		}

		switch ( resolution.SelectedCandidate.Value.Backend ) {
			case TerminalProtocolBackend.DcsSixel: {
				SixelPaletteImage sixelImage;
				try {
					sixelImage = SixelPaletteQuantizer.Quantize( image );
				} catch ( NotSupportedException exception ) {
					return TerminalControlMutationResult.Unsupported(
						exception.Message
					);
				}

				cancellationToken.ThrowIfCancellationRequested();
				await SixelOutputTransaction.WriteAsync(
					this,
					sixelImage,
					cancellationToken
				).ConfigureAwait( false );
				return TerminalControlMutationResult.Success();
			}

			case TerminalProtocolBackend.ApcKittyGraphics:
				return TerminalControlMutationResult.Unsupported(
					"Kitty Graphics is not implemented by Icod.Terminal 1.7.0."
				);

			default:
				throw new InvalidOperationException(
					"The selected raster graphics backend is not recognized by the 1.7 implementation."
				);
		}
	}

	private static bool IsVerifiedRasterBackend(
		TerminalSemanticBackendResolution resolution
	) {
		return resolution.SelectedCandidate.HasValue
			&& TerminalCapabilitySupportState.Verified == resolution.State
			&& resolution.SelectedCandidate.Value.Backend is
				TerminalProtocolBackend.DcsSixel
				or TerminalProtocolBackend.ApcKittyGraphics;
	}
}
