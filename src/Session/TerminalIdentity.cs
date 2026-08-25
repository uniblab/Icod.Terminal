namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Identifies how a live session selected its terminal description.
/// </summary>
public enum TerminalIdentitySource {
	/// <summary>The caller supplied an explicit terminal-description override.</summary>
	ExplicitOverride,

	/// <summary>A requested terminal name was resolved through the configured terminal database.</summary>
	NamedProfile,

	/// <summary>The requested name was missing or unavailable and a platform fallback was selected.</summary>
	PlatformFallback
}

/// <summary>
/// Describes the terminal profile selected for one live session and how it was resolved.
/// </summary>
public sealed class TerminalIdentity {
	internal TerminalIdentity(
		TerminalDescription terminal,
		string? requestedName,
		TerminalIdentitySource source
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		if ( requestedName is not null
			&& string.IsNullOrWhiteSpace( requestedName ) ) {
			throw new ArgumentException(
				"A requested terminal name cannot be empty or whitespace.",
				nameof( requestedName )
			);
		}
		if ( !Enum.IsDefined( source ) ) {
			throw new ArgumentOutOfRangeException( nameof( source ) );
		}

		this.Terminal = terminal;
		this.RequestedName = requestedName;
		this.Source = source;
	}

	/// <summary>Gets the selected immutable terminal description.</summary>
	public TerminalDescription Terminal {
		get;
	}

	/// <summary>Gets the requested terminal name, when resolution began with a name.</summary>
	public string? RequestedName {
		get;
	}

	/// <summary>Gets how the terminal description was selected.</summary>
	public TerminalIdentitySource Source {
		get;
	}
}

internal static class TerminalIdentityResolver {
	internal static TerminalIdentity Resolve(
		TerminalSessionOptions options,
		TerminalEndpointObservation inputObservation,
		TerminalEndpointObservation outputObservation
	) {
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( inputObservation );
		ArgumentNullException.ThrowIfNull( outputObservation );

		if ( options.TerminalOverride is not null ) {
			return new TerminalIdentity(
				options.TerminalOverride,
				null,
				TerminalIdentitySource.ExplicitOverride
			);
		}

		string? requestedName = options.TerminalName
			?? TerminalEnvironment.TerminalName;
		TerminalDatabase database = options.TerminalDatabase
			?? CreateDefaultDatabase();

		if ( requestedName is not null
			&& database.TryLoad(
				requestedName,
				out TerminalDescription? terminal ) ) {
			return new TerminalIdentity(
				terminal,
				requestedName,
				TerminalIdentitySource.NamedProfile
			);
		}

		return new TerminalIdentity(
			GetPlatformFallback(
				inputObservation,
				outputObservation
			),
			requestedName,
			TerminalIdentitySource.PlatformFallback
		);
	}

	private static TerminalDatabase CreateDefaultDatabase() {
		return new TerminalDatabase(
			new ITerminalDescriptionProvider[] {
				new SystemTerminalDescriptionProvider(),
				TerminalDatabase.BuiltIn
			}
		);
	}

	private static TerminalDescription GetPlatformFallback(
		TerminalEndpointObservation inputObservation,
		TerminalEndpointObservation outputObservation
	) {
		ArgumentNullException.ThrowIfNull( inputObservation );
		ArgumentNullException.ThrowIfNull( outputObservation );

		TerminalPlatformKind? platform = outputObservation.IsTerminal
			? outputObservation.Platform
			: inputObservation.Platform;
		if ( TerminalPlatformKind.WindowsConsole == platform ) {
			string? windowsTerminalSession = OperatingSystem.IsWindows()
				? Environment.GetEnvironmentVariable( "WT_SESSION" )
				: null;

			return string.IsNullOrWhiteSpace( windowsTerminalSession )
				? TerminalProfiles.WinConsole
				: TerminalProfiles.MsTerminalDirect;
		}

		return TerminalProfiles.Dumb;
	}
}
