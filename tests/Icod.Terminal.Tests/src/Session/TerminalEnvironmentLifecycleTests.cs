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

/// <summary>Detects stale restoration, missing re-entry and incomplete reporting teardown.</summary>
public sealed class TerminalEnvironmentLifecycleTests {
	[Theory]
	[InlineData(1, false)]
	[InlineData(2, true)]
	[InlineData(3, false)]
	public async Task ResumeReobservesOwnedModesAndRequestsFreshResize(int resumedState, bool appearanceEnable) {
		EnvironmentTestContext wire = new();
		await using TerminalSession session = await wire.OpenAsync();
		await using TerminalAppearanceReportingLease appearance = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		await using TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.Signal(TerminalLifecycleSignalKind.Suspend);
		Assert.Equal(TerminalLifecycleEventKind.Suspending, (await EnvironmentTestContext.LifecycleAsync(session)).Kind);
		Assert.Contains("\u001b[?2031l", wire.Writes); Assert.Contains("\u001b[?2048l", wire.Writes);
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.QueryAppearanceAsync(TimeSpan.Zero).AsTask());
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.AcquireAppearanceReportingAsync(TimeSpan.Zero).AsTask());
		int before = wire.Writes.Length;
		wire.AppearanceState = wire.ResizeState = resumedState;
		wire.Signal(TerminalLifecycleSignalKind.Resume);
		Assert.Equal(TerminalLifecycleEventKind.Resumed, (await EnvironmentTestContext.LifecycleAsync(session)).Kind);
		string[] reentry = wire.Writes.Skip(before).ToArray();
		Assert.Equal(new[] { "\u001b[?2031$p", "\u001b[?2048$p" }, reentry.Where(x => x.EndsWith("$p", StringComparison.Ordinal)));
		Assert.Equal(appearanceEnable, reentry.Contains("\u001b[?2031h")); Assert.Contains("\u001b[?2048h", reentry);
		Assert.True(session.IsStateValid);
		Assert.Equal(TerminalAppearance.Dark, await session.QueryAppearanceAsync(TimeSpan.FromSeconds(5)));
	}
	[Fact]
	public async Task InvalidationSuppressesSpeculativeRelease() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		session.InvalidateState(); await lease.DisposeAsync(); Assert.DoesNotContain("\u001b[?2031l", wire.Writes);
	}
	[Fact]
	public async Task AcquiringOtherModeDoesNotRevalidateStaleBaseline() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		TerminalAppearanceReportingLease appearance = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		session.InvalidateState();
		await using TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		await appearance.DisposeAsync(); Assert.DoesNotContain("\u001b[?2031l", wire.Writes);
	}
	[Fact]
	public async Task NextAcquisitionRefreshesInvalidatedActiveFacility() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		await using TerminalAppearanceReportingLease first = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		session.InvalidateState(); wire.AppearanceState = 1;
		await using TerminalAppearanceReportingLease second = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		Assert.Equal(2, wire.Writes.Count(x => x == "\u001b[?2031$p"));
		await second.DisposeAsync(); await first.DisposeAsync(); Assert.DoesNotContain("\u001b[?2031l", wire.Writes);
	}
	[Fact]
	public async Task ReleaseWhileSuspendedDoesNotDisableTwiceOrRequeryRetiredOwner() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.Signal(TerminalLifecycleSignalKind.Suspend); _ = await EnvironmentTestContext.LifecycleAsync(session);
		await lease.DisposeAsync(); wire.Signal(TerminalLifecycleSignalKind.Resume); _ = await EnvironmentTestContext.LifecycleAsync(session);
		Assert.Equal(new[] { "\u001b[?2031$p", "\u001b[?2031h", "\u001b[?2031l" }, wire.Writes);
	}
	[Theory]
	[InlineData(0)]
	[InlineData(4)]
	[InlineData(9)]
	public async Task UnusableRefreshFailsReentryWithoutClaimingResumed(int state) {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		await using TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.Signal(TerminalLifecycleSignalKind.Suspend); _ = await EnvironmentTestContext.LifecycleAsync(session);
		wire.AppearanceState = state; wire.Signal(TerminalLifecycleSignalKind.Resume);
		await Assert.ThrowsAnyAsync<Exception>(() => EnvironmentTestContext.LifecycleAsync(session));
		Assert.False(session.IsStateValid); Assert.True(session.TerminationToken.IsCancellationRequested);
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SessionCloseRestoresOnlyValidBaselinesAndRetiresLeases(bool invalidated) {
		EnvironmentTestContext wire = new(); TerminalSession session = await wire.OpenAsync();
		TerminalAppearanceReportingLease appearance = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		if (invalidated) { session.InvalidateState(); }
		int before = wire.Writes.Length;
		await session.DisposeAsync(); await appearance.DisposeAsync(); await resize.DisposeAsync(); await session.DisposeAsync();
		Assert.Equal(invalidated ? Array.Empty<string>() : new[] { "\u001b[?2031l", "\u001b[?2048l" }, wire.Writes.Skip(before));
	}
	[Fact]
	public async Task CleanupErrorsDoNotPreventOtherModeAndHostRestoration() {
		EnvironmentTestContext wire = new(); TerminalSession session = await wire.OpenAsync();
		TerminalAppearanceReportingLease appearance = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.FailWrite = "\u001b[?2031l"; bool restored = false;
		wire.OnHostRestore = () => restored = true;
		await Assert.ThrowsAnyAsync<Exception>(() => session.DisposeAsync().AsTask());
		Assert.Contains("\u001b[?2048l", wire.Writes); Assert.True(restored);
		int before = wire.Writes.Length; await appearance.DisposeAsync(); await resize.DisposeAsync(); Assert.Equal(before, wire.Writes.Length);
	}
	[Fact]
	public async Task CloseRestoresEnvironmentBeforeInputPresentationAndHost() {
		EnvironmentTestContext wire = new(); TerminalSession session = await wire.OpenAsync();
		_ = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		_ = (await session.AcquireInputProtocolsAsync(new() { BracketedPaste = true })).GetRequiredValue();
		_ = (await session.AcquirePresentationAsync(new() { AlternateScreen = true })).GetRequiredValue();
		wire.BeforeWrite = value => { if (value == "\u001b[?2031l") { Assert.True(wire.LifecycleDisposed); } return ValueTask.CompletedTask; };
		wire.OnHostRestore = () => Assert.Equal("<A->", wire.Writes.Last());
		await session.DisposeAsync();
		Assert.Equal(new[] { "\u001b[?2031l", "<P->", "<A->" }, wire.Writes.TakeLast(3));
	}

	[Fact]
	public async Task ExternalResumeWithoutPriorSuspendStillUsesObservationWindow() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		await using TerminalAppearanceReportingLease lease = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.AppearanceState = 1; wire.Signal(TerminalLifecycleSignalKind.Resume);
		Assert.Equal(TerminalLifecycleEventKind.Resumed, (await EnvironmentTestContext.LifecycleAsync(session)).Kind);
		Assert.True(session.IsStateValid); Assert.Equal(2, wire.Writes.Count(x => x == "\u001b[?2031$p"));
	}
	[Fact]
	public async Task FailedSecondModeReentryRollsBackFirstModeBeforeRestoringHost() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync();
		await using TerminalAppearanceReportingLease appearance = (await session.AcquireAppearanceReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		await using TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(TimeSpan.FromSeconds(5))).GetRequiredValue();
		wire.Signal(TerminalLifecycleSignalKind.Suspend); _ = await EnvironmentTestContext.LifecycleAsync(session);
		int before = wire.Writes.Length; wire.FailWrite = "\u001b[?2048h";
		wire.OnHostRestore = () => Assert.Contains("\u001b[?2031l", wire.Writes.Skip(before));
		wire.Signal(TerminalLifecycleSignalKind.Resume);
		await Assert.ThrowsAnyAsync<Exception>(() => EnvironmentTestContext.LifecycleAsync(session));
		Assert.Contains("\u001b[?2031l", wire.Writes.Skip(before)); Assert.False(session.IsStateValid);
		wire.OnHostRestore = null;
	}
}
