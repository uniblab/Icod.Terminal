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
/// Verifies the stable VS Code OSC 633 ContinuationPrompt property.
/// </summary>
public sealed class Osc633ContinuationPromptWriterTests {
	[Fact]
	public void ContinuationPromptUsesVsCodeEscapingAndStrictUtf8() {
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b]633;P;ContinuationPrompt=more\\x20café\\x3b\u001b\\"
			),
			OscWriter.EncodeOsc633ContinuationPromptFrame(
				"more café;"
			)
		);
	}

	[Fact]
	public void EmptyContinuationPromptIsValid() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]633;P;ContinuationPrompt=\u001b\\"
			),
			OscWriter.EncodeOsc633ContinuationPromptFrame( string.Empty )
		);
	}

	[Fact]
	public void MalformedContinuationPromptIsRejected() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc633ContinuationPromptFrame(
				"\ud800"
			)
		);
	}

	[Fact]
	public void OversizedContinuationPromptIsRejected() {
		string continuationPrompt = new(
			'a',
			TerminalOsc633Encoder.MaximumPayloadLength
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc633ContinuationPromptFrame(
				continuationPrompt
			)
		);
	}
}
