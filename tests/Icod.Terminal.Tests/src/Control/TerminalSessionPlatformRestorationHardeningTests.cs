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
namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies platform-specific session input-mode application and exact baseline restoration.
/// </summary>
public sealed class TerminalSessionPlatformRestorationHardeningTests {
	[Fact]
	public async Task PosixSessionRestoresCapturedBaselineAfterOutputDrained() {
		TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0x1234UL,
			0x2345UL,
			0x3456UL,
			0x4567UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);
		RecordingProvider provider = new(
			baseline,
			TerminalPlatformKind.PosixTermios
		);

		TerminalSession session = await OpenSessionAsync( provider );

		Assert.Single( provider.SetModeCalls );
		Assert.NotSame( baseline, provider.SetModeCalls[ 0 ].Mode );
		Assert.Equal(
			TerminalModeApplyTiming.AfterOutputDrained,
			provider.SetModeCalls[ 0 ].Timing
		);

		await session.DisposeAsync();

		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
		Assert.Equal(
			TerminalModeApplyTiming.AfterOutputDrained,
			provider.SetModeCalls[ 1 ].Timing
		);
	}

	[Fact]
	public async Task WindowsSessionRestoresCapturedBaselineImmediately() {
		TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x8123U
		);
		RecordingProvider provider = new(
			baseline,
			TerminalPlatformKind.WindowsConsole
		);

		TerminalSession session = await OpenSessionAsync( provider );

		Assert.Single( provider.SetModeCalls );
		Assert.NotSame( baseline, provider.SetModeCalls[ 0 ].Mode );
		Assert.Equal(
			TerminalModeApplyTiming.Immediately,
			provider.SetModeCalls[ 0 ].Timing
		);

		await session.DisposeAsync();

		Assert.Equal( 2, provider.SetModeCalls.Count );
		Assert.Same( baseline, provider.SetModeCalls[ 1 ].Mode );
		Assert.Equal(
			TerminalModeApplyTiming.Immediately,
			provider.SetModeCalls[ 1 ].Timing
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingProvider provider
	) {
		ArgumentNullException.ThrowIfNull( provider );

		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestInput(),
			new TestOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				InputMode = TerminalInputMode.CBreak,
				EchoInput = false
			}
		);
	}

	private sealed class TestInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class TestOutput : ITerminalOutput {
		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline;
		private readonly TerminalPlatformKind platform;

		internal RecordingProvider(
			TerminalModeSnapshot baseline,
			TerminalPlatformKind platform
		) {
			ArgumentNullException.ThrowIfNull( baseline );
			if ( !Enum.IsDefined( platform ) ) {
				throw new ArgumentOutOfRangeException( nameof( platform ) );
			}

			this.baseline = baseline;
			this.platform = platform;
		}

		internal List<SetModeCall> SetModeCalls {
			get;
		} = [];

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					this.platform,
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
				"Live size is not required by platform restoration hardening."
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

			this.SetModeCalls.Add( new SetModeCall( mode, timing ) );
			return TerminalControlMutationResult.Success();
		}
	}

	private readonly record struct SetModeCall(
		TerminalModeSnapshot Mode,
		TerminalModeApplyTiming Timing
	);
}
