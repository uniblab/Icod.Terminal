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

/// <summary>Identifies one focused persistent-raster frame operation.</summary>
public enum TerminalRasterOperation {
	/// <summary>Composition between two known frames.</summary>
	FrameComposition = 0,

	/// <summary>Replacement of a known-frame region from RGB24 pixels.</summary>
	FrameRegionUpdateRgb24 = 1,

	/// <summary>Replacement of a known-frame region from RGBA32 pixels.</summary>
	FrameRegionUpdateRgba32 = 2
}

/// <summary>Describes current generation-scoped evidence for one focused raster operation.</summary>
public readonly record struct TerminalRasterOperationStatus {
	internal TerminalRasterOperationStatus(
		TerminalRasterOperation operation,
		TerminalCapabilitySupport support,
		TerminalCapabilityEndpointAvailability endpointAvailability,
		TerminalCapabilityEvidenceKind evidenceKind,
		bool isUsable
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException( nameof( operation ) );
		}
		if ( !Enum.IsDefined( support ) ) {
			throw new ArgumentOutOfRangeException( nameof( support ) );
		}
		if ( !Enum.IsDefined( endpointAvailability ) ) {
			throw new ArgumentOutOfRangeException( nameof( endpointAvailability ) );
		}
		if ( !Enum.IsDefined( evidenceKind ) ) {
			throw new ArgumentOutOfRangeException( nameof( evidenceKind ) );
		}
		if ( support is TerminalCapabilitySupport.Verified
			or TerminalCapabilitySupport.Unsupported
			&& TerminalCapabilityEvidenceKind.LiveObservation != evidenceKind ) {
			throw new ArgumentException(
				"Verified or unsupported operation support requires live evidence.",
				nameof( evidenceKind )
			);
		}
		if ( TerminalCapabilitySupport.Advertised == support
			&& TerminalCapabilityEvidenceKind.StaticDescription != evidenceKind ) {
			throw new ArgumentException(
				"Advertised operation support requires static evidence.",
				nameof( evidenceKind )
			);
		}
		if ( isUsable && TerminalCapabilityEndpointAvailability.Unavailable == endpointAvailability ) {
			throw new ArgumentException(
				"A raster operation cannot be usable without its output endpoint.",
				nameof( isUsable )
			);
		}
		if ( isUsable && TerminalCapabilitySupport.Unsupported == support ) {
			throw new ArgumentException(
				"An unsupported raster operation cannot be usable.",
				nameof( isUsable )
			);
		}

		this.Operation = operation;
		this.Support = support;
		this.EndpointAvailability = endpointAvailability;
		this.EvidenceKind = evidenceKind;
		this.IsUsable = isUsable;
	}

	/// <summary>Gets the focused raster operation.</summary>
	public TerminalRasterOperation Operation { get; }

	/// <summary>Gets current generation-scoped support knowledge.</summary>
	public TerminalCapabilitySupport Support { get; }

	/// <summary>Gets whether the required terminal output endpoint is available.</summary>
	public TerminalCapabilityEndpointAvailability EndpointAvailability { get; }

	/// <summary>Gets the lifetime class of the effective evidence.</summary>
	public TerminalCapabilityEvidenceKind EvidenceKind { get; }

	/// <summary>Gets whether the operation can presently be attempted under reviewed policy.</summary>
	public bool IsUsable { get; }
}
