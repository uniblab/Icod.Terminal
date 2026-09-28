/*
	Icod.Terminal.RichInput.Sample
	Sample application demonstrating Icod.Terminal RichInput features.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.RichInput.Sample;

using Icod.Terminal;

/// <summary>Tracks the reported editor keys from one terminal input stream.</summary>
public sealed class EditorKeyState {
	public bool LeftHeld { get; private set; }
	public int ControlInsertRepeats { get; private set; }

	/// <summary>Applies a key with an explicit phase from an active reporting lease.</summary>
	/// <returns>True when the editor consumed a phased Left or Control+Insert event.</returns>
	public bool Observe( TerminalInputEvent input ) {
		ArgumentNullException.ThrowIfNull( input );
		if ( input.Kind != TerminalInputEventKind.Key ) {
			return false;
		}
		if ( input.Key == TerminalKey.Left ) {
			if ( input.KeyPhase == TerminalKeyEventPhase.Press ) {
				this.LeftHeld = true;
				return true;
			}
			if ( input.KeyPhase == TerminalKeyEventPhase.Release ) {
				this.LeftHeld = false;
				return true;
			}
		}
		if ( input.Key == TerminalKey.Insert
			&& input.KeyPhase == TerminalKeyEventPhase.Repeat
			&& ( input.Modifiers & TerminalKeyModifiers.Control ) == TerminalKeyModifiers.Control ) {
			this.ControlInsertRepeats = checked( this.ControlInsertRepeats + 1 );
			return true;
		}
		return false;
	}
}
