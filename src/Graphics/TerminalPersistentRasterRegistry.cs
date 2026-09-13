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

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Owns bounded, generation-scoped bookkeeping for persistent raster resources and placements.
/// </summary>
internal sealed class TerminalPersistentRasterRegistry {
	internal const int MaximumResources = 256;
	internal const int MaximumPlacements = 4096;
	internal const int MaximumRelativeDepth = 8;

	private readonly object synchronization = new();
	private readonly Dictionary<
		TerminalPersistentRasterResourceState,
		HashSet<TerminalPersistentRasterPlacementState>
	> resources = [];
	private readonly HashSet<TerminalPersistentRasterPlacementState> placements = [];
	private readonly Dictionary<
		TerminalPersistentRasterPlacementState,
		HashSet<TerminalPersistentRasterPlacementState>
	> relativeChildren = [];
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
			if ( !this.TryGetCurrentResourcePlacementsUnsafe(
				resource,
				out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
			) || MaximumPlacements <= this.placements.Count ) {
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
			this.RegisterPlacementUnsafe(
				placement,
				resourcePlacements,
				parent: null
			);
			return true;
		}
	}

	internal bool TryReserveRelativePlacement(
		TerminalPersistentRasterResourceState resource,
		TerminalPersistentRasterPlacementState parent,
		int columnOffset,
		int rowOffset,
		out TerminalPersistentRasterPlacementState? placement
	) {
		ArgumentNullException.ThrowIfNull( resource );
		ArgumentNullException.ThrowIfNull( parent );

		lock ( this.synchronization ) {
			if ( !this.TryGetCurrentResourcePlacementsUnsafe(
				resource,
				out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
			) || !this.IsPlacementCurrentUnsafe( parent )
				|| MaximumRelativeDepth <= parent.RelativeDepth
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
				this.generation,
				parent,
				checked( parent.RelativeDepth + 1 ),
				columnOffset,
				rowOffset
			);
			this.RegisterPlacementUnsafe(
				placement,
				resourcePlacements,
				parent
			);
			return true;
		}
	}

	internal bool IsResourceCurrent(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );

		lock ( this.synchronization ) {
			return this.IsResourceCurrentUnsafe( resource );
		}
	}

	internal bool IsPlacementCurrent(
		TerminalPersistentRasterPlacementState placement
	) {
		ArgumentNullException.ThrowIfNull( placement );

		lock ( this.synchronization ) {
			return this.IsPlacementCurrentUnsafe( placement );
		}
	}

	internal void Invalidate() {
		lock ( this.synchronization ) {
			this.generation = AdvanceGeneration( this.generation );
			this.resources.Clear();
			this.placements.Clear();
			this.relativeChildren.Clear();
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
				out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
			) ) {
				return false;
			}

			TerminalPersistentRasterPlacementState[] affectedPlacements =
				this.CollectResourcePlacementSubtreesUnsafe( resourcePlacements );
			foreach ( TerminalPersistentRasterPlacementState placement in affectedPlacements ) {
				this.RemovePlacementUnsafe(
					placement,
					close: false
				);
			}

			this.resources.Remove( resource );
			this.imageNumbers.Remove( resource.ImageNumber );
			return true;
		}
	}

	internal bool InvalidatePlacementSubtree(
		TerminalPersistentRasterPlacementState placement
	) {
		ArgumentNullException.ThrowIfNull( placement );

		lock ( this.synchronization ) {
			if ( !this.placements.Contains( placement ) ) {
				return false;
			}

			TerminalPersistentRasterPlacementState[] affectedPlacements =
				this.CollectPlacementSubtreeUnsafe( placement );
			foreach ( TerminalPersistentRasterPlacementState affectedPlacement in affectedPlacements ) {
				this.RemovePlacementUnsafe(
					affectedPlacement,
					close: false
				);
			}
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
				ComparePlacementsForRelease
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
			this.relativeChildren.Clear();
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
				out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
			) ) {
				releasedPlacements = [];
				return false;
			}

			releasedPlacements = this.CollectResourcePlacementSubtreesUnsafe(
				resourcePlacements
			);
			foreach ( TerminalPersistentRasterPlacementState placement in releasedPlacements ) {
				this.RemovePlacementUnsafe(
					placement,
					close: true
				);
			}

			this.resources.Remove( resource );
			this.imageNumbers.Remove( resource.ImageNumber );
			resource.Close();
			return true;
		}
	}

	internal bool TryReleasePlacement(
		TerminalPersistentRasterPlacementState placement
	) {
		return this.TryReleasePlacement(
			placement,
			out _
		);
	}

	internal bool TryReleasePlacement(
		TerminalPersistentRasterPlacementState placement,
		out TerminalPersistentRasterPlacementState[] releasedPlacements
	) {
		ArgumentNullException.ThrowIfNull( placement );

		lock ( this.synchronization ) {
			if ( !this.placements.Contains( placement ) ) {
				releasedPlacements = [];
				return false;
			}

			releasedPlacements = this.CollectPlacementSubtreeUnsafe( placement );
			foreach ( TerminalPersistentRasterPlacementState released in releasedPlacements ) {
				this.RemovePlacementUnsafe(
					released,
					close: true
				);
			}
			return true;
		}
	}

	private bool TryGetCurrentResourcePlacementsUnsafe(
		TerminalPersistentRasterResourceState resource,
		[NotNullWhen( true )] out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
	) {
		resourcePlacements = null;
		if ( !this.IsResourceCurrentUnsafe( resource ) ) {
			return false;
		}

		return this.resources.TryGetValue(
			resource,
			out resourcePlacements
		);
	}

	private bool IsResourceCurrentUnsafe(
		TerminalPersistentRasterResourceState resource
	) {
		return !resource.IsClosed
			&& resource.Generation == this.generation
			&& this.resources.ContainsKey( resource );
	}

	private bool IsPlacementCurrentUnsafe(
		TerminalPersistentRasterPlacementState placement
	) {
		HashSet<TerminalPersistentRasterPlacementState> visited = [];
		TerminalPersistentRasterPlacementState? current = placement;
		int expectedDepth = placement.RelativeDepth;
		while ( current is not null ) {
			if ( !visited.Add( current )
				|| current.IsClosed
				|| current.Generation != this.generation
				|| !this.IsResourceCurrentUnsafe( current.Resource )
				|| !this.placements.Contains( current )
				|| !this.resources.TryGetValue(
					current.Resource,
					out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
				) || !resourcePlacements.Contains( current )
				|| current.RelativeDepth != expectedDepth
				|| current.RelativeDepth is < 0 or > MaximumRelativeDepth
				|| !this.relativeChildren.ContainsKey( current ) ) {
				return false;
			}

			TerminalPersistentRasterPlacementState? parent = current.Parent;
			if ( parent is null ) {
				return 0 == current.RelativeDepth;
			}
			if ( 0 >= current.RelativeDepth
				|| parent.Generation != this.generation
				|| parent.RelativeDepth != current.RelativeDepth - 1
				|| !this.relativeChildren.TryGetValue(
					parent,
					out HashSet<TerminalPersistentRasterPlacementState>? siblings
				) || !siblings.Contains( current ) ) {
				return false;
			}

			current = parent;
			--expectedDepth;
		}

		return false;
	}

	private void RegisterPlacementUnsafe(
		TerminalPersistentRasterPlacementState placement,
		HashSet<TerminalPersistentRasterPlacementState> resourcePlacements,
		TerminalPersistentRasterPlacementState? parent
	) {
		ArgumentNullException.ThrowIfNull( placement );
		ArgumentNullException.ThrowIfNull( resourcePlacements );

		this.placementIds.Add( placement.PlacementId );
		this.placements.Add( placement );
		resourcePlacements.Add( placement );
		this.relativeChildren.Add(
			placement,
			[]
		);
		if ( parent is not null ) {
			this.relativeChildren[ parent ].Add( placement );
		}
	}

	private TerminalPersistentRasterPlacementState[] CollectPlacementSubtreeUnsafe(
		TerminalPersistentRasterPlacementState root
	) {
		ArgumentNullException.ThrowIfNull( root );
		HashSet<TerminalPersistentRasterPlacementState> collected = [];
		this.CollectPlacementSubtreeUnsafe(
			root,
			collected
		);
		TerminalPersistentRasterPlacementState[] values = collected.ToArray();
		Array.Sort(
			values,
			ComparePlacementsForRelease
		);
		return values;
	}

	private void CollectPlacementSubtreeUnsafe(
		TerminalPersistentRasterPlacementState placement,
		HashSet<TerminalPersistentRasterPlacementState> collected
	) {
		ArgumentNullException.ThrowIfNull( placement );
		ArgumentNullException.ThrowIfNull( collected );
		if ( !collected.Add( placement ) ) {
			return;
		}

		if ( !this.relativeChildren.TryGetValue(
			placement,
			out HashSet<TerminalPersistentRasterPlacementState>? children
		) ) {
			return;
		}
		foreach ( TerminalPersistentRasterPlacementState child in children ) {
			this.CollectPlacementSubtreeUnsafe(
				child,
				collected
			);
		}
	}

	private TerminalPersistentRasterPlacementState[] CollectResourcePlacementSubtreesUnsafe(
		HashSet<TerminalPersistentRasterPlacementState> resourcePlacements
	) {
		ArgumentNullException.ThrowIfNull( resourcePlacements );
		HashSet<TerminalPersistentRasterPlacementState> collected = [];
		foreach ( TerminalPersistentRasterPlacementState placement in resourcePlacements.ToArray() ) {
			this.CollectPlacementSubtreeUnsafe(
				placement,
				collected
			);
		}

		TerminalPersistentRasterPlacementState[] values = collected.ToArray();
		Array.Sort(
			values,
			ComparePlacementsForRelease
		);
		return values;
	}

	private void RemovePlacementUnsafe(
		TerminalPersistentRasterPlacementState placement,
		bool close
	) {
		ArgumentNullException.ThrowIfNull( placement );
		if ( !this.placements.Remove( placement ) ) {
			return;
		}

		if ( this.resources.TryGetValue(
			placement.Resource,
			out HashSet<TerminalPersistentRasterPlacementState>? resourcePlacements
		) ) {
			resourcePlacements.Remove( placement );
		}
		if ( placement.Parent is not null
			&& this.relativeChildren.TryGetValue(
				placement.Parent,
				out HashSet<TerminalPersistentRasterPlacementState>? siblings
			) ) {
			siblings.Remove( placement );
		}
		this.relativeChildren.Remove( placement );
		this.placementIds.Remove( placement.PlacementId );
		if ( close ) {
			placement.Close();
		}
	}

	private static int ComparePlacementsForRelease(
		TerminalPersistentRasterPlacementState left,
		TerminalPersistentRasterPlacementState right
	) {
		int depthOrder = right.RelativeDepth.CompareTo( left.RelativeDepth );
		if ( 0 != depthOrder ) {
			return depthOrder;
		}

		int resourceOrder = left.Resource.ImageNumber.CompareTo(
			right.Resource.ImageNumber
		);
		return 0 != resourceOrder
			? resourceOrder
			: left.PlacementId.CompareTo( right.PlacementId )
		;
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
