/*
	Icod.Terminal.PackageColorSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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

Func<TerminalSession, byte, TerminalColor, CancellationToken, ValueTask> setPalette =
	BindSetPalette;
Func<TerminalSession, IReadOnlyList<TerminalPaletteColor>, CancellationToken, ValueTask> setPalettes =
	BindSetPalettes;
Func<TerminalSession, byte, TimeSpan, CancellationToken, ValueTask<TerminalColor>> queryPalette =
	BindQueryPalette;
Func<TerminalSession, byte, CancellationToken, ValueTask> resetPalette =
	BindResetPalette;
Func<TerminalSession, IReadOnlyList<byte>, CancellationToken, ValueTask> resetPalettes =
	BindResetPalettes;
Func<TerminalSession, CancellationToken, ValueTask> resetAllPalette =
	BindResetAllPalette;
Func<TerminalSession, TerminalDynamicColor, TerminalColor, CancellationToken, ValueTask> setDynamic =
	BindSetDynamic;
Func<TerminalSession, TerminalDynamicColor, TimeSpan, CancellationToken, ValueTask<TerminalColor>> queryDynamic =
	BindQueryDynamic;
Func<TerminalSession, TerminalDynamicColor, CancellationToken, ValueTask> resetDynamic =
	BindResetDynamic;

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
