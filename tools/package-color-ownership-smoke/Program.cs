/*
	Icod.Terminal.PackageColorOwnershipSmoke
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

Func<TerminalSession, byte, TerminalColor, TimeSpan, CancellationToken, ValueTask<TerminalPaletteColorLease>> acquirePalette =
	BindAcquirePalette;
Func<TerminalSession, TerminalDynamicColor, TerminalColor, TimeSpan, CancellationToken, ValueTask<TerminalDynamicColorLease>> acquireDynamic =
	BindAcquireDynamic;

TerminalPaletteColorLease? paletteLease = null;
TerminalDynamicColorLease? dynamicLease = null;

_ = acquirePalette;
_ = acquireDynamic;
_ = paletteLease?.Index;
_ = paletteLease?.Color;
_ = dynamicLease?.Kind;
_ = dynamicLease?.Color;

Console.WriteLine( "Icod.Terminal 0.14 lifecycle-safe color ownership package API smoke passed." );

static ValueTask<TerminalPaletteColorLease> BindAcquirePalette(
	TerminalSession session,
	byte index,
	TerminalColor color,
	TimeSpan timeout,
	CancellationToken cancellationToken
) {
	return session.AcquirePaletteColorAsync(
		index,
		color,
		timeout,
		cancellationToken
	);
}

static ValueTask<TerminalDynamicColorLease> BindAcquireDynamic(
	TerminalSession session,
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan timeout,
	CancellationToken cancellationToken
) {
	return session.AcquireDynamicColorAsync(
		kind,
		color,
		timeout,
		cancellationToken
	);
}
