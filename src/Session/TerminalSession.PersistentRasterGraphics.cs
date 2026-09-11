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
		return TerminalControlResult<TerminalRasterResource>.Available(
			new TerminalRasterResource(
				this,
				resourceState
			)
		);
	}

	internal ValueTask ReleasePersistentRasterResourceAsync(
		TerminalPersistentRasterResourceState resourceState
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		_ = this.persistentRasterRegistry.TryReleaseResource( resourceState );
		return ValueTask.CompletedTask;
	}
}
