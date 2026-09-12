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
/// Parses one bounded persistent Kitty Graphics placement acknowledgement correlated by
/// terminal image and placement identity.
/// </summary>
internal sealed class KittyGraphicsPersistentPlacementResponse {
	private KittyGraphicsPersistentPlacementResponse(
		uint imageId,
		uint placementId,
		bool isSuccess,
		string message
	) {
		this.ImageId = imageId;
		this.PlacementId = placementId;
		this.IsSuccess = isSuccess;
		this.Message = message;
	}

	internal uint ImageId {
		get;
	}

	internal uint PlacementId {
		get;
	}

	internal bool IsSuccess {
		get;
	}

	internal bool IsUnavailable {
		get {
			return !this.IsSuccess
				&& this.Message.StartsWith(
					"ENOENT",
					StringComparison.Ordinal
				);
		}
	}

	internal string Message {
		get;
	}

	internal static KittyGraphicsPersistentPlacementResponse Parse(
		TerminalResponseFrame frame,
		uint expectedImageId,
		uint expectedPlacementId
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( 0u == expectedImageId ) {
			throw new ArgumentOutOfRangeException( nameof( expectedImageId ) );
		}
		if ( 0u == expectedPlacementId ) {
			throw new ArgumentOutOfRangeException( nameof( expectedPlacementId ) );
		}

		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse( frame );
		if ( expectedImageId != response.ImageId ) {
			throw new FormatException(
				"The persistent Kitty Graphics placement response contains the wrong image id."
			);
		}
		if ( !response.PlacementId.HasValue ) {
			throw new FormatException(
				"A persistent Kitty Graphics placement response must contain the correlated placement id."
			);
		}
		if ( expectedPlacementId != response.PlacementId.Value ) {
			throw new FormatException(
				"The persistent Kitty Graphics placement response contains the wrong placement id."
			);
		}

		return new KittyGraphicsPersistentPlacementResponse(
			response.ImageId,
			response.PlacementId.Value,
			response.IsSuccess,
			response.Message
		);
	}
}
