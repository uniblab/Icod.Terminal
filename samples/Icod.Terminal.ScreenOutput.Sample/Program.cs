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

/// <summary>Runs a semantic screen-frame demonstration in an owned alternate screen.</summary>
internal static class Program {
	/// <summary>Accepts --help or --recovery; r redraws, q or Escape exits.</summary>
	private static async Task<int> Main( string[] args ) {
		if ( args.Length == 1 && args[ 0 ] is "--help" or "-h" ) {
			Console.WriteLine( "Usage: Icod.Terminal.ScreenOutput.Sample [--recovery]" );
			Console.WriteLine( "Requires interactive input/output and alternate-screen support. Press r to refresh, q or Escape to exit." );
			Console.WriteLine( "--recovery demonstrates stale-frame rejection followed by one freshly planned frame." );
			return 0;
		}
		if ( args.Length > 1 || ( args.Length == 1 && args[ 0 ] != "--recovery" ) ) {
			Console.Error.WriteLine( "Unknown arguments. Use --help for usage." );
			return 2;
		}
		try {
			// Dispose presentation and session ownership before reporting to the shell.
			bool available;
			await using ( TerminalSession session = await TerminalSession.OpenAsync() ) {
				available = await ScreenOutputExample.RunInteractiveAsync( session, args.Length == 1 );
			}
			if ( available ) {
				return 0;
			}
			Console.Error.WriteLine( "This terminal lacks the alternate-screen, cursor, or rendition operations required by the demo." );
			return 1;
		} catch ( OperationCanceledException ) {
			Console.Error.WriteLine( "Screen demonstration cancelled." );
			return 130;
		} catch ( Exception exception ) {
			// Committed output may be partial. Surface failures; do not replay the frame.
			Console.Error.WriteLine( $"Screen demonstration failed: {exception}" );
			return 1;
		}
	}
}
