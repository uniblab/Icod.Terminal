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
/// Identifies one event returned by the session's unified interactive reader.
/// </summary>
public enum TerminalEventKind {
	/// <summary>A decoded terminal-input event is available.</summary>
	Input,

	/// <summary>A terminal/process lifecycle event is available.</summary>
	Lifecycle,

	/// <summary>The requested wait interval or deadline expired.</summary>
	Timeout,

	/// <summary>The caller canceled this wait.</summary>
	Cancelled,

	/// <summary>An unsolicited semantic terminal event is available.</summary>
	Semantic
}

/// <summary>
/// Represents one decoded input, lifecycle, semantic, timeout, or caller-cancellation event.
/// </summary>
public sealed class TerminalEvent {
	private TerminalEvent(
		TerminalEventKind kind,
		TerminalInputEvent? input,
		TerminalLifecycleEvent? lifecycle,
		TerminalSemanticEvent? semantic
	) {
		this.Kind = kind;
		this.Input = input;
		this.Lifecycle = lifecycle;
		this.Semantic = semantic;
	}

	/// <summary>Gets the high-level event kind.</summary>
	public TerminalEventKind Kind {
		get;
	}

	/// <summary>
	/// Gets the decoded input event when <see cref="Kind"/> is
	/// <see cref="TerminalEventKind.Input"/>.
	/// </summary>
	public TerminalInputEvent? Input {
		get;
	}

	/// <summary>
	/// Gets the lifecycle event when <see cref="Kind"/> is
	/// <see cref="TerminalEventKind.Lifecycle"/>.
	/// </summary>
	public TerminalLifecycleEvent? Lifecycle {
		get;
	}

	/// <summary>
	/// Gets the semantic terminal event when <see cref="Kind"/> is
	/// <see cref="TerminalEventKind.Semantic"/>.
	/// </summary>
	public TerminalSemanticEvent? Semantic {
		get;
	}

	internal static TerminalEvent FromInput(
		TerminalInputEvent input
	) {
		ArgumentNullException.ThrowIfNull( input );

		return new TerminalEvent(
			TerminalEventKind.Input,
			input,
			null,
			null
		);
	}

	internal static TerminalEvent FromLifecycle(
		TerminalLifecycleEvent lifecycle
	) {
		ArgumentNullException.ThrowIfNull( lifecycle );

		return new TerminalEvent(
			TerminalEventKind.Lifecycle,
			null,
			lifecycle,
			null
		);
	}

	internal static TerminalEvent FromSemantic(
		TerminalSemanticEvent semantic
	) {
		ArgumentNullException.ThrowIfNull( semantic );

		return new TerminalEvent(
			TerminalEventKind.Semantic,
			null,
			null,
			semantic
		);
	}

	internal static TerminalEvent TimedOut() {
		return new TerminalEvent(
			TerminalEventKind.Timeout,
			null,
			null,
			null
		);
	}

	internal static TerminalEvent Cancelled() {
		return new TerminalEvent(
			TerminalEventKind.Cancelled,
			null,
			null,
			null
		);
	}
}
