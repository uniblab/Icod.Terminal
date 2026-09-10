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

using Icod.Terminal;
using Xunit;

/// <summary>
/// Defines the E192 ordered application-event substrate before decoder protocol recognition is added.
/// </summary>
public sealed class TerminalSemanticEventSubstrateTests {
	[Fact]
	public void DecodeResultCanCarrySemanticApplicationEvent() {
		TerminalNotificationEvent notification = TerminalNotificationEvent.Activated( "job" );
		TerminalSemanticEvent semantic = TerminalSemanticEvent.FromNotification( notification );

		TerminalInputDecodeResult result = TerminalInputDecodeResult.FromSemantic( semantic );

		Assert.False( result.ResponseRouted );
		Assert.NotNull( result.ApplicationEvent );
		Assert.Null( result.ApplicationEvent.Value.InputEvent );
		Assert.Same( semantic, result.ApplicationEvent.Value.SemanticEvent );
		Assert.False( result.ApplicationEvent.Value.IsEndOfInput );
		Assert.Equal(
			TerminalEventKind.Semantic,
			result.ApplicationEvent.Value.ToTerminalEvent().Kind
		);
	}

	[Fact]
	public void InputDecodeResultUsesSameApplicationEventEnvelope() {
		TerminalInputEvent input = TerminalInputEvent.Text( new Rune( 'x' ) );

		TerminalInputDecodeResult result = TerminalInputDecodeResult.FromInput( input );

		Assert.False( result.ResponseRouted );
		Assert.NotNull( result.ApplicationEvent );
		Assert.Same( input, result.ApplicationEvent.Value.InputEvent );
		Assert.Null( result.ApplicationEvent.Value.SemanticEvent );
		Assert.False( result.ApplicationEvent.Value.IsEndOfInput );
		Assert.Equal(
			TerminalEventKind.Input,
			result.ApplicationEvent.Value.ToTerminalEvent().Kind
		);
	}

	[Fact]
	public void EndOfInputRemainsAnInputApplicationEvent() {
		TerminalInputEvent input = TerminalInputEvent.EndOfInput();

		TerminalInputDecodeResult result = TerminalInputDecodeResult.FromInput( input );

		Assert.NotNull( result.ApplicationEvent );
		Assert.True( result.ApplicationEvent.Value.IsEndOfInput );
		Assert.Same( input, result.ApplicationEvent.Value.InputEvent );
		Assert.Null( result.ApplicationEvent.Value.SemanticEvent );
	}
}
