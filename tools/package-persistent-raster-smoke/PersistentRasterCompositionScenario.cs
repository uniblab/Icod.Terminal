/*
	Icod.Terminal.PackagePersistentRasterSmoke
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
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Icod.TermInfo;
using Icod.Terminal;
using Icod.Terminal.RasterAnimation.Sample;

internal static class PersistentRasterCompositionScenario {
	internal static Task RunAsync() => RunCoreAsync().WaitAsync( TimeSpan.FromSeconds( 30 ) );

	private static async Task RunCoreAsync() {
		const int frameWidth = 16;
		const int frameHeight = 16;
		using CancellationTokenSource deadline = new( TimeSpan.FromSeconds( 20 ) );
		ScriptedTerminal transport = new();
		await using TerminalSession session = await TerminalSession.OpenAsync(
			new Control(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			},
			deadline.Token
		);
		Require(
			await RasterAnimationCompositionExample.VerifyPrerequisiteAsync(
				session, deadline.Token
			),
			"The package-only sample preflight did not establish usable graphics."
		);
		Require(
			TerminalCapabilitySupport.Unknown == session.InspectCapability(
				TerminalCapability.PersistentRasterAnimation
			).Support,
			"The preflight claimed unobserved animation support."
		);

		TerminalControlResult<TerminalRasterResource> created =
			await session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					frameWidth,
					frameHeight,
					new byte[frameWidth * frameHeight * 3]
				),
				deadline.Token
			);
		Require( TerminalControlStatus.Available == created.Status, "Root image creation failed." );
		await using TerminalRasterResource resource = created.Value
			?? throw new InvalidOperationException( "Root image token was not returned." );
		TerminalControlResult<TerminalRasterAnimationFrame> appended =
			await resource.Animation.AddFrameAsync(
				TerminalRasterImage.CreateRgb24(
					frameWidth,
					frameHeight,
					new byte[frameWidth * frameHeight * 3]
				),
				TimeSpan.FromMilliseconds( 180 ),
				deadline.Token
			);
		Require( TerminalControlStatus.Available == appended.Status, "Tile frame append failed." );
		TerminalRasterAnimationFrame tile = appended.Value
			?? throw new InvalidOperationException( "Tile frame token was not returned." );
		TerminalControlMutationResult updated = await RasterAnimationCompositionExample
			.UpdateRegionAsync( resource.Animation, tile, deadline.Token );
		Require( updated.Succeeded, "Package-only partial frame replacement failed." );
		TerminalControlMutationResult composed = await RasterAnimationCompositionExample
			.ComposeAsync( resource.Animation, tile, deadline.Token );
		Require( composed.Succeeded, "Package-only frame composition failed." );
		Require(
			transport.Probes == 1 && transport.PersistentProbes == 1
				&& transport.Roots == 1
				&& transport.Appends == 1 && transport.PartialUpdates == 1
				&& transport.Compositions == 1,
			"The sample did not execute exactly one verified graphics, partial update, and composition path."
		);
		Require(
			TerminalRasterAnimationStatus.Current == resource.Animation.State.Status,
			"Composition changed the known frame sequence."
		);

		await MeasurePartialTransferAsync(
			resource.Animation,
			tile,
			transport,
			frameWidth,
			frameHeight,
			deadline.Token
		);
		await PersistentRasterTileAtlasScenario.RunAsync(
			session,
			resource,
			tile,
			transport,
			deadline.Token
		);
	}

	private static async Task MeasurePartialTransferAsync(
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame frame,
		ScriptedTerminal transport,
		int frameWidth,
		int frameHeight,
		CancellationToken cancellationToken
	) {
		const int partialWidth = 4;
		const int partialHeight = 4;
		const int iterations = 32;
		TerminalRasterImage full = TerminalRasterImage.CreateRgba32(
			frameWidth,
			frameHeight,
			new byte[frameWidth * frameHeight * 4]
		);
		TerminalRasterImage partial = TerminalRasterImage.CreateRgba32(
			partialWidth,
			partialHeight,
			new byte[partialWidth * partialHeight * 4]
		);

		Require(
			( await animation.UpdateFrameRegionAsync(
				frame, full, 0, 0, cancellationToken
			) ).Succeeded,
			"Full-frame measurement warmup failed."
		);
		Require(
			( await animation.UpdateFrameRegionAsync(
				frame, partial, 0, 0, cancellationToken
			) ).Succeeded,
			"Partial-frame measurement warmup failed."
		);

		Measurement fullMeasurement = await MeasureAsync(
			animation,
			frame,
			full,
			iterations,
			transport,
			cancellationToken
		);
		Measurement partialMeasurement = await MeasureAsync(
			animation,
			frame,
			partial,
			iterations,
			transport,
			cancellationToken
		);

		Require(
			partialMeasurement.WireBytes < fullMeasurement.WireBytes,
			"A bounded region must transfer fewer measured wire bytes than the full frame."
		);
		Console.WriteLine(
			string.Concat(
				FormattableString.Invariant(
					$"Partial-frame measurement {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}: "
				),
				FormattableString.Invariant(
					$"full={frameWidth}x{frameHeight}, partial={partialWidth}x{partialHeight}, iterations={iterations}; "
				),
				FormattableString.Invariant(
					$"full wire={fullMeasurement.WireBytes} B/op, first={fullMeasurement.FirstLatency.TotalMicroseconds:F1} us, "
				),
				FormattableString.Invariant(
					$"total={fullMeasurement.TotalLatency.TotalMilliseconds:F3} ms, cpu={fullMeasurement.CpuTime.TotalMilliseconds:F3} ms, "
				),
				FormattableString.Invariant(
					$"allocated={fullMeasurement.AllocatedBytes} B; partial wire={partialMeasurement.WireBytes} B/op, "
				),
				FormattableString.Invariant(
					$"first={partialMeasurement.FirstLatency.TotalMicroseconds:F1} us, total={partialMeasurement.TotalLatency.TotalMilliseconds:F3} ms, "
				),
				FormattableString.Invariant(
					$"cpu={partialMeasurement.CpuTime.TotalMilliseconds:F3} ms, allocated={partialMeasurement.AllocatedBytes} B."
				)
			)
		);
	}

	private static async Task<Measurement> MeasureAsync(
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame frame,
		TerminalRasterImage image,
		int iterations,
		ScriptedTerminal transport,
		CancellationToken cancellationToken
	) {
		transport.BeginMeasurement();
		long allocatedBefore = GC.GetTotalAllocatedBytes( precise: true );
		using Process process = Process.GetCurrentProcess();
		TimeSpan cpuBefore = process.TotalProcessorTime;
		Stopwatch total = Stopwatch.StartNew();
		Stopwatch first = Stopwatch.StartNew();
		for ( int index = 0; index < iterations; ++index ) {
			TerminalControlMutationResult result = await animation.UpdateFrameRegionAsync(
				frame,
				image,
				0,
				0,
				cancellationToken
			);
			Require( result.Succeeded, "Measured partial-frame operation failed." );
			if ( 0 == index ) first.Stop();
		}
		total.Stop();
		TimeSpan cpu = process.TotalProcessorTime - cpuBefore;
		long allocated = GC.GetTotalAllocatedBytes( precise: true ) - allocatedBefore;
		long wireBytes = transport.EndMeasurement( iterations );
		return new Measurement( wireBytes, first.Elapsed, total.Elapsed, cpu, allocated );
	}

	private static void Require( bool condition, string message ) {
		if ( !condition ) throw new InvalidOperationException( message );
	}

	internal sealed class ScriptedTerminal : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> responses = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
		);
		internal int Probes;
		internal int PersistentProbes;
		internal int Roots;
		internal int Appends;
		internal int PartialUpdates;
		internal int Compositions;
		private int measuredWrites;
		private long measuredBytes;
		private bool measuring;

		internal void BeginMeasurement() {
			Require( !this.measuring, "A wire measurement is already active." );
			this.measuredWrites = 0;
			this.measuredBytes = 0;
			this.measuring = true;
		}

		internal long EndMeasurement( int expectedWrites ) {
			long totalBytes = this.EndMeasurementTotal( expectedWrites );
			Require(
				0 == totalBytes % expectedWrites,
				"Measured writes did not have a stable byte count."
			);
			return totalBytes / expectedWrites;
		}

		internal long EndMeasurementTotal( int expectedWrites ) {
			Require( this.measuring, "No wire measurement is active." );
			this.measuring = false;
			Require(
				expectedWrites == this.measuredWrites,
				"The measured update did not emit exactly one write per operation."
			);
			return this.measuredBytes;
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer, CancellationToken cancellationToken = default
		) {
			byte[] response = await this.responses.Reader.ReadAsync( cancellationToken );
			Require( response.Length <= buffer.Length, "Scripted response exceeds input buffer." );
			response.AsSpan().CopyTo( buffer.Span );
			return response.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string request = Encoding.ASCII.GetString( buffer.Span );
			if ( request.StartsWith( "\u001b_Gi=", StringComparison.Ordinal )
				&& request.Contains( ",a=q,", StringComparison.Ordinal )
				&& request.EndsWith( "\u001b[c", StringComparison.Ordinal ) ) {
				++this.Probes;
				int end = request.IndexOf( ',', "\u001b_Gi=".Length );
				Require( end > 4, "Graphics probe image identity is missing." );
				string id = request.Substring( "\u001b_Gi=".Length,
					end - "\u001b_Gi=".Length );
				this.Publish( $"\u001b_Gi={id};OK\u001b\\\u001b[?64;4c" );
			} else if ( request.StartsWith( "\u001b_Ga=t,", StringComparison.Ordinal ) ) {
				int start = request.IndexOf( ",I=", StringComparison.Ordinal );
				Require( start >= 0, "Root upload did not carry an opaque image number." );
				start += 3;
				int end = request.IndexOf( ',', start );
				if ( end < 0 ) end = request.IndexOf( ';', start );
				Require( end > start, "Root upload image number was malformed." );
				uint imageNumber = uint.Parse( request.AsSpan( start, end - start ),
					NumberStyles.None, CultureInfo.InvariantCulture );
				if ( 0 != ( imageNumber & 0x80000000u ) ) {
					++this.PersistentProbes;
				} else {
					++this.Roots;
				}
				this.Publish( $"\u001b_Gi=77,I={imageNumber};OK\u001b\\" );
			} else if ( request.StartsWith( "\u001b_Ga=f,", StringComparison.Ordinal )
				&& request.Contains( ",r=", StringComparison.Ordinal ) ) {
				if ( this.measuring ) {
					++this.measuredWrites;
					this.measuredBytes += buffer.Length;
				} else if ( request
					== "\u001b_Ga=f,f=32,s=1,v=1,t=d,i=77,r=2,x=0,y=1,X=1,m=0;IOCg/w==\u001b\\" ) {
					++this.PartialUpdates;
				}
				this.Publish( "\u001b_Gi=77;OK\u001b\\" );
			} else if ( request.StartsWith( "\u001b_Ga=f,", StringComparison.Ordinal ) ) {
				++this.Appends;
				this.Publish( "\u001b_Gi=77;OK\u001b\\" );
			} else if ( request.StartsWith( "\u001b_Ga=c,", StringComparison.Ordinal ) ) {
				Require(
					request == "\u001b_Ga=c,i=77,r=1,c=2,w=1,h=1,X=0,Y=0,x=1,y=1,C=1\u001b\\",
					"The package consumer emitted unexpected composition geometry or identities."
				);
				++this.Compositions;
				this.Publish( "\u001b_Gi=77;OK\u001b\\" );
			} else if ( request.StartsWith( "\u001b_Ga=a,", StringComparison.Ordinal ) ) {
				this.Publish( "\u001b_Gi=77;OK\u001b\\" );
			} else if ( !request.StartsWith( "\u001b_Ga=d,", StringComparison.Ordinal ) ) {
				throw new InvalidOperationException( "Unexpected package witness output: "
					+ Convert.ToHexString( buffer.Span ) );
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		private void Publish( string response ) {
			Require( this.responses.Writer.TryWrite( Encoding.ASCII.GetBytes( response ) ),
				"The scripted terminal response channel is closed." );
		}
	}

	private readonly record struct Measurement(
		long WireBytes,
		TimeSpan FirstLatency,
		TimeSpan TotalLatency,
		TimeSpan CpuTime,
		long AllocatedBytes
	);

	private sealed class Control : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0, 0, 0, 2, new byte[32], 0, 32, 0,
			new TerminalSpeed( 13, 9600 ), new TerminalSpeed( 13, 9600 )
		);
		public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) =>
			TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true, null, TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		public TerminalControlResult<TerminalSize> GetSize( TerminalEndpoint endpoint ) =>
			TerminalControlResult<TerminalSize>.Unavailable( "Scripted terminal." );
		public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) =>
			TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint, TerminalModeSnapshot mode, TerminalModeApplyTiming timing
		) => TerminalControlMutationResult.Success();
	}
}
