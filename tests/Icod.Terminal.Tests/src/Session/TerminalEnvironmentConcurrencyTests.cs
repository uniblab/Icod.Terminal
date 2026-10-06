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
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Detects deadlocks and lost ownership with controlled interleavings.</summary>
public sealed class TerminalEnvironmentConcurrencyTests {
	[Fact]
	public async Task DisposalClosesInflightNegotiationBeforeWaitingForManager() {
		EnvironmentTestContext wire = new() { Respond = false }; TerminalSession session = await wire.OpenAsync();
		Task acquisition = session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(30)).AsTask();
		await wire.WaitForAsync("\u001b[?2031$p");
		Task disposal = session.DisposeAsync().AsTask();
		await Assert.ThrowsAnyAsync<Exception>(() => acquisition.WaitAsync(TimeSpan.FromSeconds(5)));
		await disposal.WaitAsync(TimeSpan.FromSeconds(5)); Assert.DoesNotContain("\u001b[?2031h", wire.Writes);
	}
	[Fact]
	public async Task SuspendAbortsNegotiationWithoutGateCycle() {
		EnvironmentTestContext wire = new() { Respond = false }; await using TerminalSession session = await wire.OpenAsync();
		Task acquisition = session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(30)).AsTask();
		await wire.WaitForAsync("\u001b[?2048$p"); wire.Signal(TerminalLifecycleSignalKind.Suspend);
		await Assert.ThrowsAnyAsync<Exception>(() => acquisition.WaitAsync(TimeSpan.FromSeconds(5)));
		Assert.Equal(TerminalLifecycleEventKind.Suspending, (await EnvironmentTestContext.LifecycleAsync(session)).Kind);
		Assert.DoesNotContain("\u001b[?2048h", wire.Writes);
	}
	[Fact]
	public async Task CanceledNegotiationReleasesCompositionForOtherFacility() {
		EnvironmentTestContext wire = new() { Respond = false }; await using TerminalSession session = await wire.OpenAsync(false);
		using CancellationTokenSource cancellation = new();
		Task acquisition = session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(30), cancellation.Token).AsTask();
		await wire.WaitForAsync("\u001b[?2031$p"); cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acquisition);
		wire.Respond = true; await using TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		Assert.Contains("\u001b[?2048h", wire.Writes);
	}
	[Fact]
	public async Task InputEofUnblocksNegotiationAndDisposal() {
		EnvironmentTestContext wire = new() { Respond = false }; TerminalSession session = await wire.OpenAsync(false);
		Task acquisition = session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(30)).AsTask();
		await wire.WaitForAsync("\u001b[?2031$p"); wire.EndInput();
		await Assert.ThrowsAnyAsync<Exception>(() => acquisition.WaitAsync(TimeSpan.FromSeconds(5)));
		await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
	}
	[Fact]
	public async Task ConcurrentFinalReleaseAndCloseEmitOneDisable() {
		EnvironmentTestContext wire = new(); TerminalSession session = await wire.OpenAsync(false);
		TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously), release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		wire.BeforeWrite = async value => { if (value == "\u001b[?2031l") { entered.TrySetResult(); await release.Task; } };
		Task releasing = lease.DisposeAsync().AsTask(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		Task closing = session.DisposeAsync().AsTask(); release.TrySetResult();
		await Task.WhenAll(releasing, closing).WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(1, wire.Writes.Count(x => x == "\u001b[?2031l"));
	}
	[Fact]
	public async Task MixedBurstPreservesReportsAndFollowingKeyboard() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		for (int i = 0; i < 40; ++i) { wire.Publish("\u001b[?997;1n\u001b[48;24;80;0;0t\u001b[?997;9n"); wire.Publish("x"); }
		for (int i = 0; i < 40; ++i) {
			Assert.Equal(TerminalSemanticEventKind.Appearance, (await session.ReadEventAsync(TimeSpan.FromSeconds(5))).Semantic?.Kind);
			Assert.Equal(TerminalSemanticEventKind.InBandResize, (await session.ReadEventAsync(TimeSpan.FromSeconds(5))).Semantic?.Kind);
			Assert.Equal(TerminalEventKind.Input, (await session.ReadEventAsync(TimeSpan.FromSeconds(5))).Kind);
		}
	}

	[Fact]
	public async Task InvalidationDuringCommittedEnableDoesNotPublishAvailableOrBlindCleanup() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		wire.BeforeWrite = value => { if (value == "\u001b[?2031h") { session.InvalidateState(); } return ValueTask.CompletedTask; };
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5)).AsTask());
		Assert.DoesNotContain("\u001b[?2031l", wire.Writes);
	}
	[Fact]
	public async Task InvalidationDuringFailedEnableDoesNotRestoreUnknownBaseline() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		wire.BeforeWrite = value => { if (value == "\u001b[?2031h") { session.InvalidateState(); } return ValueTask.CompletedTask; };
		wire.FailWrite = "\u001b[?2031h";
		await Assert.ThrowsAsync<IOException>(() => session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5)).AsTask());
		Assert.DoesNotContain("\u001b[?2031l", wire.Writes);
	}
	[Fact]
	public async Task LeaseCanRetireWhileResumeObservationAwaitsReply() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.Signal(TerminalLifecycleSignalKind.Suspend); _ = await EnvironmentTestContext.LifecycleAsync(session);
		await wire.WaitForAsync("\u001b[?2031$p"); wire.Respond = false;
		wire.Signal(TerminalLifecycleSignalKind.Resume); await wire.WaitForAsync("\u001b[?2031$p");
		await lease.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); wire.Publish("\u001b[?2031;2$y");
		Assert.Equal(TerminalLifecycleEventKind.Resumed, (await EnvironmentTestContext.LifecycleAsync(session)).Kind);
		Assert.Equal(1, wire.Writes.Count(x => x == "\u001b[?2031h"));
	}

	[Fact]
	public async Task InvalidationWhileCleanupWaitsForOutputSkipsUnknownRestoration() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		IDisposable output = await session.AcquireControlOutputAsync(CancellationToken.None);
		Task release = lease.DisposeAsync().AsTask(); Assert.False(release.IsCompleted);
		session.InvalidateState(); output.Dispose(); await release.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.DoesNotContain("\u001b[?2031l", wire.Writes);
	}
}
