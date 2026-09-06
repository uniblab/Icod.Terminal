namespace Icod.Terminal;

internal static partial class OscWriter {
	/// <summary>
	/// Encodes one complete canonical legacy OSC 9 notification frame.
	/// </summary>
	internal static byte[] EncodeOsc9NotificationFrame(
		string message
	) {
		ArgumentNullException.ThrowIfNull( message );
		return TerminalOsc9SafeTextEncoder.EncodeNotificationFrame( message );
	}

	/// <summary>
	/// Encodes one complete canonical OSC 9;9 Windows-current-directory compatibility frame.
	/// </summary>
	internal static byte[] EncodeOsc9WindowsCurrentDirectoryFrame(
		string windowsPath
	) {
		ArgumentNullException.ThrowIfNull( windowsPath );
		return TerminalOsc9SafeTextEncoder.EncodeWindowsCurrentDirectoryFrame(
			windowsPath
		);
	}

	/// <summary>
	/// Validates and emits one complete legacy OSC 9 notification frame through one output write.
	/// </summary>
	internal static ValueTask WriteOsc9NotificationAsync(
		ITerminalOutput output,
		string message,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( message );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc9NotificationFrame( message );
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	/// <summary>
	/// Validates and emits one complete OSC 9;9 Windows-current-directory compatibility frame through one output write.
	/// </summary>
	internal static ValueTask WriteOsc9WindowsCurrentDirectoryAsync(
		ITerminalOutput output,
		string windowsPath,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( windowsPath );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc9WindowsCurrentDirectoryFrame( windowsPath );
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}
}
