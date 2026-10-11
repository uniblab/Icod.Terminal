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
/// Provides explicit OSC 52 clipboard and selection reads for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Explicitly requests bounded binary content from one terminal-managed clipboard or selection target.
	/// </summary>
	/// <param name="selection">The terminal-managed selection to query.</param>
	/// <param name="timeout">The caller-visible operation timeout, measured from transaction queueing.</param>
	/// <param name="cancellationToken">Cancellation for the caller's wait.</param>
	/// <returns>The exact decoded selection bytes returned by the terminal.</returns>
	/// <remarks>
	/// <para>
	/// Calling this method is the explicit privacy opt-in for one OSC 52 clipboard
	/// read. A session never performs this query automatically during open, capability
	/// discovery, lifecycle handling, or disposal.
	/// </para>
	/// <para>
	/// The request is serialized through the session query transaction manager and
	/// flushed as part of query emission. The caller-visible timeout begins when the
	/// transaction is queued, so queueing and waiting for the control-output gate count
	/// against the requested deadline. If the deadline expires before emission commits,
	/// no request bytes are emitted.
	/// </para>
	/// <para>
	/// After an emitted request times out or the caller cancels, the transaction retains
	/// bounded late-response ownership so a delayed reply cannot satisfy a later query or
	/// leak into ordinary input. Larger correlated replies use a separate bounded
	/// accumulator. Malformed or oversized replies are drained through their terminator
	/// before reporting <see cref="FormatException"/>. If bounded recovery or late
	/// ownership expires before termination, the input coordinator fails closed.
	/// A caller deadline may expire first. No partial payload is returned.
	/// </para>
	/// <para>
	/// The decoded-byte limit is <see cref="TerminalClipboardOptions.MaximumReadBytes"/>.
	/// Ordinary decoder settings still govern prefix recognition; a seven-byte buffer
	/// accommodates both supported OSC 52 introducers. Budgets at or below 64 KiB retain
	/// the existing ordinary-buffer framing behavior.
	/// </para>
	/// <para>
	/// A successful result means a syntactically correlated OSC 52 response was
	/// received and decoded. Timeout alone does not prove that OSC 52 reads are unsupported;
	/// a terminal may disable or ignore clipboard queries by policy.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The selection or timeout is outside the supported contract.</exception>
	/// <exception cref="InvalidOperationException">The session endpoints cannot support an active terminal query.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels the query.</exception>
	/// <exception cref="TimeoutException">The caller-visible operation deadline expires.</exception>
	/// <exception cref="FormatException">The terminal returns a correlated but malformed or oversized OSC 52 response.</exception>
	public async ValueTask<byte[]> ReadClipboardAsync(
		TerminalClipboardSelection selection,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		TerminalOsc52Selection protocolSelection = ToOsc52Selection( selection );
		ValidateClipboardQueryTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] request = OscWriter.EncodeOsc52QueryFrame( protocolSelection );
		int maximumBytes = this.Options.ClipboardOptions.MaximumReadBytes;
		TerminalQueryResponseResult result = await this.ExecuteQueryTransactionAsync(
			request,
			TerminalQueryResponsePlan.ForClipboard(
				TerminalOsc52Protocol.CreateResponseMatcher( protocolSelection ), maximumBytes ),
			timeout,
			cancellationToken
		).ConfigureAwait( false );

		return TerminalOsc52Protocol.ParsePayload(
			result.Frame,
			protocolSelection,
			maximumBytes
		);
	}

	private static void ValidateClipboardQueryTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal clipboard query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}
}
