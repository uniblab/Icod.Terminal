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
/// Creates and owns persistent terminal-resident raster resources for a live session.
/// </summary>
public sealed partial class TerminalSession {
	private static TimeSpan PersistentRasterCreationTimeout {
		get;
	} = TimeSpan.FromSeconds( 1 );

	private readonly TerminalPersistentRasterRegistry persistentRasterRegistry = new();

	/// <summary>
	/// Uploads one raster as a persistent terminal-resident resource through a verified backend.
	/// </summary>
	/// <param name="image">The immutable backend-neutral raster image.</param>
	/// <param name="cancellationToken">
	/// Cancellation observed before upload commitment and while the caller waits for the correlated
	/// acknowledgement. Once output commits, caller cancellation does not truncate the upload frames.
	/// </param>
	/// <returns>
	/// An available opaque resource after correlated acknowledgement, or a controlled unavailable,
	/// unsupported, or failed result when the persistent operation cannot be established.
	/// </returns>
	public async ValueTask<TerminalControlResult<TerminalRasterResource>> CreateRasterResourceAsync(
		TerminalRasterImage image,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( image );
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		TerminalCapabilityStatus capability = this.InspectCapability(
			TerminalCapability.PersistentRasterGraphics
		);
		if ( TerminalCapabilityEndpointAvailability.Unavailable
			== capability.EndpointAvailability ) {
			return TerminalControlResult<TerminalRasterResource>.Unavailable(
				"Persistent raster graphics require an interactive terminal output endpoint."
			);
		}
		if ( TerminalCapabilitySupport.Unsupported == capability.Support ) {
			return TerminalControlResult<TerminalRasterResource>.Unsupported(
				"The verified terminal backend does not support persistent raster graphics."
			);
		}
		if ( TerminalCapabilitySupport.Verified != capability.Support
			|| !capability.IsUsable ) {
			return TerminalControlResult<TerminalRasterResource>.Unavailable(
				"Persistent raster graphics must be explicitly verified before resource creation."
			);
		}

		KittyRasterData raster = KittyRasterAdapter.Adapt( image );
		if ( !this.persistentRasterRegistry.TryReserveResource(
			out TerminalPersistentRasterResourceState? resourceState
		) ) {
			return TerminalControlResult<TerminalRasterResource>.Unavailable(
				$"The session already owns the maximum {TerminalPersistentRasterRegistry.MaximumResources} persistent raster resources."
			);
		}
		if ( resourceState is null ) {
			throw new InvalidOperationException(
				"The persistent raster registry reported a successful reservation without state."
			);
		}

		KittyGraphicsPersistentResponseMatcher matcher = new(
			resourceState.ImageNumber,
			validateMatchedResponse: false
		);
		ValueTask<TerminalQueryResponseResult> transaction;
		try {
			transaction = this.GetQueryTransactionManager().ExecuteAsync(
				_ => KittyGraphicsPersistentUploadTransaction.WriteAsync(
					this,
					raster,
					resourceState.ImageNumber
				),
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				PersistentRasterCreationTimeout,
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken,
				abandonedCleanup: () => {
					_ = this.persistentRasterRegistry.TryReleaseResource(
						resourceState
					);
				}
			);
		} catch {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			throw;
		}

		TerminalQueryResponseResult queryResult = await transaction.ConfigureAwait( false );
		KittyGraphicsPersistentCreationResponse response;
		try {
			response = KittyGraphicsPersistentCreationResponse.Parse(
				queryResult.Frame,
				resourceState.ImageNumber
			);
		} catch {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			throw;
		}

		if ( !response.IsSuccess ) {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			return response.IsUnavailable
				? TerminalControlResult<TerminalRasterResource>.Unavailable(
					response.Message
				)
				: TerminalControlResult<TerminalRasterResource>.Failed(
					response.Message
				)
			;
		}
		if ( !response.ImageId.HasValue ) {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			throw new FormatException(
				"A successful persistent raster upload did not return a terminal-assigned image id."
			);
		}

		resourceState.BindImageId( response.ImageId.Value );
		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlResult<TerminalRasterResource>.Unavailable(
				"The persistent raster resource lost generation ownership before creation completed."
			);
		}

		return TerminalControlResult<TerminalRasterResource>.Available(
			new TerminalRasterResource(
				this,
				resourceState
			)
		);
	}

	internal async ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreatePersistentRasterPlacementAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterPlacementOptions? options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		options?.Validate();
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();
		if ( resourceState.IsClosed ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterResource ),
				"The persistent raster resource has already been disposed."
			);
		}
		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				"The persistent raster resource is no longer current for this session generation."
			);
		}
		if ( 0u == resourceState.ImageId ) {
			throw new InvalidOperationException(
				"The persistent raster resource does not have a terminal-assigned image id."
			);
		}

		if ( !this.persistentRasterRegistry.TryReservePlacement(
			resourceState,
			out TerminalPersistentRasterPlacementState? placementState
		) ) {
			if ( resourceState.IsClosed ) {
				throw new ObjectDisposedException(
					nameof( TerminalRasterResource ),
					"The persistent raster resource has already been disposed."
				);
			}
			if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
				return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
					"The persistent raster resource is no longer current for this session generation."
				);
			}
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				$"The session already owns the maximum {TerminalPersistentRasterRegistry.MaximumPlacements} persistent raster placements."
			);
		}
		if ( placementState is null ) {
			throw new InvalidOperationException(
				"The persistent raster registry reported a successful placement reservation without state."
			);
		}

		try {
			await KittyGraphicsPersistentPlacementTransaction.WriteAsync(
				this,
				resourceState.ImageId,
				placementState.PlacementId,
				options,
				cancellationToken
			).ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				"The persistent raster placement lost generation ownership before creation completed."
			);
		}

		return TerminalControlResult<TerminalRasterPlacement>.Available(
			new TerminalRasterPlacement(
				this,
				placementState
			)
		);
	}

	internal async ValueTask<TerminalControlMutationResult> UpdatePersistentRasterPlacementAsync(
		TerminalPersistentRasterPlacementState placementState,
		TerminalRasterPlacementOptions? options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( placementState );
		options?.Validate();
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster placement is no longer current for this session generation."
			);
		}

		uint imageId = placementState.Resource.ImageId;
		if ( 0u == imageId ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource no longer has a usable terminal image identity."
			);
		}

		await KittyGraphicsPersistentPlacementTransaction.WriteCoreAsync(
			this,
			imageId,
			placementState.PlacementId,
			options
		).ConfigureAwait( false );
		return TerminalControlMutationResult.Success();
	}

	internal async ValueTask ReleasePersistentRasterResourceAsync(
		TerminalPersistentRasterResourceState resourceState
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			return;
		}

		IDisposable outputLease;
		try {
			outputLease = await this.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
			throw;
		}

		using ( outputLease ) {
			if ( !this.persistentRasterRegistry.TryReleaseResource(
				resourceState,
				out TerminalPersistentRasterPlacementState[] releasedPlacements
			) ) {
				return;
			}

			uint imageId = resourceState.ImageId;
			if ( 0u == imageId ) {
				return;
			}

			List<Exception> failures = [];
			foreach ( TerminalPersistentRasterPlacementState placement in releasedPlacements ) {
				try {
					await this.WritePersistentRasterControlFrameCoreAsync(
						KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
							imageId,
							placement.PlacementId
						)
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					failures.Add( exception );
				}
			}

			try {
				await this.WritePersistentRasterControlFrameCoreAsync(
					KittyGraphicsPersistentEncoder.EncodeDeleteResourcePayload(
						imageId
					)
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				failures.Add( exception );
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

	internal async ValueTask ReleasePersistentRasterPlacementAsync(
		TerminalPersistentRasterPlacementState placementState
	) {
		ArgumentNullException.ThrowIfNull( placementState );
		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			return;
		}

		IDisposable outputLease;
		try {
			outputLease = await this.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		using ( outputLease ) {
			if ( !this.persistentRasterRegistry.TryReleasePlacement( placementState ) ) {
				return;
			}

			uint imageId = placementState.Resource.ImageId;
			if ( 0u == imageId ) {
				return;
			}

			await this.WritePersistentRasterControlFrameCoreAsync(
				KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
					imageId,
					placementState.PlacementId
				)
			).ConfigureAwait( false );
			await this.Output.FlushAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}

	private void InvalidatePersistentRasterState() {
		this.persistentRasterRegistry.Invalidate();
	}

	private async ValueTask<Exception?> ClosePersistentRasterStateAsync() {
		IDisposable outputLease;
		try {
			outputLease = await this.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception exception ) {
			this.persistentRasterRegistry.DrainCurrent(
				out _,
				out _
			);
			return exception;
		}

		using ( outputLease ) {
			this.persistentRasterRegistry.DrainCurrent(
				out TerminalPersistentRasterPlacementState[] releasedPlacements,
				out TerminalPersistentRasterResourceState[] releasedResources
			);
			if ( 0 == releasedPlacements.Length
				&& 0 == releasedResources.Length ) {
				return null;
			}

			List<Exception> failures = [];
			foreach ( TerminalPersistentRasterPlacementState placement in releasedPlacements ) {
				uint imageId = placement.Resource.ImageId;
				if ( 0u == imageId ) {
					continue;
				}

				try {
					await this.WritePersistentRasterControlFrameCoreAsync(
						KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
							imageId,
							placement.PlacementId
						)
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					failures.Add( exception );
				}
			}

			foreach ( TerminalPersistentRasterResourceState resource in releasedResources ) {
				uint imageId = resource.ImageId;
				if ( 0u == imageId ) {
					continue;
				}

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

			return failures.Count switch {
				0 => null,
				1 => failures[ 0 ],
				_ => new AggregateException(
					"Multiple persistent raster cleanup operations failed.",
					failures
				)
			};
		}
	}

	private ValueTask WritePersistentRasterControlFrameCoreAsync(
		ReadOnlyMemory<byte> payload
	) {
		if ( payload.IsEmpty ) {
			throw new ArgumentException(
				"A persistent raster control payload cannot be empty.",
				nameof( payload )
			);
		}

		return this.Output.WriteAsync(
			ApcWriter.EncodeFrame( payload.Span ),
			CancellationToken.None
		);
	}
}
