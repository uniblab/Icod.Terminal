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
