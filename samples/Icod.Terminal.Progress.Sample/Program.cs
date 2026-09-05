using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync();
await using TerminalProgressLease progress = await session.AcquireProgressAsync();

for ( long stage = 1; stage <= 3; ++stage ) {
	await progress.ReportAsync(
		stage,
		3
	);
	await session.WriteTextAsync(
		$"Completed stage {stage} of 3.\r\n"
	);
	await Task.Delay( 250 );
}

await progress.SetIndeterminateAsync();
await session.WriteTextAsync(
	"Finishing work with indeterminate duration.\r\n"
);
await Task.Delay( 750 );

await progress.ReportAsync(
	TerminalProgressState.Attention,
	3,
	3
);
await session.WriteTextAsync(
	"Progress sample complete; disposing the lease clears terminal progress.\r\n"
);
