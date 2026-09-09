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
/// Verifies the C160 complete bounded CSI syntax view.
/// </summary>
public sealed class TerminalCsiSyntaxTests {
	[Fact]
	public void SevenBitSyntaxPreservesPrivateParametersSubparametersAndIntermediateBytes() {
		TerminalCsiSyntax syntax = Parse(
			"\u001b[?1;2:3;;4$q"
		);

		Assert.False( syntax.UsesEightBitIntroducer );
		Assert.Equal( "?1;2:3;;4", AsAscii( syntax.RawParameterBytes ) );
		Assert.Equal( "?", AsAscii( syntax.PrivateParameterBytes ) );
		Assert.Equal( "1;2:3;;4", AsAscii( syntax.ParameterDataBytes ) );
		Assert.Equal( "$", AsAscii( syntax.IntermediateBytes ) );
		Assert.Equal( (byte)'q', syntax.FinalByte );

		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		Assert.Equal( 4, parameters.Length );
		Assert.Equal( "1", AsAscii( parameters[ 0 ].RawBytes ) );
		Assert.Equal( "2:3", AsAscii( parameters[ 1 ].RawBytes ) );
		Assert.True( parameters[ 2 ].IsEmpty );
		Assert.Equal( "4", AsAscii( parameters[ 3 ].RawBytes ) );

		ReadOnlySpan<TerminalCsiSubparameter> subparameters = parameters[ 1 ].Subparameters.Span;
		Assert.Equal( 2, subparameters.Length );
		Assert.Equal( "2", AsAscii( subparameters[ 0 ].RawBytes ) );
		Assert.Equal( "3", AsAscii( subparameters[ 1 ].RawBytes ) );
	}

	[Fact]
	public void EightBitSyntaxPreservesEmptyColonSubparameter() {
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Csi,
			new byte[] {
				0x9B,
				(byte)'>',
				(byte)'0',
				(byte)';',
				(byte)':',
				(byte)'1',
				(byte)'c'
			}
		);

		TerminalCsiSyntax syntax = TerminalCsiSyntax.Parse( frame );

		Assert.True( syntax.UsesEightBitIntroducer );
		Assert.Equal( ">", AsAscii( syntax.PrivateParameterBytes ) );
		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		Assert.Equal( 2, parameters.Length );
		Assert.Equal( "0", AsAscii( parameters[ 0 ].RawBytes ) );

		ReadOnlySpan<TerminalCsiSubparameter> subparameters = parameters[ 1 ].Subparameters.Span;
		Assert.Equal( 2, subparameters.Length );
		Assert.True( subparameters[ 0 ].IsEmpty );
		Assert.Equal( "1", AsAscii( subparameters[ 1 ].RawBytes ) );
	}

	[Fact]
	public void NoParameterBytesRemainDistinctFromEmptyComponents() {
		TerminalCsiSyntax noParameters = Parse( "\u001b[c" );
		TerminalCsiSyntax emptyComponents = Parse( "\u001b[;c" );

		Assert.True( noParameters.Parameters.IsEmpty );
		Assert.Equal( 2, emptyComponents.Parameters.Length );
		Assert.True( emptyComponents.Parameters.Span[ 0 ].IsEmpty );
		Assert.True( emptyComponents.Parameters.Span[ 1 ].IsEmpty );
	}

	[Fact]
	public void ParameterBytesOutsideLeadingPrivatePrefixRemainLossless() {
		TerminalCsiSyntax syntax = Parse( "\u001b[1?2m" );

		Assert.True( syntax.PrivateParameterBytes.IsEmpty );
		Assert.Equal( "1?2", AsAscii( syntax.ParameterDataBytes ) );
		Assert.Single( syntax.Parameters.ToArray() );
		Assert.Equal( "1?2", AsAscii( syntax.Parameters.Span[ 0 ].RawBytes ) );
	}

	[Fact]
	public void RawParameterByteLimitIsEnforced() {
		string wire = "\u001b["
			+ new string( '1', TerminalCsiSyntax.MaximumRawParameterBytes + 1 )
			+ "m";

		Assert.Throws<FormatException>(
			() => Parse( wire )
		);
	}

	[Fact]
	public void ParameterCountLimitIsEnforced() {
		string wire = "\u001b["
			+ string.Join(
				';',
				Enumerable.Repeat(
					"1",
					TerminalCsiSyntax.MaximumParameterCount + 1
				)
			)
			+ "m";

		Assert.Throws<FormatException>(
			() => Parse( wire )
		);
	}

	[Fact]
	public void SubparameterCountLimitIsEnforced() {
		string wire = "\u001b["
			+ string.Join(
				':',
				Enumerable.Repeat(
					"1",
					TerminalCsiSyntax.MaximumSubparameterCount + 1
				)
			)
			+ "m";

		Assert.Throws<FormatException>(
			() => Parse( wire )
		);
	}

	private static TerminalCsiSyntax Parse(
		string wire
	) {
		ArgumentNullException.ThrowIfNull( wire );
		return TerminalCsiSyntax.Parse(
			new TerminalResponseFrame(
				TerminalResponseFrameKind.Csi,
				Encoding.ASCII.GetBytes( wire )
			)
		);
	}

	private static string AsAscii(
		ReadOnlyMemory<byte> bytes
	) {
		return Encoding.ASCII.GetString( bytes.Span );
	}
}
