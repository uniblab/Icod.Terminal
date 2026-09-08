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
/// Owns one session-managed OSC 8 hyperlink state.
/// </summary>
/// <remarks>
/// Hyperlink leases are strictly nested. The most recently acquired active lease
/// must be released first. Releasing an inner lease restores the immediately
/// preceding session-owned hyperlink; releasing the outermost lease emits the
/// canonical OSC 8 close frame.
/// </remarks>
public sealed class TerminalHyperlinkLease : IAsyncDisposable {
	private readonly object sync = new();
	private readonly long leaseId;
	private TerminalHyperlinkManager? owner;
	private Task? disposeTask;

	internal TerminalHyperlinkLease(
		TerminalHyperlinkManager owner,
		long leaseId,
		string uri,
		string? identifier
	) {
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentException.ThrowIfNullOrWhiteSpace( uri );

		this.owner = owner;
		this.leaseId = leaseId;
		this.Uri = uri;
		this.Identifier = identifier;
	}

	/// <summary>Gets the canonical URI text emitted for this hyperlink.</summary>
	public string Uri {
		get;
	}

	/// <summary>Gets the optional OSC 8 hyperlink identifier.</summary>
	public string? Identifier {
		get;
	}

	/// <summary>
	/// Releases this hyperlink state and restores the immediately preceding
	/// session-owned hyperlink, or closes OSC 8 state when this is the outermost lease.
	/// </summary>
	/// <returns>A value task representing asynchronous release.</returns>
	public async ValueTask DisposeAsync() {
		Task? task;
		lock ( this.sync ) {
			if ( this.owner is null ) {
				return;
			}

			this.disposeTask ??= this.DisposeCoreAsync();
			task = this.disposeTask;
		}

		try {
			await task.ConfigureAwait( false );
		} finally {
			lock ( this.sync ) {
				if ( ReferenceEquals( this.disposeTask, task ) ) {
					this.disposeTask = null;
				}
			}
		}
	}

	internal void MarkReleasedByOwner() {
		lock ( this.sync ) {
			this.owner = null;
			this.disposeTask = null;
		}
	}

	private async Task DisposeCoreAsync() {
		TerminalHyperlinkManager? currentOwner;
		lock ( this.sync ) {
			currentOwner = this.owner;
		}
		if ( currentOwner is null ) {
			return;
		}

		await currentOwner.ReleaseAsync( this.leaseId ).ConfigureAwait( false );
		lock ( this.sync ) {
			this.owner = null;
		}
	}
}
