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
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T174 decode-only xterm modifyOtherKeys compatibility.
/// </summary>
public sealed class TerminalXtermModifyOtherKeysDecoderTests {
	[Theory]
	[InlineData( 2, TerminalKeyModifiers.Shift )]
	[InlineData( 3, TerminalKeyModifiers.Alt )]
	[InlineData( 5, TerminalKeyModifiers.Control )]
	[InlineData( 8, TerminalKeyModifiers.Shift | TerminalKeyModifiers.Alt | TerminalKeyModifiers.Control )]
	public async Task Level2OrdinaryCharacterMapsOnlyTraditionalModifiers(
		int encodedModifiers,
		TerminalKeyModifiers expectedModifiers
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			$"\u001b[27;{encodedModifiers};97~"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'a' ), inputEvent.Character );
		Assert.Equal( expectedModifiers, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
		Assert.Null( inputEvent.ShiftedCharacter );
		Assert.Null( inputEvent.BaseLayoutCharacter );
		Assert.Null( inputEvent.AssociatedText );
	}

	[Theory]
	[InlineData( 9, TerminalKey.Tab )]
	[InlineData( 13, TerminalKey.Enter )]
	[InlineData( 27, TerminalKey.Escape )]
	[InlineData( 32, TerminalKey.Space )]
	[InlineData( 127, TerminalKey.Backspace )]
	public async Task Level2KnownControlIdentityMapsToNamedKey(
		int codePoint,
		TerminalKey expectedKey
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			$"\u001b[27;5;{codePoint}~"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( expectedKey, inputEvent.Key );
		Assert.Equal( TerminalKeyModifiers.Control, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
		Assert.Null( inputEvent.Character );
	}

	[Fact]
	public async Task Level2UnicodeScalarIsPreserved() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[27;3;128578~"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 0x1F642 ), inputEvent.Character );
		Assert.Equal( TerminalKeyModifiers.Alt, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
	}

	[Fact]
	public async Task FragmentedLevel2FrameDecodesAsOnePressEvent() {
		ScriptedTerminalInput input = new(
			[
				Encoding.ASCII.GetBytes( "\u001b[27;" ),
				Encoding.ASCII.GetBytes( "6;" ),
				Encoding.ASCII.GetBytes( "120~" )
			]
		);
		TerminalInputDecoder decoder = CreateDecoder( input );

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'x' ), inputEvent.Character );
		Assert.Equal(
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control,
			inputEvent.Modifiers
		);
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
	}

	[Fact]
	public async Task CoalescedLevel2AndTraditionalTextRemainSeparateEvents() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[27;3;97~z"
		);

		TerminalInputEvent modified = await decoder.ReadAsync();
		TerminalInputEvent text = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, modified.Kind );
		Assert.Equal( TerminalKeyEventPhase.Press, modified.KeyPhase );
		Assert.Equal( TerminalInputEventKind.Text, text.Kind );
		Assert.Equal( new Rune( 'z' ), text.Character );
		Assert.Null( text.KeyPhase );
	}

	[Fact]
	public async Task XtermCsiUCompatibilityNormalizesToPressWithoutKittyOnlyMetadata() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[97;5u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'a' ), inputEvent.Character );
		Assert.Equal( TerminalKeyModifiers.Control, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
		Assert.Null( inputEvent.ShiftedCharacter );
		Assert.Null( inputEvent.BaseLayoutCharacter );
		Assert.Null( inputEvent.AssociatedText );
	}

	private static TerminalInputDecoder CreateDecoder(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		return CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.UTF8.GetBytes( value )
				]
			)
		);
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input
	) {
		ArgumentNullException.ThrowIfNull( input );
		return new TerminalInputDecoder(
			input,
			new TerminalDescriptionBuilder( "xterm-modify-other-keys" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes
		);
	}

	private sealed class ScriptedTerminalInput : ITerminalInput {
		private readonly Queue<byte[]> chunks;

		internal ScriptedTerminalInput(
			IEnumerable<byte[]> chunks
		) {
			ArgumentNullException.ThrowIfNull( chunks );
			this.chunks = new Queue<byte[]>(
				chunks.Select( static chunk => chunk.ToArray() )
			);
		}

		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( 0 == this.chunks.Count ) {
				return ValueTask.FromResult( 0 );
			}

			byte[] chunk = this.chunks.Dequeue();
			chunk.AsSpan().CopyTo( buffer.Span );
			return ValueTask.FromResult( chunk.Length );
		}
	}
}
