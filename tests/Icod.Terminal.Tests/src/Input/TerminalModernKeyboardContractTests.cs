/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
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
namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the additive T171 modern keyboard semantic contract without requiring modern wire decoding.
/// </summary>
public sealed class TerminalModernKeyboardContractTests {
	[Fact]
	public void ExistingInputEventKindValuesRemainStable() {
		Assert.Equal( 0, (int)TerminalInputEventKind.Text );
		Assert.Equal( 1, (int)TerminalInputEventKind.Key );
		Assert.Equal( 2, (int)TerminalInputEventKind.Mouse );
		Assert.Equal( 3, (int)TerminalInputEventKind.Focus );
		Assert.Equal( 4, (int)TerminalInputEventKind.Paste );
		Assert.Equal( 5, (int)TerminalInputEventKind.EndOfInput );
	}

	[Fact]
	public void ExistingTerminalKeyValuesRemainStable() {
		Assert.Equal( 0, (int)TerminalKey.None );
		Assert.Equal( 1, (int)TerminalKey.Character );
		Assert.Equal( 2, (int)TerminalKey.Enter );
		Assert.Equal( 3, (int)TerminalKey.Space );
		Assert.Equal( 4, (int)TerminalKey.Escape );
		Assert.Equal( 5, (int)TerminalKey.Backspace );
		Assert.Equal( 6, (int)TerminalKey.Tab );
		Assert.Equal( 7, (int)TerminalKey.Up );
		Assert.Equal( 8, (int)TerminalKey.Down );
		Assert.Equal( 9, (int)TerminalKey.Left );
		Assert.Equal( 10, (int)TerminalKey.Right );
		Assert.Equal( 11, (int)TerminalKey.Home );
		Assert.Equal( 12, (int)TerminalKey.End );
		Assert.Equal( 13, (int)TerminalKey.PageUp );
		Assert.Equal( 14, (int)TerminalKey.PageDown );
		Assert.Equal( 15, (int)TerminalKey.Insert );
		Assert.Equal( 16, (int)TerminalKey.Delete );
		Assert.Equal( 17, (int)TerminalKey.Function );
	}

	[Fact]
	public void ModifierValuesRemainStableAndModernFlagsAppend() {
		Assert.Equal( 0, (int)TerminalKeyModifiers.None );
		Assert.Equal( 1, (int)TerminalKeyModifiers.Shift );
		Assert.Equal( 2, (int)TerminalKeyModifiers.Control );
		Assert.Equal( 4, (int)TerminalKeyModifiers.Alt );
		Assert.Equal( 8, (int)TerminalKeyModifiers.Super );
		Assert.Equal( 16, (int)TerminalKeyModifiers.Hyper );
		Assert.Equal( 32, (int)TerminalKeyModifiers.Meta );
		Assert.Equal( 64, (int)TerminalKeyModifiers.CapsLock );
		Assert.Equal( 128, (int)TerminalKeyModifiers.NumLock );
	}

	[Fact]
	public void KeyPhaseAndReportingModeValuesAreFrozen() {
		Assert.Equal( 0, (int)TerminalKeyEventPhase.Press );
		Assert.Equal( 1, (int)TerminalKeyEventPhase.Repeat );
		Assert.Equal( 2, (int)TerminalKeyEventPhase.Release );
		Assert.Equal( 0, (int)TerminalKeyboardReportingMode.Disambiguated );
		Assert.Equal( 1, (int)TerminalKeyboardReportingMode.EventTypes );
		Assert.Equal( 2, (int)TerminalKeyboardReportingMode.AllKeys );
	}

	[Fact]
	public void TraditionalKeyFactoryDefaultsToPress() {
		TerminalInputEvent inputEvent = TerminalInputEvent.FromKey(
			TerminalKey.Enter
		);

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Enter, inputEvent.Key );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
		Assert.Null( inputEvent.ShiftedCharacter );
		Assert.Null( inputEvent.BaseLayoutCharacter );
		Assert.Null( inputEvent.AssociatedText );
	}

	[Fact]
	public void TextEventsDoNotPretendToBeKeyEvents() {
		TerminalInputEvent inputEvent = TerminalInputEvent.FromText(
			new Rune( 'x' )
		);

		Assert.Equal( TerminalInputEventKind.Text, inputEvent.Kind );
		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'x' ), inputEvent.Character );
		Assert.Null( inputEvent.KeyPhase );
		Assert.Null( inputEvent.AssociatedText );
	}

	[Fact]
	public void ModernCharacterMetadataRoundTrips() {
		TerminalKeyModifiers modifiers =
			TerminalKeyModifiers.Shift
			| TerminalKeyModifiers.Control
			| TerminalKeyModifiers.Alt
			| TerminalKeyModifiers.Super
			| TerminalKeyModifiers.Hyper
			| TerminalKeyModifiers.Meta
			| TerminalKeyModifiers.CapsLock
			| TerminalKeyModifiers.NumLock;
		TerminalInputEvent inputEvent = TerminalInputEvent.FromKey(
			TerminalKey.Character,
			modifiers,
			new Rune( 'a' ),
			keyPhase: TerminalKeyEventPhase.Repeat,
			shiftedCharacter: new Rune( 'A' ),
			baseLayoutCharacter: new Rune( 'q' ),
			associatedText: "Å😀"
		);

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'a' ), inputEvent.Character );
		Assert.Equal( new Rune( 'A' ), inputEvent.ShiftedCharacter );
		Assert.Equal( new Rune( 'q' ), inputEvent.BaseLayoutCharacter );
		Assert.Equal( "Å😀", inputEvent.AssociatedText );
		Assert.Equal( modifiers, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Repeat, inputEvent.KeyPhase );
	}

	[Fact]
	public void ModernNamedKeyCanCarryPhaseModifiersAndAssociatedText() {
		TerminalInputEvent inputEvent = TerminalInputEvent.FromKey(
			TerminalKey.MediaPlayPause,
			TerminalKeyModifiers.Super | TerminalKeyModifiers.Meta,
			keyPhase: TerminalKeyEventPhase.Release,
			associatedText: "▶"
		);

		Assert.Equal( TerminalKey.MediaPlayPause, inputEvent.Key );
		Assert.Equal( TerminalKeyEventPhase.Release, inputEvent.KeyPhase );
		Assert.Equal(
			TerminalKeyModifiers.Super | TerminalKeyModifiers.Meta,
			inputEvent.Modifiers
		);
		Assert.Equal( "▶", inputEvent.AssociatedText );
	}

	[Fact]
	public void FunctionKeyZeroCompatibilityRangeIsRetained() {
		TerminalInputEvent inputEvent = TerminalInputEvent.FromKey(
			TerminalKey.Function,
			functionKeyNumber: 0
		);

		Assert.Equal( 0, inputEvent.FunctionKeyNumber );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
	}

	[Fact]
	public void UnknownModernKeyUsesSemanticBucketWithoutRawCode() {
		TerminalInputEvent inputEvent = TerminalInputEvent.FromKey(
			TerminalKey.Unrecognized,
			TerminalKeyModifiers.Hyper,
			keyPhase: TerminalKeyEventPhase.Release
		);

		Assert.Equal( TerminalKey.Unrecognized, inputEvent.Key );
		Assert.Equal( TerminalKeyModifiers.Hyper, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Release, inputEvent.KeyPhase );
		Assert.Null( inputEvent.FunctionKeyNumber );
	}

	[Fact]
	public void CharacterMetadataCannotLeakOntoNamedKey() {
		Assert.Throws<ArgumentException>(
			() => TerminalInputEvent.FromKey(
				TerminalKey.Enter,
				shiftedCharacter: new Rune( 'E' )
			)
		);
	}

	[Fact]
	public void EmptyAssociatedTextIsRejected() {
		Assert.Throws<ArgumentException>(
			() => TerminalInputEvent.FromKey(
				TerminalKey.Enter,
				associatedText: string.Empty
			)
		);
	}
}
