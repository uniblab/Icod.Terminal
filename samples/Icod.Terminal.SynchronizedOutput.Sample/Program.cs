/*
	Icod.Terminal.SynchronizedOutput.Sample
	Sample application demonstrating Icod.Terminal SynchronizedOutput features.
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

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

Console.WriteLine(
	"Beginning a synchronized-output scope. Successful completion proves only that mode-2026 frames were emitted."
);

await using ( TerminalSynchronizedOutputLease synchronized =
	await session.AcquireSynchronizedOutputAsync() ) {
	await session.WriteTextAsync( "line 1: synchronized update\r\n" );
	await session.WriteTextAsync( "line 2: ordinary session writes remain ordinary writes\r\n" );
	await session.SetWindowTitleAsync( "Icod.Terminal synchronized output" );
	await session.WriteTextAsync( "line 3: final lease disposal emits the synchronized-output end boundary\r\n" );
}

Console.WriteLine(
	"Synchronized-output scope released."
);
