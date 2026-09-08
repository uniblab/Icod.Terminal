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
namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T152 typed OSC 133 prompt-start metadata and public session integration.
/// </summary>
public sealed class TerminalSemanticPromptOptionsTests {
	[Fact]
	public void DefaultOptionsRepresentPortablePrimaryPrompt() {
		TerminalSemanticPromptOptions options = default;

		Assert.Equal( TerminalSemanticPromptKind.Primary, options.Kind );
		Assert.Equal(
			TerminalSemanticPromptResizeBehavior.Unspecified,
			options.ResizeBehavior
		);
		Assert.False( options.UseSpecialCursorKey );
		Assert.Equal( TerminalSemanticPromptClickMode.None, options.ClickMode );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 2 )]
	public void UndefinedPromptKindIsRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalSemanticPromptOptions(
				kind: (TerminalSemanticPromptKind)value
			)
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 2 )]
	public void UndefinedResizeBehaviorIsRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalSemanticPromptOptions(
				resizeBehavior: (TerminalSemanticPromptResizeBehavior)value
			)
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 3 )]
	public void UndefinedClickModeIsRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalSemanticPromptOptions(
				clickMode: (TerminalSemanticPromptClickMode)value
			)
		);
	}

	[Fact]
	public async Task DefaultOptionsEmitExactlyThePortableBarePromptFrame() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.BeginPromptAsync( default( TerminalSemanticPromptOptions ) );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task CombinedOptionsEmitCanonicalExtendedPromptFrame() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSemanticPromptOptions options = new(
			TerminalSemanticPromptKind.Secondary,
			TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
			useSpecialCursorKey: true,
			TerminalSemanticPromptClickMode.Relative
		);

		await session.BeginPromptAsync( options );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;A;redraw=0;special_key=1;k=s;click_events=2\u001b\\"
			),
			output.Writes[ 0 ]
		);
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Theory]
	[InlineData(
		TerminalSemanticPromptClickMode.Absolute,
		"\u001b]133;A;click_events=1\u001b\\"
	)]
	[InlineData(
		TerminalSemanticPromptClickMode.Relative,
		"\u001b]133;A;click_events=2\u001b\\"
	)]
	public async Task ClickModesMapToFrozenWireValues(
		TerminalSemanticPromptClickMode clickMode,
		string expected
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSemanticPromptOptions options = new(
			clickMode: clickMode
		);

		await session.BeginPromptAsync( options );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task PreCancelledExtendedPromptEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		TerminalSemanticPromptOptions options = new(
			TerminalSemanticPromptKind.Secondary,
			TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
			useSpecialCursorKey: true,
			TerminalSemanticPromptClickMode.Absolute
		);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.BeginPromptAsync(
				options,
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
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
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
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
