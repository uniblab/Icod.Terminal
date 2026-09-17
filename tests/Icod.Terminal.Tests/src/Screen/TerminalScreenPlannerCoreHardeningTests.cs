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
namespace Icod.Terminal.Tests.Screen;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Hardens literal cost and exact output for core screen-planner candidates.</summary>
public sealed class TerminalScreenPlannerCoreHardeningTests {
	[Fact]
	public async Task AbsoluteCursorCandidateHasLiteralCostAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "absolute-cursor" )
			.SetString( StringCapability.CursorAddress, "<A:%p1%d,%p2%d>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			null,
			new TerminalScreenPosition( 2, 4 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( TerminalScreenOperationKind.CursorMove, plan.Kind );
		Assert.Equal( 7, plan.ByteCount );
		Assert.Equal( 1, plan.AffectedLines );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "<A:2,4>" ), output.Bytes );
	}

	[Fact]
	public async Task HomeCandidateWinsWhenCheaperWithLiteralCostAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "home-cursor" )
			.SetString( StringCapability.CursorAddress, "absolute" )
			.SetString( StringCapability.CursorHome, "H" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			null,
			new TerminalScreenPosition( 0, 0 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 1, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "H" ), output.Bytes );
	}

	[Fact]
	public async Task EqualCostCursorCandidatesRetainDeterministicDiscoveryOrder() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "cursor-tie" )
			.SetString( StringCapability.CursorAddress, "A" )
			.SetString( StringCapability.CursorHome, "H" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			null,
			new TerminalScreenPosition( 0, 0 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 1, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "A" ), output.Bytes );
	}

	[Fact]
	public async Task RowAndColumnCandidateCombinesInOrderWithLiteralCostAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "row-column-cursor" )
			.SetString( StringCapability.RowAddress, "<r:%p1%d>" )
			.SetString( StringCapability.ColumnAddress, "<c:%p1%d>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			null,
			new TerminalScreenPosition( 3, 5 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 10, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "<r:3><c:5>" ), output.Bytes );
	}

	[Fact]
	public async Task CurrentPositionEnablesCarriageReturnCandidate() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "carriage-return-cursor" )
			.SetString( StringCapability.CursorAddress, "AAAA" )
			.SetString( StringCapability.CarriageReturn, "C" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan unknownCurrent = session.Screen.PlanCursorMove(
			null,
			new TerminalScreenPosition( 3, 0 )
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan knownCurrent = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 3, 7 ),
			new TerminalScreenPosition( 3, 0 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 4, unknownCurrent.ByteCount );
		Assert.Equal( 1, knownCurrent.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, unknownCurrent, knownCurrent );

		Assert.Equal( Encoding.Latin1.GetBytes( "AAAAC" ), output.Bytes );
	}

	[Fact]
	public async Task CurrentPositionSelectsIndividualRowOrColumnCandidate() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "axis-cursor" )
			.SetString( StringCapability.RowAddress, "<r:%p1%d>" )
			.SetString( StringCapability.ColumnAddress, "<c:%p1%d>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan sameRow = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 4, 1 ),
			new TerminalScreenPosition( 4, 6 )
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan sameColumn = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 1, 6 ),
			new TerminalScreenPosition( 4, 6 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 5, sameRow.ByteCount );
		Assert.Equal( 5, sameColumn.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, sameRow, sameColumn );

		Assert.Equal( Encoding.Latin1.GetBytes( "<c:6><r:4>" ), output.Bytes );
	}

	[Theory]
	[InlineData( StringCapability.CursorUp, 5, 0, 2, 0, "<U:%p1%d>", "<U:3>" )]
	[InlineData( StringCapability.CursorDown, 2, 0, 5, 0, "<D:%p1%d>", "<D:3>" )]
	[InlineData( StringCapability.CursorLeft, 0, 5, 0, 2, "<L:%p1%d>", "<L:3>" )]
	[InlineData( StringCapability.CursorRight, 0, 2, 0, 5, "<R:%p1%d>", "<R:3>" )]
	public async Task ParameterizedRelativeCursorCandidatesHaveLiteralCostAndExactOutput(
		StringCapability capability,
		int currentRow,
		int currentColumn,
		int targetRow,
		int targetColumn,
		string source,
		string expected
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "parameterized-relative-cursor" )
			.SetString( capability, source )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( currentRow, currentColumn ),
			new TerminalScreenPosition( targetRow, targetColumn )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 5, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( expected ), output.Bytes );
	}

	[Theory]
	[InlineData( StringCapability.CursorUp, StringCapability.CursorUpOne, 5, 0, 2, 0, "uuu" )]
	[InlineData( StringCapability.CursorDown, StringCapability.CursorDownOne, 2, 0, 5, 0, "ddd" )]
	[InlineData( StringCapability.CursorLeft, StringCapability.CursorLeftOne, 0, 5, 0, 2, "lll" )]
	[InlineData( StringCapability.CursorRight, StringCapability.CursorRightOne, 0, 2, 0, 5, "rrr" )]
	public async Task RepeatedRelativeCursorCandidatesWinWhenCheaper(
		StringCapability parameterizedCapability,
		StringCapability oneCapability,
		int currentRow,
		int currentColumn,
		int targetRow,
		int targetColumn,
		string expected
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "repeated-relative-cursor" )
			.SetString( parameterizedCapability, "PARAM" )
			.SetString( oneCapability, expected[ 0 ].ToString() )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( currentRow, currentColumn ),
			new TerminalScreenPosition( targetRow, targetColumn )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 3, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( expected ), output.Bytes );
	}

	[Fact]
	public async Task EqualCostRelativeCandidatesPreferParameterizedFormDeterministically() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "relative-cursor-tie" )
			.SetString( StringCapability.CursorRight, "PPP" )
			.SetString( StringCapability.CursorRightOne, "r" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 0, 2 ),
			new TerminalScreenPosition( 0, 5 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 3, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "PPP" ), output.Bytes );
	}

	[Fact]
	public async Task RelativeCursorCostExcludesTermInfoPaddingMarkers() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "padding-relative-cursor" )
			.SetString( StringCapability.CursorRight, "PPPP" )
			.SetString( StringCapability.CursorRightOne, "R$<0>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 0, 1 ),
			new TerminalScreenPosition( 0, 4 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 3, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "RRR" ), output.Bytes );
	}

	[Fact]
	public async Task UnavailableCursorCandidatesReturnNoPlanWithoutOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "no-cursor" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan? plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 1, 1 ),
			new TerminalScreenPosition( 2, 2 )
		);

		Assert.Null( plan );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task SameCursorPositionProducesZeroCostExactEmptyOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "stationary-cursor" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 2, 2 ),
			new TerminalScreenPosition( 2, 2 )
		) ?? throw new InvalidOperationException();

		Assert.Equal( 0, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task EverySemanticLineGlyphResolvesAndEmitsExactMappedContent() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "line-glyphs" )
			.SetString( StringCapability.AlternateCharacterSet, "qHxVlAkBmCjDvEwFuGtInJ" )
			.SetString( StringCapability.EnterAlternateCharacterSetMode, "E" )
			.SetString( StringCapability.ExitAlternateCharacterSetMode, "X" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalLineGlyph[] glyphs = [
			TerminalLineGlyph.Horizontal,
			TerminalLineGlyph.Vertical,
			TerminalLineGlyph.UpperLeftCorner,
			TerminalLineGlyph.UpperRightCorner,
			TerminalLineGlyph.LowerLeftCorner,
			TerminalLineGlyph.LowerRightCorner,
			TerminalLineGlyph.TeeUp,
			TerminalLineGlyph.TeeDown,
			TerminalLineGlyph.TeeLeft,
			TerminalLineGlyph.TeeRight,
			TerminalLineGlyph.Crossing
		];
		string[] expected = [ "H", "V", "A", "B", "C", "D", "E", "F", "G", "I", "J" ];
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.Add(
			session.Screen.PlanAlternateCharacterSet( enabled: true )
				?? throw new InvalidOperationException()
		);

		for ( int index = 0; index < glyphs.Length; ++index ) {
			TerminalLineGlyphRepresentation representation = Assert.IsType<TerminalLineGlyphRepresentation>(
				session.Screen.ResolveLineGlyph( glyphs[ index ] )
			);
			Assert.Equal( expected[ index ], representation.Content );
			Assert.True( representation.UsesAlternateCharacterSet );
			transaction.WriteText( representation.Content );
		}
		transaction.Add(
			session.Screen.PlanAlternateCharacterSet( enabled: false )
				?? throw new InvalidOperationException()
		);
		Assert.Empty( output.Bytes );

		await transaction.CommitAsync();

		Assert.Equal( Encoding.Latin1.GetBytes( "EHVABCDEFGIJX" ), output.Bytes );
	}

	[Theory]
	[InlineData( "qH", null, null, TerminalLineGlyph.Horizontal )]
	[InlineData( "qH", "E", "X", TerminalLineGlyph.Vertical )]
	[InlineData( "q\u0001", "E", "X", TerminalLineGlyph.Horizontal )]
	public async Task UnusableLineGlyphMappingsReturnNoRepresentationWithoutOutput(
		string mapping,
		string? enter,
		string? exit,
		TerminalLineGlyph glyph
	) {
		TerminalDescriptionBuilder builder = new TerminalDescriptionBuilder( "unusable-line-glyph" )
			.SetString( StringCapability.AlternateCharacterSet, mapping );
		if ( enter is not null ) {
			builder.SetString( StringCapability.EnterAlternateCharacterSetMode, enter );
		}
		if ( exit is not null ) {
			builder.SetString( StringCapability.ExitAlternateCharacterSetMode, exit );
		}
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, builder.Build() );

		TerminalLineGlyphRepresentation? representation = session.Screen.ResolveLineGlyph( glyph );

		Assert.Null( representation );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task AlternateCharacterSetEntryAndExitHaveLiteralCostsAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "acs-plans" )
			.SetString( StringCapability.AlternateCharacterSet, "qH" )
			.SetString( StringCapability.EnterAlternateCharacterSetMode, "E$<0>" )
			.SetString( StringCapability.ExitAlternateCharacterSetMode, "XX$<0>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan enter = session.Screen.PlanAlternateCharacterSet(
			enabled: true
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan exit = session.Screen.PlanAlternateCharacterSet(
			enabled: false
		) ?? throw new InvalidOperationException();

		Assert.Equal( TerminalScreenOperationKind.AlternateCharacterSet, enter.Kind );
		Assert.Equal( TerminalScreenOperationKind.AlternateCharacterSet, exit.Kind );
		Assert.Equal( 1, enter.ByteCount );
		Assert.Equal( 2, exit.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, enter, exit );

		Assert.Equal( Encoding.Latin1.GetBytes( "EXX" ), output.Bytes );
	}

	[Fact]
	public async Task UnavailableAlternateCharacterSetEntryAndExitReturnNoPlan() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "no-acs-plans" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		Assert.Null( session.Screen.PlanAlternateCharacterSet( enabled: true ) );
		Assert.Null( session.Screen.PlanAlternateCharacterSet( enabled: false ) );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task AlertPreferencesHaveLiteralCostsAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "alert-preferences" )
			.SetString( StringCapability.Bell, "B$<0>" )
			.SetString( StringCapability.FlashScreen, "VV$<0>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan audible = session.Screen.PlanAlert(
			TerminalAlertKind.Audible
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan visual = session.Screen.PlanAlert(
			TerminalAlertKind.Visual
		) ?? throw new InvalidOperationException();

		Assert.Equal( TerminalScreenOperationKind.Alert, audible.Kind );
		Assert.Equal( TerminalScreenOperationKind.Alert, visual.Kind );
		Assert.Equal( 1, audible.ByteCount );
		Assert.Equal( 2, visual.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, audible, visual );

		Assert.Equal( Encoding.Latin1.GetBytes( "BVV" ), output.Bytes );
	}

	[Theory]
	[InlineData( TerminalAlertKind.Audible, StringCapability.FlashScreen, "V" )]
	[InlineData( TerminalAlertKind.Visual, StringCapability.Bell, "B" )]
	public async Task AlertUsesOppositePresentationAsExactFallback(
		TerminalAlertKind requested,
		StringCapability fallbackCapability,
		string expected
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "alert-fallback" )
			.SetString( fallbackCapability, expected )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanAlert( requested )
			?? throw new InvalidOperationException();

		Assert.Equal( 1, plan.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( expected ), output.Bytes );
	}

	[Fact]
	public async Task UnavailableAlertsReturnNoPlanWithoutOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "no-alerts" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		Assert.Null( session.Screen.PlanAlert( TerminalAlertKind.Audible ) );
		Assert.Null( session.Screen.PlanAlert( TerminalAlertKind.Visual ) );
		Assert.Empty( output.Bytes );
	}

	private static async ValueTask CommitAsync(
		TerminalSession session,
		params TerminalScreenOperationPlan[] plans
	) {
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		foreach ( TerminalScreenOperationPlan plan in plans ) {
			transaction.Add( plan );
		}
		await transaction.CommitAsync();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalOutput output,
		TerminalDescription terminal
	) {
		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ObserveLifecycleEvents = false
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
		internal List<byte> Bytes {
			get;
		} = [];

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
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
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
						| TerminalControlCapabilities.LiveSize
				)
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) => TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) => TerminalControlResult<TerminalSize>.Available( new TerminalSize( 80, 24 ) );

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) => TerminalControlMutationResult.Success();
	}
}
