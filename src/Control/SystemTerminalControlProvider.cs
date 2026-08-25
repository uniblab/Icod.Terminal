namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Selects the native terminal-control provider for the current operating
/// system and exposes controlled unsupported results on other platforms.
/// </summary>
public sealed class SystemTerminalControlProvider : ITerminalControlProvider {
	private readonly ITerminalControlProvider provider;

	private SystemTerminalControlProvider() {
		if ( OperatingSystem.IsWindows() ) {
			this.provider = WindowsTerminalControlProvider.Instance;
		} else if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ) {
			this.provider = PosixTerminalControlProvider.Instance;
		} else {
			this.provider = UnsupportedTerminalControlProvider.Instance;
		}
	}

	/// <summary>Gets the process-wide system provider.</summary>
	public static SystemTerminalControlProvider Instance {
		get;
	} = new SystemTerminalControlProvider();

	/// <inheritdoc />
	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );

		return this.provider.Observe( endpoint );
	}

	/// <inheritdoc />
	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );

		return this.provider.GetSize( endpoint );
	}

	/// <inheritdoc />
	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );

		return this.provider.GetMode( endpoint );
	}

	/// <inheritdoc />
	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( mode );

		if ( !Enum.IsDefined( timing ) ) {
			throw new ArgumentOutOfRangeException( nameof( timing ) );
		}

		return this.provider.SetMode(
			endpoint,
			mode,
			timing
		);
	}

	private sealed class UnsupportedTerminalControlProvider : ITerminalControlProvider {
		private UnsupportedTerminalControlProvider() {
		}

		internal static UnsupportedTerminalControlProvider Instance {
			get;
		} = new UnsupportedTerminalControlProvider();

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			return TerminalControlResult<TerminalEndpointObservation>.Unsupported(
				"Terminal identification is unsupported on this platform."
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			return TerminalControlResult<TerminalSize>.Unsupported(
				"Live terminal-size observation is unsupported on this platform."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			return TerminalControlResult<TerminalModeSnapshot>.Unsupported(
				"Terminal-mode retrieval is unsupported on this platform."
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );

			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}

			return TerminalControlMutationResult.Unsupported(
				"Terminal-mode mutation is unsupported on this platform."
			);
		}
	}
}