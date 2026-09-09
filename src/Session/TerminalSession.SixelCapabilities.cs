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
/// Internal Sixel capability observation and Primary Device Attributes evidence integration.
/// </summary>
public sealed partial class TerminalSession {
	private const int SixelPrimaryDeviceAttribute = 4;

	private static TimeSpan SixelProbeTimeout {
		get;
	} = TimeSpan.FromSeconds( 1 );

	internal async ValueTask<bool> ProbeSixelSupportAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();

		try {
			TerminalPrimaryDeviceAttributes attributes =
				await this.QueryPrimaryDeviceAttributesAsync(
					SixelProbeTimeout,
					cancellationToken
				).ConfigureAwait( false );
			return attributes.HasAttribute( SixelPrimaryDeviceAttribute );
		} catch ( TimeoutException ) {
			this.RecordSemanticBackendEvidence(
				TerminalProtocolBackend.DcsSixel,
				TerminalCapabilitySupportState.Unknown,
				TerminalCapabilityEvidenceSource.LiveProbe
			);
			return false;
		}
	}

	private void RecordPrimaryDeviceAttributesCapabilityEvidence(
		TerminalPrimaryDeviceAttributes attributes
	) {
		ArgumentNullException.ThrowIfNull( attributes );

		this.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.DcsSixel,
			attributes.HasAttribute( SixelPrimaryDeviceAttribute )
				? TerminalCapabilitySupportState.Verified
				: TerminalCapabilitySupportState.Unknown,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
	}
}
