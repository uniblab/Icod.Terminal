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
/// Hardens resource/session lifecycle propagation across virtual-parent physical descendants.
/// </summary>
public sealed partial class TerminalSession {
	internal bool InvalidatePersistentRasterResourceWithVirtualDescendants(
		TerminalPersistentRasterResourceState resourceState
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		TerminalPersistentRasterPlacementState[] virtualRoots =
			this.persistentRasterVirtualParents.TakeChildrenForResource(
				resourceState
			);
		bool invalidated = this.persistentRasterRegistry.InvalidateResource(
			resourceState
		);

		foreach ( TerminalPersistentRasterPlacementState root in virtualRoots ) {
			if ( this.persistentRasterRegistry.IsPlacementCurrent( root ) ) {
				_ = this.persistentRasterRegistry.InvalidatePlacementSubtree( root );
			}
		}
		return invalidated;
	}

	internal async ValueTask ReleasePersistentRasterResourceWithVirtualDescendantsAsync(
		TerminalPersistentRasterResourceState resourceState
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		TerminalPersistentRasterPlacementState[] virtualRoots =
			this.persistentRasterVirtualParents.TakeChildrenForResource(
				resourceState
			);
		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			this.ReleaseVirtualRootsWithoutOutput( virtualRoots );
			return;
		}

		IDisposable outputLease;
		try {
			outputLease = await this.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			this.ReleaseVirtualRootsWithoutOutput( virtualRoots );
			throw;
		}

		using ( outputLease ) {
			bool resourceReleased = this.persistentRasterRegistry.TryReleaseResource(
				resourceState,
				out TerminalPersistentRasterPlacementState[] releasedPlacements
			);
			List<TerminalPersistentRasterPlacementState> allReleased = [
				.. releasedPlacements,
			];
			foreach ( TerminalPersistentRasterPlacementState root in virtualRoots ) {
				if ( this.persistentRasterRegistry.TryReleasePlacement(
					root,
					out TerminalPersistentRasterPlacementState[] subtree
				) ) {
					allReleased.AddRange( subtree );
				}
			}
			if ( !resourceReleased ) {
				return;
			}

			uint imageId = resourceState.ImageId;
			List<Exception> failures = [];
			HashSet<TerminalPersistentRasterPlacementState> emitted = [];
			foreach ( TerminalPersistentRasterPlacementState placement in allReleased ) {
				if ( !emitted.Add( placement ) ) {
					continue;
				}

				uint placementImageId = placement.Resource.ImageId;
				if ( 0u == placementImageId ) {
					continue;
				}

				try {
					await this.WritePersistentRasterControlFrameCoreAsync(
						KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
							placementImageId,
							placement.PlacementId
						)
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					failures.Add( exception );
				}
			}

			if ( 0u != imageId ) {
				try {
					await this.WritePersistentRasterControlFrameCoreAsync(
						KittyGraphicsPersistentEncoder.EncodeDeleteResourcePayload(
							imageId
						)
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					failures.Add( exception );
				}
			}

			try {
				await this.Output.FlushAsync(
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				failures.Add( exception );
			}

			if ( 0 < failures.Count ) {
				throw new AggregateException(
					"One or more persistent raster resource cleanup operations failed.",
					failures
				);
			}
		}
	}

	internal ValueTask ReleasePersistentRasterPlacementWithVirtualParentAsync(
		TerminalPersistentRasterPlacementState placementState
	) {
		ArgumentNullException.ThrowIfNull( placementState );
		this.persistentRasterVirtualParents.RemovePlacement( placementState );
		return this.ReleasePersistentRasterPlacementAsync( placementState );
	}

	private void InvalidatePersistentRasterStateWithVirtualParents() {
		this.persistentRasterVirtualParents.Clear();
		this.InvalidatePersistentRasterState();
	}

	private async ValueTask<Exception?> ClosePersistentRasterStateWithVirtualParentsAsync() {
		try {
			return await this.ClosePersistentRasterStateAsync().ConfigureAwait( false );
		} finally {
			this.persistentRasterVirtualParents.Clear();
		}
	}

	private void ReleaseVirtualRootsWithoutOutput(
		IEnumerable<TerminalPersistentRasterPlacementState> virtualRoots
	) {
		ArgumentNullException.ThrowIfNull( virtualRoots );
		foreach ( TerminalPersistentRasterPlacementState root in virtualRoots ) {
			_ = this.persistentRasterRegistry.TryReleasePlacement( root );
		}
	}
}
