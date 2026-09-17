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
/// Owns acknowledged terminal-driven persistent-raster animation playback controls.
/// </summary>
public sealed partial class TerminalSession {
	internal ValueTask<TerminalControlMutationResult> StopPersistentRasterAnimationAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		CancellationToken cancellationToken
	) {
		return this.ControlPersistentRasterAnimationPlaybackAsync(
			resourceState,
			animation,
			static imageId => KittyGraphicsPersistentAnimationEncoder.EncodeStopPayload(
				imageId
			),
			allowSequenceUncertain: true,
			cancellationToken
		);
	}

	internal ValueTask<TerminalControlMutationResult> RunLoadingPersistentRasterAnimationAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		CancellationToken cancellationToken
	) {
		return this.ControlPersistentRasterAnimationPlaybackAsync(
			resourceState,
			animation,
			static imageId => KittyGraphicsPersistentAnimationEncoder.EncodeRunLoadingPayload(
				imageId
			),
			allowSequenceUncertain: false,
			cancellationToken
		);
	}

	internal ValueTask<TerminalControlMutationResult> RunPersistentRasterAnimationAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		int? repeatCount,
		CancellationToken cancellationToken
	) {
		return this.ControlPersistentRasterAnimationPlaybackAsync(
			resourceState,
			animation,
			imageId => KittyGraphicsPersistentAnimationEncoder.EncodeRunPayload(
				imageId,
				repeatCount
			),
			allowSequenceUncertain: false,
			cancellationToken
		);
	}

	private async ValueTask<TerminalControlMutationResult> ControlPersistentRasterAnimationPlaybackAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterAnimation animation,
		Func<uint, ReadOnlyMemory<byte>> encodePayload,
		bool allowSequenceUncertain,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( encodePayload );
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource is no longer current for animation playback control."
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

		TerminalRasterAnimationStatus status = animationState.ObserveState().Status;
		if ( TerminalRasterAnimationStatus.Current != status
			&& !( allowSequenceUncertain
				&& TerminalRasterAnimationStatus.SequenceUncertain == status ) ) {
			return TerminalControlMutationResult.Unavailable(
				allowSequenceUncertain
					? "The persistent raster animation is no longer available for playback control."
					: "The persistent raster animation frame sequence is not certain enough to start terminal-driven playback."
			);
		}

		ReadOnlyMemory<byte> payload = encodePayload( imageId );
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
}
