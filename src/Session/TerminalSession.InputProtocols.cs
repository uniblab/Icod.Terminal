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
/// Reversible rich-input protocol ownership for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private readonly TerminalInputProtocolManager inputProtocolManager;

	/// <summary>
	/// Acquires one reversible set of rich-input protocol reporting requirements.
	/// </summary>
	/// <param name="options">The input protocols required while the lease is active.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>
	/// An available result containing the acquired lease, or a controlled unavailable
	/// result when the selected terminal cannot establish the required protocol contract.
	/// </returns>
	public async ValueTask<TerminalControlResult<TerminalInputProtocolLease>> AcquireInputProtocolsAsync(
		TerminalInputProtocolOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		using IDisposable composition = await this.AcquireStateCompositionAsync(
			cancellationToken
		).ConfigureAwait( false );
		this.ThrowIfStateAcquisitionUnavailable();

		if ( options.KeyboardReportingMode.HasValue ) {
			bool kittySupported = await this.ProbeKittyKeyboardSupportAsync(
				lifecycleObservation: false,
				cancellationToken
			).ConfigureAwait( false );
			if ( !kittySupported ) {
				return TerminalControlResult<TerminalInputProtocolLease>.Unavailable(
					"The terminal did not establish Kitty progressive keyboard protocol support."
				);
			}
		}

		return await this.inputProtocolManager.AcquireAsync(
			options,
			cancellationToken
		).ConfigureAwait( false );
	}

	private void InvalidateInputProtocolState() {
		this.AdvanceSemanticLiveEvidenceGeneration();
		this.inputProtocolManager.Invalidate();
	}

	private async ValueTask SuspendInputProtocolStateAsync() {
		Interlocked.Exchange( ref this.inputProtocolsReenteredBeforePresentation, 0 );
		using IDisposable composition = await this.AcquireStateCompositionAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		await this.inputProtocolManager.SuspendAsync().ConfigureAwait( false );
	}

	private async ValueTask ResumeInputProtocolStateAsync() {
		if ( 0 != Interlocked.Exchange(
			ref this.inputProtocolsReenteredBeforePresentation,
			0
		) ) {
			return;
		}

		using IDisposable composition = await this.AcquireStateCompositionAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		await this.inputProtocolManager.ReenterAsync().ConfigureAwait( false );
	}

	private async ValueTask<Exception?> CloseInputProtocolStateAsync() {
		Interlocked.Exchange( ref this.inputProtocolsReenteredBeforePresentation, 0 );
		try {
			using IDisposable composition = await this.AcquireStateCompositionAsync(
				CancellationToken.None
			).ConfigureAwait( false );
			await this.inputProtocolManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
