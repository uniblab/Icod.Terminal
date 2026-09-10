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
/// Defines the E193 Kitty OSC 99 unsolicited-report grammar before decoder routing is added.
/// </summary>
public sealed class TerminalOsc99UnsolicitedReportTests {
	[Fact]
	public void ParsesActivationReport() {
		bool recognized = TerminalOsc99UnsolicitedReportParser.TryParse(
			OscFrame( "99;i=build-finished;" ),
			out TerminalSemanticEvent? parsed
		);

		Assert.True( recognized );
		TerminalSemanticEvent semantic = Assert.IsType<TerminalSemanticEvent>( parsed );
		TerminalNotificationEvent notification = Assert.IsType<TerminalNotificationEvent>(
			semantic.Notification
		);
		Assert.Equal( TerminalSemanticEventKind.Notification, semantic.Kind );
		Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
		Assert.Equal( "build-finished", notification.Identifier );
		Assert.Null( notification.ButtonNumber );
	}

	[Fact]
	public void ParsesOneBasedButtonReport() {
		bool recognized = TerminalOsc99UnsolicitedReportParser.TryParse(
			OscFrame( "99;i=question;2" ),
			out TerminalSemanticEvent? parsed
		);

		Assert.True( recognized );
		TerminalNotificationEvent notification = Assert.IsType<TerminalNotificationEvent>(
			Assert.IsType<TerminalSemanticEvent>( parsed ).Notification
		);
		Assert.Equal( TerminalNotificationEventKind.ButtonActivated, notification.Kind );
		Assert.Equal( "question", notification.Identifier );
		Assert.Equal( 2, notification.ButtonNumber );
	}

	[Fact]
	public void ParsesCloseAndUntrackedReports() {
		Assert.True(
			TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=job:p=close;" ),
				out TerminalSemanticEvent? closed
			)
		);
		Assert.True(
			TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;p=close:i=job;untracked" ),
				out TerminalSemanticEvent? untracked
			)
		);

		Assert.Equal(
			TerminalNotificationEventKind.Closed,
			Assert.IsType<TerminalNotificationEvent>(
				Assert.IsType<TerminalSemanticEvent>( closed ).Notification
			).Kind
		);
		Assert.Equal(
			TerminalNotificationEventKind.CloseTrackingUnavailable,
			Assert.IsType<TerminalNotificationEvent>(
				Assert.IsType<TerminalSemanticEvent>( untracked ).Notification
			).Kind
		);
	}

	[Fact]
	public void AcceptsEightBitOscReportFraming() {
		byte[] bytes = Encoding.ASCII.GetBytes( "99;i=legacy;" );
		byte[] framed = new byte[ bytes.Length + 2 ];
		framed[ 0 ] = 0x9d;
		bytes.CopyTo( framed, 1 );
		framed[ ^1 ] = 0x9c;
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Osc,
			framed
		);

		Assert.True(
			TerminalOsc99UnsolicitedReportParser.TryParse(
				frame,
				out TerminalSemanticEvent? parsed
			)
		);
		Assert.Equal(
			TerminalNotificationEventKind.Activated,
			Assert.IsType<TerminalNotificationEvent>(
				Assert.IsType<TerminalSemanticEvent>( parsed ).Notification
			).Kind
		);
	}

	[Fact]
	public void QueryResponseFormsRemainOutsideUnsolicitedGrammar() {
		Assert.False(
			TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=query:p=?;p=title,body" ),
				out TerminalSemanticEvent? support
			)
		);
		Assert.Null( support );
		Assert.False(
			TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=query:p=alive;job1,job2" ),
				out TerminalSemanticEvent? alive
			)
		);
		Assert.Null( alive );
	}

	[Theory]
	[InlineData( "0" )]
	[InlineData( "-1" )]
	[InlineData( "abc" )]
	[InlineData( "2147483648" )]
	public void RejectsMalformedOwnedButtonPayload(
		string payload
	) {
		Assert.Throws<FormatException>(
			() => TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=job;" + payload ),
				out _
			)
		);
	}

	[Fact]
	public void RejectsMalformedOwnedClosePayload() {
		Assert.Throws<FormatException>(
			() => TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=job:p=close;unknown" ),
				out _
			)
		);
	}

	[Fact]
	public void RejectsInvalidReturnedIdentifierAfterReportRecognition() {
		string oversized = new( 'a', TerminalOsc99NotificationEncoder.MaximumIdentifierLength + 1 );

		Assert.Throws<FormatException>(
			() => TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=" + oversized + ";" ),
				out _
			)
		);
		Assert.Throws<FormatException>(
			() => TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=bad:id;" ),
				out _
			)
		);
	}

	[Fact]
	public void UnknownPayloadSelectorRemainsOutsideUnsolicitedGrammar() {
		Assert.False(
			TerminalOsc99UnsolicitedReportParser.TryParse(
				OscFrame( "99;i=job:p=future;data" ),
				out TerminalSemanticEvent? parsed
			)
		);
		Assert.Null( parsed );
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
