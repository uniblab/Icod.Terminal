/*
	Icod.Terminal.Progress.Sample
	Sample application demonstrating Icod.Terminal Progress features.
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
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync();
await using TerminalProgressLease progress = await session.AcquireProgressAsync();

for ( long stage = 1; stage <= 3; ++stage ) {
	await progress.ReportAsync(
		stage,
		3
	);
	await session.WriteTextAsync(
		$"Completed stage {stage} of 3.\r\n"
	);
	await Task.Delay( 250 );
}

await progress.SetIndeterminateAsync();
await session.WriteTextAsync(
	"Finishing work with indeterminate duration.\r\n"
);
await Task.Delay( 750 );

await progress.ReportAsync(
	TerminalProgressState.Attention,
	3,
	3
);
await session.WriteTextAsync(
	"Progress sample complete; disposing the lease clears terminal progress.\r\n"
);
