/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Session;

using System.Reflection;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Freezes focused generation-scoped raster-operation evidence.</summary>
public sealed class TerminalRasterOperationEvidenceTests {
	[Fact]
	public async Task PublicSurfaceHasThreeFrozenOperationsAndNoPublicStatusConstructor() {
		Assert.Equal( 0, (int)TerminalRasterOperation.FrameComposition );
		Assert.Equal( 1, (int)TerminalRasterOperation.FrameRegionUpdateRgb24 );
		Assert.Equal( 2, (int)TerminalRasterOperation.FrameRegionUpdateRgba32 );
		Assert.Empty(
			typeof( TerminalRasterOperationStatus ).GetConstructors(
				BindingFlags.Instance | BindingFlags.Public
			)
		);
		Assert.DoesNotContain(
			typeof( TerminalSession ).GetMethods( BindingFlags.Instance | BindingFlags.Public ),
			method => method.Name.Contains( "VerifyRasterOperation", StringComparison.Ordinal )
		);

		await using TerminalSession session = await OpenSessionAsync( outputIsTerminal: true );
		Assert.Throws<ArgumentOutOfRangeException>(
			() => session.InspectRasterOperation( (TerminalRasterOperation)int.MaxValue )
		);
	}

	[Fact]
	public async Task InitialStatusIsUnknownButUsableOnlyWithOutputEndpoint() {
		await using TerminalSession available = await OpenSessionAsync( outputIsTerminal: true );
		foreach ( TerminalRasterOperation operation in Enum.GetValues<TerminalRasterOperation>() ) {
			TerminalRasterOperationStatus status = available.InspectRasterOperation( operation );
			Assert.Equal( operation, status.Operation );
			Assert.Equal( TerminalCapabilitySupport.Unknown, status.Support );
			Assert.Equal( TerminalCapabilityEndpointAvailability.Available, status.EndpointAvailability );
			Assert.Equal( TerminalCapabilityEvidenceKind.None, status.EvidenceKind );
			Assert.True( status.IsUsable );
		}

		await using TerminalSession unavailable = await OpenSessionAsync( outputIsTerminal: false );
		TerminalRasterOperationStatus unavailableStatus = unavailable.InspectRasterOperation(
			TerminalRasterOperation.FrameComposition
		);
		Assert.Equal( TerminalCapabilitySupport.Unknown, unavailableStatus.Support );
		Assert.Equal( TerminalCapabilityEndpointAvailability.Unavailable, unavailableStatus.EndpointAvailability );
		Assert.Equal( TerminalCapabilityEvidenceKind.None, unavailableStatus.EvidenceKind );
		Assert.False( unavailableStatus.IsUsable );
	}

	[Fact]
	public async Task FocusedEvidenceIsIsolatedAndGenerationScoped() {
		await using TerminalSession session = await OpenSessionAsync( outputIsTerminal: true );
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyPersistentRasterAnimation,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		Assert.Equal(
			TerminalCapabilitySupport.Unknown,
			session.InspectRasterOperation( TerminalRasterOperation.FrameComposition ).Support
		);

		long evidenceGeneration = session.GetSemanticCapabilityEvidence().LiveGeneration;
		session.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.RasterFrameRegionUpdateRgb24,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		Assert.Equal(
			TerminalCapabilitySupport.Verified,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgb24
			).Support
		);
		Assert.Equal(
			TerminalCapabilitySupport.Unknown,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgba32
			).Support
		);
		Assert.Equal(
			TerminalCapabilitySupport.Unknown,
			session.InspectRasterOperation( TerminalRasterOperation.FrameComposition ).Support
		);

		session.AdvanceSemanticLiveEvidenceGeneration();
		session.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.RasterFrameRegionUpdateRgb24,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidenceGeneration
		);
		Assert.Equal(
			TerminalCapabilitySupport.Unknown,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgb24
			).Support
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		bool outputIsTerminal
	) {
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider( outputIsTerminal ),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyTerminalInput(),
			new NullTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
	}

	private sealed class EmptyTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class NullTerminalOutput : ITerminalOutput {
		public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly bool outputIsTerminal;
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0, 0, 0, 0x0002UL, new byte[ 32 ], 0, 32, 0,
			new TerminalSpeed( 13, 9600 ), new TerminalSpeed( 13, 9600 )
		);

		internal RecordingTerminalControlProvider( bool outputIsTerminal ) {
			this.outputIsTerminal = outputIsTerminal;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = 1 != endpoint.FileDescriptor || this.outputIsTerminal;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					isTerminal ? TerminalPlatformKind.PosixTermios : null,
					isTerminal
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize( TerminalEndpoint endpoint ) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported( "Size is not required by this test." );
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}
			return TerminalControlMutationResult.Success();
		}
	}
}
