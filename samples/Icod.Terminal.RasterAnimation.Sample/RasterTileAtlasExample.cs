/*
	Icod.Terminal.RasterAnimation.Sample
	Sample application demonstrating Icod.Terminal RasterAnimation features.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.RasterAnimation.Sample;

using System.Globalization;
using Icod.Terminal;

internal static class RasterTileAtlasExample {
	private const int AtlasColumns = 8;
	private const int AtlasRows = 8;
	private static readonly int[] Workloads = [ 1, 4, 16, 64 ];

	internal static async Task<int> RunAsync(
		TerminalSession session,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		if ( !await RasterAnimationCompositionExample.VerifyPrerequisiteAsync(
			session,
			cancellationToken
		) ) {
			await WriteFallbackAsync(
				session,
				"persistent raster graphics or animation are not currently usable",
				cancellationToken
			);
			return 0;
		}
		if ( TerminalCapabilitySupport.Unsupported == session.InspectCapability(
			TerminalCapability.UnicodeRasterPlaceholders
		).Support ) {
			await WriteFallbackAsync(
				session,
				"Unicode raster placeholders are known to be unsupported",
				cancellationToken
			);
			return 0;
		}
		TerminalPixelDimensions? cellDimensions = await TryGetCellDimensionsAsync(
			session,
			cancellationToken
		);
		if ( !cellDimensions.HasValue ) {
			await WriteFallbackAsync(
				session,
				"cell pixel geometry was unavailable or not exactly derivable",
				cancellationToken
			);
			return 0;
		}

		TerminalRasterPlanningSnapshot planning = session.GetRasterPlanningSnapshot();
		if ( !TryPlanAtlas( cellDimensions.Value, planning, out int width, out int height) ) {
			await WriteFallbackAsync(
				session,
				"the requested atlas exceeds a local raster-planning ceiling",
				cancellationToken
			);
			return 0;
		}
		TerminalRasterOperationStatus regionStatus = session.InspectRasterOperation(
			TerminalRasterOperation.FrameRegionUpdateRgb24
		);
		if ( !regionStatus.IsUsable ) {
			await WriteFallbackAsync(
				session,
				"RGB24 frame-region updates are not currently usable",
				cancellationToken
			);
			return 0;
		}

		TerminalRasterImage atlas = CreateAtlas( width, height, cellDimensions.Value );
		TerminalControlResult<TerminalRasterResource> resourceResult =
			await session.CreateRasterResourceAsync( atlas, cancellationToken );
		if ( TerminalControlStatus.Available != resourceResult.Status
			|| resourceResult.Value is null ) {
			await WriteFallbackAsync(
				session,
				"the terminal did not accept the generated atlas resource",
				cancellationToken
			);
			return 0;
		}
		await using TerminalRasterResource resource = resourceResult.Value;
		if ( width != resource.PixelWidth || height != resource.PixelHeight ) {
			throw new InvalidOperationException( "The resource did not preserve atlas geometry." );
		}

		TerminalControlResult<TerminalRasterPlaceholder> placeholderResult =
			await resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = AtlasColumns,
					Rows = AtlasRows
				},
				cancellationToken
			);
		if ( TerminalControlStatus.Available != placeholderResult.Status
			|| placeholderResult.Value is null ) {
			await WriteFallbackAsync(
				session,
				"a placeholder grid could not be created for the atlas",
				cancellationToken
			);
			return 0;
		}
		await using TerminalRasterPlaceholder placeholder = placeholderResult.Value;
		TerminalRasterPlaceholderCell[] cells = CreatePlaceholderCells( placeholder );
		await session.WriteRasterPlaceholderCellsAsync( cells, cancellationToken );

		TerminalRasterAnimation animation = resource.Animation;
		TerminalRasterAnimationFrame front = animation.RootFrame;
		TerminalControlResult<TerminalRasterAnimationFrame> backResult =
			await animation.AddFrameAsync(
				atlas,
				TimeSpan.FromMilliseconds( 40 ),
				cancellationToken
			);
		if ( TerminalControlStatus.Available != backResult.Status
			|| backResult.Value is null ) {
			await WriteFallbackAsync(
				session,
				"a second known frame could not be created",
				cancellationToken
			);
			return 0;
		}
		TerminalRasterAnimationFrame back = backResult.Value;

		foreach ( int regionCount in Workloads ) {
			TerminalControlMutationResult damageResult = await ApplyDamageAsync(
				animation,
				back,
				cellDimensions.Value,
				regionCount,
				cancellationToken
			);
			await WriteMutationOutcomeAsync(
				session,
				string.Create(
					CultureInfo.InvariantCulture,
					$"{regionCount}-region damage workload"
				),
				damageResult,
				cancellationToken
			);
			if ( !damageResult.Succeeded ) {
				await WriteFallbackAsync(
					session,
					string.Create(
						CultureInfo.InvariantCulture,
						$"the {regionCount}-region update did not complete"
					),
					cancellationToken
				);
				return 0;
			}
			TerminalControlMutationResult selected = await animation.SelectFrameAsync(
				back,
				cancellationToken
			);
			await WriteMutationOutcomeAsync(
				session,
				string.Create(
					CultureInfo.InvariantCulture,
					$"{regionCount}-region completed-frame selection"
				),
				selected,
				cancellationToken
			);
			if ( !selected.Succeeded ) {
				await WriteFallbackAsync(
					session,
					"the completed back frame could not be selected",
					cancellationToken
				);
				return 0;
			}
			(front, back) = (back, front);
		}
		regionStatus = session.InspectRasterOperation(
			TerminalRasterOperation.FrameRegionUpdateRgb24
		);
		if ( TerminalCapabilitySupport.Verified != regionStatus.Support
			|| TerminalCapabilityEvidenceKind.LiveObservation != regionStatus.EvidenceKind ) {
			await WriteFallbackAsync(
				session,
				"acknowledged RGB24 updates did not establish matching live operation evidence",
				cancellationToken
			);
			return 0;
		}

		await session.WriteTextAsync(
			"Tile-atlas witness completed 1/4/16/64 acknowledged damage workloads with two reusable known frames and Verified / LiveObservation RGB24 evidence. This demonstrates ordered completion, not atomic or gapless presentation.\r\n",
			cancellationToken
		);
		return 0;
	}

	private static async ValueTask<TerminalPixelDimensions?> TryGetCellDimensionsAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		TimeSpan timeout = TimeSpan.FromMilliseconds( 250 );
		try {
			return await session.QueryCellPixelDimensionsAsync( timeout, cancellationToken );
		} catch ( TimeoutException ) {
			TerminalControlResult<TerminalDimensions> characters = session.GetDimensions();
			if ( TerminalControlStatus.Available != characters.Status ) {
				return null;
			}
			TerminalDimensions characterDimensions = characters.GetRequiredValue();
			try {
				TerminalPixelDimensions terminal =
					await session.QueryTerminalPixelDimensionsAsync( timeout, cancellationToken );
				return TerminalPixelGeometry.TryDeriveCellDimensions(
					characterDimensions,
					terminal,
					out TerminalPixelDimensions derived
				)
					? derived
					: null;
			} catch ( Exception error ) when ( IsGeometryUnavailableFailure( error ) ) {
				return null;
			}
		} catch ( Exception error ) when ( IsGeometryUnavailableFailure( error ) ) {
			return null;
		}
	}

	internal static bool IsGeometryUnavailableFailure( Exception error ) {
		ArgumentNullException.ThrowIfNull( error );
		return error is TimeoutException or InvalidOperationException or FormatException;
	}

	internal static string FormatTransactionFallback( Exception error ) {
		ArgumentNullException.ThrowIfNull( error );
		return string.Concat(
			"Tile-atlas text fallback: ",
			RasterAnimationCompositionExample.FormatExceptionOutcome(
				"Tile-atlas frame transaction",
				error
			),
			"."
		);
	}

	private static bool TryPlanAtlas(
		TerminalPixelDimensions cell,
		TerminalRasterPlanningSnapshot planning,
		out int width,
		out int height
	) {
		width = 0;
		height = 0;
		try {
			width = checked( cell.Width * AtlasColumns );
			height = checked( cell.Height * AtlasRows );
			long pixels = checked( (long)width * height );
			long bytes = checked( pixels * 3L );
			return width <= planning.MaximumImageDimension
				&& height <= planning.MaximumImageDimension
				&& pixels <= planning.MaximumPixelCount
				&& bytes <= planning.MaximumOwnedPixelBytes
				&& planning.OwnedResourceCount < planning.MaximumResources
				&& planning.OwnedPlacementCount < planning.MaximumPlacements
				&& planning.AllocatedAnimationFrameCount + 2
					<= planning.MaximumAnimationFrames;
		} catch ( OverflowException ) {
			width = 0;
			height = 0;
			return false;
		}
	}

	private static TerminalRasterImage CreateAtlas(
		int width,
		int height,
		TerminalPixelDimensions cell
	) {
		byte[] pixels = new byte[checked( width * height * 3 )];
		for ( int row = 0; row < AtlasRows; ++row ) {
			for ( int column = 0; column < AtlasColumns; ++column ) {
				int tile = row * AtlasColumns + column;
				byte red = checked( (byte)( 32 + tile * 3 ) );
				byte green = checked( (byte)( 224 - tile * 2 ) );
				byte blue = checked( (byte)( 64 + tile * 2 ) );
				FillRectangle(
					pixels,
					width,
					column * cell.Width,
					row * cell.Height,
					cell.Width,
					cell.Height,
					red,
					green,
					blue
				);
			}
		}
		return TerminalRasterImage.CreateRgb24( width, height, pixels );
	}

	private static TerminalRasterPlaceholderCell[] CreatePlaceholderCells(
		TerminalRasterPlaceholder placeholder
	) {
		TerminalRasterPlaceholderCell[] cells = new TerminalRasterPlaceholderCell[
			checked( placeholder.Columns * placeholder.Rows )
		];
		int index = 0;
		for ( int row = 0; row < placeholder.Rows; ++row ) {
			for ( int column = 0; column < placeholder.Columns; ++column ) {
				cells[ index++ ] = placeholder.GetCell( row, column );
			}
		}
		return cells;
	}

	private static async ValueTask<TerminalControlMutationResult> ApplyDamageAsync(
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame destination,
		TerminalPixelDimensions cell,
		int regionCount,
		CancellationToken cancellationToken
	) {
		TerminalControlMutationResult? lastResult = null;
		for ( int index = 0; index < regionCount; ++index ) {
			int column = index % AtlasColumns;
			int row = index / AtlasColumns;
			byte[] pixels = new byte[checked( cell.Width * cell.Height * 3 )];
			FillRectangle(
				pixels,
				cell.Width,
				0,
				0,
				cell.Width,
				cell.Height,
				checked( (byte)( 255 - index * 3 ) ),
				checked( (byte)( 48 + index * 2 ) ),
				checked( (byte)( 96 + index * 2 ) )
			);
			lastResult = await animation.UpdateFrameRegionAsync(
				destination,
				TerminalRasterImage.CreateRgb24( cell.Width, cell.Height, pixels ),
				column * cell.Width,
				row * cell.Height,
				cancellationToken
			);
			if ( !lastResult.Succeeded ) return lastResult;
		}
		return lastResult ?? throw new InvalidOperationException(
			"A damage workload must contain at least one region."
		);
	}

	private static void FillRectangle(
		byte[] pixels,
		int stridePixels,
		int x,
		int y,
		int width,
		int height,
		byte red,
		byte green,
		byte blue
	) {
		for ( int row = y; row < y + height; ++row ) {
			for ( int column = x; column < x + width; ++column ) {
				int offset = checked( ( row * stridePixels + column ) * 3 );
				pixels[ offset ] = red;
				pixels[ offset + 1 ] = green;
				pixels[ offset + 2 ] = blue;
			}
		}
	}

	private static ValueTask WriteFallbackAsync(
		TerminalSession session,
		string reason,
		CancellationToken cancellationToken
	) {
		return session.WriteTextAsync(
			string.Concat( "Tile-atlas text fallback: ", reason, ".\r\n" ),
			cancellationToken
		);
	}

	private static ValueTask WriteMutationOutcomeAsync(
		TerminalSession session,
		string operation,
		TerminalControlMutationResult result,
		CancellationToken cancellationToken
	) {
		return session.WriteTextAsync(
			string.Concat(
				RasterAnimationCompositionExample.FormatMutationOutcome(
					operation,
					result
				),
				".\r\n"
			),
			cancellationToken
		);
	}
}
