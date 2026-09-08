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
/// Selects the requested physical cursor presentation for a terminal presentation lease.
/// </summary>
public enum TerminalCursorVisibility {
	/// <summary>Hide the physical cursor.</summary>
	Hidden,

	/// <summary>Use the terminal's normal cursor presentation.</summary>
	Normal,

	/// <summary>Use the terminal's most visible cursor presentation.</summary>
	VeryVisible
}

/// <summary>
/// Describes reversible presentation state requested from a live terminal session.
/// </summary>
public sealed class TerminalPresentationOptions {
	/// <summary>
	/// Gets or initializes whether cursor-addressing/full-screen presentation mode is required.
	/// </summary>
	public bool AlternateScreen {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes whether keypad/application transmit mode is required.
	/// </summary>
	public bool KeypadMode {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the requested physical cursor presentation, when any.
	/// </summary>
	public TerminalCursorVisibility? CursorVisibility {
		get;
		init;
	}

	internal void Validate() {
		if ( this.CursorVisibility.HasValue
			&& !Enum.IsDefined( this.CursorVisibility.Value ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.CursorVisibility ),
				this.CursorVisibility.Value,
				"The terminal cursor visibility is not recognized."
			);
		}
		if ( !this.AlternateScreen
			&& !this.KeypadMode
			&& !this.CursorVisibility.HasValue ) {
			throw new ArgumentException(
				"At least one terminal presentation state must be requested."
			);
		}
	}
}

/// <summary>
/// Owns one reversible terminal presentation-state request.
/// </summary>
/// <remarks>
/// Leases may overlap. Alternate-screen and keypad state remain active until the
/// last requesting lease is released. Cursor requests use acquisition order: the
/// most recently acquired active cursor request wins, and releasing it restores
/// the next-most-recent request or the terminal's normal cursor capability.
/// </remarks>
public sealed class TerminalPresentationLease : IAsyncDisposable {
	private readonly object sync = new();
	private readonly long leaseId;
	private TerminalPresentationManager? owner;
	private Task? disposeTask;

	internal TerminalPresentationLease(
		TerminalPresentationManager owner,
		long leaseId,
		TerminalPresentationOptions options
	) {
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( options );

		this.owner = owner;
		this.leaseId = leaseId;
		this.AlternateScreen = options.AlternateScreen;
		this.KeypadMode = options.KeypadMode;
		this.CursorVisibility = options.CursorVisibility;
	}

	/// <summary>Gets whether this lease requests alternate/full-screen mode.</summary>
	public bool AlternateScreen {
		get;
	}

	/// <summary>Gets whether this lease requests keypad/application mode.</summary>
	public bool KeypadMode {
		get;
	}

	/// <summary>Gets the cursor request owned by this lease, when any.</summary>
	public TerminalCursorVisibility? CursorVisibility {
		get;
	}

	/// <summary>
	/// Releases this presentation request and applies any remaining outer requests.
	/// </summary>
	/// <returns>A value task representing asynchronous release.</returns>
	public ValueTask DisposeAsync() {
		lock ( this.sync ) {
			if ( this.owner is null ) {
				return ValueTask.CompletedTask;
			}

			this.disposeTask ??= this.DisposeCoreAsync();
			return new ValueTask( this.disposeTask );
		}
	}

	internal void MarkReleasedByOwner() {
		lock ( this.sync ) {
			this.owner = null;
			this.disposeTask = null;
		}
	}

	private async Task DisposeCoreAsync() {
		TerminalPresentationManager? currentOwner;
		lock ( this.sync ) {
			currentOwner = this.owner;
		}
		if ( currentOwner is null ) {
			return;
		}

		bool released = false;
		try {
			using IDisposable composition = await TerminalStateComposition.AcquireAsync(
				currentOwner,
				CancellationToken.None
			).ConfigureAwait( false );
			await currentOwner.ReleaseAsync( this.leaseId ).ConfigureAwait( false );
			released = true;
		} finally {
			lock ( this.sync ) {
				if ( released ) {
					this.owner = null;
				}
				this.disposeTask = null;
			}
		}
	}
}
