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
