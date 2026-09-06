using Icod.Terminal;

TerminalColor color = new(
	0x1234,
	0x5678,
	0x9abc
);
TerminalColor from8 = TerminalColor.FromRgb8(
	0x12,
	0x34,
	0x56
);
TerminalPaletteColor paletteEntry = new(
	7,
	color
);

if ( 0x1212 != from8.Red
	|| 0x3434 != from8.Green
	|| 0x5656 != from8.Blue ) {
	throw new InvalidOperationException( "TerminalColor.FromRgb8 did not preserve byte replication semantics." );
}
if ( 7 != paletteEntry.Index
	|| color != paletteEntry.Color ) {
	throw new InvalidOperationException( "TerminalPaletteColor did not preserve typed entry semantics." );
}

_ = TerminalDynamicColor.DefaultForeground;
_ = TerminalDynamicColor.DefaultBackground;
_ = TerminalDynamicColor.TextCursor;
_ = TerminalDynamicColor.MouseForeground;
_ = TerminalDynamicColor.MouseBackground;
_ = TerminalDynamicColor.HighlightBackground;
_ = TerminalDynamicColor.HighlightForeground;

_ = BindSetPalette;
_ = BindSetPalettes;
_ = BindQueryPalette;
_ = BindResetPalette;
_ = BindResetPalettes;
_ = BindResetAllPalette;
_ = BindSetDynamic;
_ = BindQueryDynamic;
_ = BindResetDynamic;

Console.WriteLine( "Icod.Terminal 0.13 color package API smoke passed." );

static ValueTask BindSetPalette(
	TerminalSession session,
	byte index,
	TerminalColor value,
	CancellationToken cancellationToken
) {
	return session.SetPaletteColorAsync(
		index,
		value,
		cancellationToken
	);
}

static ValueTask BindSetPalettes(
	TerminalSession session,
	IReadOnlyList<TerminalPaletteColor> entries,
	CancellationToken cancellationToken
) {
	return session.SetPaletteColorsAsync(
		entries,
		cancellationToken
	);
}

static ValueTask<TerminalColor> BindQueryPalette(
	TerminalSession session,
	byte index,
	TimeSpan timeout,
	CancellationToken cancellationToken
) {
	return session.QueryPaletteColorAsync(
		index,
		timeout,
		cancellationToken
	);
}

static ValueTask BindResetPalette(
	TerminalSession session,
	byte index,
	CancellationToken cancellationToken
) {
	return session.ResetPaletteColorAsync(
		index,
		cancellationToken
	);
}

static ValueTask BindResetPalettes(
	TerminalSession session,
	IReadOnlyList<byte> indices,
	CancellationToken cancellationToken
) {
	return session.ResetPaletteColorsAsync(
		indices,
		cancellationToken
	);
}

static ValueTask BindResetAllPalette(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	return session.ResetPaletteAsync( cancellationToken );
}

static ValueTask BindSetDynamic(
	TerminalSession session,
	TerminalDynamicColor kind,
	TerminalColor value,
	CancellationToken cancellationToken
) {
	return session.SetDynamicColorAsync(
		kind,
		value,
		cancellationToken
	);
}

static ValueTask<TerminalColor> BindQueryDynamic(
	TerminalSession session,
	TerminalDynamicColor kind,
	TimeSpan timeout,
	CancellationToken cancellationToken
) {
	return session.QueryDynamicColorAsync(
		kind,
		timeout,
		cancellationToken
	);
}

static ValueTask BindResetDynamic(
	TerminalSession session,
	TerminalDynamicColor kind,
	CancellationToken cancellationToken
) {
	return session.ResetDynamicColorAsync(
		kind,
		cancellationToken
	);
}
