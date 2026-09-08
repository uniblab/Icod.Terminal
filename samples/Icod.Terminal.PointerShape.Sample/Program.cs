/*
	Icod.Terminal.PointerShape.Sample
	Sample application demonstrating Icod.Terminal PointerShape features.
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

await session.SetPointerShapeAsync(
	TerminalPointerShape.Crosshair
);
await session.WriteTextAsync(
	"Explicit crosshair pointer requested.\r\n"
);
await Task.Delay( 500 );
await session.ResetPointerShapeAsync();

await using ( TerminalPointerShapeLease pointer =
	await session.AcquirePointerShapeAsync(
		TerminalPointerShape.Pointer
	) ) {
	await session.WriteTextAsync(
		"Outer scoped pointer shape: Pointer.\r\n"
	);
	await Task.Delay( 500 );

	await using ( TerminalPointerShapeLease wait =
		await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Wait
		) ) {
		await session.WriteTextAsync(
			"Inner scoped pointer shape: Wait.\r\n"
		);
		await Task.Delay( 500 );
	}

	await session.WriteTextAsync(
		"Disposing the inner lease restored Pointer.\r\n"
	);
	await Task.Delay( 500 );
}

await session.WriteTextAsync(
	"Disposing the final lease reset pointer shape to terminal policy.\r\n"
);

TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );
try {
	bool supportsPointer = await session.QueryPointerShapeSupportAsync(
		TerminalPointerShape.Pointer,
		timeout
	);
	await session.WriteTextAsync(
		$"Explicit OSC 22 support reply for Pointer: {supportsPointer}.\r\n"
	);

	TerminalPointerShapeObservation current =
		await session.QueryCurrentPointerShapeAsync( timeout );
	string currentText = current.HasShape
		? current.Shape!.Value.ToString()
		: "no application pointer shape"
	;
	await session.WriteTextAsync(
		$"Explicit current pointer observation: {currentText}.\r\n"
	);
} catch ( TimeoutException ) {
	await session.WriteTextAsync(
		"No OSC 22 query reply arrived before the deadline; support is unknown.\r\n"
	);
}
