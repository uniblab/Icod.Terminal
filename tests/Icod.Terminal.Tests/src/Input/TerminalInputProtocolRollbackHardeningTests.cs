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

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T183 rich-input transition and rollback failure semantics.
/// </summary>
public sealed class TerminalInputProtocolRollbackHardeningTests {
	private const string EnablePaste = "<P+>";
	private const string DisablePaste = "<P->";
	private const string PasteStart = "<PS>";
	private const string PasteEnd = "<PE>";
	private const string EnableFocus = "<F+>";
	private const string DisableFocus = "<F->";
	private const string FocusIn = "<FI>";
	private const string FocusOut = "<FO>";

	[Fact]
	public async Task FailedTransitionAndFailedRollbackLeaveNoGhostLeaseAndAllowRecovery() {
		FailureTransport transport = new(
			EnableFocus,
			DisablePaste
		);
		await using TerminalSession session = await OpenSessionAsync( transport );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true
				}
			).AsTask()
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
		Assert.Contains(
			"transition failed and rollback also reported an error",
			exception.Message,
			StringComparison.Ordinal
		);
		Assert.Equal(
			new[] {
				EnablePaste,
				EnableFocus,
				DisablePaste
			},
			transport.WriteAttempts
		);
		Assert.Equal(
			new[] { EnablePaste },
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		TerminalInputProtocolLease recovery = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			)
		).GetRequiredValue();

		Assert.Equal( new[] { EnablePaste }, transport.SuccessfulWrites );

		transport.ClearWrites();
		await recovery.DisposeAsync();
		Assert.Equal( new[] { DisablePaste }, transport.SuccessfulWrites );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		FailureTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );

		TerminalDescription terminal = new TerminalDescriptionBuilder(
			"input-rollback-hardening"
		)
			.SetExtendedString( "BE", EnablePaste )
			.SetExtendedString( "BD", DisablePaste )
			.SetExtendedString( "PS", PasteStart )
			.SetExtendedString( "PE", PasteEnd )
			.SetExtendedString( "fe", EnableFocus )
			.SetExtendedString( "fd", DisableFocus )
			.SetExtendedString( "kxIN", FocusIn )
			.SetExtendedString( "kxOUT", FocusOut )
			.Build();

		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyInput(),
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class FailureTransport : ITerminalOutput {
		private readonly HashSet<string> failOnce;
		private readonly HashSet<string> failuresUsed = [];

		internal FailureTransport(
			params string[] failOnce
		) {
			ArgumentNullException.ThrowIfNull( failOnce );
			this.failOnce = new HashSet<string>(
				failOnce,
				StringComparer.Ordinal
			);
		}

		internal List<string> WriteAttempts {
			get;
		} = [];

		internal List<string> SuccessfulWrites {
			get;
		} = [];

		internal void ClearWrites() {
			this.WriteAttempts.Clear();
			this.SuccessfulWrites.Clear();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string value = Encoding.Latin1.GetString( buffer.Span );
			this.WriteAttempts.Add( value );

			if ( this.failOnce.Contains( value )
				&& this.failuresUsed.Add( value ) ) {
				throw new IOException(
					$"Injected terminal output failure for '{value}'."
				);
			}

			this.SuccessfulWrites.Add( value );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class EmptyInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0x0002UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unavailable(
				"Live size is not required by input rollback hardening."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}
			return TerminalControlMutationResult.Success();
		}
	}
}
