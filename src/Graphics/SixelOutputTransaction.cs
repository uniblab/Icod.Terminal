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
/// Emits one bounded D175 Sixel payload as a committed serialized DCS transaction.
/// </summary>
internal static class SixelOutputTransaction {
	private static readonly byte[] CanonicalEmptyFrame = DcsWriter.EncodeFrame(
		SixelCodec.EncodeCanonicalDcsParameters(),
		ReadOnlySpan<byte>.Empty,
		(byte)'q',
		ReadOnlySpan<byte>.Empty
	);

	private static readonly ReadOnlyMemory<byte> CanonicalPrefix =
		CanonicalEmptyFrame.AsMemory(
			0,
			CanonicalEmptyFrame.Length - 2
		);

	private static readonly ReadOnlyMemory<byte> CanonicalTerminator =
		CanonicalEmptyFrame.AsMemory(
			CanonicalEmptyFrame.Length - 2,
			2
		);

	internal static async ValueTask WriteAsync(
		TerminalSession session,
		SixelPaletteImage image,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( image );
		cancellationToken.ThrowIfCancellationRequested();

		ValidateImageForStreaming( image );
		IEnumerable<ReadOnlyMemory<byte>> segments =
			SixelEncoder.EncodePayloadSegments( image );

		using IDisposable outputLease = await session.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await session.Output.WriteAsync(
			CanonicalPrefix,
			CancellationToken.None
		).ConfigureAwait( false );

		foreach ( ReadOnlyMemory<byte> segment in segments ) {
			if ( segment.IsEmpty ) {
				throw new InvalidOperationException(
					"The Sixel encoder produced an empty payload segment after output commitment."
				);
			}

			await session.Output.WriteAsync(
				segment,
				CancellationToken.None
			).ConfigureAwait( false );
		}

		await session.Output.WriteAsync(
			CanonicalTerminator,
			CancellationToken.None
		).ConfigureAwait( false );
		await session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private static void ValidateImageForStreaming(
		SixelPaletteImage image
	) {
		ArgumentNullException.ThrowIfNull( image );

		if ( image.Width is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new InvalidOperationException(
				"A Sixel palette image contains an invalid width."
			);
		}
		if ( image.Height is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new InvalidOperationException(
				"A Sixel palette image contains an invalid height."
			);
		}
		if ( image.PixelCount != checked( image.Width * image.Height ) ) {
			throw new InvalidOperationException(
				"A Sixel palette image contains an inconsistent pixel count."
			);
		}
		if ( image.PixelCount != image.Indices.Length ) {
			throw new InvalidOperationException(
				"A Sixel palette image must contain exactly one palette index per pixel."
			);
		}
		if ( TerminalRasterImage.MaximumPaletteEntries < image.Palette.Length ) {
			throw new InvalidOperationException(
				"A Sixel palette image exceeds the supported palette size."
			);
		}
		if ( 0 != image.TransparentMask.Length
			&& image.PixelCount != image.TransparentMask.Length ) {
			throw new InvalidOperationException(
				"A Sixel palette image contains an inconsistent transparency mask."
			);
		}

		ReadOnlySpan<TerminalRasterColor> palette = image.Palette.Span;
		for ( int register = 0; register < palette.Length; register++ ) {
			if ( byte.MaxValue != palette[ register ].Alpha ) {
				throw new InvalidOperationException(
					"A Sixel wire palette cannot contain a non-opaque color entry."
				);
			}
		}

		ReadOnlySpan<byte> indices = image.Indices.Span;
		ReadOnlySpan<byte> transparency = image.TransparentMask.Span;
		for ( int pixelIndex = 0; pixelIndex < image.PixelCount; pixelIndex++ ) {
			bool isTransparent = false;
			if ( 0 != transparency.Length ) {
				byte mask = transparency[ pixelIndex ];
				if ( mask is not 0 and not 1 ) {
					throw new InvalidOperationException(
						"A Sixel transparency mask must contain only zero or one values."
					);
				}
				isTransparent = 0 != mask;
			}
			if ( isTransparent ) {
				continue;
			}
			if ( palette.Length <= indices[ pixelIndex ] ) {
				throw new InvalidOperationException(
					"A Sixel palette image contains an opaque pixel whose palette index is not defined."
				);
			}
		}
	}
}
