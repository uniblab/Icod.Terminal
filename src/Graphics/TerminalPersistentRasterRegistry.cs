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
/// Owns bounded, generation-scoped bookkeeping for persistent raster resources and placements.
/// </summary>
internal sealed class TerminalPersistentRasterRegistry {
	internal const int MaximumResources = 256;
	internal const int MaximumPlacements = 4096;

	private readonly object synchronization = new();
	private readonly Dictionary<
		TerminalPersistentRasterResourceState,
		HashSet<TerminalPersistentRasterPlacementState>
	> resources = [];
	private readonly HashSet<TerminalPersistentRasterPlacementState> placements = [];
	private readonly HashSet<uint> imageNumbers = [];
	private readonly HashSet<uint> placementIds = [];
	private uint nextImageNumber;
	private uint nextPlacementId;
	private long generation;

	internal TerminalPersistentRasterRegistry(
		uint initialImageNumber = 1,
		uint initialPlacementId = 1,
		long initialGeneration = 1
	) {
		if ( initialGeneration < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( initialGeneration ) );
		}

		this.nextImageNumber = NormalizeIdentity( initialImageNumber );
		this.nextPlacementId = NormalizeIdentity( initialPlacementId );
		this.generation = initialGeneration;
	}

	internal long Generation {
		get {
			lock ( this.synchronization ) {
				return this.generation;
			}
		}
	}

	internal int LiveResourceCount {
		get {
			lock ( this.synchronization ) {
				return this.resources.Count;
			}
		}
	}

	internal int LivePlacementCount {
		get {
			lock ( this.synchronization ) {
				return this.placements.Count;
			}
		}
	}

	internal bool TryReserveResource(
		out TerminalPersistentRasterResourceState? resource
	) {
		return this.TryReserveResource(
			TerminalRasterImage.MaximumDimension,
			TerminalRasterImage.MaximumDimension,
			out resource
		);
	}

	internal bool TryReserveResource(
		int sourceWidth,
		int sourceHeight,
		out TerminalPersistentRasterResourceState? resource
	) {
		if ( sourceWidth is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException( nameof( sourceWidth ) );
		}
		if ( sourceHeight is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException( nameof( sourceHeight ) );
		}

		lock ( this.synchronization ) {
			if ( MaximumResources <= this.resources.Count ) {
				resource = null;
				return false;
			}

			uint imageNumber = AllocateIdentity(
				this.imageNumbers,
				ref this.nextImageNumber
			);
			resource = new TerminalPersistentRasterResourceState(
				imageNumber,
				this.generation,
				sourceWidth,
				sourceHeight
			);
			this.imageNumbers.Add( imageNumber );
			this.resources.Add(
				resource,
				[]
			);
			return true;
		}
	}

	internal bool TryReservePlacement(
		TerminalPersistentRasterResourceState resource,
		out TerminalPersistentRasterPlacementState? placement
	) {
		ArgumentNullException.ThrowIfNull( resource );

		lock ( this.synchronization ) {
			if ( resource.IsClosed
				|| resource.Generation != this.generation
				|| !this.resources.TryGetValue(
					resource,
					out HashSet<TerminalPersistentRasterPlacementState>? children
				)
				|| MaximumPlacements <= this.placements.Count ) {
				placement = null;
				return false;
			}

			uint placementId = AllocateIdentity(
				this.placementIds,
				ref this.nextPlacementId
			);
			placement = new TerminalPersistentRasterPlacementState(
				resource,
				placementId,
				this.generation
			);
			this.placementIds.Add( placementId );
			this.placements.Add( placement );
			children.Add( placement );
			return true;
		}
	}

	internal bool IsResourceCurrent(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );

		lock ( this.synchronization ) {
			return !resource.IsClosed
				&& resource.Generation == this.generation
				&& this.resources.ContainsKey( resource );
		}
	}

	internal bool IsPlacementCurrent(
		TerminalPersistentRasterPlacementState placement
	) {
		ArgumentNullException.ThrowIfNull( placement );

		lock ( this.synchronization ) {
			if ( placement.IsClosed
				|| placement.Resource.IsClosed
				|| placement.Generation != this.generation
				|| placement.Resource.Generation != this.generation
				|| !this.placements.Contains( placement )
				|| !this.resources.TryGetValue(
					placement.Resource,
					out HashSet<TerminalPersistentRasterPlacementState>? children
				) ) {
				return false;
			}

			return children.Contains( placement );
		}
	}

	internal void Invalidate() {
		lock ( this.synchronization ) {
			this.generation = AdvanceGeneration( this.generation );
			this.resources.Clear();
			this.placements.Clear();
			this.imageNumbers.Clear();
			this.placementIds.Clear();
		}
	}

	internal bool InvalidateResource(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );

		lock ( this.synchronization ) {
			if ( !this.resources.TryGetValue(
				resource,
				out HashSet<TerminalPersistentRasterPlacementState>? children
			) ) {
				return false;
			}

			foreach ( TerminalPersistentRasterPlacementState placement in children ) {
				this.placements.Remove( placement );
				this.placementIds.Remove( placement.PlacementId );
			}
			children.Clear();
			this.resources.Remove( resource );
			this.imageNumbers.Remove( resource.ImageNumber );
			return true;
		}
	}

	internal void DrainCurrent(
		out TerminalPersistentRasterPlacementState[] releasedPlacements,
		out TerminalPersistentRasterResourceState[] releasedResources
	) {
		lock ( this.synchronization ) {
			releasedPlacements = this.placements.ToArray();
			Array.Sort(
				releasedPlacements,
				static ( left, right ) => {
					int resourceOrder = left.Resource.ImageNumber.CompareTo(
						right.Resource.ImageNumber
					);
					return 0 != resourceOrder
						? resourceOrder
						: left.PlacementId.CompareTo( right.PlacementId )
					;
				}
			);

			releasedResources = this.resources.Keys.ToArray();
			Array.Sort(
				releasedResources,
				static ( left, right ) => left.ImageNumber.CompareTo( right.ImageNumber )
			);

			foreach ( TerminalPersistentRasterPlacementState placement in releasedPlacements ) {
				placement.Close();
			}
			foreach ( TerminalPersistentRasterResourceState resource in releasedResources ) {
				resource.Close();
			}

			this.resources.Clear();
			this.placements.Clear();
			this.imageNumbers.Clear();
			this.placementIds.Clear();
		}
	}

	internal bool TryReleaseResource(
		TerminalPersistentRasterResourceState resource
	) {
		return this.TryReleaseResource(
			resource,
			out _
		);
	}

	internal bool TryReleaseResource(
		TerminalPersistentRasterResourceState resource,
		out TerminalPersistentRasterPlacementState[] releasedPlacements
	) {
		ArgumentNullException.ThrowIfNull( resource );

		lock ( this.synchronization ) {
			if ( !this.resources.TryGetValue(
				resource,
				out HashSet<TerminalPersistentRasterPlacementState>? children
			) ) {
				releasedPlacements = [];
				return false;
			}

			releasedPlacements = new TerminalPersistentRasterPlacementState[
				children.Count
			];
			children.CopyTo( releasedPlacements );
			Array.Sort(
				releasedPlacements,
				static ( left, right ) => left.PlacementId.CompareTo( right.PlacementId )
			);

			foreach ( TerminalPersistentRasterPlacementState placement in releasedPlacements ) {
				this.placements.Remove( placement );
				this.placementIds.Remove( placement.PlacementId );
				placement.Close();
			}
			children.Clear();

			this.resources.Remove( resource );
			this.imageNumbers.Remove( resource.ImageNumber );
			resource.Close();
			return true;
		}
	}

	internal bool TryReleasePlacement(
		TerminalPersistentRasterPlacementState placement
	) {
		ArgumentNullException.ThrowIfNull( placement );

		lock ( this.synchronization ) {
			if ( !this.placements.Remove( placement ) ) {
				return false;
			}

			if ( this.resources.TryGetValue(
				placement.Resource,
				out HashSet<TerminalPersistentRasterPlacementState>? children
			) ) {
				children.Remove( placement );
			}
			this.placementIds.Remove( placement.PlacementId );
			placement.Close();
			return true;
		}
	}

	private static uint AllocateIdentity(
		HashSet<uint> liveIdentities,
		ref uint nextIdentity
	) {
		ArgumentNullException.ThrowIfNull( liveIdentities );

		uint candidate = NormalizeIdentity( nextIdentity );
		for ( int attempt = 0; attempt <= liveIdentities.Count; ++attempt ) {
			if ( !liveIdentities.Contains( candidate ) ) {
				nextIdentity = AdvanceIdentity( candidate );
				return candidate;
			}
			candidate = AdvanceIdentity( candidate );
		}

		throw new InvalidOperationException(
			"A free persistent raster protocol identity could not be allocated."
		);
	}

	private static uint NormalizeIdentity(
		uint identity
	) {
		return ( 0u == identity )
			? 1u
			: identity
		;
	}

	private static uint AdvanceIdentity(
		uint identity
	) {
		return ( uint.MaxValue == identity )
			? 1u
			: identity + 1u
		;
	}

	private static long AdvanceGeneration(
		long value
	) {
		if ( 0 > value ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}

		return long.MaxValue == value
			? 0L
			: value + 1L
		;
	}
}
