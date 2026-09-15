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

internal sealed class KittyGraphicsPersistentAnimationResponse {
	private KittyGraphicsPersistentAnimationResponse(
		uint imageId,
		bool isSuccess,
		string message
	) {
		this.ImageId = imageId;
		this.IsSuccess = isSuccess;
		this.Message = message;
	}

	internal uint ImageId {
		get;
	}

	internal bool IsSuccess {
		get;
	}

	internal bool IsMissingResource {
		get {
			return !this.IsSuccess
				&& this.Message.StartsWith( "ENOENT", StringComparison.Ordinal );
		}
	}

	internal string Message {
		get;
	}

	internal static KittyGraphicsPersistentAnimationResponse Parse(
		TerminalResponseFrame frame,
		uint expectedImageId
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( 0u == expectedImageId ) {
			throw new ArgumentOutOfRangeException( nameof( expectedImageId ) );
		}

		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse( frame );
		if ( expectedImageId != response.ImageId ) {
			throw new FormatException(
				"The persistent Kitty Graphics animation response contains the wrong image id."
			);
		}
		if ( response.ImageNumber.HasValue || response.PlacementId.HasValue ) {
			throw new FormatException(
				"A persistent Kitty Graphics animation response must identify only the existing image id."
			);
		}

		return new KittyGraphicsPersistentAnimationResponse(
			response.ImageId,
			response.IsSuccess,
			response.Message
		);
	}
}
