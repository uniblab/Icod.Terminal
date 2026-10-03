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
	private static int kittyPersistentRasterProbeIdentity;

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

		long evidenceGeneration = this.GetSemanticCapabilityEvidence().LiveGeneration;
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
						TerminalCapabilityEvidenceSource.ProtocolResponse,
						evidenceGeneration
					);
					return evidenceGeneration == this.GetSemanticCapabilityEvidence().LiveGeneration;
				}
				if ( probe.CorrelationObserved ) {
					throw new FormatException(
						"The correlated Kitty Graphics response did not terminate structurally before the probe deadline."
					);
				}

				this.RecordSemanticBackendEvidence(
					TerminalProtocolBackend.ApcKittyGraphics,
					TerminalCapabilitySupportState.Unknown,
					TerminalCapabilityEvidenceSource.LiveProbe,
					evidenceGeneration
				);
				return false;
			}

			TerminalPrimaryDeviceAttributes attributes =
				TerminalCsiQueryProtocol.ParsePrimaryDeviceAttributes( primaryDaFrame );
			this.RecordPrimaryDeviceAttributesCapabilityEvidence( attributes, evidenceGeneration );

			if ( probe.Failure is not null ) {
				throw probe.Failure;
			}
			if ( probe.Response is not null ) {
				this.RecordSemanticBackendEvidence(
					TerminalProtocolBackend.ApcKittyGraphics,
					TerminalCapabilitySupportState.Verified,
					TerminalCapabilityEvidenceSource.ProtocolResponse,
					evidenceGeneration
				);
				return evidenceGeneration == this.GetSemanticCapabilityEvidence().LiveGeneration;
			}

			this.RecordSemanticBackendEvidence(
				TerminalProtocolBackend.ApcKittyGraphics,
				TerminalCapabilitySupportState.Unsupported,
				TerminalCapabilityEvidenceSource.ProtocolResponse,
				evidenceGeneration
			);
			return false;
		} finally {
			coordinator.RemoveKittyGraphicsSupportProbe( probe );
		}
	}

	internal async ValueTask<bool> ProbeKittyPersistentRasterSupportAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalCapabilityEvidenceLedger evidence = this.GetSemanticCapabilityEvidence();
		long evidenceGeneration = evidence.LiveGeneration;
		TerminalCapabilityResolution kitty = evidence.Resolve(
			TerminalCapabilitySubject.ForProtocolBackend(
				TerminalProtocolBackend.ApcKittyGraphics
			)
		);
		if ( TerminalCapabilitySupportState.Verified != kitty.State ) {
			bool kittySupported = await this.ProbeKittyGraphicsSupportAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( !kittySupported ) {
				TerminalCapabilityResolution currentKitty = evidence.Resolve(
					TerminalCapabilitySubject.ForProtocolBackend(
						TerminalProtocolBackend.ApcKittyGraphics
					)
				);
				this.RecordSemanticOperationEvidence(
					TerminalSemanticOperation.PersistentRasterGraphics,
					TerminalCapabilitySupportState.Unsupported == currentKitty.State
						? TerminalCapabilitySupportState.Unsupported
						: TerminalCapabilitySupportState.Unknown,
					currentKitty.EvidenceSource
						?? TerminalCapabilityEvidenceSource.LiveProbe,
					evidenceGeneration
				);
				return false;
			}
		}

		uint imageNumber = CreateKittyPersistentRasterProbeImageNumber();
		KittyRasterData raster = new(
			1,
			1,
			KittyGraphicsPixelFormat.Rgb24,
			new byte[ 3 ]
		);
		KittyGraphicsPersistentResponseMatcher matcher = new(
			imageNumber,
			validateMatchedResponse: false
		);
		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await this.GetQueryTransactionManager().ExecuteAsync(
				_ => KittyGraphicsPersistentUploadTransaction.WriteAsync(
					this,
					raster,
					imageNumber
				),
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				KittyGraphicsProbeTimeout,
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( TimeoutException ) {
			this.RecordSemanticOperationEvidence(
				TerminalSemanticOperation.PersistentRasterGraphics,
				TerminalCapabilitySupportState.Unknown,
				TerminalCapabilityEvidenceSource.LiveProbe,
				evidenceGeneration
			);
			return false;
		}

		KittyGraphicsPersistentCreationResponse response;
		try {
			response = KittyGraphicsPersistentCreationResponse.Parse(
				queryResult.Frame,
				imageNumber
			);
		} catch ( FormatException ) {
			this.RecordSemanticOperationEvidence(
				TerminalSemanticOperation.PersistentRasterGraphics,
				TerminalCapabilitySupportState.Unsupported,
				TerminalCapabilityEvidenceSource.ProtocolResponse,
				evidenceGeneration
			);
			return false;
		}

		if ( !response.IsSuccess || !response.ImageId.HasValue ) {
			this.RecordSemanticOperationEvidence(
				TerminalSemanticOperation.PersistentRasterGraphics,
				TerminalCapabilitySupportState.Unsupported,
				TerminalCapabilityEvidenceSource.ProtocolResponse,
				evidenceGeneration
			);
			return false;
		}

		using ( await this.AcquireControlOutputAsync(
			CancellationToken.None
		).ConfigureAwait( false ) ) {
			await this.WritePersistentRasterControlFrameCoreAsync(
				KittyGraphicsPersistentEncoder.EncodeDeleteResourcePayload(
					response.ImageId.Value
				)
			).ConfigureAwait( false );
			await this.Output.FlushAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		}

		this.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.PersistentRasterGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		return evidenceGeneration == evidence.LiveGeneration;
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

	private static uint CreateKittyPersistentRasterProbeImageNumber() {
		while ( true ) {
			uint value = unchecked(
				(uint)Interlocked.Increment( ref kittyPersistentRasterProbeIdentity )
			);
			value |= 0x80000000u;
			if ( 0 != value ) {
				return value;
			}
		}
	}
}
