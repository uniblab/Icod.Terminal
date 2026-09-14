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
/// Tracks the direct physical roots owned beneath persistent virtual raster placeholders.
/// </summary>
internal sealed class TerminalPersistentRasterVirtualParentRegistry {
	private readonly object synchronization = new();
	private readonly Dictionary<
		TerminalPersistentRasterPlaceholderState,
		HashSet<TerminalPersistentRasterPlacementState>
	> children = [];

	internal bool TryRegister(
		TerminalPersistentRasterPlaceholderState parent,
		TerminalPersistentRasterPlacementState child
	) {
		ArgumentNullException.ThrowIfNull( parent );
		ArgumentNullException.ThrowIfNull( child );
		if ( !ReferenceEquals(
			parent,
			child.VirtualParent
		) ) {
			return false;
		}

		lock ( this.synchronization ) {
			if ( !this.children.TryGetValue(
				parent,
				out HashSet<TerminalPersistentRasterPlacementState>? values
			) ) {
				values = [];
				this.children.Add(
					parent,
					values
				);
			}
			return values.Add( child );
		}
	}

	internal bool Contains(
		TerminalPersistentRasterPlaceholderState parent,
		TerminalPersistentRasterPlacementState child
	) {
		ArgumentNullException.ThrowIfNull( parent );
		ArgumentNullException.ThrowIfNull( child );

		lock ( this.synchronization ) {
			return this.children.TryGetValue(
				parent,
				out HashSet<TerminalPersistentRasterPlacementState>? values
			) && values.Contains( child );
		}
	}

	internal void RemovePlacement(
		TerminalPersistentRasterPlacementState placement
	) {
		ArgumentNullException.ThrowIfNull( placement );
		TerminalPersistentRasterPlaceholderState? parent = placement.VirtualParent;
		if ( parent is null ) {
			return;
		}

		lock ( this.synchronization ) {
			if ( !this.children.TryGetValue(
				parent,
				out HashSet<TerminalPersistentRasterPlacementState>? values
			) ) {
				return;
			}

			values.Remove( placement );
			if ( 0 == values.Count ) {
				this.children.Remove( parent );
			}
		}
	}

	internal TerminalPersistentRasterPlacementState[] TakeChildren(
		TerminalPersistentRasterPlaceholderState parent
	) {
		ArgumentNullException.ThrowIfNull( parent );

		lock ( this.synchronization ) {
			if ( !this.children.Remove(
				parent,
				out HashSet<TerminalPersistentRasterPlacementState>? values
			) ) {
				return [];
			}

			TerminalPersistentRasterPlacementState[] result = values.ToArray();
			Array.Sort(
				result,
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
			return result;
		}
	}

	internal void Clear() {
		lock ( this.synchronization ) {
			this.children.Clear();
		}
	}
}
