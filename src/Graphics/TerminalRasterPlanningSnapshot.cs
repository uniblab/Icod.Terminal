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
/// Describes fixed library raster-admission ceilings and advisory current local ownership counts.
/// </summary>
/// <remarks>
/// This value does not describe authenticated terminal storage, reserve capacity, or guarantee a
/// later allocation. Counts are captured under their owning registries but are not an atomic
/// cross-registry reservation.
/// </remarks>
public readonly record struct TerminalRasterPlanningSnapshot {
	internal TerminalRasterPlanningSnapshot(
		int ownedResourceCount,
		int ownedPlacementCount,
		int allocatedAnimationFrameCount
	) {
		ValidateCount(
			ownedResourceCount,
			TerminalPersistentRasterRegistry.MaximumResources,
			nameof( ownedResourceCount )
		);
		ValidateCount(
			ownedPlacementCount,
			TerminalPersistentRasterRegistry.MaximumPlacements,
			nameof( ownedPlacementCount )
		);
		ValidateCount(
			allocatedAnimationFrameCount,
			TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames,
			nameof( allocatedAnimationFrameCount )
		);

		this.OwnedResourceCount = ownedResourceCount;
		this.OwnedPlacementCount = ownedPlacementCount;
		this.AllocatedAnimationFrameCount = allocatedAnimationFrameCount;
	}

	/// <summary>Gets the maximum accepted raster width or height in pixels.</summary>
	public int MaximumImageDimension => TerminalRasterImage.MaximumDimension;

	/// <summary>Gets the maximum accepted pixel count for one raster.</summary>
	public int MaximumPixelCount => TerminalRasterImage.MaximumPixelCount;

	/// <summary>Gets the maximum owned pixel-byte count for one raster.</summary>
	public int MaximumOwnedPixelBytes => TerminalRasterImage.MaximumOwnedPixelBytes;

	/// <summary>Gets the maximum accepted Indexed8 palette entry count.</summary>
	public int MaximumPaletteEntries => TerminalRasterImage.MaximumPaletteEntries;

	/// <summary>Gets the maximum number of locally owned persistent raster resources.</summary>
	public int MaximumResources => TerminalPersistentRasterRegistry.MaximumResources;

	/// <summary>Gets the shared maximum for physical and virtual raster placements.</summary>
	public int MaximumPlacements => TerminalPersistentRasterRegistry.MaximumPlacements;

	/// <summary>Gets the maximum supported relative-placement depth.</summary>
	public int MaximumRelativePlacementDepth => TerminalPersistentRasterRegistry.MaximumRelativeDepth;

	/// <summary>Gets the maximum placeholder extent on either terminal-cell axis.</summary>
	public int MaximumPlaceholderExtent => TerminalRasterPlaceholderOptions.MaximumExtent;

	/// <summary>Gets the maximum number of allocated animation frames, including reservations.</summary>
	public int MaximumAnimationFrames => TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames;

	/// <summary>Gets the advisory count of locally owned persistent raster resources.</summary>
	public int OwnedResourceCount {
		get;
	}

	/// <summary>Gets the advisory combined count of physical and virtual placements.</summary>
	public int OwnedPlacementCount {
		get;
	}

	/// <summary>Gets the advisory count of known frames plus in-flight append reservations.</summary>
	public int AllocatedAnimationFrameCount {
		get;
	}

	private static void ValidateCount(
		int value,
		int maximum,
		string parameterName
	) {
		if ( value < 0 || maximum < value ) {
			throw new ArgumentOutOfRangeException( parameterName );
		}
	}
}
