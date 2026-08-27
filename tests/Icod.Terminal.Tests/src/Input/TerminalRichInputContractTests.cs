namespace Icod.Terminal.Tests.Input;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T14 rich-input event and decoder-policy contract.
/// </summary>
public sealed class TerminalRichInputContractTests {
	[Fact]
	public void MouseEnvelopeCarriesNormalizedTypedPayload() {
		TerminalMouseEvent mouse = new(
			TerminalMouseAction.Move,
			TerminalMouseButton.Primary,
			0,
			7,
			TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control
		);

		TerminalInputEvent inputEvent = TerminalInputEvent.FromMouse( mouse );

		Assert.Equal( TerminalInputEventKind.Mouse, inputEvent.Kind );
		Assert.Same( mouse, inputEvent.Mouse );
		Assert.Null( inputEvent.Focus );
		Assert.Null( inputEvent.Paste );
		Assert.Equal( TerminalKey.None, inputEvent.Key );
		Assert.Null( inputEvent.Character );
		Assert.Equal( TerminalKeyModifiers.None, inputEvent.Modifiers );
		Assert.Null( inputEvent.FunctionKeyNumber );
		Assert.Equal( 0, mouse.Column );
		Assert.Equal( 7, mouse.Row );
	}

	[Fact]
	public void MouseContractRejectsInvalidSemanticStates() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalMouseEvent(
				TerminalMouseAction.Move,
				TerminalMouseButton.None,
				-1,
				0
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalMouseEvent(
				TerminalMouseAction.Press,
				TerminalMouseButton.None,
				0,
				0
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalMouseEvent(
				TerminalMouseAction.WheelUp,
				TerminalMouseButton.Primary,
				0,
				0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalMouseEvent(
				TerminalMouseAction.Move,
				TerminalMouseButton.None,
				0,
				0,
				(TerminalKeyModifiers)8
			)
		);
	}

	[Fact]
	public void FocusEnvelopeCarriesTypedPayload() {
		TerminalFocusEvent focus = new( TerminalFocusState.Focused );

		TerminalInputEvent inputEvent = TerminalInputEvent.FromFocus( focus );

		Assert.Equal( TerminalInputEventKind.Focus, inputEvent.Kind );
		Assert.Same( focus, inputEvent.Focus );
		Assert.Null( inputEvent.Mouse );
		Assert.Null( inputEvent.Paste );
		Assert.Equal( TerminalFocusState.Focused, focus.State );
	}

	[Fact]
	public void PasteEnvelopeUsesFramedBoundedTextPayloadContract() {
		TerminalPasteEvent begin = new( TerminalPastePhase.Begin );
		TerminalPasteEvent data = new(
			TerminalPastePhase.Data,
			"alpha\u001b[31mbeta"
		);
		TerminalPasteEvent end = new( TerminalPastePhase.End );

		TerminalInputEvent inputEvent = TerminalInputEvent.FromPaste( data );

		Assert.Equal( TerminalInputEventKind.Paste, inputEvent.Kind );
		Assert.Same( data, inputEvent.Paste );
		Assert.Equal( TerminalPastePhase.Begin, begin.Phase );
		Assert.Null( begin.Text );
		Assert.Equal( "alpha\u001b[31mbeta", data.Text );
		Assert.Equal( TerminalPastePhase.End, end.Phase );
		Assert.Null( end.Text );
	}

	[Fact]
	public void PasteContractRejectsTextOnFramingEventsAndEmptyData() {
		Assert.Throws<ArgumentException>(
			() => new TerminalPasteEvent(
				TerminalPastePhase.Begin,
				"unexpected"
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalPasteEvent(
				TerminalPastePhase.Data,
				string.Empty
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalPasteEvent(
				TerminalPastePhase.Data
			)
		);
	}

	[Fact]
	public void DecoderPolicyDefaultsPreserve01Behavior() {
		TerminalInputDecoderOptions options = new();

		Assert.Equal(
			TerminalSession.DefaultEscapeSequenceTimeout,
			options.EscapeSequenceTimeout
		);
		Assert.Equal(
			TerminalSession.MaximumBufferedInputBytes,
			options.MaximumBufferedBytes
		);
		Assert.Equal(
			TerminalSession.MaximumBufferedInputBytes,
			options.PasteChunkBytes
		);
		options.Validate();
	}

	[Fact]
	public void DecoderPolicyRejectsUnboundedOrInvalidValues() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalInputDecoderOptions {
				EscapeSequenceTimeout = TimeSpan.FromMilliseconds( -2 )
			}.Validate()
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalInputDecoderOptions {
				MaximumBufferedBytes = 3
			}.Validate()
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalInputDecoderOptions {
				MaximumBufferedBytes = 4097
			}.Validate()
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalInputDecoderOptions {
				PasteChunkBytes = 0
			}.Validate()
		);
	}

	[Fact]
	public void SessionOptionsRequireDecoderPolicy() {
		TerminalSessionOptions options = new() {
			InputDecoderOptions = null!
		};

		Assert.Throws<ArgumentNullException>(
			() => options.Validate()
		);
	}
}
