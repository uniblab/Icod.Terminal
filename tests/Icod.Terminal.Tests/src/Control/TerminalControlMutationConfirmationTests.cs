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
namespace Icod.Terminal.Tests.Control;

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the additive mutation-confirmation contract.
/// </summary>
public sealed class TerminalControlMutationConfirmationTests {
	[Fact]
	public void ConfirmationEnumHasFrozenNamesAndValues() {
		Assert.Equal(
			[
				nameof( TerminalControlMutationConfirmation.Unspecified ),
				nameof( TerminalControlMutationConfirmation.OutputCommitted ),
				nameof( TerminalControlMutationConfirmation.ProtocolAcknowledged )
			],
			Enum.GetNames<TerminalControlMutationConfirmation>()
		);
		Assert.Equal( 0, (int)TerminalControlMutationConfirmation.Unspecified );
		Assert.Equal( 1, (int)TerminalControlMutationConfirmation.OutputCommitted );
		Assert.Equal( 2, (int)TerminalControlMutationConfirmation.ProtocolAcknowledged );
	}

	[Fact]
	public void ConfirmationIsReadOnlyAndConstructionRemainsControlled() {
		PropertyInfo confirmation = Assert.IsAssignableFrom<PropertyInfo>(
			typeof( TerminalControlMutationResult ).GetProperty(
				nameof( TerminalControlMutationResult.Confirmation )
			)
		);
		Assert.Equal(
			typeof( TerminalControlMutationConfirmation ),
			confirmation.PropertyType
		);
		Assert.True( confirmation.CanRead );
		Assert.False( confirmation.CanWrite );
		Assert.Empty(
			typeof( TerminalControlMutationResult ).GetConstructors(
				BindingFlags.Instance | BindingFlags.Public
			)
		);
		Assert.Single(
			typeof( TerminalControlMutationResult ).GetMethods(
				BindingFlags.Static | BindingFlags.Public
			).Where(
				method => method.Name == nameof( TerminalControlMutationResult.Success )
			)
		);
	}

	[Fact]
	public void ExistingSuccessFactoryDefaultsToUnspecified() {
		TerminalControlMutationResult result = TerminalControlMutationResult.Success();

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.True( result.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.Unspecified,
			result.Confirmation
		);
	}

	[Theory]
	[InlineData( TerminalControlMutationConfirmation.OutputCommitted )]
	[InlineData( TerminalControlMutationConfirmation.ProtocolAcknowledged )]
	public void ExplicitSuccessFactoryPreservesConfirmation(
		TerminalControlMutationConfirmation confirmation
	) {
		TerminalControlMutationResult result =
			TerminalControlMutationResult.Success( confirmation );

		Assert.True( result.Succeeded );
		Assert.Equal( confirmation, result.Confirmation );
	}

	[Fact]
	public void ControlledNonSuccessResultsRemainUnspecified() {
		TerminalControlMutationResult[] results = [
			TerminalControlMutationResult.Unavailable( "unavailable" ),
			TerminalControlMutationResult.Unsupported( "unsupported" ),
			TerminalControlMutationResult.Failed( "failed" )
		];

		Assert.All(
			results,
			result => {
				Assert.False( result.Succeeded );
				Assert.Equal(
					TerminalControlMutationConfirmation.Unspecified,
					result.Confirmation
				);
			}
		);
	}
}
