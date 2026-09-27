/*
	Icod.Terminal.PackageCapabilityPlanningSmoke
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
namespace Icod.Terminal.CapabilityPlanning.Smoke;

using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Icod.TermInfo;
using Icod.Terminal.CapabilityPlanning.Sample;

// Test-host fixture only. The linked sample consumes Terminal-owned APIs exclusively.
internal static class CapabilityPlanningScenario {
	internal static async Task RunAsync( bool outputAvailable ) {
		Transport transport = new();
		Control control = new( outputAvailable );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "capability-sample" )
			.SetString( StringCapability.CursorHome, "H" )
			.SetString( StringCapability.CursorDownOne, "D" )
			.SetString( StringCapability.CursorRightOne, "R" )
			.SetString( StringCapability.ClearToEndOfLine, "" ).Build();
		await using TerminalSession session = await TerminalSession.OpenAsync(
			control, TerminalEndpoint.StandardInput, TerminalEndpoint.StandardOutput, transport, transport,
			new TerminalSessionOptions { TerminalOverride = terminal, ConfigureOutput = false,
				ObserveLifecycleEvents = false, RequireInteractiveOutput = false }
		);
		int modesAfterOpen = control.ModeWrites;
		using StringWriter report = new();
		await CapabilityPlanningExample.WriteReportAsync( session, report, false );
		string initial = report.ToString();
		Require( Enum.GetValues<TerminalCapability>().Length == 12, "Expected twelve capabilities." );
		foreach ( TerminalCapability capability in Enum.GetValues<TerminalCapability>() ) {
			Require( initial.Split('\n').Count( line => line.StartsWith( capability + ": support=", StringComparison.Ordinal ) ) == 1,
				"Missing or duplicate sample status: " + capability );
		}
		Require( initial.Contains( "absolute=False, home=True", StringComparison.Ordinal ), "Static advertisement missing." );
		Require( initial.Contains( "Cursor to (2,3), current unknown: available, 6 bytes", StringComparison.Ordinal ), "Alternative cursor plan rejected." );
		Require( initial.Contains( "Erase to end of line: available, 0 bytes", StringComparison.Ordinal ), "Empty representation lost." );
		Require( initial.Contains( "Insert one character: unavailable", StringComparison.Ordinal ), "Missing operation was invented." );
		Require( initial.Contains( "support=Unknown", StringComparison.Ordinal ), "Unknown evidence was promoted." );
		Require( initial.Contains( "endpoint=" + (outputAvailable ? "Available" : "Unavailable"), StringComparison.Ordinal ), "Endpoint missing." );
		Require( transport.Writes == 0 && transport.Reads == 0 && control.ModeWrites == modesAfterOpen, "Default report caused terminal I/O or mode changes." );
		foreach ( TerminalCapability capability in Enum.GetValues<TerminalCapability>() ) {
			if ( capability is TerminalCapability.KeyboardReporting or TerminalCapability.RasterGraphics or TerminalCapability.PersistentRasterGraphics ) continue;
			TerminalCapabilityStatus before = session.InspectCapability( capability );
			TerminalCapabilityStatus after = await session.VerifyCapabilityAsync( capability );
			Require( before.Equals( after ), "Inspection-only capability changed: " + capability );
		}
		Require( transport.Writes == 0 && transport.Reads == 0, "Inspection-only verification caused I/O." );
		report.GetStringBuilder().Clear();
		await CapabilityPlanningExample.WriteReportAsync( session, report, true );
		string verified = report.ToString();
		Require( verified.Split('\n').Count( line => line.StartsWith( "Verified result:", StringComparison.Ordinal ) ) == 3, "Wrong verification selection." );
		Require( control.ModeWrites == modesAfterOpen, "Verification enabled a reporting mode." );
		Require( outputAvailable ? transport.KeyboardQueries == 1 && transport.GraphicsQueries == 1 : transport.Writes == 0,
			"Unexpected verification traffic." );
		if ( outputAvailable ) {
			foreach ( TerminalCapability capability in new[] { TerminalCapability.KeyboardReporting, TerminalCapability.RasterGraphics, TerminalCapability.PersistentRasterGraphics } ) {
				Require( session.InspectCapability( capability ).Support == TerminalCapabilitySupport.Verified, "Support not verified: " + capability );
			}
		}
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		try {
			await CapabilityPlanningExample.WriteReportAsync( session, report, true, cancellation.Token );
			throw new InvalidOperationException( "Cancelled report succeeded." );
		} catch ( OperationCanceledException ) when ( cancellation.IsCancellationRequested ) { }
	}

	private static void Require( bool condition, string message ) {
		if ( !condition ) throw new InvalidOperationException( message );
	}

	private sealed class Transport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte> replies = Channel.CreateUnbounded<byte>();
		internal int Reads;
		internal int Writes;
		internal int KeyboardQueries;
		internal int GraphicsQueries;
		public async ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			Interlocked.Increment( ref this.Reads );
			byte first = await this.replies.Reader.ReadAsync( cancellationToken );
			buffer.Span[0] = first;
			int length = 1;
			while ( length < buffer.Length && this.replies.Reader.TryRead( out byte value ) ) buffer.Span[length++] = value;
			return length;
		}
		public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.Writes;
			string request = Encoding.ASCII.GetString( buffer.Span );
			string response;
			if ( request == "\u001b[?u\u001b[c" ) {
				++this.KeyboardQueries;
				response = "\u001b[?1u\u001b[?64;4c";
			} else if ( Regex.IsMatch( request, "^\u001b_G.*a=q.*\u001b\\\\\u001b\\[c$" ) ) {
				++this.GraphicsQueries;
				string id = Regex.Match( request, @"i=([0-9]+)," ).Groups[1].Value;
				Require( id.Length > 0, "Graphics correlation missing." );
				response = $"\u001b_Gi={id};OK\u001b\\\u001b[?64;4c";
			} else {
				throw new InvalidOperationException( "Unexpected sample output: " + Convert.ToHexString( buffer.Span ) );
			}
			foreach ( byte value in Encoding.ASCII.GetBytes( response ) ) this.replies.Writer.TryWrite( value );
			return ValueTask.CompletedTask;
		}
		public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class Control( bool outputAvailable ) : ITerminalControlProvider {
		internal int ModeWrites;
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix( 0, 0, 0, 2, new byte[32], 0, 32, 0,
			new TerminalSpeed( 13, 9600 ), new TerminalSpeed( 13, 9600 ) );
		public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) {
			bool attached = outputAvailable || ReferenceEquals( endpoint, TerminalEndpoint.StandardInput );
			return TerminalControlResult<TerminalEndpointObservation>.Available( new TerminalEndpointObservation(
				attached, null, attached ? TerminalPlatformKind.PosixTermios : null,
				attached ? TerminalControlCapabilities.Attachment | TerminalControlCapabilities.ModeRead | TerminalControlCapabilities.ModeWrite : TerminalControlCapabilities.None ) );
		}
		public TerminalControlResult<TerminalSize> GetSize( TerminalEndpoint endpoint ) => TerminalControlResult<TerminalSize>.Unavailable( "Scripted fixture." );
		public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) => TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
		public TerminalControlMutationResult SetMode( TerminalEndpoint endpoint, TerminalModeSnapshot mode, TerminalModeApplyTiming timing ) {
			++this.ModeWrites;
			return TerminalControlMutationResult.Success();
		}
	}
}