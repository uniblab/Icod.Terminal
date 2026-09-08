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
/// Verifies T172 incremental Kitty CSI-u decoding without negotiating terminal state.
/// </summary>
public sealed class TerminalKittyKeyboardDecoderTests {
	[Fact]
	public async Task CharacterPressDecodesFromCanonicalCsiU() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[97u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'a' ), inputEvent.Character );
		Assert.Equal( TerminalKeyModifiers.None, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
	}

	[Fact]
	public async Task KittyModifierBitsMapToSemanticModifierValues() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[97;256:2u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		TerminalKeyModifiers allModifiers =
			TerminalKeyModifiers.Shift
			| TerminalKeyModifiers.Control
			| TerminalKeyModifiers.Alt
			| TerminalKeyModifiers.Super
			| TerminalKeyModifiers.Hyper
			| TerminalKeyModifiers.Meta
			| TerminalKeyModifiers.CapsLock
			| TerminalKeyModifiers.NumLock;
		Assert.Equal( allModifiers, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Repeat, inputEvent.KeyPhase );
	}

	[Fact]
	public async Task AlternateKeysAndAssociatedTextArePreserved() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[97:65:113;1:3;197:128512u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( new Rune( 'a' ), inputEvent.Character );
		Assert.Equal( new Rune( 'A' ), inputEvent.ShiftedCharacter );
		Assert.Equal( new Rune( 'q' ), inputEvent.BaseLayoutCharacter );
		Assert.Equal( "Å😀", inputEvent.AssociatedText );
		Assert.Equal( TerminalKeyEventPhase.Release, inputEvent.KeyPhase );
	}

	[Fact]
	public async Task PureAssociatedTextProducesOrdinaryTextEvents() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[0;1;97:128512u"
		);

		TerminalInputEvent first = await decoder.ReadAsync();
		TerminalInputEvent second = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Text, first.Kind );
		Assert.Equal( new Rune( 'a' ), first.Character );
		Assert.Null( first.KeyPhase );
		Assert.Equal( TerminalInputEventKind.Text, second.Kind );
		Assert.Equal( new Rune( 0x1f600 ), second.Character );
		Assert.Null( second.KeyPhase );
	}

	[Theory]
	[InlineData( 57358, TerminalKey.CapsLock )]
	[InlineData( 57399, TerminalKey.Keypad0 )]
	[InlineData( 57414, TerminalKey.KeypadEnter )]
	[InlineData( 57430, TerminalKey.MediaPlayPause )]
	[InlineData( 57440, TerminalKey.VolumeMute )]
	[InlineData( 57441, TerminalKey.LeftShift )]
	[InlineData( 57452, TerminalKey.RightMeta )]
	[InlineData( 57454, TerminalKey.IsoLevel5Shift )]
	public async Task CurrentKittyFunctionalKeysMapSemantically(
		int keyCode,
		TerminalKey expected
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			$"\u001b[{keyCode};1:3u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( expected, inputEvent.Key );
		Assert.Equal( TerminalKeyEventPhase.Release, inputEvent.KeyPhase );
		Assert.Null( inputEvent.Character );
	}

	[Theory]
	[InlineData( 57376, 13 )]
	[InlineData( 57398, 35 )]
	public async Task KittyHighFunctionKeysRetainFunctionNumber(
		int keyCode,
		int expectedFunction
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			$"\u001b[{keyCode};1u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Function, inputEvent.Key );
		Assert.Equal( expectedFunction, inputEvent.FunctionKeyNumber );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
	}

	[Fact]
	public async Task UnknownKittyPrivateUseCodeUsesUnrecognizedBucket() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[57455;17:3u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Unrecognized, inputEvent.Key );
		Assert.Null( inputEvent.Character );
		Assert.Equal( TerminalKeyModifiers.Hyper, inputEvent.Modifiers );
		Assert.Equal( TerminalKeyEventPhase.Release, inputEvent.KeyPhase );
	}

	[Theory]
	[InlineData( 13, TerminalKey.Enter )]
	[InlineData( 27, TerminalKey.Escape )]
	[InlineData( 9, TerminalKey.Tab )]
	[InlineData( 32, TerminalKey.Space )]
	[InlineData( 127, TerminalKey.Backspace )]
	public async Task CanonicalUnicodeControlKeyIdentitiesMapToNamedKeys(
		int keyCode,
		TerminalKey expected
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			$"\u001b[{keyCode};1:2u"
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( expected, inputEvent.Key );
		Assert.Equal( TerminalKeyEventPhase.Repeat, inputEvent.KeyPhase );
		Assert.Null( inputEvent.Character );
	}

	[Fact]
	public async Task FragmentedKittyFrameDecodesAsOneEvent() {
		ScriptedTerminalInput input = new(
			[
				Encoding.ASCII.GetBytes( "\u001b[97:" ),
				Encoding.ASCII.GetBytes( "65:113;" ),
				Encoding.ASCII.GetBytes( "6:2;" ),
				Encoding.ASCII.GetBytes( "120u" )
			]
		);
		TerminalInputDecoder decoder = CreateDecoder( input );

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Character, inputEvent.Key );
		Assert.Equal( new Rune( 'a' ), inputEvent.Character );
		Assert.Equal( new Rune( 'A' ), inputEvent.ShiftedCharacter );
		Assert.Equal( new Rune( 'q' ), inputEvent.BaseLayoutCharacter );
		Assert.Equal(
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control,
			inputEvent.Modifiers
		);
		Assert.Equal( TerminalKeyEventPhase.Repeat, inputEvent.KeyPhase );
		Assert.Equal( "x", inputEvent.AssociatedText );
	}

	[Fact]
	public async Task CoalescedKittyAndTraditionalTextRemainSeparate() {
		TerminalInputDecoder decoder = CreateDecoder(
			"\u001b[97;1ux"
		);

		TerminalInputEvent key = await decoder.ReadAsync();
		TerminalInputEvent text = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, key.Kind );
		Assert.Equal( new Rune( 'a' ), key.Character );
		Assert.Equal( TerminalInputEventKind.Text, text.Kind );
		Assert.Equal( new Rune( 'x' ), text.Character );
	}

	[Theory]
	[InlineData( "\u001b[97;1:9uZ" )]
	[InlineData( "\u001b[97;0uZ" )]
	[InlineData( "\u001b[97;1;9uZ" )]
	[InlineData( "\u001b[1114112;1uZ" )]
	[InlineData( "\u001b[97;1;10uZ" )]
	public async Task MalformedKittyFrameIsConsumedAndNextInputRecovers(
		string inputText
	) {
		TerminalInputDecoder decoder = CreateDecoder( inputText );

		TerminalInputEvent recovered = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Text, recovered.Kind );
		Assert.Equal( new Rune( 'Z' ), recovered.Character );
	}

	[Fact]
	public async Task ExistingTraditionalArrowSequenceStillWinsOutsideCsiU() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "traditional-arrow" )
			.SetString( StringCapability.KeyCursorUp, "\u001b[A" )
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.ASCII.GetBytes( "\u001b[A" )
				]
			),
			terminal
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Up, inputEvent.Key );
		Assert.Equal( TerminalKeyEventPhase.Press, inputEvent.KeyPhase );
	}

	private static TerminalInputDecoder CreateDecoder(
		string input
	) {
		ArgumentNullException.ThrowIfNull( input );
		return CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.ASCII.GetBytes( input )
				]
			)
		);
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( input );
		return new TerminalInputDecoder(
			input,
			terminal ?? new TerminalDescriptionBuilder( "kitty" ).Build(),
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
				chunks.Select( static value => value.ToArray() )
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
			if ( chunk.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted chunk exceeds the decoder read buffer."
				);
			}
			chunk.AsSpan().CopyTo( buffer.Span );
			return ValueTask.FromResult( chunk.Length );
		}
	}
}
