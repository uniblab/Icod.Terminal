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
/// Identifies a semantic terminal capability that callers may inspect without learning
/// which concrete terminal protocol implements it.
/// </summary>
public enum TerminalCapability {
	/// <summary>
	/// Reading terminal clipboard or selection content.
	/// </summary>
	ClipboardRead,

	/// <summary>
	/// Writing terminal clipboard or selection content.
	/// </summary>
	ClipboardWrite,

	/// <summary>
	/// Selecting the terminal cursor presentation style.
	/// </summary>
	CursorStyle,

	/// <summary>
	/// Grouping terminal output into synchronized presentation transactions.
	/// </summary>
	SynchronizedOutput,

	/// <summary>
	/// Acquiring modern terminal keyboard reporting.
	/// </summary>
	KeyboardReporting,

	/// <summary>
	/// Acquiring terminal mouse reporting.
	/// </summary>
	MouseReporting,

	/// <summary>
	/// Acquiring terminal focus reporting.
	/// </summary>
	FocusReporting,

	/// <summary>
	/// Acquiring bracketed-paste framing.
	/// </summary>
	BracketedPaste,

	/// <summary>
	/// Displaying backend-neutral raster graphics.
	/// </summary>
	RasterGraphics,

	/// <summary>
	/// Owning terminal-resident persistent raster resources and placements.
	/// </summary>
	PersistentRasterGraphics
}

/// <summary>
/// Describes the session's current semantic support knowledge for a terminal capability.
/// </summary>
public enum TerminalCapabilitySupport {
	/// <summary>
	/// The session does not currently have decisive support evidence.
	/// </summary>
	Unknown,

	/// <summary>
	/// Current live evidence establishes that the capability is not supported.
	/// </summary>
	Unsupported,

	/// <summary>
	/// Static terminal-description evidence advertises support.
	/// </summary>
	Advertised,

	/// <summary>
	/// Current live observation establishes support.
	/// </summary>
	Verified
}

/// <summary>
/// Describes whether the endpoint required by one terminal capability is presently available.
/// </summary>
public enum TerminalCapabilityEndpointAvailability {
	/// <summary>
	/// The required terminal endpoint is unavailable through this session.
	/// </summary>
	Unavailable,

	/// <summary>
	/// The required terminal endpoint is available through this session.
	/// </summary>
	Available
}

/// <summary>
/// Describes the lifetime class of evidence supporting one capability status without exposing
/// dependency-specific or protocol-specific implementation details.
/// </summary>
public enum TerminalCapabilityEvidenceKind {
	/// <summary>
	/// No effective evidence currently contributes to the support answer.
	/// </summary>
	None,

	/// <summary>
	/// Evidence comes from a static terminal description or reviewed static profile.
	/// </summary>
	StaticDescription,

	/// <summary>
	/// Evidence comes from current lifecycle-generation live observation.
	/// </summary>
	LiveObservation
}

/// <summary>
/// Represents the current side-effect-free planning status of one semantic terminal capability.
/// </summary>
public readonly record struct TerminalCapabilityStatus {
	internal TerminalCapabilityStatus(
		TerminalCapability capability,
		TerminalCapabilitySupport support,
		TerminalCapabilityEndpointAvailability endpointAvailability,
		TerminalCapabilityEvidenceKind evidenceKind,
		bool isUsable
	) {
		if ( !Enum.IsDefined( capability ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( capability ),
				capability,
				"The terminal capability is not recognized."
			);
		}
		if ( !Enum.IsDefined( support ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( support ),
				support,
				"The terminal capability support state is not recognized."
			);
		}
		if ( !Enum.IsDefined( endpointAvailability ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( endpointAvailability ),
				endpointAvailability,
				"The terminal capability endpoint availability is not recognized."
			);
		}
		if ( !Enum.IsDefined( evidenceKind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( evidenceKind ),
				evidenceKind,
				"The terminal capability evidence kind is not recognized."
			);
		}
		if ( TerminalCapabilitySupport.Advertised == support
			&& TerminalCapabilityEvidenceKind.StaticDescription != evidenceKind ) {
			throw new ArgumentException(
				"Advertised support must be backed by static terminal-description evidence.",
				nameof( evidenceKind )
			);
		}
		if ( support is TerminalCapabilitySupport.Verified
			or TerminalCapabilitySupport.Unsupported
			&& TerminalCapabilityEvidenceKind.LiveObservation != evidenceKind ) {
			throw new ArgumentException(
				"Verified or unsupported support must be backed by live observation evidence.",
				nameof( evidenceKind )
			);
		}
		if ( isUsable
			&& TerminalCapabilityEndpointAvailability.Unavailable == endpointAvailability ) {
			throw new ArgumentException(
				"A capability cannot be presently usable when its required endpoint is unavailable.",
				nameof( isUsable )
			);
		}
		if ( isUsable && TerminalCapabilitySupport.Unsupported == support ) {
			throw new ArgumentException(
				"An unsupported capability cannot be presently usable.",
				nameof( isUsable )
			);
		}

		this.Capability = capability;
		this.Support = support;
		this.EndpointAvailability = endpointAvailability;
		this.EvidenceKind = evidenceKind;
		this.IsUsable = isUsable;
	}

	/// <summary>
	/// Gets the semantic capability described by this status.
	/// </summary>
	public TerminalCapability Capability {
		get;
	}

	/// <summary>
	/// Gets the session's current support knowledge for the capability.
	/// </summary>
	public TerminalCapabilitySupport Support {
		get;
	}

	/// <summary>
	/// Gets whether the terminal endpoint required by the capability is presently available.
	/// </summary>
	public TerminalCapabilityEndpointAvailability EndpointAvailability {
		get;
	}

	/// <summary>
	/// Gets the lifetime class of evidence contributing to the current support answer.
	/// </summary>
	public TerminalCapabilityEvidenceKind EvidenceKind {
		get;
	}

	/// <summary>
	/// Gets whether the capability can presently be used through this session under its existing
	/// semantic routing policy and current endpoint availability.
	/// </summary>
	public bool IsUsable {
		get;
	}
}
