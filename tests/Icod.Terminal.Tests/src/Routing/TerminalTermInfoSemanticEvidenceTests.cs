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
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies N158 reconciliation between selected TermInfo capabilities and semantic routing.
/// </summary>
public sealed class TerminalTermInfoSemanticEvidenceTests {
	[Fact]
	public void ExactTermInfoRecipesResolveThroughTermInfoBackend() {
		TerminalDescription terminal = CreateCompleteTerminal();
		TerminalCapabilityEvidenceLedger evidence = new();
		TerminalTermInfoSemanticEvidence.Seed(
			terminal,
			evidence
		);

		AssertTermInfoBackend(
			TerminalSemanticOperation.ClipboardWrite,
			evidence
		);
		AssertTermInfoBackend(
			TerminalSemanticOperation.CursorStyle,
			evidence
		);
		AssertTermInfoBackend(
			TerminalSemanticOperation.PaletteColor,
			evidence
		);
	}

	[Fact]
	public void TermInfoMetadataAdvertisesExistingInputProtocolBackends() {
		TerminalCapabilityEvidenceLedger evidence = new();
		TerminalTermInfoSemanticEvidence.Seed(
			CreateCompleteTerminal(),
			evidence
		);

		AssertAdvertisedBackend(
			TerminalSemanticOperation.FocusReporting,
			TerminalProtocolBackend.CsiFocusReporting,
			evidence
		);
		AssertAdvertisedBackend(
			TerminalSemanticOperation.BracketedPaste,
			TerminalProtocolBackend.CsiBracketedPaste,
			evidence
		);
		AssertAdvertisedBackend(
			TerminalSemanticOperation.MouseReporting,
			TerminalProtocolBackend.CsiMouseReporting,
			evidence
		);
	}

	[Fact]
	public void PartialInputMetadataDoesNotBecomeCapabilityEvidence() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "partial-input" )
			.SetExtendedString( "fe", "\u001b[?1004h" )
			.SetExtendedString( "fd", "\u001b[?1004l" )
			.SetExtendedString( "kxIN", "\u001b[I" )
			.SetExtendedString( "BE", "\u001b[?2004h" )
			.SetExtendedString( "BD", "\u001b[?2004l" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "XM", "mouse-mode" )
			.SetString( StringCapability.KeyMouse, "\u001b[<" )
			.Build();
		TerminalCapabilityEvidenceLedger evidence = new();

		TerminalTermInfoSemanticEvidence.Seed(
			terminal,
			evidence
		);

		AssertUnknownBackend(
			TerminalProtocolBackend.CsiFocusReporting,
			evidence
		);
		AssertUnknownBackend(
			TerminalProtocolBackend.CsiBracketedPaste,
			evidence
		);
		AssertUnknownBackend(
			TerminalProtocolBackend.CsiMouseReporting,
			evidence
		);
	}

	[Fact]
	public void CursorColorMetadataDoesNotOverclaimWholeDynamicColorFamily() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "cursor-color-only" )
			.SetExtendedString( "Cs", "\u001b]12;%p1%s\u001b\\" )
			.SetExtendedString( "Cr", "\u001b]112\u001b\\" )
			.Build();
		TerminalCapabilityEvidenceLedger evidence = new();

		TerminalTermInfoSemanticEvidence.Seed(
			terminal,
			evidence
		);
		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.DynamicColor,
			evidence
		);

		Assert.Null( resolution.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, resolution.State );
	}

	[Fact]
	public void ExactImplementationClassifierRejectsPartialOrUnrelatedContracts() {
		TerminalDescription complete = CreateCompleteTerminal();
		TerminalDescription partial = new TerminalDescriptionBuilder( "partial" )
			.SetExtendedString( "Ms", "clipboard" )
			.SetExtendedString( "fe", "focus-enable" )
			.Build();

		Assert.True(
			TerminalTermInfoSemanticEvidence.HasExactImplementation(
				complete,
				TerminalSemanticOperation.ClipboardWrite
			)
		);
		Assert.True(
			TerminalTermInfoSemanticEvidence.HasExactImplementation(
				complete,
				TerminalSemanticOperation.FocusReporting
			)
		);
		Assert.False(
			TerminalTermInfoSemanticEvidence.HasExactImplementation(
				partial,
				TerminalSemanticOperation.FocusReporting
			)
		);
		Assert.False(
			TerminalTermInfoSemanticEvidence.HasExactImplementation(
				complete,
				TerminalSemanticOperation.DynamicColor
			)
		);
	}

	private static TerminalDescription CreateCompleteTerminal() {
		return new TerminalDescriptionBuilder( "n158-complete" )
			.SetExtendedString( "Ms", "\u001b]52;%p1%s;%p2%s\u001b\\" )
			.SetExtendedString( "Ss", "\u001b[%p1%d q" )
			.SetBoolean( BooleanCapability.CanChangeColor )
			.SetString(
				StringCapability.InitializeColor,
				"\u001b]4;%p1%d;rgb:%p2%d/%p3%d/%p4%d\u001b\\"
			)
			.SetExtendedString( "fe", "\u001b[?1004h" )
			.SetExtendedString( "fd", "\u001b[?1004l" )
			.SetExtendedString( "kxIN", "\u001b[I" )
			.SetExtendedString( "kxOUT", "\u001b[O" )
			.SetExtendedString( "BE", "\u001b[?2004h" )
			.SetExtendedString( "BD", "\u001b[?2004l" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "PE", "\u001b[201~" )
			.SetExtendedString( "XM", "mouse-mode" )
			.SetExtendedString( "xm", "mouse-event" )
			.SetString( StringCapability.KeyMouse, "\u001b[<" )
			.Build();
	}

	private static void AssertTermInfoBackend(
		TerminalSemanticOperation operation,
		TerminalCapabilityEvidenceLedger evidence
	) {
		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			operation,
			evidence
		);

		Assert.NotNull( resolution.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.TermInfoCapability,
			resolution.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalCapabilitySupportState.Advertised, resolution.State );
		Assert.Equal( TerminalCapabilityEvidenceSource.TermInfo, resolution.EvidenceSource );
		Assert.Equal( TerminalBackendSelectionReason.TermInfo, resolution.SelectionReason );
	}

	private static void AssertAdvertisedBackend(
		TerminalSemanticOperation operation,
		TerminalProtocolBackend backend,
		TerminalCapabilityEvidenceLedger evidence
	) {
		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			operation,
			evidence
		);

		Assert.NotNull( resolution.SelectedCandidate );
		Assert.Equal( backend, resolution.SelectedCandidate.Value.Backend );
		Assert.Equal( TerminalCapabilitySupportState.Advertised, resolution.State );
		Assert.Equal( TerminalCapabilityEvidenceSource.TermInfo, resolution.EvidenceSource );
		Assert.Equal( TerminalBackendSelectionReason.Advertised, resolution.SelectionReason );
	}

	private static void AssertUnknownBackend(
		TerminalProtocolBackend backend,
		TerminalCapabilityEvidenceLedger evidence
	) {
		TerminalCapabilityResolution resolution = evidence.Resolve(
			TerminalCapabilitySubject.ForProtocolBackend( backend )
		);

		Assert.Equal( TerminalCapabilitySupportState.Unknown, resolution.State );
	}
}
