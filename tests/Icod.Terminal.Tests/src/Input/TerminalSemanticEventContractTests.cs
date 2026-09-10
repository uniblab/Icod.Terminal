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
/// Freezes the E191 additive unsolicited semantic-event public contract.
/// </summary>
public sealed class TerminalSemanticEventContractTests {
	[Fact]
	public void ExistingTerminalEventKindValuesRemainStableAndSemanticIsAppended() {
		Assert.Equal( 0, (int)TerminalEventKind.Input );
		Assert.Equal( 1, (int)TerminalEventKind.Lifecycle );
		Assert.Equal( 2, (int)TerminalEventKind.Timeout );
		Assert.Equal( 3, (int)TerminalEventKind.Cancelled );
		Assert.Equal( 4, (int)TerminalEventKind.Semantic );
	}

	[Fact]
	public void NotificationActivationHasOnlyNotificationPayload() {
		TerminalNotificationEvent notification = TerminalNotificationEvent.Activated(
			"build-finished"
		);
		TerminalSemanticEvent semantic = TerminalSemanticEvent.FromNotification(
			notification
		);
		TerminalEvent terminalEvent = TerminalEvent.FromSemantic( semantic );

		Assert.Equal( TerminalEventKind.Semantic, terminalEvent.Kind );
		Assert.Null( terminalEvent.Input );
		Assert.Null( terminalEvent.Lifecycle );
		Assert.Same( semantic, terminalEvent.Semantic );
		Assert.Equal( TerminalSemanticEventKind.Notification, semantic.Kind );
		Assert.Same( notification, semantic.Notification );
		Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
		Assert.Equal( "build-finished", notification.Identifier );
		Assert.Null( notification.ButtonNumber );
	}

	[Fact]
	public void ButtonActivationCarriesOneBasedButtonNumber() {
		TerminalNotificationEvent notification = TerminalNotificationEvent.ButtonActivated(
			"question",
			2
		);

		Assert.Equal( TerminalNotificationEventKind.ButtonActivated, notification.Kind );
		Assert.Equal( "question", notification.Identifier );
		Assert.Equal( 2, notification.ButtonNumber );
	}

	[Fact]
	public void CloseTrackingUnavailableIsDistinctFromClosed() {
		TerminalNotificationEvent closed = TerminalNotificationEvent.Closed( "job" );
		TerminalNotificationEvent untracked =
			TerminalNotificationEvent.CloseTrackingUnavailable( "job" );

		Assert.Equal( TerminalNotificationEventKind.Closed, closed.Kind );
		Assert.Equal(
			TerminalNotificationEventKind.CloseTrackingUnavailable,
			untracked.Kind
		);
		Assert.Null( closed.ButtonNumber );
		Assert.Null( untracked.ButtonNumber );
	}

	[Fact]
	public void NotificationFactoriesRejectInvalidArguments() {
		Assert.Throws<ArgumentNullException>(
			static () => TerminalNotificationEvent.Activated( null! )
		);
		Assert.Throws<ArgumentException>(
			static () => TerminalNotificationEvent.Activated( string.Empty )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			static () => TerminalNotificationEvent.ButtonActivated( "job", 0 )
		);
	}
}
