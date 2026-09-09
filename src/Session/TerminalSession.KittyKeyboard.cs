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
/// Internal Kitty progressive-keyboard negotiation support.
/// </summary>
public sealed partial class TerminalSession {
	private static TimeSpan KittyKeyboardProbeTimeout {
		get;
	} = TimeSpan.FromSeconds( 1 );

	internal async ValueTask<bool> ProbeKittyKeyboardSupportAfterResumeAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();

		bool useLifecycleObservation;
		lock ( this.queryTransactionSync ) {
			useLifecycleObservation = this.queryTransactionsSuspended;
		}

		if ( !useLifecycleObservation ) {
			return await this.ProbeKittyKeyboardSupportAsync(
				lifecycleObservation: false,
				cancellationToken
			).ConfigureAwait( false );
		}

		this.BeginLifecycleObservationQueryWindow();
		try {
			return await this.ProbeKittyKeyboardSupportAsync(
				lifecycleObservation: true,
				cancellationToken
			).ConfigureAwait( false );
		} finally {
			this.EndLifecycleObservationQueryWindow();
		}
	}

	internal async ValueTask<bool> ProbeKittyKeyboardSupportAsync(
		bool lifecycleObservation,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalInputCoordinator coordinator = this.GetInputCoordinator();
		KittyKeyboardFlagsProbe probe = coordinator.RegisterKittyKeyboardFlagsProbe();

		try {
			byte[] kittyQuery = CsiWriter.EncodeKittyKeyboardQueryFrame();
			ReadOnlyMemory<byte> primaryDa =
				TerminalCsiQueryProtocol.PrimaryDeviceAttributesRequest;
			byte[] request = new byte[ checked( kittyQuery.Length + primaryDa.Length ) ];
			kittyQuery.CopyTo(
				request,
				0
			);
			primaryDa.Span.CopyTo( request.AsSpan( kittyQuery.Length ) );

			try {
				_ = lifecycleObservation
					? await this.ExecuteLifecycleObservationQueryAsync(
						request,
						TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
						KittyKeyboardProbeTimeout,
						cancellationToken
					).ConfigureAwait( false )
					: await this.ExecuteQueryAsync(
						request,
						TerminalCsiQueryProtocol.PrimaryDeviceAttributesMatcher,
						KittyKeyboardProbeTimeout,
						cancellationToken
					).ConfigureAwait( false )
					;
			} catch ( TimeoutException ) {
				this.RecordSemanticBackendEvidence(
					TerminalProtocolBackend.CsiKittyKeyboard,
					TerminalCapabilitySupportState.Unknown,
					TerminalCapabilityEvidenceSource.LiveProbe
				);
				return false;
			}

			bool supported = probe.Flags.HasValue;
			this.RecordSemanticBackendEvidence(
				TerminalProtocolBackend.CsiKittyKeyboard,
				supported
					? TerminalCapabilitySupportState.Verified
					: TerminalCapabilitySupportState.Unsupported,
				TerminalCapabilityEvidenceSource.ProtocolResponse
			);
			return supported;
		} finally {
			coordinator.RemoveKittyKeyboardFlagsProbe( probe );
		}
	}
}
