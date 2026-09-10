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
/// Internal Kitty Graphics capability probing and evidence integration.
/// </summary>
public sealed partial class TerminalSession {
	private static int kittyGraphicsProbeIdentity;

	private static TimeSpan KittyGraphicsProbeTimeout {
		get;
	} = TimeSpan.FromSeconds( 1 );

	internal ValueTask<bool> ProbeKittyGraphicsSupportAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.ProbeKittyGraphicsSupportAsync(
			CreateKittyGraphicsProbeImageId(),
			cancellationToken
		);
	}

	internal async ValueTask<bool> ProbeKittyGraphicsSupportAsync(
		uint imageId,
		CancellationToken cancellationToken = default
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}
		cancellationToken.ThrowIfCancellationRequested();

		TerminalInputCoordinator coordinator = this.GetInputCoordinator();
		KittyGraphicsSupportProbe probe =
			coordinator.RegisterKittyGraphicsSupportProbe( imageId );
		try {
			byte[] request = KittyGraphicsCapabilityProtocol.CreateProbeRequest( imageId );
			TerminalResponseFrame primaryDaFrame;
			try {
				primaryDaFrame = await this.ExecuteQueryAsync(
					request,
					TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
					KittyGraphicsProbeTimeout,
					cancellationToken
				).ConfigureAwait( false );
			} catch ( TimeoutException ) {
				if ( probe.Failure is not null ) {
					throw probe.Failure;
				}
				if ( probe.Response is not null ) {
					this.RecordSemanticBackendEvidence(
						TerminalProtocolBackend.ApcKittyGraphics,
						TerminalCapabilitySupportState.Verified,
						TerminalCapabilityEvidenceSource.ProtocolResponse
					);
					return true;
				}

				this.RecordSemanticBackendEvidence(
					TerminalProtocolBackend.ApcKittyGraphics,
					TerminalCapabilitySupportState.Unknown,
					TerminalCapabilityEvidenceSource.LiveProbe
				);
				return false;
			}

			TerminalPrimaryDeviceAttributes attributes =
				TerminalCsiQueryProtocol.ParsePrimaryDeviceAttributes( primaryDaFrame );
			this.RecordPrimaryDeviceAttributesCapabilityEvidence( attributes );

			if ( probe.Failure is not null ) {
				throw probe.Failure;
			}
			if ( probe.Response is not null ) {
				this.RecordSemanticBackendEvidence(
					TerminalProtocolBackend.ApcKittyGraphics,
					TerminalCapabilitySupportState.Verified,
					TerminalCapabilityEvidenceSource.ProtocolResponse
				);
				return true;
			}

			this.RecordSemanticBackendEvidence(
				TerminalProtocolBackend.ApcKittyGraphics,
				TerminalCapabilitySupportState.Unsupported,
				TerminalCapabilityEvidenceSource.ProtocolResponse
			);
			return false;
		} finally {
			coordinator.RemoveKittyGraphicsSupportProbe( probe );
		}
	}

	private static uint CreateKittyGraphicsProbeImageId() {
		while ( true ) {
			uint value = unchecked(
				(uint)Interlocked.Increment( ref kittyGraphicsProbeIdentity )
			);
			if ( 0 != value ) {
				return value;
			}
		}
	}
}
