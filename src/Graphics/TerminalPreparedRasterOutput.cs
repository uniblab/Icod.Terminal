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

/// <summary>Fully encoded, bounded raster frames retained before screen output begins.</summary>
internal sealed class TerminalPreparedRasterOutput {
	private readonly TerminalProtocolBackend backend;
	private readonly long evidenceGeneration;

	private TerminalPreparedRasterOutput(
		TerminalProtocolBackend backend,
		long evidenceGeneration,
		ReadOnlyMemory<byte>[] segments,
		int byteCount
	) {
		this.backend = backend;
		this.evidenceGeneration = evidenceGeneration;
		this.Segments = segments;
		this.ByteCount = byteCount;
	}

	internal IReadOnlyList<ReadOnlyMemory<byte>> Segments { get; }
	internal int ByteCount { get; }

	internal static TerminalPreparedRasterOutput Prepare(
		TerminalSession session,
		TerminalRasterImage image,
		int availableBytes,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( image );
		if ( availableBytes < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( availableBytes ) );
		}
		cancellationToken.ThrowIfCancellationRequested();
		long generation = session.GetSemanticCapabilityEvidence().LiveGeneration;
		TerminalProtocolBackend backend = ResolveVerifiedBackend( session );
		List<ReadOnlyMemory<byte>> segments = [];
		int byteCount = 0;

		void AddSegment( ReadOnlyMemory<byte> segment ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( segment.IsEmpty ) {
				throw new InvalidOperationException( "The raster encoder produced an empty segment." );
			}
			if ( segment.Length > availableBytes - byteCount ) {
				throw new InvalidOperationException( "The screen-output transaction exceeds its encoded-payload limit." );
			}
			segments.Add( segment );
			byteCount += segment.Length;
		}

		switch ( backend ) {
			case TerminalProtocolBackend.ApcKittyGraphics:
				KittyRasterData kitty = KittyRasterAdapter.Adapt( image );
				foreach ( ReadOnlyMemory<byte> payload in KittyGraphicsDirectEncoder.EncodeDisplayPayloads( kitty ) ) {
					AddSegment( ApcWriter.EncodeFrame( payload.Span ) );
				}
				break;

			case TerminalProtocolBackend.DcsSixel:
				SixelPaletteImage sixel = SixelPaletteQuantizer.Quantize( image );
				SixelOutputTransaction.ValidateImageForStreaming( sixel );
				AddSegment( SixelOutputTransaction.CanonicalPrefix );
				foreach ( ReadOnlyMemory<byte> segment in SixelEncoder.EncodePayloadSegments( sixel ) ) {
					AddSegment( segment );
				}
				AddSegment( SixelOutputTransaction.CanonicalTerminator );
				break;

			default:
				throw new InvalidOperationException( "The selected raster backend is invalid." );
		}
		if ( segments.Count == 0 ) {
			throw new InvalidOperationException( "The raster encoder produced no output." );
		}
		if ( TerminalProtocolBackend.DcsSixel == backend ) {
			// Encoder fragments include single-byte separators. They must not become
			// transport scheduling points that expose avoidable partial-image redraws.
			// The complete encoded size has already passed the transaction's bound.
			byte[] completeImage = new byte[ byteCount ];
			int offset = 0;
			foreach ( ReadOnlyMemory<byte> segment in segments ) {
				cancellationToken.ThrowIfCancellationRequested();
				segment.Span.CopyTo( completeImage.AsSpan( offset ) );
				offset += segment.Length;
			}
			segments.Clear();
			segments.Add( completeImage );
		}
		TerminalPreparedRasterOutput result = new( backend, generation, segments.ToArray(), byteCount );
		result.ValidateEvidence( session );
		return result;
	}

	internal void ValidateEvidence( TerminalSession session ) {
		if ( this.evidenceGeneration != session.GetSemanticCapabilityEvidence().LiveGeneration
			|| this.backend != ResolveVerifiedBackendOrDefault( session ) ) {
			throw new InvalidOperationException( "Raster backend evidence changed before screen output." );
		}
	}

	private static TerminalProtocolBackend ResolveVerifiedBackend( TerminalSession session ) {
		TerminalProtocolBackend? backend = ResolveVerifiedBackendOrDefault( session );
		return backend ?? throw new NotSupportedException(
			"No verified raster graphics backend is available for this terminal session."
		);
	}

	private static TerminalProtocolBackend? ResolveVerifiedBackendOrDefault( TerminalSession session ) {
		TerminalSemanticBackendResolution resolution = session.ResolveSemanticBackend(
			TerminalSemanticOperation.RasterGraphics
		);
		if ( TerminalCapabilitySupportState.Verified != resolution.State
			|| !resolution.SelectedCandidate.HasValue ) {
			return null;
		}
		TerminalProtocolBackend backend = resolution.SelectedCandidate.Value.Backend;
		return backend is TerminalProtocolBackend.ApcKittyGraphics or TerminalProtocolBackend.DcsSixel
			? backend
			: null;
	}
}
