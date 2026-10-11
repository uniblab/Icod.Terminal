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
using System.Text;

namespace Icod.Terminal.Tests.Samples;
using Icod.Terminal.Clipboard.Sample;
using Xunit;

public sealed class ClipboardCommandLineTests {
	[Theory]
	[InlineData( "" )]
	[InlineData( "--help" )]
	[InlineData( "--read --maximum-read-bytes 0" )]
	[InlineData( "--write secret --maximum-write-bytes 1" )]
	[InlineData( "--generated-bytes 65537" )]
	[InlineData( "--read --maximum-write-bytes 16777217" )]
	public async Task HeadlessOrInvalidCommandNeverOpensSession( string command ) {
		bool opened = false;
		using StringWriter output = new();
		using StringWriter error = new();
		int result = await ClipboardCommandLine.RunAsync(
			command.Split( ' ', StringSplitOptions.RemoveEmptyEntries ), output, error,
			( _, _ ) => { opened = true; throw new InvalidOperationException(); } );
		Assert.False( opened );
		Assert.Equal( command is "" or "--help" ? 0 : 2, result );
		Assert.DoesNotContain( "secret", error.ToString() );
	}

	[Fact]
	public async Task IndependentLimitsReachSessionFactory() {
		TerminalSessionOptions? captured = null;
		using StringWriter output = new();
		using StringWriter error = new();
		int result = await ClipboardCommandLine.RunAsync(
			["--generated-bytes", "100000", "--maximum-write-bytes", "1048576"],
			output, error, ( options, _ ) => {
				captured = options;
				throw new InvalidOperationException( "Synthetic open failure." );
			} );
		Assert.Equal( 1, result );
		Assert.NotNull( captured );
		Assert.Equal( 65_536, captured.ClipboardOptions.MaximumReadBytes );
		Assert.Equal( 1_048_576, captured.ClipboardOptions.MaximumWriteBytes );
	}
}
