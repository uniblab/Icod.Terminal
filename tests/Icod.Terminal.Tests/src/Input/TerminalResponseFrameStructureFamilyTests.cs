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
/// Verifies that routed N154 string-family frames reuse the N153 structural model.
/// </summary>
public sealed class TerminalResponseFrameStructureFamilyTests {
	[Fact]
	public void StructuredResponseFramesCoverApcPmAndSos() {
		AssertStringFrame(
			TerminalResponseFrameKind.Apc,
			TerminalControlFamily.Apc,
			"\u001b_payload\u001b\\",
			"payload"
		);
		AssertStringFrame(
			TerminalResponseFrameKind.Pm,
			TerminalControlFamily.Pm,
			"\u001b^privacy\u001b\\",
			"privacy"
		);
		AssertStringFrame(
			TerminalResponseFrameKind.Sos,
			TerminalControlFamily.Sos,
			"\u001bXstart\u001b\\",
			"start"
		);
	}

	private static void AssertStringFrame(
		TerminalResponseFrameKind frameKind,
		TerminalControlFamily family,
		string wire,
		string payload
	) {
		if ( !Enum.IsDefined( frameKind ) ) {
			throw new ArgumentOutOfRangeException( nameof( frameKind ) );
		}
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}
		ArgumentNullException.ThrowIfNull( wire );
		ArgumentNullException.ThrowIfNull( payload );

		TerminalResponseFrame frame = new(
			frameKind,
			Encoding.ASCII.GetBytes( wire )
		);
		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			frame
		);

		Assert.Equal( family, structure.Family );
		Assert.False( structure.UsesEightBitIntroducer );
		Assert.Equal( 2, structure.IntroducerLength );
		Assert.Null( structure.FinalByte );
		Assert.Equal(
			Encoding.ASCII.GetBytes( payload ),
			structure.PayloadBytes.ToArray()
		);
		Assert.Equal(
			TerminalStringTerminatorKind.SevenBitSt,
			structure.TerminatorKind
		);
		Assert.Equal( 2, structure.TerminatorLength );
	}
}
