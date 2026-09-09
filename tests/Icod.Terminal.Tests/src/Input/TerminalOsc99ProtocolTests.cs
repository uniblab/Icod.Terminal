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

public sealed class TerminalOsc99ProtocolTests {
	[Fact]
	public void ParsesSupportObservation() {
		TerminalResponseFrame frame = OscFrame(
			"99;i=query1:p=?;a=focus,report:c=1:o=always,unfocused,invisible:p=title,body,close,icon,alive,buttons:s=system,silent:u=0,1,2:w=1"
		);

		KittyNotificationSupport support = TerminalOsc99Protocol.ParseSupportResponse(
			frame,
			"query1"
		);

		Assert.True( support.SupportsFocusAction );
		Assert.True( support.SupportsActivationReports );
		Assert.True( support.SupportsCloseEvents );
		Assert.True( support.SupportsTitle );
		Assert.True( support.SupportsBody );
		Assert.True( support.SupportsClose );
		Assert.True( support.SupportsIconData );
		Assert.True( support.SupportsAliveQuery );
		Assert.True( support.SupportsButtons );
		Assert.True( support.SupportsAutoExpiration );
		Assert.Equal(
			[
				KittyNotificationOccasion.Always,
				KittyNotificationOccasion.Unfocused,
				KittyNotificationOccasion.Invisible
			],
			support.Occasions
		);
		Assert.Equal(
			[
				KittyNotificationUrgency.Low,
				KittyNotificationUrgency.Normal,
				KittyNotificationUrgency.Critical
			],
			support.Urgencies
		);
		Assert.Equal(
			[ "system", "silent" ],
			support.Sounds
		);
	}

	[Fact]
	public void ParsesAliveIdentifiers() {
		TerminalResponseFrame frame = OscFrame(
			"99;i=query2:p=alive;build.1,build_2,job-3"
		);

		IReadOnlyList<string> alive = TerminalOsc99Protocol.ParseAliveResponse(
			frame,
			"query2"
		);

		Assert.Equal(
			[ "build.1", "build_2", "job-3" ],
			alive
		);
	}

	[Fact]
	public void MatchersRequireExactQueryIdentity() {
		ITerminalResponseMatcher matcher = TerminalOsc99Protocol.CreateSupportResponseMatcher(
			"query1"
		);

		Assert.True(
			matcher.IsMatch(
				OscFrame( "99;p=?:i=query1;p=title" )
			)
		);
		Assert.False(
			matcher.IsMatch(
				OscFrame( "99;i=query2:p=?;p=title" )
			)
		);
		Assert.False(
			matcher.IsMatch(
				OscFrame( "99;i=query1:p=alive;query1" )
			)
		);
	}

	[Fact]
	public void RejectsMalformedAliveIdentifier() {
		TerminalResponseFrame frame = OscFrame(
			"99;i=query2:p=alive;good,bad:id"
		);

		Assert.Throws<FormatException>(
			() => TerminalOsc99Protocol.ParseAliveResponse(
				frame,
				"query2"
			)
		);
	}

	[Fact]
	public void QueryRequestsUseCanonicalSt() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=query1:p=?;\u001b\\"
			),
			TerminalOsc99Protocol.CreateSupportQueryRequest( "query1" )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=query1:p=alive;\u001b\\"
			),
			TerminalOsc99Protocol.CreateAliveQueryRequest( "query1" )
		);
	}

	private static TerminalResponseFrame OscFrame(
		string body
	) {
		ArgumentNullException.ThrowIfNull( body );
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Osc,
			Encoding.ASCII.GetBytes(
				"\u001b]" + body + "\u001b\\"
			)
		);
	}
}
