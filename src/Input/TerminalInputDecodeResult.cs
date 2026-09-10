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
/// Represents one unit of progress through the shared terminal input decoder.
/// </summary>
internal readonly struct TerminalInputDecodeResult {
	private readonly TerminalResponseExpectation? responseExpectation;
	private readonly TerminalResponseFrame? responseFrame;
	private readonly TerminalQueryResponseDisposition responseDisposition;
	private readonly Exception? responseException;

	private TerminalInputDecodeResult(
		TerminalApplicationEvent? applicationEvent,
		TerminalResponseExpectation? responseExpectation,
		TerminalResponseFrame? responseFrame,
		TerminalQueryResponseDisposition responseDisposition,
		Exception? responseException
	) {
		bool responseRouted = responseExpectation is not null;
		if ( !Enum.IsDefined( responseDisposition ) ) {
			throw new ArgumentOutOfRangeException( nameof( responseDisposition ) );
		}
		if ( responseRouted == applicationEvent.HasValue
			|| !responseRouted && ( responseFrame is not null || responseException is not null )
			|| responseRouted && ( responseFrame is null ) == ( responseException is null ) ) {
			throw new ArgumentException(
				"A terminal decode result must represent either one application event or one routed response."
			);
		}

		this.ApplicationEvent = applicationEvent;
		this.responseExpectation = responseExpectation;
		this.responseFrame = responseFrame;
		this.responseDisposition = responseDisposition;
		this.responseException = responseException;
		this.RoutingRestartRequired = false;
	}

	private TerminalInputDecodeResult(
		bool routingRestartRequired
	) {
		if ( !routingRestartRequired ) {
			throw new ArgumentException(
				"The routing-restart decode result must request a routing restart.",
				nameof( routingRestartRequired )
			);
		}

		this.ApplicationEvent = null;
		this.responseExpectation = null;
		this.responseFrame = null;
		this.responseDisposition = TerminalQueryResponseDisposition.Completion;
		this.responseException = null;
		this.RoutingRestartRequired = true;
	}

	internal TerminalApplicationEvent? ApplicationEvent {
		get;
	}

	internal TerminalInputEvent? InputEvent {
		get {
			return this.ApplicationEvent?.InputEvent;
		}
	}

	internal bool ResponseRouted {
		get {
			return this.responseExpectation is not null;
		}
	}

	internal bool RoutingRestartRequired {
		get;
	}

	internal void CompleteRoutedResponse() {
		if ( this.RoutingRestartRequired ) {
			return;
		}
		if ( this.responseExpectation is null ) {
			throw new InvalidOperationException(
				"The terminal decode result does not contain a routed response."
			);
		}

		if ( this.responseFrame is not null ) {
			this.responseExpectation.TrySetResult(
				this.responseFrame,
				this.responseDisposition
			);
			return;
		}
		if ( this.responseException is not null ) {
			this.responseExpectation.TrySetException( this.responseException );
			return;
		}

		throw new InvalidOperationException(
			"The routed terminal response contains neither a frame nor a failure."
		);
	}

	internal static TerminalInputDecodeResult FromInput(
		TerminalInputEvent inputEvent
	) {
		ArgumentNullException.ThrowIfNull( inputEvent );
		return new TerminalInputDecodeResult(
			TerminalApplicationEvent.FromInput( inputEvent ),
			responseExpectation: null,
			responseFrame: null,
			TerminalQueryResponseDisposition.Completion,
			responseException: null
		);
	}

	internal static TerminalInputDecodeResult FromSemantic(
		TerminalSemanticEvent semanticEvent
	) {
		ArgumentNullException.ThrowIfNull( semanticEvent );
		return new TerminalInputDecodeResult(
			TerminalApplicationEvent.FromSemantic( semanticEvent ),
			responseExpectation: null,
			responseFrame: null,
			TerminalQueryResponseDisposition.Completion,
			responseException: null
		);
	}

	internal static TerminalInputDecodeResult RestartRouting() {
		return new TerminalInputDecodeResult( routingRestartRequired: true );
	}

	internal static TerminalInputDecodeResult RoutedResponse(
		TerminalResponseExpectation expectation,
		TerminalResponseFrame frame,
		TerminalQueryResponseDisposition disposition = TerminalQueryResponseDisposition.Completion
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		ArgumentNullException.ThrowIfNull( frame );
		if ( !Enum.IsDefined( disposition ) ) {
			throw new ArgumentOutOfRangeException( nameof( disposition ) );
		}
		return new TerminalInputDecodeResult(
			applicationEvent: null,
			expectation,
			frame,
			disposition,
			responseException: null
		);
	}

	internal static TerminalInputDecodeResult RoutedFailure(
		TerminalResponseExpectation expectation,
		Exception exception
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		ArgumentNullException.ThrowIfNull( exception );
		return new TerminalInputDecodeResult(
			applicationEvent: null,
			expectation,
			responseFrame: null,
			TerminalQueryResponseDisposition.Completion,
			exception
		);
	}
}
