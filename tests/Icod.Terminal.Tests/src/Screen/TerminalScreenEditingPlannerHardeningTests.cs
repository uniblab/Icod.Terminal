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

/// <summary>Hardens erase, shift, scroll, and scroll-region planning.</summary>
public sealed class TerminalScreenEditingPlannerHardeningTests {
	[Theory]
	[InlineData( TerminalScreenEraseKind.ToEndOfLine, StringCapability.ClearToEndOfLine, "EL" )]
	[InlineData( TerminalScreenEraseKind.ToBeginningOfLine, StringCapability.ClearToBeginningOfLine, "BL" )]
	[InlineData( TerminalScreenEraseKind.ToEndOfScreen, StringCapability.ClearToEndOfScreen, "ES" )]
	[InlineData( TerminalScreenEraseKind.Screen, StringCapability.ClearScreen, "SC" )]
	public async Task EveryEraseKindHasLiteralCostAndExactOutput(
		TerminalScreenEraseKind kind,
		StringCapability capability,
		string expected
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "erase-kind" )
			.SetString( capability, expected + "$<0>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanErase(
			kind,
			affectedLines: 3
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			TerminalScreenOperationKind.Erase,
			byteCount: 2,
			affectedLines: 3,
			expected
		);
	}

	[Fact]
	public async Task ErasePaddingUsesAffectedLinesButDoesNotIncreasePlanCost() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "erase-padding" )
			.SetString( StringCapability.ClearScreen, "E$<2*/>" )
			.Build();
		RecordingDelayProvider delays = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			terminal,
			delays
		);

		TerminalScreenOperationPlan plan = session.Screen.PlanErase(
			TerminalScreenEraseKind.Screen,
			affectedLines: 3
		) ?? throw new InvalidOperationException();

		Assert.Equal( 1, plan.ByteCount );
		Assert.Empty( output.Bytes );
		Assert.Empty( delays.Delays );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "E" ), output.Bytes );
		TermInfoDelay delay = Assert.Single( delays.Delays );
		Assert.Equal( TimeSpan.FromMilliseconds( 6 ), delay.Duration );
		Assert.True( delay.IsMandatory );
		Assert.Equal( 0, delays.SynchronousDelayCount );
		Assert.Equal( 1, delays.AsynchronousDelayCount );
	}

	[Theory]
	[InlineData( TerminalScreenEraseKind.ToEndOfLine )]
	[InlineData( TerminalScreenEraseKind.ToBeginningOfLine )]
	[InlineData( TerminalScreenEraseKind.ToEndOfScreen )]
	[InlineData( TerminalScreenEraseKind.Screen )]
	public async Task EveryUnavailableEraseKindReturnsNoPlanWithoutOutput(
		TerminalScreenEraseKind kind
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "unavailable-erase" ).Build()
		);

		Assert.Null( session.Screen.PlanErase( kind, affectedLines: 2 ) );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CharacterEraseHasLiteralExpandedCostAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "character-erase" )
			.SetString( StringCapability.EraseCharacters, "<E:%p1%d>$<1*/>" )
			.Build();
		RecordingDelayProvider delays = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			terminal,
			delays
		);

		TerminalScreenOperationPlan plan = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Erase,
			3
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			TerminalScreenOperationKind.CharacterShift,
			byteCount: 5,
			affectedLines: 1,
			expected: "<E:3>"
		);
		TermInfoDelay delay = Assert.Single( delays.Delays );
		Assert.Equal( TimeSpan.FromMilliseconds( 1 ), delay.Duration );
		Assert.True( delay.IsMandatory );
	}

	[Fact]
	public async Task UnavailableCharacterEraseReturnsNoPlanWithoutOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "unavailable-character-erase" ).Build()
		);

		Assert.Null(
			session.Screen.PlanCharacterShift(
				TerminalScreenCharacterShiftKind.Erase,
				3
			)
		);
		Assert.Empty( output.Bytes );
	}

	[Theory]
	[InlineData( RepeatedFamily.CharacterInsert, StringCapability.InsertCharacters, "P3", 1 )]
	[InlineData( RepeatedFamily.CharacterDelete, StringCapability.DeleteCharacters, "P3", 1 )]
	[InlineData( RepeatedFamily.LineInsert, StringCapability.InsertLines, "P3", 4 )]
	[InlineData( RepeatedFamily.LineDelete, StringCapability.DeleteLines, "P3", 4 )]
	[InlineData( RepeatedFamily.ScrollForward, StringCapability.ScrollForwardLines, "P3", 4 )]
	[InlineData( RepeatedFamily.ScrollReverse, StringCapability.ScrollReverseLines, "P3", 4 )]
	public async Task EveryRepeatedFamilyUsesParameterizedCapabilityWhenSingleIsUnavailable(
		RepeatedFamily family,
		StringCapability capability,
		string expected,
		int affectedLines
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "parameterized-only" )
			.SetString( capability, "P%p1%d$<0>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = PlanRepeated(
			session.Screen,
			family,
			count: 3,
			affectedLines
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			OperationKind( family ),
			byteCount: 2,
			affectedLines,
			expected
		);
	}

	[Theory]
	[InlineData( RepeatedFamily.CharacterInsert, StringCapability.InsertCharacter, "iii", 1 )]
	[InlineData( RepeatedFamily.CharacterDelete, StringCapability.DeleteCharacter, "ddd", 1 )]
	[InlineData( RepeatedFamily.LineInsert, StringCapability.InsertLine, "lll", 4 )]
	[InlineData( RepeatedFamily.LineDelete, StringCapability.DeleteLine, "DDD", 4 )]
	[InlineData( RepeatedFamily.ScrollForward, StringCapability.ScrollForward, "fff", 4 )]
	[InlineData( RepeatedFamily.ScrollReverse, StringCapability.ScrollReverse, "rrr", 4 )]
	public async Task EveryRepeatedFamilyUsesSingleCapabilityWhenParameterizedIsUnavailable(
		RepeatedFamily family,
		StringCapability capability,
		string expected,
		int affectedLines
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "single-only" )
			.SetString( capability, expected[ 0 ] + "$<0>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = PlanRepeated(
			session.Screen,
			family,
			count: 3,
			affectedLines
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			OperationKind( family ),
			byteCount: 3,
			affectedLines,
			expected
		);
	}

	[Theory]
	[InlineData( RepeatedFamily.CharacterInsert )]
	[InlineData( RepeatedFamily.CharacterDelete )]
	[InlineData( RepeatedFamily.LineInsert )]
	[InlineData( RepeatedFamily.LineDelete )]
	[InlineData( RepeatedFamily.ScrollForward )]
	[InlineData( RepeatedFamily.ScrollReverse )]
	public async Task EveryRepeatedFamilyReturnsNoPlanWhenBothCapabilitiesAreUnavailable(
		RepeatedFamily family
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "unavailable-repeated-family" ).Build()
		);

		Assert.Null(
			PlanRepeated(
				session.Screen,
				family,
				count: 3,
				affectedLines: 4
			)
		);
		Assert.Empty( output.Bytes );
	}

	[Theory]
	[InlineData( RepeatedFamily.CharacterInsert, StringCapability.InsertCharacters, StringCapability.InsertCharacter, 1 )]
	[InlineData( RepeatedFamily.CharacterDelete, StringCapability.DeleteCharacters, StringCapability.DeleteCharacter, 1 )]
	[InlineData( RepeatedFamily.LineInsert, StringCapability.InsertLines, StringCapability.InsertLine, 4 )]
	[InlineData( RepeatedFamily.LineDelete, StringCapability.DeleteLines, StringCapability.DeleteLine, 4 )]
	[InlineData( RepeatedFamily.ScrollForward, StringCapability.ScrollForwardLines, StringCapability.ScrollForward, 4 )]
	[InlineData( RepeatedFamily.ScrollReverse, StringCapability.ScrollReverseLines, StringCapability.ScrollReverse, 4 )]
	public async Task EveryRepeatedFamilySelectsCheaperParameterizedCandidate(
		RepeatedFamily family,
		StringCapability parameterizedCapability,
		StringCapability singleCapability,
		int affectedLines
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "parameterized-wins" )
			.SetString( parameterizedCapability, "P%p1%d" )
			.SetString( singleCapability, "ss" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = PlanRepeated(
			session.Screen,
			family,
			count: 3,
			affectedLines
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			OperationKind( family ),
			byteCount: 2,
			affectedLines,
			expected: "P3"
		);
	}

	[Theory]
	[InlineData( RepeatedFamily.CharacterInsert, StringCapability.InsertCharacters, StringCapability.InsertCharacter, 1 )]
	[InlineData( RepeatedFamily.CharacterDelete, StringCapability.DeleteCharacters, StringCapability.DeleteCharacter, 1 )]
	[InlineData( RepeatedFamily.LineInsert, StringCapability.InsertLines, StringCapability.InsertLine, 4 )]
	[InlineData( RepeatedFamily.LineDelete, StringCapability.DeleteLines, StringCapability.DeleteLine, 4 )]
	[InlineData( RepeatedFamily.ScrollForward, StringCapability.ScrollForwardLines, StringCapability.ScrollForward, 4 )]
	[InlineData( RepeatedFamily.ScrollReverse, StringCapability.ScrollReverseLines, StringCapability.ScrollReverse, 4 )]
	public async Task EveryRepeatedFamilySelectsCheaperRepeatedSingleCandidate(
		RepeatedFamily family,
		StringCapability parameterizedCapability,
		StringCapability singleCapability,
		int affectedLines
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "single-wins" )
			.SetString( parameterizedCapability, "PARAM%p1%d" )
			.SetString( singleCapability, "s" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = PlanRepeated(
			session.Screen,
			family,
			count: 3,
			affectedLines
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			OperationKind( family ),
			byteCount: 3,
			affectedLines,
			expected: "sss"
		);
	}

	[Theory]
	[InlineData( RepeatedFamily.CharacterInsert, StringCapability.InsertCharacters, StringCapability.InsertCharacter, 1 )]
	[InlineData( RepeatedFamily.CharacterDelete, StringCapability.DeleteCharacters, StringCapability.DeleteCharacter, 1 )]
	[InlineData( RepeatedFamily.LineInsert, StringCapability.InsertLines, StringCapability.InsertLine, 4 )]
	[InlineData( RepeatedFamily.LineDelete, StringCapability.DeleteLines, StringCapability.DeleteLine, 4 )]
	[InlineData( RepeatedFamily.ScrollForward, StringCapability.ScrollForwardLines, StringCapability.ScrollForward, 4 )]
	[InlineData( RepeatedFamily.ScrollReverse, StringCapability.ScrollReverseLines, StringCapability.ScrollReverse, 4 )]
	public async Task EqualRepeatedCandidateCostsPreferParameterizedCapability(
		RepeatedFamily family,
		StringCapability parameterizedCapability,
		StringCapability singleCapability,
		int affectedLines
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "repeated-tie" )
			.SetString( parameterizedCapability, "P%p1%d" )
			.SetString( singleCapability, "s" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = PlanRepeated(
			session.Screen,
			family,
			count: 2,
			affectedLines
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			OperationKind( family ),
			byteCount: 2,
			affectedLines,
			expected: "P2"
		);
	}

	[Fact]
	public async Task RepeatedLinePaddingIsCostedBeforeSelectionAndUsesAffectedLines() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "line-padding" )
			.SetString( StringCapability.ScrollForwardLines, "PARAM" )
			.SetString( StringCapability.ScrollForward, "s$<2*/>" )
			.Build();
		RecordingDelayProvider delays = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			terminal,
			delays
		);

		TerminalScreenOperationPlan plan = session.Screen.PlanLineShift(
			TerminalScreenLineShiftKind.ScrollForward,
			count: 3,
			affectedLines: 4
		) ?? throw new InvalidOperationException();

		Assert.Equal( 3, plan.ByteCount );
		Assert.Empty( output.Bytes );
		Assert.Empty( delays.Delays );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "sss" ), output.Bytes );
		Assert.Collection(
			delays.Delays,
			delay => AssertDelay( delay, 8 ),
			delay => AssertDelay( delay, 8 ),
			delay => AssertDelay( delay, 8 )
		);
	}

	[Fact]
	public async Task ScrollRegionHasLiteralExpandedCostPaddingAndExactOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "scroll-region" )
			.SetString( StringCapability.ChangeScrollRegion, "R%p1%d,%p2%d$<2*/>" )
			.Build();
		RecordingDelayProvider delays = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			terminal,
			delays
		);

		TerminalScreenOperationPlan plan = session.Screen.PlanScrollRegion(
			topRow: 1,
			bottomRow: 8,
			affectedLines: 8
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			TerminalScreenOperationKind.ScrollRegion,
			byteCount: 4,
			affectedLines: 8,
			expected: "R1,8"
		);
		TermInfoDelay delay = Assert.Single( delays.Delays );
		AssertDelay( delay, 16 );
	}

	[Fact]
	public async Task UnavailableScrollRegionReturnsNoPlanWithoutOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "unavailable-scroll-region" ).Build()
		);

		Assert.Null(
			session.Screen.PlanScrollRegion(
				topRow: 0,
				bottomRow: 23,
				affectedLines: 24
			)
		);
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task MinimumPositiveCountsLinesAndRowsProduceExactPlans() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "editing-minimums" )
			.SetString( StringCapability.InsertCharacters, "C%p1%d" )
			.SetString( StringCapability.InsertLines, "L%p1%d" )
			.SetString( StringCapability.ClearScreen, "E" )
			.SetString( StringCapability.ChangeScrollRegion, "R%p1%d,%p2%d" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan character = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Insert,
			count: 1
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan line = session.Screen.PlanLineShift(
			TerminalScreenLineShiftKind.Insert,
			count: 1,
			affectedLines: 1
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan erase = session.Screen.PlanErase(
			TerminalScreenEraseKind.Screen,
			affectedLines: 1
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan region = session.Screen.PlanScrollRegion(
			topRow: 0,
			bottomRow: 0,
			affectedLines: 1
		) ?? throw new InvalidOperationException();

		Assert.Equal( 2, character.ByteCount );
		Assert.Equal( 2, line.ByteCount );
		Assert.Equal( 1, erase.ByteCount );
		Assert.Equal( 4, region.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, character, line, erase, region );

		Assert.Equal( Encoding.Latin1.GetBytes( "C1L1ER0,0" ), output.Bytes );
	}

	[Fact]
	public async Task InvalidEditingEnumsReportKindWithoutPlanningSideEffects() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "invalid-editing-enums" ).Build()
		);

		ArgumentOutOfRangeException erase = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanErase( (TerminalScreenEraseKind)(-1) )
		);
		ArgumentOutOfRangeException character = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanCharacterShift(
				(TerminalScreenCharacterShiftKind)(-1),
				1
			)
		);
		ArgumentOutOfRangeException line = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanLineShift(
				(TerminalScreenLineShiftKind)(-1),
				1,
				1
			)
		);

		Assert.Equal( "kind", erase.ParamName );
		Assert.Equal( "kind", character.ParamName );
		Assert.Equal( "kind", line.ParamName );
		Assert.Empty( output.Bytes );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	[InlineData( int.MinValue )]
	public async Task InvalidCountsReportCountWithoutPlanningSideEffects( int count ) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "invalid-counts" ).Build()
		);

		ArgumentOutOfRangeException character = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanCharacterShift(
				TerminalScreenCharacterShiftKind.Insert,
				count
			)
		);
		ArgumentOutOfRangeException line = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanLineShift(
				TerminalScreenLineShiftKind.Insert,
				count,
				affectedLines: 1
			)
		);

		Assert.Equal( "count", character.ParamName );
		Assert.Equal( "count", line.ParamName );
		Assert.Empty( output.Bytes );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	[InlineData( int.MinValue )]
	public async Task InvalidAffectedLinesReportAffectedLinesWithoutPlanningSideEffects(
		int affectedLines
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "invalid-affected-lines" ).Build()
		);

		ArgumentOutOfRangeException erase = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanErase(
				TerminalScreenEraseKind.Screen,
				affectedLines
			)
		);
		ArgumentOutOfRangeException line = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanLineShift(
				TerminalScreenLineShiftKind.Insert,
				count: 1,
				affectedLines
			)
		);
		ArgumentOutOfRangeException region = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanScrollRegion(
				topRow: 0,
				bottomRow: 0,
				affectedLines
			)
		);

		Assert.Equal( "affectedLines", erase.ParamName );
		Assert.Equal( "affectedLines", line.ParamName );
		Assert.Equal( "affectedLines", region.ParamName );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task InvalidScrollRegionRowsReportTheOffendingParameter() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			new TerminalDescriptionBuilder( "invalid-scroll-region" ).Build()
		);

		ArgumentOutOfRangeException negativeTop = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanScrollRegion( -1, 0, 1 )
		);
		ArgumentOutOfRangeException negativeBottom = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanScrollRegion( 0, -1, 1 )
		);
		ArgumentOutOfRangeException reversed = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.Screen.PlanScrollRegion( 2, 1, 1 )
		);

		Assert.Equal( "topRow", negativeTop.ParamName );
		Assert.Equal( "bottomRow", negativeBottom.ParamName );
		Assert.Equal( "bottomRow", reversed.ParamName );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task BoundaryAndExtremeValuesExpandWithoutArithmeticOverflow() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "editing-extremes" )
			.SetString( StringCapability.DeleteCharacters, "C%p1%d" )
			.SetString( StringCapability.ScrollReverseLines, "L%p1%d" )
			.SetString( StringCapability.ClearScreen, "E$<99999*/>" )
			.SetString( StringCapability.ChangeScrollRegion, "R%p1%d,%p2%d" )
			.Build();
		RecordingDelayProvider delays = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			terminal,
			delays
		);

		TerminalScreenOperationPlan character = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Delete,
			int.MaxValue
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan line = session.Screen.PlanLineShift(
			TerminalScreenLineShiftKind.ScrollReverse,
			int.MaxValue,
			affectedLines: int.MaxValue
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan erase = session.Screen.PlanErase(
			TerminalScreenEraseKind.Screen,
			affectedLines: int.MaxValue
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan zeroRegion = session.Screen.PlanScrollRegion( 0, 0, 1 )
			?? throw new InvalidOperationException();
		TerminalScreenOperationPlan maximumRegion = session.Screen.PlanScrollRegion(
			int.MaxValue,
			int.MaxValue,
			int.MaxValue
		) ?? throw new InvalidOperationException();

		Assert.Equal( 11, character.ByteCount );
		Assert.Equal( 11, line.ByteCount );
		Assert.Equal( 1, erase.ByteCount );
		Assert.Equal( 4, zeroRegion.ByteCount );
		Assert.Equal( 22, maximumRegion.ByteCount );
		Assert.Empty( output.Bytes );

		await CommitAsync(
			session,
			character,
			line,
			erase,
			zeroRegion,
			maximumRegion
		);

		Assert.Equal(
			Encoding.Latin1.GetBytes(
				"C2147483647L2147483647ER0,0R2147483647,2147483647"
			),
			output.Bytes
		);
		TermInfoDelay delay = Assert.Single( delays.Delays );
		Assert.Equal( TimeSpan.FromSeconds( 30 ), delay.Duration );
		Assert.True( delay.IsMandatory );
	}

	[Fact]
	public async Task RepeatedFallbackHonorsExactSourceBoundAndRejectsNextCount() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "repetition-bound" )
			.SetString( StringCapability.InsertCharacter, "ss" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan boundary = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Insert,
			524_288
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan? aboveBoundary = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Insert,
			524_289
		);

		Assert.Equal( 1_048_576, boundary.ByteCount );
		Assert.Null( aboveBoundary );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task EmptySingleCapabilitySupportsExtremeCountWithoutMaterialization() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "empty-repetition" )
			.SetString( StringCapability.DeleteLine, string.Empty )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanLineShift(
			TerminalScreenLineShiftKind.Delete,
			int.MaxValue,
			affectedLines: 1
		) ?? throw new InvalidOperationException();

		await AssertPlanAsync(
			session,
			output,
			plan,
			TerminalScreenOperationKind.LineShift,
			byteCount: 0,
			affectedLines: 1,
			expected: string.Empty
		);
	}

	private static void AssertDelay( TermInfoDelay delay, double milliseconds ) {
		Assert.Equal( TimeSpan.FromMilliseconds( milliseconds ), delay.Duration );
		Assert.True( delay.IsMandatory );
	}

	private static async ValueTask AssertPlanAsync(
		TerminalSession session,
		RecordingTerminalOutput output,
		TerminalScreenOperationPlan plan,
		TerminalScreenOperationKind kind,
		int byteCount,
		int affectedLines,
		string expected
	) {
		Assert.Equal( kind, plan.Kind );
		Assert.Equal( byteCount, plan.ByteCount );
		Assert.Equal( affectedLines, plan.AffectedLines );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( expected ), output.Bytes );
	}

	private static TerminalScreenOperationKind OperationKind( RepeatedFamily family ) {
		return family is RepeatedFamily.CharacterInsert or RepeatedFamily.CharacterDelete
			? TerminalScreenOperationKind.CharacterShift
			: TerminalScreenOperationKind.LineShift;
	}

	private static TerminalScreenOperationPlan? PlanRepeated(
		TerminalScreenPlanner planner,
		RepeatedFamily family,
		int count,
		int affectedLines
	) {
		return family switch {
			RepeatedFamily.CharacterInsert => planner.PlanCharacterShift(
				TerminalScreenCharacterShiftKind.Insert,
				count
			),
			RepeatedFamily.CharacterDelete => planner.PlanCharacterShift(
				TerminalScreenCharacterShiftKind.Delete,
				count
			),
			RepeatedFamily.LineInsert => planner.PlanLineShift(
				TerminalScreenLineShiftKind.Insert,
				count,
				affectedLines
			),
			RepeatedFamily.LineDelete => planner.PlanLineShift(
				TerminalScreenLineShiftKind.Delete,
				count,
				affectedLines
			),
			RepeatedFamily.ScrollForward => planner.PlanLineShift(
				TerminalScreenLineShiftKind.ScrollForward,
				count,
				affectedLines
			),
			RepeatedFamily.ScrollReverse => planner.PlanLineShift(
				TerminalScreenLineShiftKind.ScrollReverse,
				count,
				affectedLines
			),
			_ => throw new ArgumentOutOfRangeException( nameof( family ) )
		};
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
		TerminalDescription terminal,
		ITermInfoDelayProvider? delayProvider = null
	) {
		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ObserveLifecycleEvents = false,
				CapabilityPaddingMode = PaddingMode.Delay,
				CapabilityDelayProvider = delayProvider
			}
		);
	}

	public enum RepeatedFamily {
		CharacterInsert,
		CharacterDelete,
		LineInsert,
		LineDelete,
		ScrollForward,
		ScrollReverse
	}

	private sealed class RecordingDelayProvider : ITermInfoDelayProvider {
		internal List<TermInfoDelay> Delays {
			get;
		} = [];

		internal int SynchronousDelayCount {
			get;
			private set;
		}

		internal int AsynchronousDelayCount {
			get;
			private set;
		}

		public void Delay( TermInfoDelay delay ) {
			this.SynchronousDelayCount++;
			this.Delays.Add( delay );
		}

		public ValueTask DelayAsync(
			TermInfoDelay delay,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.AsynchronousDelayCount++;
			this.Delays.Add( delay );
			return ValueTask.CompletedTask;
		}
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
