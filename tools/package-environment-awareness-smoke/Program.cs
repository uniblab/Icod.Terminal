/*
	Icod.Terminal.PackageEnvironmentAwarenessSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;

Assert((int)TerminalAppearance.Unknown == 0 && (int)TerminalAppearance.Dark == 1 && (int)TerminalAppearance.Light == 2, "Appearance values changed.");
Assert((int)TerminalSemanticEventKind.Notification == 0 && (int)TerminalSemanticEventKind.Appearance == 1 && (int)TerminalSemanticEventKind.InBandResize == 2, "Semantic values changed.");
Wire wire = new();
TerminalSession session = await TerminalSession.OpenAsync(wire, TerminalEndpoint.StandardInput, TerminalEndpoint.StandardOutput, wire, wire,
	new TerminalSessionOptions { TerminalOverride = TerminalProfiles.Dumb, ConfigureOutput = false, ObserveLifecycleEvents = false });
TimeSpan timeout = TimeSpan.FromSeconds(2);
Assert(await session.QueryAppearanceAsync(timeout) == TerminalAppearance.Dark, "Dark query failed.");
wire.Appearance = 2; Assert(await session.QueryAppearanceAsync(timeout) == TerminalAppearance.Light, "Light query failed.");
TerminalAppearanceReportingLease appearance = (await session.AcquireAppearanceReportingAsync(timeout)).GetRequiredValue();
TerminalAppearanceReportingLease nestedAppearance = (await session.AcquireAppearanceReportingAsync(timeout)).GetRequiredValue();
TerminalInBandResizeReportingLease resize = (await session.AcquireInBandResizeReportingAsync(timeout)).GetRequiredValue();
TerminalInBandResizeReportingLease nestedResize = (await session.AcquireInBandResizeReportingAsync(timeout)).GetRequiredValue();
TerminalEvent initial = await session.ReadEventAsync(timeout);
TerminalInBandResizeEvent initialResize = initial.Semantic?.InBandResize ?? throw new InvalidOperationException("Missing initial in-band event.");
Assert(initialResize.Dimensions == new TerminalDimensions(80, 24) && initialResize.PixelDimensions is null, "Initial dimensions were not typed.");
wire.Publish("\u001b[?997;2n\u001b[?997;2n\u001b[48;24;80;600;800t");
for (int index = 0; index < 2; ++index) {
	TerminalSemanticEvent semantic = (await session.ReadEventAsync(timeout)).Semantic ?? throw new InvalidOperationException("Missing semantic appearance event.");
	TerminalAppearanceEvent report = semantic.Appearance ?? throw new InvalidOperationException("Missing appearance payload.");
	Assert(report.Appearance == TerminalAppearance.Light && semantic.Notification is null && semantic.InBandResize is null, "Payload contract changed.");
}
TerminalInBandResizeEvent pixelResize = (await session.ReadEventAsync(timeout)).Semantic?.InBandResize ?? throw new InvalidOperationException("Missing pixel resize.");
Assert(pixelResize.Dimensions == initialResize.Dimensions && pixelResize.PixelDimensions == new TerminalPixelDimensions(800, 600), "Pixel-only resize was lost.");
Assert(session.GetDimensions().GetRequiredValue() == new TerminalDimensions(80, 24), "Native geometry changed.");
await appearance.DisposeAsync(); await resize.DisposeAsync();
Assert(wire.Writes.Count(value => value == "\u001b[?2031h") == 1 && wire.Writes.Count(value => value == "\u001b[?2048h") == 1, "Nested leases enabled more than once.");
Assert(!wire.Writes.Any(value => value.EndsWith("l", StringComparison.Ordinal)), "Non-final owners disabled reporting.");
await session.DisposeAsync(); await nestedAppearance.DisposeAsync(); await nestedResize.DisposeAsync();
Assert(wire.Writes.TakeLast(2).SequenceEqual(new[] { "\u001b[?2031l", "\u001b[?2048l" }), "Session cleanup order or retirement changed.");
Console.WriteLine("Icod.Terminal environment-awareness package smoke passed.");

static void Assert(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }

sealed class Wire : ITerminalInput, ITerminalOutput, ITerminalControlProvider {
	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private byte[]? pending;
	private int offset;
	internal int Appearance { get; set; } = 1;
	internal List<string> Writes { get; } = [];
	internal void Publish(string value) { this.input.Writer.TryWrite(Encoding.Latin1.GetBytes(value)); }
	public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
		if (this.pending is null) { this.pending = await this.input.Reader.ReadAsync(cancellationToken); this.offset = 0; }
		int count = Math.Min(buffer.Length, this.pending.Length - this.offset);
		this.pending.AsMemory(this.offset, count).CopyTo(buffer); this.offset += count;
		if (this.offset == this.pending.Length) { this.pending = null; }
		return count;
	}
	public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
		cancellationToken.ThrowIfCancellationRequested(); string value = Encoding.Latin1.GetString(buffer.Span); this.Writes.Add(value);
		if (value == "\u001b[?996n") { this.Publish($"\u001b[?997;{this.Appearance}n"); }
		if (value == "\u001b[?2031$p") { this.Publish("\u001b[?2031;2$y"); }
		if (value == "\u001b[?2048$p") { this.Publish("\u001b[?2048;2$y"); }
		if (value == "\u001b[?2048h") { this.Publish("\u001b[48;24;80;0;0t"); }
		return ValueTask.CompletedTask;
	}
	public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
	public TerminalControlResult<TerminalEndpointObservation> Observe(TerminalEndpoint endpoint) => TerminalControlResult<TerminalEndpointObservation>.Available(new(true, null, TerminalPlatformKind.PosixTermios, TerminalControlCapabilities.Attachment | TerminalControlCapabilities.ModeRead | TerminalControlCapabilities.ModeWrite | TerminalControlCapabilities.LiveSize));
	public TerminalControlResult<TerminalSize> GetSize(TerminalEndpoint endpoint) => TerminalControlResult<TerminalSize>.Available(new(80, 24));
	public TerminalControlResult<TerminalModeSnapshot> GetMode(TerminalEndpoint endpoint) => TerminalControlResult<TerminalModeSnapshot>.Available(TerminalModeSnapshot.CreatePosix(0, 0, 0, 2, new byte[32], 0, 32, 0, new TerminalSpeed(13, 9600), new TerminalSpeed(13, 9600)));
	public TerminalControlMutationResult SetMode(TerminalEndpoint endpoint, TerminalModeSnapshot mode, TerminalModeApplyTiming timing) => TerminalControlMutationResult.Success();
}
