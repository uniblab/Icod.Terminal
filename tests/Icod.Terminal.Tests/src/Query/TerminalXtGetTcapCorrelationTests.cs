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
namespace Icod.Terminal.Tests.Query;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies request-specific XTGETTCAP response correlation.
/// </summary>
public sealed class TerminalXtGetTcapCorrelationTests {
	[Fact]
	public void RequestedMatcherRejectsDifferentPositiveCapability() {
		ITerminalResponseMatcher matcher = TerminalXtGetTcapProtocol.CreateResponseMatcher(
			"Co"
		);
		TerminalResponseFrame stale = CreateFrame(
			"\u001bP1+r544E=787465726D\u001b\\"
		);
		TerminalResponseFrame current = CreateFrame(
			"\u001bP1+r436F=323536\u001b\\"
		);

		Assert.False( matcher.IsMatch( stale ) );
		Assert.True( matcher.IsMatch( current ) );
	}

	[Fact]
	public void RequestedMatcherRetainsNegativeResponseCorrelation() {
		ITerminalResponseMatcher matcher = TerminalXtGetTcapProtocol.CreateResponseMatcher(
			"Co"
		);
		TerminalResponseFrame negative = CreateFrame(
			"\u001bP0+r\u001b\\"
		);

		Assert.True( matcher.IsMatch( negative ) );
	}

	[Fact]
	public void RequestedMatcherRetainsMalformedPositiveResponseForParserFailure() {
		ITerminalResponseMatcher matcher = TerminalXtGetTcapProtocol.CreateResponseMatcher(
			"Co"
		);
		TerminalResponseFrame malformed = CreateFrame(
			"\u001bP1+r436=323536\u001b\\"
		);

		Assert.True( matcher.IsMatch( malformed ) );
	}

	private static TerminalResponseFrame CreateFrame(
		string wire
	) {
		ArgumentNullException.ThrowIfNull( wire );
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Dcs,
			Encoding.ASCII.GetBytes( wire )
		);
	}
}
