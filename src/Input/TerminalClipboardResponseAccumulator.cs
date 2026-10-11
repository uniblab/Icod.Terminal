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

/// <summary>Incrementally retains only an explicitly authorized correlated clipboard frame.</summary>
internal sealed class TerminalClipboardResponseAccumulator : IDisposable {
	private readonly int maximumFrameBytes;
	private readonly TerminalControlSequenceScanner scanner;
	private byte[] buffer = [];
	private int count;

	internal TerminalClipboardResponseAccumulator( int maximumBytes ) {
		this.maximumFrameBytes = TerminalClipboardLimits.GetResponseFrameLimit( maximumBytes );
		this.scanner = new TerminalControlSequenceScanner( this.maximumFrameBytes, clipboardCapacity: true );
	}

	internal TerminalResponseFrameParseStatus Feed( byte value ) {
		TerminalResponseFrameParseStatus status = this.scanner.Feed( value );
		if ( status == TerminalResponseFrameParseStatus.Invalid ) return status;
		if ( this.count == this.buffer.Length ) {
			int capacity = Math.Min( this.maximumFrameBytes,
				this.buffer.Length == 0 ? 4096 : checked( this.buffer.Length * 2 ) );
			byte[] next = new byte[ capacity ];
			this.buffer.AsSpan( 0, this.count ).CopyTo( next );
			Array.Clear( this.buffer );
			this.buffer = next;
		}
		this.buffer[ this.count++ ] = value;
		return status;
	}

	internal TerminalResponseFrame ToFrame() {
		if ( this.scanner.Status != TerminalResponseFrameParseStatus.Complete )
			throw new InvalidOperationException( "Clipboard response is incomplete." );
		return new TerminalResponseFrame( TerminalResponseFrameKind.Osc, this.buffer.AsSpan( 0, this.count ) );
	}

	public void Dispose() {
		Array.Clear( this.buffer );
		this.buffer = [];
		this.count = 0;
	}
}
