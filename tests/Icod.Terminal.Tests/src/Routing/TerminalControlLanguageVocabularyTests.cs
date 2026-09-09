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
/// Verifies the internal terminology and family classification frozen by N150.
/// </summary>
public sealed class TerminalControlLanguageVocabularyTests {
	[Fact]
	public void VocabularyEnumsHaveUniqueNumericValues() {
		AssertUniqueValues<TerminalSemanticOperation>();
		AssertUniqueValues<TerminalProtocolBackend>();
		AssertUniqueValues<TerminalControlFamily>();
		AssertUniqueValues<TerminalCapabilitySupportState>();
		AssertUniqueValues<TerminalCapabilityEvidenceSource>();
	}

	[Fact]
	public void EveryBackendHasAnExplicitFamilyClassification() {
		foreach ( TerminalProtocolBackend backend in Enum.GetValues<TerminalProtocolBackend>() ) {
			TerminalControlFamily? family = TerminalControlLanguageVocabulary.GetControlFamily(
				backend
			);

			if ( TerminalProtocolBackend.TermInfoCapability == backend ) {
				Assert.Null( family );
			} else {
				Assert.NotNull( family );
				Assert.True( Enum.IsDefined( family.Value ) );
			}
		}
	}

	[Theory]
	[InlineData( TerminalProtocolBackend.Osc99KittyNotification, TerminalControlFamily.Osc )]
	[InlineData( TerminalProtocolBackend.CsiKittyKeyboard, TerminalControlFamily.Csi )]
	[InlineData( TerminalProtocolBackend.DcsDecrqss, TerminalControlFamily.Dcs )]
	[InlineData( TerminalProtocolBackend.DcsSixel, TerminalControlFamily.Dcs )]
	[InlineData( TerminalProtocolBackend.ApcKittyGraphics, TerminalControlFamily.Apc )]
	public void RepresentativeBackendsMapToExpectedFamily(
		TerminalProtocolBackend backend,
		TerminalControlFamily expected
	) {
		Assert.Equal(
			expected,
			TerminalControlLanguageVocabulary.GetControlFamily( backend )
		);
	}

	[Fact]
	public void StringControlFamilyVocabularyIncludesFutureFramingFamilies() {
		TerminalControlFamily[] families = Enum.GetValues<TerminalControlFamily>();

		Assert.Contains( TerminalControlFamily.Csi, families );
		Assert.Contains( TerminalControlFamily.Dcs, families );
		Assert.Contains( TerminalControlFamily.Osc, families );
		Assert.Contains( TerminalControlFamily.Apc, families );
		Assert.Contains( TerminalControlFamily.Pm, families );
		Assert.Contains( TerminalControlFamily.Sos, families );
	}

	[Fact]
	public void UnknownBackendIsRejected() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalControlLanguageVocabulary.GetControlFamily(
				(TerminalProtocolBackend)int.MaxValue
			)
		);
	}

	private static void AssertUniqueValues<TEnum>()
		where TEnum : struct, Enum {
		TEnum[] values = Enum.GetValues<TEnum>();
		int distinctCount = values
			.Select( value => Convert.ToInt64( value ) )
			.Distinct()
			.Count();

		Assert.Equal(
			values.Length,
			distinctCount
		);
	}
}
