/*
	Icod.Terminal.Sample
	Sample application demonstrating Icod.Terminal Sample features.
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
using Icod.TermInfo;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

TerminalControlResult<TerminalSize> sizeResult = session.GetSize();
string sizeText;
if ( sizeResult.IsAvailable ) {
	TerminalSize size = sizeResult.GetRequiredValue();
	sizeText = string.Concat(
		size.Columns.ToString( System.Globalization.CultureInfo.InvariantCulture ),
		"x",
		size.Rows.ToString( System.Globalization.CultureInfo.InvariantCulture )
	);
} else {
	sizeText = sizeResult.Status.ToString();
}

await session.WriteTextAsync(
	string.Concat(
		"Icod.Terminal session opened via ",
		session.Identity.Source.ToString(),
		"; size=",
		sizeText,
		".\r\n"
	)
);

return 0;
