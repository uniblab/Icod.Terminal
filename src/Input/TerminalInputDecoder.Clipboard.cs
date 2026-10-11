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

internal sealed partial class TerminalInputDecoder {
	private async ValueTask<TerminalInputDecodeResult> ReadLargeClipboardResponseAsync(
		TerminalResponseExpectation expectation, CancellationToken cancellationToken
	) {
		using TerminalClipboardResponseAccumulator accumulator = new(
			expectation.ResponsePlan.MaximumClipboardBytes );
		int frameLimit = TerminalClipboardLimits.GetResponseFrameLimit(
			expectation.ResponsePlan.MaximumClipboardBytes );
		byte previous = 0;
		try {
			while ( true ) {
				int consumed = 0;
				TerminalResponseFrameParseStatus status = TerminalResponseFrameParseStatus.Incomplete;
				byte last = 0;
				bool terminated = false;
				while ( consumed < this.bufferedBytes.Count ) {
					last = this.bufferedBytes[ consumed++ ];
					terminated = last == 0x07 || last == 0x9C || ( previous == 0x1B && last == (byte)'\\' );
					previous = last;
					status = accumulator.Feed( last );
					if ( status != TerminalResponseFrameParseStatus.Incomplete ) break;
				}
				this.Consume( consumed );
				if ( status == TerminalResponseFrameParseStatus.Complete ) {
					TerminalResponseFrame frame = accumulator.ToFrame();
					this.DetachClipboardExpectation( expectation );
					if ( !expectation.ResponsePlan.TryMatch( frame, out var disposition ) )
						return TerminalInputDecodeResult.RoutedFailure( expectation,
							new FormatException( "The correlated clipboard response is malformed." ) );
					return TerminalInputDecodeResult.RoutedResponse( expectation, frame, disposition );
				}
				if ( status != TerminalResponseFrameParseStatus.Incomplete ) {
					accumulator.Dispose();
					if ( !terminated ) {
						await this.DrainLargeClipboardResponseAsync(
							expectation, frameLimit, last, cancellationToken ).ConfigureAwait( false );
					}
					this.DetachClipboardExpectation( expectation );
					return TerminalInputDecodeResult.RoutedFailure( expectation,
						new FormatException( "The correlated clipboard response is malformed or exceeds its frame budget." ) );
				}
				if ( !await this.ReadClipboardContinuationAsync( expectation, cancellationToken ).ConfigureAwait( false ) ) {
					this.DetachClipboardExpectation( expectation );
					return TerminalInputDecodeResult.RoutedFailure( expectation,
						new FormatException( "Terminal input ended within a clipboard response." ) );
				}
			}
		} catch ( Exception exception ) {
			// An unresolved correlated string must never resume as ordinary keyboard input.
			expectation.TrySetException( exception );
			lock ( this.responseExpectationGate ) {
				this.responseExpectation?.TrySetException( exception );
				this.responseExpectation = null;
			}
			throw;
		}
	}

	private void DetachClipboardExpectation( TerminalResponseExpectation expectation ) {
		lock ( this.responseExpectationGate ) {
			if ( ReferenceEquals( this.responseExpectation, expectation ) ) this.responseExpectation = null;
		}
	}

	private async ValueTask<bool> ReadClipboardContinuationAsync(
		TerminalResponseExpectation expectation, CancellationToken cancellationToken
	) {
		if ( expectation.OwnershipEnded.IsCompleted )
			throw new InvalidOperationException( "Clipboard response ownership expired before its terminator." );
		if ( this.endOfInput ) return false;
		// The pending transport read remains owned by the session, not the query caller.
		Task<int> read = this.EnsurePendingRead( cancellationToken );
		Task ready = await Task.WhenAny( read, expectation.OwnershipEnded ).ConfigureAwait( false );
		if ( ready == expectation.OwnershipEnded )
			throw new InvalidOperationException( "Clipboard response ownership expired before its terminator." );
		return 0 < await this.CompletePendingReadAsync( read ).ConfigureAwait( false );
	}

	private async ValueTask DrainLargeClipboardResponseAsync(
		TerminalResponseExpectation expectation, int maximumDiscardBytes, byte previous,
		CancellationToken cancellationToken
	) {
		int discarded = 0;
		while ( true ) {
			int consumed = 0;
			bool terminated = false;
			while ( consumed < this.bufferedBytes.Count ) {
				byte value = this.bufferedBytes[ consumed++ ];
				if ( ++discarded > maximumDiscardBytes )
					throw new InvalidOperationException( "Clipboard response exceeded its bounded recovery allowance." );
				terminated = value == 0x07 || value == 0x9C || ( previous == 0x1B && value == (byte)'\\' );
				previous = value;
				if ( terminated ) break;
			}
			if ( consumed > 0 ) this.Consume( consumed );
			if ( terminated ) return;
			if ( !await this.ReadClipboardContinuationAsync( expectation, cancellationToken ).ConfigureAwait( false ) ) return;
		}
	}
}
