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
/// Tracks local ownership state for one persistent raster placement.
/// </summary>
internal sealed class TerminalPersistentRasterPlacementState {
	private readonly TerminalPersistentRasterLifecycleState lifecycle = new();
	private TerminalPersistentRasterPlaceholderState? virtualParent;
	private long relativeOffsetBits;
	private int closed;

	internal TerminalPersistentRasterPlacementState(
		TerminalPersistentRasterResourceState resource,
		uint placementId,
		long generation
	) : this(
		resource,
		placementId,
		generation,
		parent: null,
		relativeDepth: 0,
		columnOffset: 0,
		rowOffset: 0
	) {
	}

	internal TerminalPersistentRasterPlacementState(
		TerminalPersistentRasterResourceState resource,
		uint placementId,
		long generation,
		TerminalPersistentRasterPlacementState? parent,
		int relativeDepth,
		int columnOffset,
		int rowOffset
	) {
		ArgumentNullException.ThrowIfNull( resource );
		if ( 0u == placementId ) {
			throw new ArgumentOutOfRangeException( nameof( placementId ) );
		}
		if ( generation < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( generation ) );
		}
		if ( parent is null ) {
			if ( 0 != relativeDepth ) {
				throw new ArgumentOutOfRangeException(
					nameof( relativeDepth ),
					relativeDepth,
					"A current-cursor persistent raster placement must have relative depth zero."
				);
			}
			if ( 0 != columnOffset ) {
				throw new ArgumentOutOfRangeException(
					nameof( columnOffset ),
					columnOffset,
					"A current-cursor persistent raster placement cannot retain a relative column offset."
				);
			}
			if ( 0 != rowOffset ) {
				throw new ArgumentOutOfRangeException(
					nameof( rowOffset ),
					rowOffset,
					"A current-cursor persistent raster placement cannot retain a relative row offset."
				);
			}
		} else {
			if ( generation != parent.Generation ) {
				throw new ArgumentException(
					"A relative persistent raster placement must use the same generation as its parent.",
					nameof( parent )
				);
			}
			int expectedDepth = checked( parent.RelativeDepth + 1 );
			if ( expectedDepth != relativeDepth ) {
				throw new ArgumentOutOfRangeException(
					nameof( relativeDepth ),
					relativeDepth,
					"A relative persistent raster placement depth must be exactly one greater than its immutable parent."
				);
			}
		}

		this.Resource = resource;
		this.PlacementId = placementId;
		this.Generation = generation;
		this.Parent = parent;
		this.RelativeDepth = relativeDepth;
		this.relativeOffsetBits = PackRelativeOffsets(
			columnOffset,
			rowOffset
		);
	}

	internal TerminalPersistentRasterResourceState Resource {
		get;
	}

	internal uint PlacementId {
		get;
	}

	internal long Generation {
		get;
	}

	internal TerminalPersistentRasterPlacementState? Parent {
		get;
	}

	internal TerminalPersistentRasterPlaceholderState? VirtualParent {
		get {
			return Volatile.Read( ref this.virtualParent );
		}
	}

	internal bool HasRelativeParent {
		get {
			return this.Parent is not null || this.VirtualParent is not null;
		}
	}

	internal int RelativeDepth {
		get;
	}

	internal int ColumnOffset {
		get {
			long bits = Volatile.Read( ref this.relativeOffsetBits );
			return unchecked( (int)(uint)bits );
		}
	}

	internal int RowOffset {
		get {
			long bits = Volatile.Read( ref this.relativeOffsetBits );
			return unchecked( (int)( bits >> 32 ) );
		}
	}

	internal bool IsClosed {
		get {
			return 0 != Volatile.Read( ref this.closed );
		}
	}

	internal TerminalRasterOwnershipState ObserveOwnershipState() {
		return this.lifecycle.Observe();
	}

	internal bool TryMarkStale(
		TerminalRasterOwnershipLossReason reason
	) {
		return this.lifecycle.TryMarkStale( reason );
	}

	internal bool TryMarkReleased(
		TerminalRasterOwnershipLossReason reason
	) {
		return this.lifecycle.TryMarkReleased( reason );
	}

	internal void BindVirtualParent(
		TerminalPersistentRasterPlaceholderState parent,
		int columnOffset,
		int rowOffset
	) {
		ArgumentNullException.ThrowIfNull( parent );
		if ( this.Parent is not null ) {
			throw new InvalidOperationException(
				"A physical-parent relative placement cannot also use a virtual parent."
			);
		}
		if ( this.Generation != parent.Generation ) {
			throw new ArgumentException(
				"A virtual-parent persistent raster placement must use the same generation as its parent.",
				nameof( parent )
			);
		}
		if ( Interlocked.CompareExchange(
			ref this.virtualParent,
			parent,
			null
		) is not null ) {
			throw new InvalidOperationException(
				"The persistent raster placement already has an immutable virtual parent."
			);
		}

		Interlocked.Exchange(
			ref this.relativeOffsetBits,
			PackRelativeOffsets(
				columnOffset,
				rowOffset
			)
		);
	}

	internal void CommitRelativeOffsets(
		int columnOffset,
		int rowOffset
	) {
		if ( !this.HasRelativeParent ) {
			throw new InvalidOperationException(
				"A current-cursor persistent raster placement does not own relative offsets."
			);
		}

		Interlocked.Exchange(
			ref this.relativeOffsetBits,
			PackRelativeOffsets(
				columnOffset,
				rowOffset
			)
		);
	}

	internal void Close() {
		Interlocked.Exchange(
			ref this.closed,
			1
		);
	}

	private static long PackRelativeOffsets(
		int columnOffset,
		int rowOffset
	) {
		ulong columnBits = unchecked( (uint)columnOffset );
		ulong rowBits = unchecked( (uint)rowOffset );
		return unchecked( (long)( columnBits | ( rowBits << 32 ) ) );
	}
}
