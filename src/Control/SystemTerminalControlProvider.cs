/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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