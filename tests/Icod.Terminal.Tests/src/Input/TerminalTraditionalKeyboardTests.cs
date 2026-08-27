namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T18 traditional modified-key decoding without touching the process terminal.
/// </summary>
public sealed class TerminalTraditionalKeyboardTests {
	[Fact]
	public async Task XtermProfileDecodesShiftedNavigationAndEditingKeys() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes(
						"\u001b[1;2A"
							+ "\u001b[1;2B"
							+ "\u001b[1;2D"
							+ "\u001b[1;2C"
							+ "\u001b[1;2H"
							+ "\u001b[1;2F"
							+ "\u001b[5;2~"
							+ "\u001b[6;2~"
							+ "\u001b[2;2~"
							+ "\u001b[3;2~"
					)
				]
			),
			TerminalProfiles.Xterm
		);

		await AssertKeyAsync(
			decoder,
			TerminalKey.Up,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Down,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Left,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Right,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Home,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.End,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.PageUp,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.PageDown,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Insert,
			TerminalKeyModifiers.Shift
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Delete,
			TerminalKeyModifiers.Shift
		);
	}

	[Fact]
	public async Task XtermProfileDecodesCombinedNavigationModifiers() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes(
						"\u001b[1;3A"
							+ "\u001b[1;4B"
							+ "\u001b[1;5D"
							+ "\u001b[1;6C"
							+ "\u001b[1;7H"
					)
				]
			),
			TerminalProfiles.Xterm
		);

		await AssertKeyAsync(
			decoder,
			TerminalKey.Up,
			TerminalKeyModifiers.Alt
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Down,
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Alt
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Left,
			TerminalKeyModifiers.Control
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Right,
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control
		);
		await AssertKeyAsync(
			decoder,
			TerminalKey.Home,
			TerminalKeyModifiers.Alt | TerminalKeyModifiers.Control
		);
	}

	[Fact]
	public async Task ExtendedModifierEightRepresentsAllThreeModifiers() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "modifier-eight" )
			.SetExtendedString(
				"kEND8",
				"\u001b[1;8F"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[1;8F" )
				]
			),
			terminal
		);

		await AssertKeyAsync(
			decoder,
			TerminalKey.End,
			TerminalKeyModifiers.Shift
				| TerminalKeyModifiers.Alt
				| TerminalKeyModifiers.Control
		);
	}

	[Fact]
	public async Task XtermShiftedFunctionBankNormalizesToBaseFunctionKeys() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes(
						"\u001b[1;2P\u001b[15;2~\u001b[24;2~"
					)
				]
			),
			TerminalProfiles.Xterm
		);

		await AssertFunctionKeyAsync(
			decoder,
			1,
			TerminalKeyModifiers.Shift
		);
		await AssertFunctionKeyAsync(
			decoder,
			5,
			TerminalKeyModifiers.Shift
		);
		await AssertFunctionKeyAsync(
			decoder,
			12,
			TerminalKeyModifiers.Shift
		);
	}

	[Fact]
	public async Task WindowsTerminalFunctionBanksNormalizeTraditionalModifiers() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes(
						"\u001b[1;5P"
							+ "\u001b[1;6Q"
							+ "\u001b[1;3R"
							+ "\u001b[1;4P"
					)
				]
			),
			TerminalProfiles.MsTerminal
		);

		await AssertFunctionKeyAsync(
			decoder,
			1,
			TerminalKeyModifiers.Control
		);
		await AssertFunctionKeyAsync(
			decoder,
			2,
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control
		);
		await AssertFunctionKeyAsync(
			decoder,
			3,
			TerminalKeyModifiers.Alt
		);
		await AssertFunctionKeyAsync(
			decoder,
			1,
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Alt
		);
	}

	[Fact]
	public async Task NonModifierFunctionCapabilityRetainsAdvertisedFunctionNumber() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "physical-f13" )
			.SetString(
				StringCapability.KeyF13,
				"\u001b[99~"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[99~" )
				]
			),
			terminal
		);

		await AssertFunctionKeyAsync(
			decoder,
			13,
			TerminalKeyModifiers.None
		);
	}

	[Fact]
	public async Task FunctionKeySixtyThreeRemainsSupported() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "f63" )
			.SetString(
				StringCapability.KeyF63,
				"\u001b[63~"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[63~" )
				]
			),
			terminal
		);

		await AssertFunctionKeyAsync(
			decoder,
			63,
			TerminalKeyModifiers.None
		);
	}

	[Fact]
	public async Task ModifiedSequenceCanArriveOneByteAtATime() {
		byte[] bytes = Encoding.Latin1.GetBytes( "\u001b[1;5C" );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				bytes.Select(
					static value => new[] { value }
				)
			),
			TerminalProfiles.Xterm
		);

		await AssertKeyAsync(
			decoder,
			TerminalKey.Right,
			TerminalKeyModifiers.Control
		);
	}

	[Fact]
	public async Task LinuxConsoleStyleFunctionSequenceRemainsCapabilityDriven() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "linux-console-fixture" )
			.SetString(
				StringCapability.KeyF1,
				"\u001b[[A"
			)
			.Build();
		byte[] bytes = Encoding.Latin1.GetBytes( "\u001b[[A" );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				bytes.Select(
					static value => new[] { value }
				)
			),
			terminal
		);

		await AssertFunctionKeyAsync(
			decoder,
			1,
			TerminalKeyModifiers.None
		);
	}

	[Fact]
	public async Task UnknownCsiIsNotConsumedAsModifiedKeyboardInput() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[999;9Z" )
				]
			),
			TerminalProfiles.Xterm
		);

		TerminalInputEvent escape = await decoder.ReadAsync();
		TerminalInputEvent bracket = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, escape.Kind );
		Assert.Equal( TerminalKey.Escape, escape.Key );
		Assert.Equal( TerminalInputEventKind.Text, bracket.Kind );
		Assert.Equal( new Rune( '[' ), bracket.Character );
	}

	private static async Task AssertKeyAsync(
		TerminalInputDecoder decoder,
		TerminalKey expectedKey,
		TerminalKeyModifiers expectedModifiers
	) {
		ArgumentNullException.ThrowIfNull( decoder );

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( expectedKey, inputEvent.Key );
		Assert.Equal( expectedModifiers, inputEvent.Modifiers );
		Assert.Null( inputEvent.FunctionKeyNumber );
	}

	private static async Task AssertFunctionKeyAsync(
		TerminalInputDecoder decoder,
		int expectedNumber,
		TerminalKeyModifiers expectedModifiers
	) {
		ArgumentNullException.ThrowIfNull( decoder );

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Function, inputEvent.Key );
		Assert.Equal( expectedNumber, inputEvent.FunctionKeyNumber );
		Assert.Equal( expectedModifiers, inputEvent.Modifiers );
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( terminal );

		return new TerminalInputDecoder(
			input,
			terminal,
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
				chunks.Select(
					static value => value.ToArray()
				)
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
					"The scripted input chunk exceeds the decoder read buffer."
				);
			}

			chunk.AsSpan().CopyTo( buffer.Span );
			return ValueTask.FromResult( chunk.Length );
		}
	}
}
