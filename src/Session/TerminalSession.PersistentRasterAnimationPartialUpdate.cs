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
/// Owns acknowledged bounded pixel replacement in known persistent animation frames.
/// </summary>
public sealed partial class TerminalSession {
	internal async ValueTask<TerminalControlMutationResult> UpdatePersistentRasterAnimationFrameRegionAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame destination,
		TerminalRasterImage region,
		int destinationX,
		int destinationY,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( region );
		if ( !ReferenceEquals( destination.Owner, animation ) ) {
			throw new ArgumentException(
				"The animation frame must belong to the selected controller.",
				nameof( destination )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource is no longer current for a partial frame update."
			);
		}
		TerminalCapabilityStatus capability = this.InspectCapability(
			TerminalCapability.PersistentRasterAnimation
		);
		if ( TerminalCapabilityEndpointAvailability.Unavailable
			== capability.EndpointAvailability ) {
			return TerminalControlMutationResult.Unavailable(
				"A partial animation-frame update requires a terminal output endpoint."
			);
		}
		if ( TerminalCapabilitySupport.Unsupported == capability.Support ) {
			return TerminalControlMutationResult.Unsupported(
				"The verified terminal backend does not support persistent raster animation."
			);
		}

		uint imageId = resourceState.ImageId;
		if ( imageId == 0u ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource has no current terminal image identity."
			);
		}
		if ( !this.persistentRasterAnimationRegistry.TryGetOrCreate(
			resourceState,
			out TerminalPersistentRasterAnimationState? animationState
		) || animationState is null ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster animation is no longer owned by this session generation."
			);
		}
		animation.BindState( animationState );
		TerminalPersistentRasterAnimationFrameState? destinationState = destination.State;
		if ( destinationState is null
			|| !this.persistentRasterAnimationRegistry.OwnsFrame(
				animationState, destinationState
			) ) {
			return TerminalControlMutationResult.Unavailable(
				"The destination animation frame is no longer current for this session generation."
			);
		}
		long evidenceGeneration = this.GetSemanticCapabilityEvidence().LiveGeneration;

		KittyRasterData raster = KittyRasterAdapter.Adapt( region );
		KittyGraphicsPersistentAnimationAppendCommitment commitment = new();
		KittyGraphicsPersistentAnimationResponseMatcher matcher = new( imageId );
		bool staleBeforeWrite = false;
		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await this.GetQueryTransactionManager().ExecuteAsync(
				_ => {
					if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState )
						|| TerminalRasterAnimationStatus.Current
							!= animationState.ObserveState().Status
						|| !this.persistentRasterAnimationRegistry.OwnsFrame(
							animationState, destinationState
						) ) {
						staleBeforeWrite = true;
						throw new InvalidOperationException(
							"Animation ownership changed before partial-frame output."
						);
					}
					return KittyGraphicsPersistentAnimationTransaction.WriteFrameEditAsync(
						this, raster, imageId, destinationState.FrameNumber,
						destinationX, destinationY, commitment
					);
				},
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				GetPersistentRasterTransferTimeout( raster ),
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( InvalidOperationException ) when ( staleBeforeWrite ) {
			return TerminalControlMutationResult.Unavailable(
				"Animation ownership changed before partial-frame output."
			);
		}

		KittyGraphicsPersistentAnimationResponse response =
			KittyGraphicsPersistentAnimationResponse.Parse(
				queryResult.Frame, imageId
			);
		if ( !response.IsSuccess ) {
			if ( response.IsMissingResource ) {
				_ = this.persistentRasterAnimationRegistry.InvalidateResource( resourceState );
				_ = this.InvalidatePersistentRasterResourceWithVirtualDescendants(
					resourceState
				);
				return TerminalControlMutationResult.Unavailable( response.Message );
			}
			return TerminalControlMutationResult.Failed( response.Message );
		}

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState )
			|| TerminalRasterAnimationStatus.Current != animationState.ObserveState().Status ) {
			return TerminalControlMutationResult.Unavailable(
				"The partial frame update was acknowledged after local animation ownership was lost."
			);
		}
		this.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyPersistentRasterAnimation,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		this.RecordSemanticOperationEvidence(
			region.PixelFormat switch {
				TerminalRasterPixelFormat.Rgb24
					=> TerminalSemanticOperation.RasterFrameRegionUpdateRgb24,
				TerminalRasterPixelFormat.Rgba32
					=> TerminalSemanticOperation.RasterFrameRegionUpdateRgba32,
				_ => throw new InvalidOperationException(
					"The validated partial-frame format is not recognized."
				)
			},
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		return TerminalControlMutationResult.Success();
	}
}
