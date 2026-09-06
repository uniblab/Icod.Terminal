using Icod.Terminal;

Func<TerminalSession, string, CancellationToken, ValueTask> sendNotification =
	BindSendNotification;
Func<TerminalSession, string, CancellationToken, ValueTask> publishWindowsCwd =
	BindPublishWindowsCurrentDirectory;

_ = sendNotification;
_ = publishWindowsCwd;

Console.WriteLine( "Icod.Terminal 0.16 safe OSC 9 package API smoke passed." );

static ValueTask BindSendNotification(
	TerminalSession session,
	string message,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( message );
	return session.SendNotificationAsync(
		message,
		cancellationToken
	);
}

static ValueTask BindPublishWindowsCurrentDirectory(
	TerminalSession session,
	string windowsPath,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( windowsPath );
	return session.PublishWindowsCurrentDirectoryCompatibilityAsync(
		windowsPath,
		cancellationToken
	);
}
