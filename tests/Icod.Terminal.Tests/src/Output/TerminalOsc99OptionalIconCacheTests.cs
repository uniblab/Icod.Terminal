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
/// Verifies that Kitty OSC 99 icon caching remains optional.
/// </summary>
public sealed class TerminalOsc99OptionalIconCacheTests {
	[Fact]
	public void TransmittedIconDoesNotRequireCacheIdentifier() {
		byte[] pngHeader = [
			0x89,
			(byte)'P',
			(byte)'N',
			(byte)'G',
			0x0d,
			0x0a,
			0x1a,
			0x0a
		];
		KittyNotificationOptions options = new() {
			IconData = pngHeader
		};

		byte[][] frames = TerminalOsc99NotificationEncoder.EncodeNotificationFrames(
			"Build",
			string.Empty,
			options
		);

		Assert.Equal( 2, frames.Length );
		string iconFrame = Encoding.ASCII.GetString( frames[ 0 ] );
		Assert.Contains(
			"p=icon:e=1:d=0;",
			iconFrame,
			StringComparison.Ordinal
		);
		Assert.DoesNotContain(
			"g=",
			iconFrame,
			StringComparison.Ordinal
		);
	}

	[Fact]
	public void CacheIdentifierIsEmittedOnlyWhenRequested() {
		byte[] pngHeader = [
			0x89,
			(byte)'P',
			(byte)'N',
			(byte)'G',
			0x0d,
			0x0a,
			0x1a,
			0x0a
		];
		KittyNotificationOptions options = new() {
			IconData = pngHeader,
			IconDataIdentifier = "icon-cache-1"
		};

		byte[][] frames = TerminalOsc99NotificationEncoder.EncodeNotificationFrames(
			"Build",
			string.Empty,
			options
		);

		string iconFrame = Encoding.ASCII.GetString( frames[ 0 ] );
		Assert.Contains(
			"g=icon-cache-1",
			iconFrame,
			StringComparison.Ordinal
		);
	}
}
