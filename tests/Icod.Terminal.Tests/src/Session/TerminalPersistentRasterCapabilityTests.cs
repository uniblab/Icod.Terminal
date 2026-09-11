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

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Defines the C112 semantic capability contract for persistent raster ownership.
/// </summary>
public sealed class TerminalPersistentRasterCapabilityTests {
	[Fact]
	public void PersistentRasterCapabilityAppendsAtNine() {
		Assert.Equal( 0, (int)TerminalCapability.ClipboardRead );
		Assert.Equal( 1, (int)TerminalCapability.ClipboardWrite );
		Assert.Equal( 2, (int)TerminalCapability.CursorStyle );
		Assert.Equal( 3, (int)TerminalCapability.SynchronizedOutput );
		Assert.Equal( 4, (int)TerminalCapability.KeyboardReporting );
		Assert.Equal( 5, (int)TerminalCapability.MouseReporting );
		Assert.Equal( 6, (int)TerminalCapability.FocusReporting );
		Assert.Equal( 7, (int)TerminalCapability.BracketedPaste );
		Assert.Equal( 8, (int)TerminalCapability.RasterGraphics );
		Assert.Equal( 9, (int)TerminalCapability.PersistentRasterGraphics );
	}

	[Fact]
	public void PersistentRasterSemanticOperationUsesOnlyKittyGraphics() {
		IReadOnlyList<TerminalSemanticBackendCandidate> candidates =
			TerminalSemanticBackendRegistry.GetCandidates(
				TerminalSemanticOperation.PersistentRasterGraphics
			);

		TerminalSemanticBackendCandidate candidate = Assert.Single( candidates );
		Assert.Equal(
			TerminalProtocolBackend.ApcKittyGraphics,
			candidate.Backend
		);
	}

	[Fact]
	public async Task VerifiedSixelDoesNotVerifyPersistentRaster() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityStatus raster = session.InspectCapability(
			TerminalCapability.RasterGraphics
		);
		TerminalCapabilityStatus persistent = session.InspectCapability(
			TerminalCapability.PersistentRasterGraphics
		);

		Assert.Equal( TerminalCapabilitySupport.Verified, raster.Support );
		Assert.True( raster.IsUsable );
		Assert.Equal( TerminalCapabilitySupport.Unknown, persistent.Support );
		Assert.Equal( TerminalCapabilityEvidenceKind.None, persistent.EvidenceKind );
		Assert.False( persistent.IsUsable );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task VerifiedKittyVerifiesBothRasterCapabilitiesWithoutInspectionIo() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityStatus raster = session.InspectCapability(
			TerminalCapability.RasterGraphics
		);
		TerminalCapabilityStatus persistent = session.InspectCapability(
			TerminalCapability.PersistentRasterGraphics
		);

		Assert.Equal( TerminalCapabilitySupport.Verified, raster.Support );
		Assert.Equal( TerminalCapabilitySupport.Verified, persistent.Support );
		Assert.Equal(
			TerminalCapabilityEvidenceKind.LiveObservation,
			persistent.EvidenceKind
		);
		Assert.True( persistent.IsUsable );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task RedirectedOutputMakesPersistentRasterUnavailableWithoutProbe() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RedirectedOutputControlProvider(),
			output
		);

		TerminalCapabilityStatus status = session.InspectCapability(
			TerminalCapability.PersistentRasterGraphics
		);

		Assert.Equal( TerminalCapabilitySupport.Unknown, status.Support );
		Assert.Equal(
			TerminalCapabilityEndpointAvailability.Unavailable,
			status.EndpointAvailability
		);
		Assert.False( status.IsUsable );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task PersistentVerificationUsesKittyProbeRatherThanSixelProbe() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityStatus status = await session.VerifyCapabilityAsync(
			TerminalCapability.PersistentRasterGraphics
		);

		Assert.Equal( TerminalCapabilitySupport.Unknown, status.Support );
		byte[] bytes = output.Bytes.ToArray();
		Assert.NotEmpty( bytes );
		string ascii = Encoding.ASCII.GetString( bytes );
		Assert.Contains( "a=q,t=d,f=24;AAAA", ascii, StringComparison.Ordinal );
		Assert.DoesNotContain( "?1;1;0q", ascii, StringComparison.Ordinal );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalControlProvider controlProvider,
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			controlProvider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
	}

	private sealed class EmptyTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingOutput : ITerminalOutput {
		private readonly List<byte> bytes = [];

		internal IReadOnlyList<byte> Bytes {
			get {
				return this.bytes;
			}
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0x0002UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);

		public virtual TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not required by this test."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
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

	private sealed class RedirectedOutputControlProvider : RecordingTerminalControlProvider {
		public override TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isInput = ReferenceEquals( endpoint, TerminalEndpoint.StandardInput );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isInput,
					null,
					isInput ? TerminalPlatformKind.PosixTermios : null,
					isInput
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}
	}
}
