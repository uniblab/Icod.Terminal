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

Func<byte, TerminalColor, CancellationToken, ValueTask> setPalette =
	TerminalSession.SetPaletteColorAsync;
Func<IReadOnlyList<TerminalPaletteColor>, CancellationToken, ValueTask> setPalettes =
	TerminalSession.SetPaletteColorsAsync;
Func<byte, TimeSpan, CancellationToken, ValueTask<TerminalColor>> queryPalette =
	TerminalSession.QueryPaletteColorAsync;
Func<byte, CancellationToken, ValueTask> resetPalette =
	TerminalSession.ResetPaletteColorAsync;
Func<IReadOnlyList<byte>, CancellationToken, ValueTask> resetPalettes =
	TerminalSession.ResetPaletteColorsAsync;
Func<CancellationToken, ValueTask> resetAllPalette =
	TerminalSession.ResetPaletteAsync;
Func<TerminalDynamicColor, TerminalColor, CancellationToken, ValueTask> setDynamic =
	TerminalSession.SetDynamicColorAsync;
Func<TerminalDynamicColor, TimeSpan, CancellationToken, ValueTask<TerminalColor>> queryDynamic =
	TerminalSession.QueryDynamicColorAsync;
Func<TerminalDynamicColor, CancellationToken, ValueTask> resetDynamic =
	TerminalSession.ResetDynamicColorAsync;

_ = setPalette;
_ = setPalettes;
_ = queryPalette;
_ = resetPalette;
_ = resetPalettes;
_ = resetAllPalette;
_ = setDynamic;
_ = queryDynamic;
_ = resetDynamic;

Console.WriteLine( "Icod.Terminal 0.13 color package API smoke passed." );
