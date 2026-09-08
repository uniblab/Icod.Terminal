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

/// <summary>
/// Provides semantic OSC title operations for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Sets both the terminal icon name and window title using OSC 0.
	/// </summary>
	/// <param name="value">The title text to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the title emission.</returns>
	/// <remarks>
	/// The operation is emission-oriented: successful completion means the complete
	/// OSC 0 frame was written to the session output. It does not prove that the
	/// terminal applied the title. The title text is validated by the internal OSC
	/// writer before any output occurs.
	/// </remarks>
	public ValueTask SetTitleAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		return this.WriteTitleOperationAsync(
			OscTitleSelector.IconAndWindowTitle,
			value,
			cancellationToken
		);
	}

	/// <summary>
	/// Sets the terminal icon name using OSC 1.
	/// </summary>
	/// <param name="value">The icon-name text to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the icon-name emission.</returns>
	/// <remarks>
	/// The operation is emission-oriented: successful completion means the complete
	/// OSC 1 frame was written to the session output. It does not prove that the
	/// terminal applied an icon name, and it does not alter the window-title selector.
	/// The text is validated by the shared internal OSC writer before any output occurs.
	/// </remarks>
	public ValueTask SetIconNameAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		return this.WriteTitleOperationAsync(
			OscTitleSelector.IconName,
			value,
			cancellationToken
		);
	}

	/// <summary>
	/// Sets the terminal window title using OSC 2.
	/// </summary>
	/// <param name="value">The window-title text to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the window-title emission.</returns>
	/// <remarks>
	/// The operation is emission-oriented: successful completion means the complete
	/// OSC 2 frame was written to the session output. It does not prove that the
	/// terminal applied a window title, and it does not alter the icon-name selector.
	/// The text is validated by the shared internal OSC writer before any output occurs.
	/// </remarks>
	public ValueTask SetWindowTitleAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		return this.WriteTitleOperationAsync(
			OscTitleSelector.WindowTitle,
			value,
			cancellationToken
		);
	}

	private async ValueTask WriteTitleOperationAsync(
		OscTitleSelector selector,
		string value,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( value );
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC title operations require an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		await OscWriter.WriteTitleAsync(
			this.Output,
			selector,
			value,
			cancellationToken
		).ConfigureAwait( false );
	}
}
