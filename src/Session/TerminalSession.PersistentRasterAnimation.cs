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
/// Owns acknowledged persistent-raster animation frame transactions.
/// </summary>
public sealed partial class TerminalSession {
	private readonly TerminalPersistentRasterAnimationRegistry persistentRasterAnimationRegistry = new();

	internal async ValueTask<TerminalControlResult<TerminalRasterAnimationFrame>> AddPersistentRasterAnimationFrameAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		TerminalRasterImage image,
		int gapMilliseconds,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( image );
		if ( gapMilliseconds <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( gapMilliseconds ) );
		}
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				"The persistent raster resource is no longer current for animation append."
			);
		}

		TerminalCapabilityStatus capability = this.InspectCapability(
			TerminalCapability.PersistentRasterAnimation
		);
		if ( TerminalCapabilityEndpointAvailability.Unavailable
			== capability.EndpointAvailability ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				"Persistent raster animation requires a terminal output endpoint."
			);
		}
		if ( TerminalCapabilitySupport.Unsupported == capability.Support ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unsupported(
				"The verified terminal backend does not support persistent raster animation."
			);
		}

		uint imageId = resourceState.ImageId;
		if ( 0u == imageId ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				"The persistent raster resource does not have a current terminal image identity."
			);
		}

		if ( !this.persistentRasterAnimationRegistry.TryGetOrCreate(
			resourceState,
			out TerminalPersistentRasterAnimationState? animationState
		) || animationState is null ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				$"The session cannot allocate animation ownership within the {TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames}-frame ceiling."
			);
		}
		animation.BindState( animationState );
		if ( TerminalRasterAnimationStatus.Current != animationState.ObserveState().Status ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				"The persistent raster animation sequence is no longer current."
			);
		}

		if ( !this.persistentRasterAnimationRegistry.TryReserveAppend(
			animationState,
			out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
		) || reservation is null ) {
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				"The persistent raster animation cannot reserve another frame."
			);
		}

		KittyRasterData raster = KittyRasterAdapter.Adapt( image );
		KittyGraphicsPersistentAnimationAppendCommitment commitment = new();
		KittyGraphicsPersistentAnimationResponseMatcher matcher = new( imageId );
		ValueTask<TerminalQueryResponseResult> transaction;
		try {
			transaction = this.GetQueryTransactionManager().ExecuteAsync(
				_ => KittyGraphicsPersistentAnimationTransaction.WriteAsync(
					this,
					raster,
					imageId,
					gapMilliseconds,
					commitment
				),
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				PersistentRasterCreationTimeout,
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken,
				abandonedCleanup: () => this.CleanupAbandonedAnimationAppend(
					reservation,
					animationState,
					commitment
				)
			);
		} catch {
			this.CleanupFailedAnimationAppend(
				reservation,
				animationState,
				commitment
			);
			throw;
		}

		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await transaction.ConfigureAwait( false );
		} catch {
			this.CleanupFailedAnimationAppend(
				reservation,
				animationState,
				commitment
			);
			throw;
		}

		KittyGraphicsPersistentAnimationResponse response;
		try {
			response = KittyGraphicsPersistentAnimationResponse.Parse(
				queryResult.Frame,
				imageId
			);
		} catch {
			this.CleanupFailedAnimationAppend(
				reservation,
				animationState,
				commitment
			);
			throw;
		}

		if ( !response.IsSuccess ) {
			_ = this.persistentRasterAnimationRegistry.TryRollbackAppend( reservation );
			if ( response.IsMissingResource ) {
				_ = this.persistentRasterAnimationRegistry.InvalidateResource( resourceState );
				_ = this.InvalidatePersistentRasterResourceWithVirtualDescendants(
					resourceState
				);
				return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
					response.Message
				);
			}

			return TerminalControlResult<TerminalRasterAnimationFrame>.Failed(
				response.Message
			);
		}

		if ( !this.persistentRasterAnimationRegistry.TryPublishAppend(
			reservation,
			out TerminalPersistentRasterAnimationFrameState? frameState
		) || frameState is null ) {
			_ = this.persistentRasterAnimationRegistry.TryRollbackAppend( reservation );
			_ = animationState.TryMarkSequenceUncertain();
			return TerminalControlResult<TerminalRasterAnimationFrame>.Unavailable(
				"The animation frame was acknowledged after local frame-sequence ownership was lost."
			);
		}

		this.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyPersistentRasterAnimation,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		TerminalRasterAnimationFrame frame = new(
			animation,
			checked( (int)frameState.FrameNumber )
		);
		frame.BindState( frameState );
		return TerminalControlResult<TerminalRasterAnimationFrame>.Available( frame );
	}

	internal ValueTask<TerminalControlMutationResult> SetPersistentRasterAnimationFrameDurationAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame frame,
		int gapMilliseconds,
		CancellationToken cancellationToken
	) {
		if ( gapMilliseconds <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( gapMilliseconds ) );
		}
		return this.ControlPersistentRasterAnimationFrameAsync(
			resourceState,
			animation,
			frame,
			gapMilliseconds,
			selectCurrent: false,
			cancellationToken
		);
	}

	internal ValueTask<TerminalControlMutationResult> SelectPersistentRasterAnimationFrameAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame frame,
		CancellationToken cancellationToken
	) {
		return this.ControlPersistentRasterAnimationFrameAsync(
			resourceState,
			animation,
			frame,
			gapMilliseconds: null,
			selectCurrent: true,
			cancellationToken
		);
	}

	private async ValueTask<TerminalControlMutationResult> ControlPersistentRasterAnimationFrameAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame frame,
		int? gapMilliseconds,
		bool selectCurrent,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( frame );
		if ( !selectCurrent && !gapMilliseconds.HasValue ) {
			throw new ArgumentException(
				"An animation duration control requires a positive frame gap.",
				nameof( gapMilliseconds )
			);
		}
		if ( gapMilliseconds.HasValue && gapMilliseconds.Value <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( gapMilliseconds ) );
		}
		if ( !ReferenceEquals( animation, frame.Owner ) ) {
			throw new ArgumentException(
				"The animation frame must belong to the selected animation controller.",
				nameof( frame )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource is no longer current for animation control."
			);
		}

		TerminalCapabilityStatus capability = this.InspectCapability(
			TerminalCapability.PersistentRasterAnimation
		);
		if ( TerminalCapabilityEndpointAvailability.Unavailable
			== capability.EndpointAvailability ) {
			return TerminalControlMutationResult.Unavailable(
				"Persistent raster animation requires a terminal output endpoint."
			);
		}
		if ( TerminalCapabilitySupport.Unsupported == capability.Support ) {
			return TerminalControlMutationResult.Unsupported(
				"The verified terminal backend does not support persistent raster animation."
			);
		}

		uint imageId = resourceState.ImageId;
		if ( 0u == imageId ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource does not have a current terminal image identity."
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
		TerminalPersistentRasterAnimationFrameState? frameState = frame.State;
		if ( frameState is null ) {
			throw new ArgumentException(
				"The animation frame token is not bound to an acknowledged private frame identity.",
				nameof( frame )
			);
		}
		if ( !this.persistentRasterAnimationRegistry.OwnsFrame(
			animationState,
			frameState
		) ) {
			return TerminalControlMutationResult.Unavailable(
				"The animation frame is no longer current for this session generation."
			);
		}

		ReadOnlyMemory<byte> payload = selectCurrent
			? KittyGraphicsPersistentAnimationEncoder.EncodeCurrentFramePayload(
				imageId,
				frameState.FrameNumber
			)
			: KittyGraphicsPersistentAnimationEncoder.EncodeFrameDurationPayload(
				imageId,
				frameState.FrameNumber,
				gapMilliseconds!.Value
			)
		;
		KittyGraphicsPersistentAnimationResponseMatcher matcher = new( imageId );
		TerminalQueryResponseResult queryResult = await this.GetQueryTransactionManager().ExecuteAsync(
			_ => this.WritePersistentRasterAnimationControlPayloadCoreAsync( payload ),
			TerminalQueryResponsePlan.ForCompletion( matcher ),
			PersistentRasterCreationTimeout,
			TerminalQueryTransactionManager.DefaultLateResponseOwnership,
			cancellationToken
		).ConfigureAwait( false );

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
				return TerminalControlMutationResult.Unavailable(
					response.Message
				);
			}
			return TerminalControlMutationResult.Failed( response.Message );
		}

		this.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyPersistentRasterAnimation,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		return TerminalControlMutationResult.Success();
	}

	private async ValueTask WritePersistentRasterAnimationControlPayloadCoreAsync(
		ReadOnlyMemory<byte> payload
	) {
		if ( payload.IsEmpty ) {
			throw new ArgumentException(
				"A persistent raster animation control payload cannot be empty.",
				nameof( payload )
			);
		}

		await this.WritePersistentRasterControlFrameCoreAsync(
			payload
		).ConfigureAwait( false );
		await this.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private void CleanupAbandonedAnimationAppend(
		TerminalPersistentRasterAnimationRegistry.AppendReservation reservation,
		TerminalPersistentRasterAnimationState animationState,
		KittyGraphicsPersistentAnimationAppendCommitment commitment
	) {
		this.CleanupFailedAnimationAppend(
			reservation,
			animationState,
			commitment
		);
	}

	private void CleanupFailedAnimationAppend(
		TerminalPersistentRasterAnimationRegistry.AppendReservation reservation,
		TerminalPersistentRasterAnimationState animationState,
		KittyGraphicsPersistentAnimationAppendCommitment commitment
	) {
		ArgumentNullException.ThrowIfNull( reservation );
		ArgumentNullException.ThrowIfNull( animationState );
		ArgumentNullException.ThrowIfNull( commitment );

		_ = this.persistentRasterAnimationRegistry.TryRollbackAppend( reservation );
		if ( commitment.IsCommitted ) {
			_ = animationState.TryMarkSequenceUncertain();
		}
	}
}
