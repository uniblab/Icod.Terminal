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
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T175 cross-manager Kitty keyboard composition and lifecycle hardening.
/// </summary>
public sealed class TerminalKeyboardCompositionHardeningTests {
	private const string ProbeRequest = "\u001b[?u\u001b[c";
	private const string PushAllKeys = "\u001b[>31u";
	private const string PopKeyboard = "\u001b[<u";
	private const string EnterAlternateScreen = "<A+>";
	private const string ExitAlternateScreen = "<A->";

	[Fact]
	public async Task AlternateScreenTransitionMovesKittyOwnershipToActiveScreen() {
		DuplexTerminalTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalInputProtocolLease keyboard = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			)
		).GetRequiredValue();
		transport.ClearWrites();

		TerminalPresentationLease presentation = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				PopKeyboard,
				EnterAlternateScreen,
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await presentation.DisposeAsync();

		Assert.Equal(
			new[] {
				PopKeyboard,
				ExitAlternateScreen,
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await keyboard.DisposeAsync();
		Assert.Equal( new[] { PopKeyboard }, transport.SuccessfulWrites );
	}

	[Fact]
	public async Task AlternateScreenTransitionDoesNotCycleOtherRichInputProtocols() {
		DuplexTerminalTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			CreateRichPresentationTerminal()
		);
		TerminalInputProtocolLease input = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true,
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents,
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			)
		).GetRequiredValue();
		transport.ClearWrites();

		TerminalPresentationLease presentation = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				PopKeyboard,
				EnterAlternateScreen,
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		await presentation.DisposeAsync();
		await input.DisposeAsync();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		DuplexTerminalTransport transport,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal ?? CreatePresentationTerminal(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private static TerminalDescription CreatePresentationTerminal() {
		return new TerminalDescriptionBuilder( "kitty-composition-test" )
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				EnterAlternateScreen
			)
			.SetString(
				StringCapability.ExitCursorAddressingMode,
				ExitAlternateScreen
			)
			.Build();
	}

	private static TerminalDescription CreateRichPresentationTerminal() {
		return new TerminalDescriptionBuilder( "kitty-rich-composition-test" )
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				EnterAlternateScreen
			)
			.SetString(
				StringCapability.ExitCursorAddressingMode,
				ExitAlternateScreen
			)
			.SetExtendedString( "BE", "<P+>" )
			.SetExtendedString( "BD", "<P->" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "PE", "\u001b[201~" )
			.SetExtendedString( "fe", "<F+>" )
			.SetExtendedString( "fd", "<F->" )
			.SetExtendedString( "kxIN", "\u001b[I" )
			.SetExtendedString( "kxOUT", "\u001b[O" )
			.SetString( StringCapability.KeyMouse, "\u001b[<" )
			.SetExtendedString(
				"XM",
				"\u001b[?1006;1000%?%p1%{1}%=%th%el%;"
			)
			.SetExtendedString(
				"xm",
				"\u001b[<%i%p3%d;%p1%d;%p2%d;%?%p4%tM%em%;"
			)
			.Build();
	}

	private sealed class DuplexTerminalTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();

		internal List<string> SuccessfulWrites {
			get;
		} = [];

		internal void ClearWrites() {
			this.SuccessfulWrites.Clear();
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string value = Encoding.Latin1.GetString( buffer.Span );
			this.SuccessfulWrites.Add( value );
			if ( string.Equals( ProbeRequest, value, StringComparison.Ordinal ) ) {
				byte[] response = Encoding.ASCII.GetBytes(
					"\u001b[?0u\u001b[?1;2c"
				);
				if ( !this.input.Writer.TryWrite( response ) ) {
					throw new InvalidOperationException(
						"The scripted Kitty probe response could not be queued."
					);
				}
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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
				"Live size is not required by keyboard composition tests."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
