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
/// Represents one explicit live observation of terminal cursor-style support and state.
/// </summary>
public sealed class TerminalCursorStyleObservation {
	internal TerminalCursorStyleObservation(
		bool isSupported,
		TerminalCursorStyle? style
	) {
		if ( isSupported && !style.HasValue ) {
			throw new ArgumentNullException(
				nameof( style ),
				"A supported cursor-style observation must contain a semantic style."
			);
		}
		if ( !isSupported && style.HasValue ) {
			throw new ArgumentException(
				"An unsupported cursor-style observation cannot contain a semantic style.",
				nameof( style )
			);
		}
		if ( style.HasValue && !Enum.IsDefined( style.Value ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( style ),
				style.Value,
				"The observed cursor style is not defined by the frozen 0.8 contract."
			);
		}

		this.IsSupported = isSupported;
		this.Style = style;
	}

	/// <summary>
	/// Gets whether the terminal explicitly reported DECSCUSR cursor-style state as supported.
	/// </summary>
	public bool IsSupported {
		get;
	}

	/// <summary>
	/// Gets the observed semantic cursor style when supported, or <see langword="null"/>
	/// when the terminal explicitly reports the DECRQSS request as unsupported.
	/// </summary>
	public TerminalCursorStyle? Style {
		get;
	}
}
