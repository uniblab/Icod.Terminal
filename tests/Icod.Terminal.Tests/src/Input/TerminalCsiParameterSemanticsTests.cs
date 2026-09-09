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
namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the C161 bounded CSI parameter semantic layer.
/// </summary>
public sealed class TerminalCsiParameterSemanticsTests {
	[Fact]
	public void OmittedEmptyZeroAndNumericRemainDistinct() {
		TerminalCsiSyntax syntax = Parse( "\u001b[;0;42m" );

		TerminalCsiNumericComponent empty = TerminalCsiParameterSemantics.GetNumericParameter(
			syntax,
			0
		);
		TerminalCsiNumericComponent zero = TerminalCsiParameterSemantics.GetNumericParameter(
			syntax,
			1
		);
		TerminalCsiNumericComponent numeric = TerminalCsiParameterSemantics.GetNumericParameter(
			syntax,
			2
		);
		TerminalCsiNumericComponent omitted = TerminalCsiParameterSemantics.GetNumericParameter(
			syntax,
			3
		);

		Assert.Equal( TerminalCsiNumericComponentKind.Empty, empty.Kind );
		Assert.False( empty.HasValue );
		Assert.Equal( TerminalCsiNumericComponentKind.Numeric, zero.Kind );
		Assert.True( zero.HasValue );
		Assert.Equal( 0, zero.Value );
		Assert.Equal( TerminalCsiNumericComponentKind.Numeric, numeric.Kind );
		Assert.Equal( 42, numeric.Value );
		Assert.Equal( TerminalCsiNumericComponentKind.Omitted, omitted.Kind );
		Assert.False( omitted.HasValue );
	}

	[Fact]
	public void NumericConversionHonorsReviewedMaximum() {
		TerminalCsiSyntax valid = Parse( "\u001b[100m" );
		TerminalCsiSyntax oversized = Parse( "\u001b[101m" );

		Assert.Equal(
			100,
			TerminalCsiParameterSemantics.GetNumericParameter(
				valid,
				0,
				100
			).Value
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.GetNumericParameter(
				oversized,
				0,
				100
			)
		);
	}

	[Fact]
	public void RequiredNumericParametersRejectEmptyAndColonSubparameters() {
		TerminalCsiSyntax empty = Parse( "\u001b[1;;2R" );
		TerminalCsiSyntax subparameters = Parse( "\u001b[1;2:3R" );

		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.GetRequiredNumericParameters(
				empty,
				8
			)
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.GetRequiredNumericParameters(
				subparameters,
				8
			)
		);
	}

	[Fact]
	public void RequiredNumericParametersEnforceDialectCountBound() {
		TerminalCsiSyntax syntax = Parse( "\u001b[1;2;3R" );

		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.GetRequiredNumericParameters(
				syntax,
				2
			)
		);
	}

	[Fact]
	public void HeaderPolicyHelpersValidatePrivateIntermediateAndFinalBytes() {
		TerminalCsiSyntax privateSyntax = Parse( "\u001b[?1c" );
		TerminalCsiSyntax ordinarySyntax = Parse( "\u001b[1c" );
		TerminalCsiSyntax intermediateSyntax = Parse( "\u001b[1$q" );

		TerminalCsiParameterSemantics.RequireExactPrivateParameterBytes(
			privateSyntax,
			new byte[] { (byte)'?' }
		);
		TerminalCsiParameterSemantics.RequireNoPrivateParameterBytes( ordinarySyntax );
		TerminalCsiParameterSemantics.RequireFinalByte(
			ordinarySyntax,
			(byte)'c'
		);

		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.RequireNoPrivateParameterBytes(
				privateSyntax
			)
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.RequireNoIntermediateBytes(
				intermediateSyntax
			)
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.RequireFinalByte(
				ordinarySyntax,
				(byte)'R'
			)
		);
	}

	[Fact]
	public void QueryParserPreservesHistoricalEmptyAndSubparameterRejection() {
		TerminalResponseFrame empty = CreateFrame( "\u001b[1;;2R" );
		TerminalResponseFrame subparameter = CreateFrame( "\u001b[1;2:3R" );

		Assert.Throws<FormatException>(
			() => TerminalCsiQueryProtocol.ParseCursorPosition( empty )
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiQueryProtocol.ParseCursorPosition( subparameter )
		);
	}

	private static TerminalCsiSyntax Parse(
		string wire
	) {
		return TerminalCsiSyntax.Parse( CreateFrame( wire ) );
	}

	private static TerminalResponseFrame CreateFrame(
		string wire
	) {
		ArgumentNullException.ThrowIfNull( wire );
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Csi,
			Encoding.ASCII.GetBytes( wire )
		);
	}
}
