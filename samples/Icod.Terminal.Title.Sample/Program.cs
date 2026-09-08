/*
	Icod.Terminal.Title.Sample
	Sample application demonstrating Icod.Terminal Title features.
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

await session.WriteTextAsync(
	"Icod.Terminal 0.4 title sample.\r\n"
);
await session.WriteTextAsync(
	"This sample emits OSC 0, OSC 1, and OSC 2. The previous terminal title is not queried or restored.\r\n"
);

await session.SetTitleAsync(
	"Icod.Terminal — OSC 0"
);
await Task.Delay(
	TimeSpan.FromSeconds( 1 )
);

await session.SetIconNameAsync(
	"Icod.Terminal icon"
);
await Task.Delay(
	TimeSpan.FromSeconds( 1 )
);

await session.SetWindowTitleAsync(
	"Icod.Terminal — OSC 2"
);

await session.WriteTextAsync(
	"Emitted SetTitleAsync, SetIconNameAsync, and SetWindowTitleAsync.\r\n"
);
await session.WriteTextAsync(
	"Successful completion means the frames were written; it does not prove the terminal applied them.\r\n"
);
await session.WriteTextAsync(
	"Press Enter to exit. The title will remain whatever the terminal chose to apply.\r\n"
);

while ( true ) {
	TerminalEvent terminalEvent = await session.ReadEventAsync();
	if ( TerminalEventKind.Input != terminalEvent.Kind ) {
		continue;
	}

	TerminalInputEvent? input = terminalEvent.Input;
	if ( input is null ) {
		continue;
	}
	if ( TerminalInputEventKind.Text == input.Kind
		&& input.Character.HasValue
		&& ( '\r' == input.Character.Value.Value
			|| '\n' == input.Character.Value.Value ) ) {
		break;
	}
	if ( TerminalInputEventKind.Key == input.Kind
		&& TerminalKey.Enter == input.Key ) {
		break;
	}
}

return 0;
