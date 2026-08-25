namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Acquires reversible host output-mode state needed for terminal protocol output.
/// </summary>
internal static class SystemTerminalOutputSetup {
	internal static IDisposable? Configure(
		ITerminalControlProvider controlProvider,
		TerminalEndpoint endpoint,
		TerminalEndpointObservation observation,
		bool configureOutput
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( observation );

		if ( !configureOutput
			|| !ReferenceEquals(
				controlProvider,
				SystemTerminalControlProvider.Instance
			)
			|| !OperatingSystem.IsWindows()
			|| !observation.IsTerminal
			|| ( TerminalPlatformKind.WindowsConsole != observation.Platform ) ) {
			return null;
		}

		TerminalStandardStream stream = endpoint.Kind switch {
			TerminalEndpointKind.FileDescriptor when 1 == endpoint.FileDescriptor =>
				TerminalStandardStream.Output,
			TerminalEndpointKind.FileDescriptor when 2 == endpoint.FileDescriptor =>
				TerminalStandardStream.Error,
			_ => throw new NotSupportedException(
				string.Concat(
					"Automatic Windows virtual-terminal output setup is available only for ",
					"process standard output or standard error. Set ConfigureOutput to false ",
					"only when a caller-owned endpoint is already configured."
				)
			)
		};

		IDisposable? lease = WindowsVirtualTerminal.TryEnableOutput( stream );
		if ( lease is null ) {
			throw new InvalidOperationException(
				string.Concat(
					"Windows virtual-terminal output processing could not be enabled for ",
					endpoint.DisplayName,
					"."
				)
			);
		}

		return lease;
	}
}
