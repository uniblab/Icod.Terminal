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
/// Owns optional-response composition of already known frames in one persistent raster resource.
/// </summary>
public sealed partial class TerminalSession {
	internal async ValueTask<TerminalControlMutationResult> ComposePersistentRasterAnimationFrameAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame source,
		TerminalRasterAnimationFrame destination,
		TerminalRasterSourceRectangle sourceRectangle,
		int destinationX,
		int destinationY,
		TerminalRasterFrameCompositionMode mode,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( destination );
		if ( !ReferenceEquals( source.Owner, animation )
			|| !ReferenceEquals( destination.Owner, animation ) ) {
			throw new ArgumentException(
				"Both animation frames must belong to the selected controller."
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource is no longer current for frame composition."
			);
		}

		TerminalCapabilityStatus capability = this.InspectCapability(
			TerminalCapability.PersistentRasterAnimation
		);
		if ( TerminalCapabilityEndpointAvailability.Unavailable
			== capability.EndpointAvailability ) {
			return TerminalControlMutationResult.Unavailable(
				"Animation frame composition requires a terminal output endpoint."
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
		if ( TerminalRasterAnimationStatus.Current != animationState.ObserveState().Status ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster animation frame sequence is no longer current."
			);
		}

		TerminalPersistentRasterAnimationFrameState? sourceState = source.State;
		TerminalPersistentRasterAnimationFrameState? destinationState = destination.State;
		if ( sourceState is null || destinationState is null
			|| !this.persistentRasterAnimationRegistry.OwnsFrame( animationState, sourceState )
			|| !this.persistentRasterAnimationRegistry.OwnsFrame( animationState, destinationState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"An animation frame is no longer current for this session generation."
			);
		}
		long evidenceGeneration = this.GetSemanticCapabilityEvidence().LiveGeneration;

		ReadOnlyMemory<byte> payload =
			KittyGraphicsPersistentAnimationEncoder.EncodeCompositionPayload(
				imageId,
				sourceState.FrameNumber,
				destinationState.FrameNumber,
				sourceRectangle,
				destinationX,
				destinationY,
				mode
			);
		KittyGraphicsPersistentAnimationResponseMatcher matcher = new( imageId );
		KittyGraphicsPersistentAnimationEmissionState emissionState = new();
		bool staleBeforeWrite = false;
		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await this.GetQueryTransactionManager().ExecuteAsync(
				_ => {
					if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState )
						|| TerminalRasterAnimationStatus.Current
							!= animationState.ObserveState().Status
						|| !this.persistentRasterAnimationRegistry.OwnsFrame(
							animationState, sourceState
						) || !this.persistentRasterAnimationRegistry.OwnsFrame(
							animationState, destinationState
						) ) {
						staleBeforeWrite = true;
						throw new InvalidOperationException(
							"Animation ownership changed before composition output."
						);
					}
					return KittyGraphicsPersistentAnimationTransaction.WriteControlAsync(
						this,
						payload,
						emissionState
					);
				},
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				PersistentRasterCreationTimeout,
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( InvalidOperationException ) when ( staleBeforeWrite ) {
			return TerminalControlMutationResult.Unavailable(
				"Animation ownership changed before composition output."
			);
		} catch ( TimeoutException ) when ( emissionState.OutputCommitted ) {
			if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState )
				|| TerminalRasterAnimationStatus.Current
					!= animationState.ObserveState().Status
				|| !this.persistentRasterAnimationRegistry.OwnsFrame(
					animationState,
					sourceState
				) || !this.persistentRasterAnimationRegistry.OwnsFrame(
					animationState,
					destinationState
				) ) {
				return TerminalControlMutationResult.Unavailable(
					"Animation ownership changed after composition output was committed."
				);
			}
			return TerminalControlMutationResult.Success(
				TerminalControlMutationConfirmation.OutputCommitted
			);
		}

		KittyGraphicsPersistentAnimationResponse response =
			KittyGraphicsPersistentAnimationResponse.Parse(
				queryResult.Frame,
				imageId
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
				"The frame composition was acknowledged after local animation ownership was lost."
			);
		}
		this.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyPersistentRasterAnimation,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		this.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.RasterFrameComposition,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		return TerminalControlMutationResult.Success(
			TerminalControlMutationConfirmation.ProtocolAcknowledged
		);
	}
}
