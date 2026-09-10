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
/// Represents one application-facing event decoded from the authoritative terminal byte stream.
/// </summary>
internal readonly struct TerminalApplicationEvent {
	private TerminalApplicationEvent(
		TerminalInputEvent? inputEvent,
		TerminalSemanticEvent? semanticEvent
	) {
		if ( ( inputEvent is null ) == ( semanticEvent is null ) ) {
			throw new ArgumentException(
				"A terminal application event must contain exactly one input or semantic event."
			);
		}

		this.InputEvent = inputEvent;
		this.SemanticEvent = semanticEvent;
	}

	internal TerminalInputEvent? InputEvent {
		get;
	}

	internal TerminalSemanticEvent? SemanticEvent {
		get;
	}

	internal bool IsEndOfInput {
		get {
			return this.InputEvent?.Kind == TerminalInputEventKind.EndOfInput;
		}
	}

	internal TerminalEvent ToTerminalEvent() {
		if ( this.InputEvent is not null ) {
			return TerminalEvent.FromInput( this.InputEvent );
		}
		if ( this.SemanticEvent is not null ) {
			return TerminalEvent.FromSemantic( this.SemanticEvent );
		}

		throw new InvalidOperationException(
			"The terminal application event does not contain a payload."
		);
	}

	internal static TerminalApplicationEvent FromInput(
		TerminalInputEvent inputEvent
	) {
		ArgumentNullException.ThrowIfNull( inputEvent );

		return new TerminalApplicationEvent(
			inputEvent,
			semanticEvent: null
		);
	}

	internal static TerminalApplicationEvent FromSemantic(
		TerminalSemanticEvent semanticEvent
	) {
		ArgumentNullException.ThrowIfNull( semanticEvent );

		return new TerminalApplicationEvent(
			inputEvent: null,
			semanticEvent
		);
	}
}
