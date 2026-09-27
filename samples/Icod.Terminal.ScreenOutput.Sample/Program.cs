/*
	Icod.Terminal.ScreenOutput.Sample
	Sample application demonstrating Icod.Terminal ScreenOutput features.
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
namespace Icod.Terminal.ScreenOutput.Sample;

internal static class Program {
	private static async Task<int> Main() {
		await using TerminalSession session = await TerminalSession.OpenAsync();
		if ( !await ScreenOutputExample.DrawFrameAsync( session, "Terminal-owned screen output" ) ) {
			Console.Error.WriteLine( "This terminal cannot safely position the cursor and restore rendition." );
			return 1;
		}
		return 0;
	}
}
