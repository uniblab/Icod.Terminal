/*
	Icod.Terminal.Compatibility.Sample
	Sample application demonstrating Icod.Terminal Compatibility features.
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
namespace Icod.Terminal.Compatibility.Sample;

using Icod.Terminal;

internal static class InputCompatibilityScenarios {
	private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds( 45 );

	internal sealed class ObservationSet {
		internal bool Text { get; private set; }
		internal bool Key { get; private set; }
		internal bool PasteBegin { get; private set; }
		internal bool PasteData { get; private set; }
		internal bool PasteEnd { get; private set; }
		internal bool Focus { get; private set; }
		internal bool Mouse { get; private set; }
		internal HashSet<TerminalKeyEventPhase> KeyPhases { get; } = [];

		internal void Observe(
			TerminalInputEvent input
		) {
			ArgumentNullException.ThrowIfNull( input );
			switch ( input.Kind ) {
				case TerminalInputEventKind.Text:
					this.Text = true;
					break;
				case TerminalInputEventKind.Key:
					this.Key = true;
					if ( input.KeyPhase.HasValue ) {
						this.KeyPhases.Add( input.KeyPhase.Value );
					}
					break;
				case TerminalInputEventKind.Paste:
					this.PasteBegin |= input.Paste?.Phase == TerminalPastePhase.Begin;
					this.PasteData |= input.Paste?.Phase == TerminalPastePhase.Data;
					this.PasteEnd |= input.Paste?.Phase == TerminalPastePhase.End;
					break;
				case TerminalInputEventKind.Focus:
					this.Focus = true;
					break;
				case TerminalInputEventKind.Mouse:
					this.Mouse = true;
					break;
				case TerminalInputEventKind.EndOfInput:
					break;
				default:
					break;
			}
		}
	}

	internal static ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		TerminalSession session,
		string scenarioId,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		return scenarioId switch {
			"input.text-key" => RunTextAndKeyAsync( session, cancellationToken ),
			"input.paste-focus-mouse" => RunPasteFocusMouseAsync( session, cancellationToken ),
			"input.modern-keyboard" => RunModernKeyboardAsync( session, cancellationToken ),
			"lifecycle.resize-suspend" => RunLifecycleAsync( session, cancellationToken ),
			_ => throw new ArgumentException( "Unknown input scenario.", nameof( scenarioId ) )
		};
	}

	private static async ValueTask<CompatibilityLiveExecution> RunTextAndKeyAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		await session.WriteTextAsync(
			"Type one printable character, then press a named key such as an arrow.\r\n",
			cancellationToken
		).ConfigureAwait( false );
		var observations = new ObservationSet();
		bool completed = await ObserveAsync(
			session,
			observations,
			static value => value.Text && value.Key,
			cancellationToken
		).ConfigureAwait( false );
		return new CompatibilityLiveExecution(
			completed,
			false,
			completed
				? "Distinct typed text and key events were observed."
				: "Distinct text and key events were not both observed before input ended."
		);
	}

	private static async ValueTask<CompatibilityLiveExecution> RunPasteFocusMouseAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true,
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
				},
				cancellationToken
			).ConfigureAwait( false );
		if ( !result.IsAvailable ) {
			return new CompatibilityLiveExecution(
				false,
				false,
				"The terminal could not establish the requested paste, focus, and mouse protocols."
			);
		}

		TerminalInputProtocolLease lease = result.GetRequiredValue();
		try {
			await session.WriteTextAsync(
				"Paste text, change terminal focus, and click a mouse button.\r\n",
				cancellationToken
			).ConfigureAwait( false );
			var observations = new ObservationSet();
			bool completed = await ObserveAsync(
				session,
				observations,
				static value => value.PasteBegin && value.PasteData && value.PasteEnd
					&& value.Focus && value.Mouse,
				cancellationToken
			).ConfigureAwait( false );
			return new CompatibilityLiveExecution(
				completed,
				false,
				completed
					? "Distinct paste phases, focus, and mouse events were observed."
					: "The required distinct paste, focus, and mouse events were not all observed."
			);
		} finally {
			await DisposeLeaseAsync( lease, "input protocol" ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<CompatibilityLiveExecution> RunModernKeyboardAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				},
				cancellationToken
			).ConfigureAwait( false );
		if ( !result.IsAvailable ) {
			return new CompatibilityLiveExecution(
				false,
				false,
				"Modern keyboard reporting was not negotiated."
			);
		}

		TerminalInputProtocolLease lease = result.GetRequiredValue();
		try {
			await session.WriteTextAsync(
				"Press, repeat, or release a key while modern keyboard reporting is active.\r\n",
				cancellationToken
			).ConfigureAwait( false );
			var observations = new ObservationSet();
			bool completed = await ObserveAsync(
				session,
				observations,
				static value => 0 < value.KeyPhases.Count,
				cancellationToken
			).ConfigureAwait( false );
			string phases = string.Join( ", ", observations.KeyPhases.OrderBy( value => value ) );
			return new CompatibilityLiveExecution(
				completed,
				false,
				completed
					? $"Negotiated modern keyboard phases observed: {phases}."
					: "No negotiated modern keyboard phase was observed."
			);
		} finally {
			await DisposeLeaseAsync( lease, "modern keyboard" ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<CompatibilityLiveExecution> RunLifecycleAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		await session.WriteTextAsync(
			"Resize the terminal, then suspend and resume this process if the host supports it.\r\n",
			cancellationToken
		).ConfigureAwait( false );
		bool resize = false;
		bool suspending = false;
		bool resumed = false;
		DateTimeOffset deadline = DateTimeOffset.UtcNow + ObservationTimeout;
		while ( DateTimeOffset.UtcNow < deadline ) {
			TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
			TerminalEvent terminalEvent = await session.ReadEventAsync(
				remaining,
				cancellationToken
			).ConfigureAwait( false );
			if ( terminalEvent.Kind is TerminalEventKind.Timeout or TerminalEventKind.Cancelled ) {
				break;
			}
			if ( terminalEvent.Kind == TerminalEventKind.Input
				&& terminalEvent.Input?.Kind == TerminalInputEventKind.EndOfInput ) {
				break;
			}
			if ( terminalEvent.Kind != TerminalEventKind.Lifecycle
				|| terminalEvent.Lifecycle is not { } lifecycle ) {
				continue;
			}
			resize |= lifecycle.Kind == TerminalLifecycleEventKind.Resize;
			suspending |= lifecycle.Kind == TerminalLifecycleEventKind.Suspending;
			resumed |= lifecycle.Kind == TerminalLifecycleEventKind.Resumed;
			if ( resize && suspending && resumed ) {
				break;
			}
		}

		bool completed = resize && suspending && resumed;
		return new CompatibilityLiveExecution(
			completed,
			false,
			completed
				? "Typed resize, suspending, and resumed lifecycle events were observed."
				: "The full resize and suspend/resume lifecycle was not observed."
		);
	}

	private static async ValueTask<bool> ObserveAsync(
		TerminalSession session,
		ObservationSet observations,
		Func<ObservationSet, bool> complete,
		CancellationToken cancellationToken
	) {
		DateTimeOffset deadline = DateTimeOffset.UtcNow + ObservationTimeout;
		while ( DateTimeOffset.UtcNow < deadline ) {
			TerminalEvent terminalEvent = await session.ReadEventAsync(
				deadline - DateTimeOffset.UtcNow,
				cancellationToken
			).ConfigureAwait( false );
			if ( terminalEvent.Kind is TerminalEventKind.Timeout or TerminalEventKind.Cancelled ) {
				return false;
			}
			if ( terminalEvent.Kind != TerminalEventKind.Input
				|| terminalEvent.Input is not { } input ) {
				continue;
			}
			if ( input.Kind == TerminalInputEventKind.EndOfInput ) {
				return false;
			}
			observations.Observe( input );
			if ( complete( observations ) ) {
				return true;
			}
		}
		return false;
	}

	private static async ValueTask DisposeLeaseAsync(
		IAsyncDisposable lease,
		string name
	) {
		try {
			await lease.DisposeAsync().ConfigureAwait( false );
		} catch ( Exception exception ) {
			throw new CompatibilityCleanupException(
				$"The {name} lease could not restore terminal state.",
				exception
			);
		}
	}
}
