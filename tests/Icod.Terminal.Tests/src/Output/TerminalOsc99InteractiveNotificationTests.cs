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
namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Defines the E195 interactive Kitty OSC 99 request contract before implementation.
/// </summary>
public sealed class TerminalOsc99InteractiveNotificationTests {
	[Fact]
	public void ExistingNoninteractiveEncodingRemainsByteCompatible() {
		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			"Build",
			string.Empty,
			new KittyNotificationOptions()
		);

		Assert.Single( frames );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;p=title:e=1:d=1;QnVpbGQ=\u001b\\"
			),
			frames[ 0 ]
		);
	}

	[Fact]
	public void ActivationReportingRequiresExplicitIdentifier() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc99NotificationFrames(
				"Build",
				string.Empty,
				new KittyNotificationOptions {
					ReportActivation = true
				}
			)
		);
	}

	[Fact]
	public void CloseReportingRequiresExplicitIdentifier() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc99NotificationFrames(
				"Build",
				string.Empty,
				new KittyNotificationOptions {
					ReportClose = true
				}
			)
		);
	}

	[Fact]
	public void ReportingComposesWithDisabledFocus() {
		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			"Build",
			string.Empty,
			new KittyNotificationOptions {
				Identifier = "build.42",
				FocusOnActivation = false,
				ReportActivation = true,
				ReportClose = true
			}
		);

		Assert.Single( frames );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=build.42:a=report,-focus:c=1:p=title:e=1:d=1;QnVpbGQ=\u001b\\"
			),
			frames[ 0 ]
		);
	}

	[Fact]
	public void ButtonsUseUtf8LineSeparatorPayloadAndReporting() {
		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			"Question",
			string.Empty,
			new KittyNotificationOptions {
				Identifier = "question.1",
				ReportActivation = true,
				Buttons = [ "Yes", "No" ]
			}
		);

		Assert.Equal( 2, frames.Length );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=question.1:a=report:p=title:e=1:d=0;UXVlc3Rpb24=\u001b\\"
			),
			frames[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=question.1:p=buttons:e=1:d=1;WWVz4oCoTm8=\u001b\\"
			),
			frames[ 1 ]
		);
	}

	[Fact]
	public void ButtonsAreBoundedAndRejectSeparatorInjection() {
		string[] tooMany = Enumerable.Range(
			0,
			TerminalOsc99NotificationEncoder.MaximumButtons + 1
		).Select(
			static index => "Button" + index
		).ToArray();

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc99NotificationFrames(
				"Question",
				string.Empty,
				new KittyNotificationOptions {
					Buttons = tooMany
				}
			)
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc99NotificationFrames(
				"Question",
				string.Empty,
				new KittyNotificationOptions {
					Buttons = [ "Yes\u2028No" ]
				}
			)
		);
	}
}
