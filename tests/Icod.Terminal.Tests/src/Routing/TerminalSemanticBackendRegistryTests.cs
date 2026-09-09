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
namespace Icod.Terminal.Tests.Routing;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies N156 semantic-operation to backend registry coverage and separation.
/// </summary>
public sealed class TerminalSemanticBackendRegistryTests {
	[Fact]
	public void EverySemanticOperationHasAtLeastOneUniqueCandidate() {
		TerminalSemanticOperation[] expected = Enum.GetValues<TerminalSemanticOperation>();

		Assert.Equal( expected, TerminalSemanticBackendRegistry.Operations );
		foreach ( TerminalSemanticOperation operation in expected ) {
			IReadOnlyList<TerminalSemanticBackendCandidate> candidates =
				TerminalSemanticBackendRegistry.GetCandidates( operation );

			Assert.NotEmpty( candidates );
			Assert.Equal(
				candidates.Count,
				candidates.Select( candidate => candidate.Backend ).Distinct().Count()
			);
			foreach ( TerminalSemanticBackendCandidate candidate in candidates ) {
				Assert.Equal( operation, candidate.Operation );
				if ( TerminalProtocolBackend.TermInfoCapability == candidate.Backend ) {
					Assert.Null( candidate.ControlFamily );
				} else {
					Assert.Equal(
						TerminalControlLanguageVocabulary.GetControlFamily( candidate.Backend ),
						candidate.ControlFamily
					);
				}
			}
		}
	}

	[Fact]
	public void NotificationIntentContainsAllThreeExplicitNotificationFamilies() {
		AssertCandidates(
			TerminalSemanticOperation.DesktopNotification,
			TerminalProtocolBackend.Osc9Notification,
			TerminalProtocolBackend.Osc777TitledNotification,
			TerminalProtocolBackend.Osc99KittyNotification
		);
	}

	[Fact]
	public void LocationAndShellMetadataRemainSeparateSemanticOperations() {
		AssertCandidates(
			TerminalSemanticOperation.CurrentLocation,
			TerminalProtocolBackend.Osc7CurrentLocation,
			TerminalProtocolBackend.Osc9WindowsCurrentDirectory
		);
		AssertCandidates(
			TerminalSemanticOperation.ShellCurrentDirectoryMetadata,
			TerminalProtocolBackend.Osc633VsCodeShellIntegration,
			TerminalProtocolBackend.Osc1337ITerm2ShellIntegration
		);

		IReadOnlyList<TerminalSemanticBackendCandidate> location =
			TerminalSemanticBackendRegistry.GetCandidates(
				TerminalSemanticOperation.CurrentLocation
			);
		Assert.DoesNotContain(
			location,
			candidate => TerminalProtocolBackend.Osc633VsCodeShellIntegration == candidate.Backend
		);
		Assert.DoesNotContain(
			location,
			candidate => TerminalProtocolBackend.Osc1337ITerm2ShellIntegration == candidate.Backend
		);
	}

	[Fact]
	public void ClipboardWriteCanUseTermInfoButClipboardReadRemainsOsc52Only() {
		AssertCandidates(
			TerminalSemanticOperation.ClipboardWrite,
			TerminalProtocolBackend.TermInfoCapability,
			TerminalProtocolBackend.Osc52Clipboard
		);
		AssertCandidates(
			TerminalSemanticOperation.ClipboardRead,
			TerminalProtocolBackend.Osc52Clipboard
		);
	}

	[Fact]
	public void CursorAndPaletteMutationReserveExactTermInfoCandidateSlots() {
		AssertCandidates(
			TerminalSemanticOperation.CursorStyle,
			TerminalProtocolBackend.TermInfoCapability,
			TerminalProtocolBackend.CsiDecscusrCursorStyle
		);
		AssertCandidates(
			TerminalSemanticOperation.PaletteColor,
			TerminalProtocolBackend.TermInfoCapability,
			TerminalProtocolBackend.Osc4Palette
		);
	}

	[Fact]
	public void RasterGraphicsKeepsSixelDcsAndKittyApcDistinct() {
		IReadOnlyList<TerminalSemanticBackendCandidate> candidates =
			TerminalSemanticBackendRegistry.GetCandidates(
				TerminalSemanticOperation.RasterGraphics
			);

		Assert.Equal( 2, candidates.Count );
		Assert.Contains(
			candidates,
			candidate => TerminalProtocolBackend.DcsSixel == candidate.Backend
				&& TerminalControlFamily.Dcs == candidate.ControlFamily
		);
		Assert.Contains(
			candidates,
			candidate => TerminalProtocolBackend.ApcKittyGraphics == candidate.Backend
				&& TerminalControlFamily.Apc == candidate.ControlFamily
		);
	}

	[Fact]
	public void ModeBasedSemanticOperationsUseSpecificBackendIdentities() {
		AssertCandidates(
			TerminalSemanticOperation.SynchronizedOutput,
			TerminalProtocolBackend.CsiSynchronizedOutput
		);
		AssertCandidates(
			TerminalSemanticOperation.MouseReporting,
			TerminalProtocolBackend.CsiMouseReporting
		);
		AssertCandidates(
			TerminalSemanticOperation.FocusReporting,
			TerminalProtocolBackend.CsiFocusReporting
		);
		AssertCandidates(
			TerminalSemanticOperation.BracketedPaste,
			TerminalProtocolBackend.CsiBracketedPaste
		);

		foreach ( TerminalSemanticOperation operation in TerminalSemanticBackendRegistry.Operations ) {
			Assert.DoesNotContain(
				TerminalSemanticBackendRegistry.GetCandidates( operation ),
				candidate => TerminalProtocolBackend.CsiDecPrivateMode == candidate.Backend
			);
		}
	}

	[Fact]
	public void RegistryDoesNotTreatDeclarationOrderAsCapabilityEvidence() {
		TerminalCapabilityEvidenceLedger ledger = new();
		IReadOnlyList<TerminalSemanticBackendCandidate> candidates =
			TerminalSemanticBackendRegistry.GetCandidates(
				TerminalSemanticOperation.DesktopNotification
			);

		foreach ( TerminalSemanticBackendCandidate candidate in candidates ) {
			TerminalCapabilityResolution resolution = ledger.Resolve(
				TerminalCapabilitySubject.ForProtocolBackend( candidate.Backend )
			);
			Assert.Equal( TerminalCapabilitySupportState.Unknown, resolution.State );
		}
	}

	[Fact]
	public void UnknownSemanticOperationIsRejected() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalSemanticBackendRegistry.GetCandidates(
				(TerminalSemanticOperation)int.MaxValue
			)
		);
	}

	private static void AssertCandidates(
		TerminalSemanticOperation operation,
		params TerminalProtocolBackend[] expected
	) {
		ArgumentNullException.ThrowIfNull( expected );
		Assert.Equal(
			expected,
			TerminalSemanticBackendRegistry
				.GetCandidates( operation )
				.Select( candidate => candidate.Backend )
				.ToArray()
		);
	}
}
