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
namespace Icod.Terminal.Tests.Lifecycle;

using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T165 lifecycle, failure, ordering, and public-surface hardening for
/// the bounded OSC 9 safe extensions.
/// </summary>
public sealed class TerminalSessionOsc9SafeExtensionHardeningTests {
	[Fact]
	public async Task QueuedNotificationCancellationEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable outputLease = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();

		Task write = session.SendNotificationAsync(
			"cancel me",
			cancellation.Token
		).AsTask();
		cancellation.Cancel();

		try {
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => write
			);
		} finally {
			outputLease.Dispose();
		}

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task FailedCommittedNotificationDoesNotCompensateAndLaterCallRemainsAvailable() {
		FailOnceTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<IOException>(
			() => session.SendNotificationAsync( "failure" ).AsTask()
		);

		Assert.Single( output.Attempts );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;failure\u001b\\" ),
			output.Attempts[ 0 ]
		);
		Assert.True( session.IsStateValid );

		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\after-failure"
		);

		Assert.Equal( 2, output.Attempts.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\after-failure\u001b\\" ),
			output.Attempts[ 1 ]
		);
	}

	[Fact]
	public async Task NotificationAndProgressSerializeAsWholeFrames() {
		BlockingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease progress = await session.AcquireProgressAsync();

		Task notification = session.SendNotificationAsync( "working" ).AsTask();
		await output.FirstWriteStarted;
		Task report = progress.ReportAsync( 1, 2 ).AsTask();

		Assert.False( notification.IsCompleted );
		Assert.False( report.IsCompleted );
		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;working\u001b\\" ),
			output.Writes[ 0 ]
		);

		output.ReleaseFirstWrite();
		await Task.WhenAll( notification, report );

		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 50 ),
			output.Writes[ 1 ]
		);
		Assert.All(
			output.WriteCancellationTokens,
			token => Assert.False( token.CanBeCanceled )
		);

		await progress.DisposeAsync();
		await session.DisposeAsync();
	}

	[Fact]
	public async Task InvalidationDoesNotReplayOrSynthesizeSafeOsc9Metadata() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		await session.SendNotificationAsync( "state" );
		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\state"
		);
		Assert.Equal( 2, output.Writes.Count );

		session.InvalidateState();

		Assert.Equal( 2, output.Writes.Count );
		await session.DisposeAsync();
		Assert.Equal( 2, output.Writes.Count );
	}

	[Fact]
	public async Task SuspendAndResumeDoNotReplaySafeOsc9Metadata() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);

		await session.SendNotificationAsync( "before suspend" );
		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\before-suspend"
		);
		Assert.Equal( 2, output.Writes.Count );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( 2, output.Writes.Count );

		await session.SendNotificationAsync( "after resume" );
		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;after resume\u001b\\" ),
			output.Writes[ 2 ]
		);

		await session.DisposeAsync();
		Assert.Equal( 3, output.Writes.Count );
	}

	[Fact]
	public async Task DisposalDoesNotSynthesizeSafeOsc9Output() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		await session.SendNotificationAsync( "dispose" );
		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\dispose"
		);
		Assert.Equal( 2, output.Writes.Count );

		await session.DisposeAsync();
		await session.DisposeAsync();

		Assert.Equal( 2, output.Writes.Count );
	}

	[Fact]
	public void HazardousOrGenericOsc9CommandsAreNotPubliclyReachable() {
		string[] forbiddenMethodNames = [
			"WriteOsc9Async",
			"SendOsc9Async",
			"SleepAsync",
			"ShowMessageBoxAsync",
			"WaitForKeyAsync",
			"ExecuteGuiMacroAsync",
			"RunProcessAsync",
			"ReadEnvironmentVariableAsync",
			"SetXtermEmulationAsync"
		];
		MethodInfo[] publicMethods = typeof( TerminalSession ).GetMethods(
			BindingFlags.Instance | BindingFlags.Public
		);

		foreach ( string forbidden in forbiddenMethodNames ) {
			Assert.DoesNotContain(
				publicMethods,
				method => string.Equals(
					method.Name,
					forbidden,
					StringComparison.Ordinal
				)
			);
		}

		Assert.Contains(
			publicMethods,
			method => string.Equals(
				method.Name,
				nameof( TerminalSession.SendNotificationAsync ),
				StringComparison.Ordinal
			)
		);
		Assert.Contains(
			publicMethods,
			method => string.Equals(
				method.Name,
				nameof( TerminalSession.PublishWindowsCurrentDirectoryCompatibilityAsync ),
				StringComparison.Ordinal
			)
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output,
		TestTerminalLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
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

	private class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		public virtual ValueTask WriteAsync(
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
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FailOnceTerminalOutput : RecordingTerminalOutput {
		private int writeCount;

		internal List<byte[]> Attempts {
			get;
		} = [];

		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Attempts.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			if ( 1 == Interlocked.Increment( ref this.writeCount ) ) {
				throw new IOException( "Synthetic committed OSC 9 safe-extension write failure." );
			}

			this.Writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}
	}

	private sealed class BlockingTerminalOutput : ITerminalOutput {
		private readonly TaskCompletionSource firstWriteStarted = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource releaseFirstWrite = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private int writeCount;

		internal Task FirstWriteStarted {
			get {
				return this.firstWriteStarted.Task;
			}
		}

		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal void ReleaseFirstWrite() {
			this.releaseFirstWrite.TrySetResult();
		}

		public async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			if ( 1 == Interlocked.Increment( ref this.writeCount ) ) {
				this.firstWriteStarted.TrySetResult();
				await this.releaseFirstWrite.Task.ConfigureAwait( false );
			}
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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
				"Size is not used by OSC 9 safe-extension hardening tests."
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
