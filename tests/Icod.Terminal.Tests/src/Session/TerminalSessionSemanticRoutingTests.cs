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

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies that N158 session routing combines selected TermInfo evidence with live evidence.
/// </summary>
public sealed class TerminalSessionSemanticRoutingTests {
	[Fact]
	public async Task SelectedTermInfoProfileSeedsExactClipboardRecipe() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "semantic-routing-ms" )
			.SetExtendedString( "Ms", "\u001b]52;%p1%s;%p2%s\u001b\\" )
			.Build();
		await using TerminalSession session = await OpenSessionAsync( terminal );

		TerminalSemanticBackendResolution resolution = session.ResolveSemanticBackend(
			TerminalSemanticOperation.ClipboardWrite
		);

		Assert.NotNull( resolution.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.TermInfoCapability,
			resolution.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalCapabilityEvidenceSource.TermInfo, resolution.EvidenceSource );
		Assert.Equal( TerminalBackendSelectionReason.TermInfo, resolution.SelectionReason );
	}

	[Fact]
	public async Task InvalidateStateExpiresLiveEvidenceAndRestoresStaticChoice() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "semantic-routing-live" )
			.SetExtendedString( "Ms", "\u001b]52;%p1%s;%p2%s\u001b\\" )
			.Build();
		await using TerminalSession session = await OpenSessionAsync( terminal );

		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.Osc52Clipboard,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		TerminalSemanticBackendResolution live = session.ResolveSemanticBackend(
			TerminalSemanticOperation.ClipboardWrite
		);
		Assert.NotNull( live.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.Osc52Clipboard,
			live.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalBackendSelectionReason.Verified, live.SelectionReason );

		session.InvalidateState();
		TerminalSemanticBackendResolution nextGeneration = session.ResolveSemanticBackend(
			TerminalSemanticOperation.ClipboardWrite
		);
		Assert.NotNull( nextGeneration.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.TermInfoCapability,
			nextGeneration.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalBackendSelectionReason.TermInfo, nextGeneration.SelectionReason );
	}

	[Fact]
	public async Task OutputOnlyIntentDoesNotRequireInteractiveInputEndpoint() {
		TerminalDescription terminal = TerminalProfiles.Dumb;
		TerminalSession session = await TerminalSession.OpenAsync(
			new OutputOnlyControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyTerminalInput(),
			new NullTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
		await using ( session ) {
			TerminalSemanticBackendResolution notification = session.ResolveSemanticBackend(
				TerminalSemanticOperation.DesktopNotification
			);
			TerminalSemanticBackendResolution clipboardRead = session.ResolveSemanticBackend(
				TerminalSemanticOperation.ClipboardRead
			);

			Assert.NotNull( notification.SelectedCandidate );
			Assert.Equal( TerminalBackendSelectionReason.SafeFallback, notification.SelectionReason );
			Assert.Equal( TerminalCapabilitySupportState.Unavailable, clipboardRead.State );
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyTerminalInput(),
			new NullTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
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

	private sealed class NullTerminalOutput : ITerminalOutput {
		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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

		public TerminalControlResult<TerminalEndpointObservation> Observe(
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

	private sealed class OutputOnlyControlProvider : ITerminalControlProvider {
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

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isOutput = ReferenceEquals( endpoint, TerminalEndpoint.StandardOutput );
			TerminalPlatformKind? platform = isOutput
				? TerminalPlatformKind.PosixTermios
				: null;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isOutput,
					null,
					platform,
					isOutput
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
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
}
