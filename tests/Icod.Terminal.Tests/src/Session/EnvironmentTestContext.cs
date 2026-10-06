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

/// <summary>Controlled wire and lifecycle boundaries for environment ownership tests.</summary>
internal sealed class EnvironmentTestContext : ITerminalInput, ITerminalOutput,
	ITerminalControlProvider, ITerminalLifecycleSource, ITerminalSuspendController {
	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private readonly Channel<TerminalLifecycleSignal> signals = Channel.CreateUnbounded<TerminalLifecycleSignal>();
	private readonly Channel<string> emitted = Channel.CreateUnbounded<string>();
	private readonly List<string> writes = [];
	private readonly object sync = new();
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
		0, 0, 0, 2, new byte[32], 0, 32, 0, new TerminalSpeed(13, 9600), new TerminalSpeed(13, 9600));
	internal int AppearanceState { get; set; } = 2;
	internal int ResizeState { get; set; } = 2;
	internal bool Respond { get; set; } = true;
	internal string? FailWrite { get; set; }
	internal Func<string, ValueTask>? BeforeWrite { get; set; }
	internal Action? OnHostRestore { get; set; }
	internal bool LifecycleDisposed { get; private set; }
	internal string[] Writes { get { lock (this.sync) { return this.writes.ToArray(); } } }
	internal async ValueTask<TerminalSession> OpenAsync(bool lifecycle = true) => await TerminalSession.OpenAsync(
		this, TerminalEndpoint.StandardInput, TerminalEndpoint.StandardOutput, this, this,
		new TerminalSessionOptions {
			TerminalOverride = new TerminalDescriptionBuilder("environment-tests")
				.SetExtendedString("BE", "<P+>").SetExtendedString("BD", "<P->")
				.SetExtendedString("PS", "<PS>").SetExtendedString("PE", "<PE>")
				.SetString(StringCapability.EnterCursorAddressingMode, "<A+>")
				.SetString(StringCapability.ExitCursorAddressingMode, "<A->").Build(),
			ConfigureOutput = false,
			ObserveLifecycleEvents = false,
			LifecycleSource = lifecycle ? this : null
		});
	internal void Publish(string text) => Assert.True(this.input.Writer.TryWrite(Encoding.Latin1.GetBytes(text)));
	internal void EndInput() => this.input.Writer.TryWrite([]);
	internal void Signal(TerminalLifecycleSignalKind kind) => Assert.True(this.signals.Writer.TryWrite(new(kind)));
	internal async Task WaitForAsync(string expected) {
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
		while (expected != await this.emitted.Reader.ReadAsync(timeout.Token)) { }
	}
	internal static async Task<TerminalLifecycleEvent> LifecycleAsync(TerminalSession session) {
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
		return await session.ReadLifecycleEventAsync(timeout.Token);
	}
	public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
		byte[] bytes = await this.input.Reader.ReadAsync(cancellationToken);
		bytes.CopyTo(buffer); return bytes.Length;
	}
	public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
		cancellationToken.ThrowIfCancellationRequested();
		string value = Encoding.Latin1.GetString(buffer.Span);
		lock (this.sync) { this.writes.Add(value); }
		this.emitted.Writer.TryWrite(value);
		if (this.BeforeWrite is not null) { await this.BeforeWrite(value); }
		if (value == this.FailWrite) { throw new IOException("Controlled wire failure."); }
		if (this.Respond) {
			if (value == "\u001b[?2031$p") { this.Publish($"\u001b[?2031;{this.AppearanceState}$y"); }
			if (value == "\u001b[?2048$p") { this.Publish($"\u001b[?2048;{this.ResizeState}$y"); }
			if (value == "\u001b[?996n") { this.Publish("\u001b[?997;1n"); }
		}
	}
	public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
	public TerminalControlResult<TerminalEndpointObservation> Observe(TerminalEndpoint endpoint) =>
		TerminalControlResult<TerminalEndpointObservation>.Available(new(true, null, TerminalPlatformKind.PosixTermios,
			TerminalControlCapabilities.Attachment | TerminalControlCapabilities.ModeRead | TerminalControlCapabilities.ModeWrite | TerminalControlCapabilities.LiveSize));
	public TerminalControlResult<TerminalSize> GetSize(TerminalEndpoint endpoint) => TerminalControlResult<TerminalSize>.Available(new(80, 24));
	public TerminalControlResult<TerminalModeSnapshot> GetMode(TerminalEndpoint endpoint) => TerminalControlResult<TerminalModeSnapshot>.Available(this.baseline);
	public TerminalControlMutationResult SetMode(TerminalEndpoint endpoint, TerminalModeSnapshot mode, TerminalModeApplyTiming timing) {
		if (ReferenceEquals(mode, this.baseline)) { this.OnHostRestore?.Invoke(); }
		return TerminalControlMutationResult.Success();
	}
	ValueTask<TerminalLifecycleSignal> ITerminalLifecycleSource.ReadAsync(CancellationToken cancellationToken) => this.signals.Reader.ReadAsync(cancellationToken);
	public TerminalControlMutationResult SuspendCurrentProcess() => TerminalControlMutationResult.Success();
	public void Dispose() { this.LifecycleDisposed = true; this.signals.Writer.TryComplete(); }
}
