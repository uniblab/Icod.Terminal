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

/// <summary>Hardens rendition normalization, transition, and reset planning.</summary>
public sealed class TerminalScreenRenditionPlannerHardeningTests {
	[Fact]
	public async Task RenditionBaselinePlanIsOpaqueCostedAndExactlyEmittable() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "rendition-baseline" )
			.SetNumber( NumericCapability.Colors, 16 )
			.SetString( StringCapability.EnterBoldMode, "<bold>" )
			.SetString( StringCapability.ExitAttributeMode, "<sgr0>" )
			.SetString( StringCapability.SetForegroundColor, "<f:%p1%d>" )
			.SetString( StringCapability.SetBackgroundColor, "<b:%p1%d>" )
			.SetString( StringCapability.OriginalColorPair, "<op>" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionBaseline()
			?? throw new InvalidOperationException();

		Assert.Empty( output.Bytes );
		AssertRenditionPlan( plan, 10 );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "<sgr0><op>" ), output.Bytes );
	}

	[Fact]
	public async Task DirectRgbBlackInsideRetainedIndexedPrefixDegradesToDefault() {
		TerminalDescription terminal = CreateDirectColorBuilder( "direct-reserved-zero" )
			.SetExtendedNumber( "CO", 256 )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Rgb( 0, 0, 0 ),
			TerminalScreenColor.Default
		);

		TerminalScreenRendition normalized = session.Screen.NormalizeRendition( requested );

		Assert.Equal( TerminalScreenRendition.Default, normalized );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task DirectRgbBlackWithoutRetainedIndexedPrefixRemainsExactlyRepresentable() {
		TerminalDescription terminal = CreateDirectColorBuilder( "direct-black" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition target = new(
			TerminalScreenColor.Rgb( 0, 0, 0 ),
			TerminalScreenColor.Default
		);

		Assert.Equal( target, session.Screen.NormalizeRendition( target ) );
		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			target
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 5 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "<f:0>" ), output.Bytes );
	}

	[Fact]
	public async Task DirectRgbColorsNormalizeExpandAndEmitPackedValuesExactly() {
		TerminalDescription terminal = CreateDirectColorBuilder( "direct-expansion" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition target = new(
			TerminalScreenColor.Rgb( 1, 2, 3 ),
			TerminalScreenColor.Rgb( 4, 5, 6 )
		);

		Assert.Equal( target, session.Screen.NormalizeRendition( target ) );
		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			target
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 19 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "<f:66051><b:263430>" ), output.Bytes );
	}

	[Fact]
	public async Task DirectRgbLayoutCountAndRetainedIndexedRangeAreEnforced() {
		TerminalDescription fullRangeTerminal = new TerminalDescriptionBuilder( "direct-565" )
			.SetNumber( NumericCapability.Colors, 65_536 )
			.SetExtendedString( "RGB", "5/6/5" )
			.SetExtendedNumber( "CO", 16 )
			.SetString( StringCapability.SetForegroundColor, "F%p1%d" )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.SetString( StringCapability.OriginalColorPair, "O" )
			.Build();
		RecordingTerminalOutput fullRangeOutput = new();
		await using TerminalSession fullRangeSession = await OpenSessionAsync(
			fullRangeOutput,
			fullRangeTerminal
		);

		TerminalScreenRendition retainedIndex = new(
			TerminalScreenColor.Rgb( 0, 0, 8 ),
			TerminalScreenColor.Default
		);
		TerminalScreenRendition firstDirectValue = new(
			TerminalScreenColor.Rgb( 0, 4, 0 ),
			TerminalScreenColor.Default
		);
		TerminalScreenRendition layoutMaximum = new(
			TerminalScreenColor.Rgb( 255, 255, 255 ),
			TerminalScreenColor.Default
		);

		Assert.Equal(
			TerminalScreenRendition.Default,
			fullRangeSession.Screen.NormalizeRendition( retainedIndex )
		);
		Assert.Equal(
			firstDirectValue,
			fullRangeSession.Screen.NormalizeRendition( firstDirectValue )
		);
		Assert.Equal(
			layoutMaximum,
			fullRangeSession.Screen.NormalizeRendition( layoutMaximum )
		);
		Assert.Equal(
			new TerminalScreenRendition(
				TerminalScreenColor.Indexed( 15 ),
				TerminalScreenColor.Default
			),
			fullRangeSession.Screen.NormalizeRendition(
				new TerminalScreenRendition(
					TerminalScreenColor.Indexed( 15 ),
					TerminalScreenColor.Default
				)
			)
		);
		Assert.Equal(
			TerminalScreenRendition.Default,
			fullRangeSession.Screen.NormalizeRendition(
				new TerminalScreenRendition(
					TerminalScreenColor.Indexed( 16 ),
					TerminalScreenColor.Default
				)
			)
		);
		TerminalScreenOperationPlan layoutPlan = fullRangeSession.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			firstDirectValue
		) ?? throw new InvalidOperationException();
		AssertRenditionPlan( layoutPlan, 3 );
		Assert.Empty( fullRangeOutput.Bytes );

		await CommitAsync( fullRangeSession, layoutPlan );

		Assert.Equal( Encoding.Latin1.GetBytes( "F32" ), fullRangeOutput.Bytes );

		TerminalDescription boundedTerminal = new TerminalDescriptionBuilder( "direct-565-bounded" )
			.SetNumber( NumericCapability.Colors, 32_768 )
			.SetExtendedString( "RGB", "5/6/5" )
			.SetString( StringCapability.SetForegroundColor, "F%p1%d" )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.SetString( StringCapability.OriginalColorPair, "O" )
			.Build();
		RecordingTerminalOutput boundedOutput = new();
		await using TerminalSession boundedSession = await OpenSessionAsync(
			boundedOutput,
			boundedTerminal
		);

		Assert.Equal(
			TerminalScreenRendition.Default,
			boundedSession.Screen.NormalizeRendition( layoutMaximum )
		);
		Assert.Empty( boundedOutput.Bytes );
	}

	[Fact]
	public async Task IndexedColorsRequireBoundsSelectorsAndDefaultRestoration() {
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Indexed( 15 ),
			TerminalScreenColor.Indexed( 0 )
		);
		TerminalDescription complete = CreateIndexedColorBuilder( "indexed-complete" )
			.Build();
		RecordingTerminalOutput completeOutput = new();
		await using TerminalSession completeSession = await OpenSessionAsync(
			completeOutput,
			complete
		);

		Assert.Equal( requested, completeSession.Screen.NormalizeRendition( requested ) );
		Assert.Equal(
			TerminalScreenRendition.Default,
			completeSession.Screen.NormalizeRendition(
				new TerminalScreenRendition(
					TerminalScreenColor.Indexed( 16 ),
					TerminalScreenColor.Default
				)
			)
		);
		Assert.Empty( completeOutput.Bytes );

		TerminalDescription noForeground = new TerminalDescriptionBuilder( "indexed-no-foreground" )
			.SetNumber( NumericCapability.Colors, 16 )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.SetString( StringCapability.OriginalColorPair, "O" )
			.Build();
		RecordingTerminalOutput noForegroundOutput = new();
		await using TerminalSession noForegroundSession = await OpenSessionAsync(
			noForegroundOutput,
			noForeground
		);
		Assert.Equal(
			new TerminalScreenRendition(
				TerminalScreenColor.Default,
				TerminalScreenColor.Indexed( 0 )
			),
			noForegroundSession.Screen.NormalizeRendition( requested )
		);

		TerminalDescription noBackground = new TerminalDescriptionBuilder( "indexed-no-background" )
			.SetNumber( NumericCapability.Colors, 16 )
			.SetString( StringCapability.SetForegroundColor, "F%p1%d" )
			.SetString( StringCapability.OriginalColorPair, "O" )
			.Build();
		RecordingTerminalOutput noBackgroundOutput = new();
		await using TerminalSession noBackgroundSession = await OpenSessionAsync(
			noBackgroundOutput,
			noBackground
		);
		Assert.Equal(
			new TerminalScreenRendition(
				TerminalScreenColor.Indexed( 15 ),
				TerminalScreenColor.Default
			),
			noBackgroundSession.Screen.NormalizeRendition( requested )
		);

		TerminalDescription noRestore = new TerminalDescriptionBuilder( "indexed-no-restore" )
			.SetNumber( NumericCapability.Colors, 16 )
			.SetString( StringCapability.SetForegroundColor, "F%p1%d" )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.Build();
		RecordingTerminalOutput noRestoreOutput = new();
		await using TerminalSession noRestoreSession = await OpenSessionAsync(
			noRestoreOutput,
			noRestore
		);
		Assert.Equal(
			TerminalScreenRendition.Default,
			noRestoreSession.Screen.NormalizeRendition( requested )
		);
		TerminalScreenOperationPlan noRestoreEnter = noRestoreSession.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			requested
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan noRestoreReset = noRestoreSession.Screen.PlanRenditionReset(
			requested
		) ?? throw new InvalidOperationException();
		AssertRenditionPlan( noRestoreEnter, 0 );
		AssertRenditionPlan( noRestoreReset, 0 );
		Assert.Empty( noForegroundOutput.Bytes );
		Assert.Empty( noBackgroundOutput.Bytes );
		Assert.Empty( noRestoreOutput.Bytes );

		await CommitAsync( noRestoreSession, noRestoreEnter, noRestoreReset );

		Assert.Empty( noRestoreOutput.Bytes );
	}

	[Fact]
	public async Task DirectRgbMetadataRequiresBothSelectorsAndPlannerRequiresDefaultRestoration() {
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Rgb( 1, 2, 3 ),
			TerminalScreenColor.Rgb( 4, 5, 6 )
		);
		TerminalDescription noForeground = new TerminalDescriptionBuilder( "direct-no-foreground" )
			.SetNumber( NumericCapability.Colors, 16_777_216 )
			.SetExtendedString( "RGB", "8/8/8" )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.SetString( StringCapability.OriginalColorPair, "O" )
			.Build();
		RecordingTerminalOutput noForegroundOutput = new();
		await using TerminalSession noForegroundSession = await OpenSessionAsync(
			noForegroundOutput,
			noForeground
		);
		Assert.Throws<InvalidOperationException>(
			() => noForegroundSession.Screen.NormalizeRendition( requested )
		);

		TerminalDescription noRestore = new TerminalDescriptionBuilder( "direct-no-restore" )
			.SetNumber( NumericCapability.Colors, 16_777_216 )
			.SetExtendedString( "RGB", "8/8/8" )
			.SetString( StringCapability.SetForegroundColor, "F%p1%d" )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.Build();
		RecordingTerminalOutput noRestoreOutput = new();
		await using TerminalSession noRestoreSession = await OpenSessionAsync(
			noRestoreOutput,
			noRestore
		);
		Assert.Equal(
			TerminalScreenRendition.Default,
			noRestoreSession.Screen.NormalizeRendition( requested )
		);
		Assert.Empty( noForegroundOutput.Bytes );
		Assert.Empty( noRestoreOutput.Bytes );
	}

	[Fact]
	public async Task UnsupportedAndNonReversibleAttributesDegradeToSafeSubset() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "attribute-subset" )
			.SetString( StringCapability.EnterBoldMode, "B" )
			.SetString( StringCapability.EnterUnderlineMode, "U" )
			.SetString( StringCapability.ExitUnderlineMode, "u" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Bold
				| TerminalTextAttributes.Underline
				| TerminalTextAttributes.Blink
		);
		TerminalScreenRendition expected = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Underline
		);

		Assert.Equal( expected, session.Screen.NormalizeRendition( requested ) );
		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			requested
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 1 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "U" ), output.Bytes );
	}

	[Fact]
	public async Task UnsupportedStandoutNormalizesToReversibleReverse() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "standout-fallback" )
			.SetString( StringCapability.EnterReverseMode, "R" )
			.SetString( StringCapability.ExitAttributeMode, "Z" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Standout
		);
		TerminalScreenRendition expected = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Reverse
		);

		Assert.Equal( expected, session.Screen.NormalizeRendition( requested ) );
		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			requested
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 1 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "R" ), output.Bytes );
	}

	[Fact]
	public async Task ColorRestrictedAttributesRemainDefaultOnlyAndDropWhenColorIsActive() {
		TerminalDescription terminal = CreateIndexedColorBuilder( "color-restricted" )
			.SetNumber( NumericCapability.NoColorVideo, 34 )
			.SetString( StringCapability.EnterBoldMode, "D" )
			.SetString( StringCapability.EnterUnderlineMode, "U" )
			.SetString( StringCapability.EnterReverseMode, "R" )
			.SetString( StringCapability.ExitAttributeMode, "Z" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalTextAttributes requestedAttributes = TerminalTextAttributes.Bold
			| TerminalTextAttributes.Underline
			| TerminalTextAttributes.Reverse;
		TerminalScreenRendition defaultColors = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			requestedAttributes
		);
		TerminalScreenRendition colored = new(
			TerminalScreenColor.Indexed( 3 ),
			TerminalScreenColor.Default,
			requestedAttributes
		);
		TerminalScreenRendition expectedColored = new(
			TerminalScreenColor.Indexed( 3 ),
			TerminalScreenColor.Default,
			TerminalTextAttributes.Reverse
		);

		Assert.Equal( defaultColors, session.Screen.NormalizeRendition( defaultColors ) );
		Assert.Equal( expectedColored, session.Screen.NormalizeRendition( colored ) );
		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			colored
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 3 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "F3R" ), output.Bytes );
	}

	[Fact]
	public async Task SpecificStandardAndExtendedAttributeEntersAndExitsAreOrderedExactly() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "specific-attribute-exits" )
			.SetString( StringCapability.EnterUnderlineMode, "U" )
			.SetString( StringCapability.ExitUnderlineMode, "u" )
			.SetString( StringCapability.EnterStandoutMode, "SS" )
			.SetString( StringCapability.ExitStandoutMode, "ss" )
			.SetString( StringCapability.EnterItalicMode, "III" )
			.SetString( StringCapability.ExitItalicMode, "iii" )
			.SetExtendedString( "smxx", "XXXX" )
			.SetExtendedString( "rmxx", "xxxx" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition attributes = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Underline
				| TerminalTextAttributes.Standout
				| TerminalTextAttributes.Italic
				| TerminalTextAttributes.Strikeout
		);

		TerminalScreenOperationPlan enter = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			attributes
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan exit = session.Screen.PlanRenditionTransition(
			attributes,
			TerminalScreenRendition.Default
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( enter, 10 );
		AssertRenditionPlan( exit, 10 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, enter, exit );

		Assert.Equal(
			Encoding.Latin1.GetBytes( "USSIIIXXXXussiiixxxx" ),
			output.Bytes
		);
	}

	[Fact]
	public async Task GlobalResetMakesEveryStandardAndExtendedAttributeReversible() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "global-attribute-reset" )
			.SetString( StringCapability.EnterBoldMode, "B" )
			.SetString( StringCapability.EnterDimMode, "D" )
			.SetString( StringCapability.EnterUnderlineMode, "U" )
			.SetString( StringCapability.EnterReverseMode, "R" )
			.SetString( StringCapability.EnterStandoutMode, "S" )
			.SetString( StringCapability.EnterItalicMode, "I" )
			.SetString( StringCapability.EnterBlinkMode, "K" )
			.SetString( StringCapability.EnterInvisibleMode, "C" )
			.SetExtendedString( "smxx", "X" )
			.SetString( StringCapability.ExitAttributeMode, "Z" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition attributes = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Bold
				| TerminalTextAttributes.Dim
				| TerminalTextAttributes.Underline
				| TerminalTextAttributes.Reverse
				| TerminalTextAttributes.Standout
				| TerminalTextAttributes.Italic
				| TerminalTextAttributes.Blink
				| TerminalTextAttributes.Conceal
				| TerminalTextAttributes.Strikeout
		);

		TerminalScreenOperationPlan enter = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			attributes
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan reset = session.Screen.PlanRenditionReset( attributes )
			?? throw new InvalidOperationException();

		AssertRenditionPlan( enter, 9 );
		AssertRenditionPlan( reset, 1 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, enter, reset );

		Assert.Equal( Encoding.Latin1.GetBytes( "BDURSIKCXZ" ), output.Bytes );
	}

	[Fact]
	public async Task CombinedTransitionResetsThenAppliesColorsAndAttributesInOrder() {
		TerminalDescription terminal = CreateIndexedColorBuilder( "combined-transition" )
			.SetString( StringCapability.ExitAttributeMode, "Z" )
			.SetString( StringCapability.EnterUnderlineMode, "U" )
			.SetString( StringCapability.EnterItalicMode, "I" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition current = new(
			TerminalScreenColor.Indexed( 1 ),
			TerminalScreenColor.Indexed( 2 ),
			TerminalTextAttributes.Underline
		);
		TerminalScreenRendition target = new(
			TerminalScreenColor.Indexed( 3 ),
			TerminalScreenColor.Indexed( 4 ),
			TerminalTextAttributes.Italic
		);

		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			current,
			target
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 7 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "ZOF3B4I" ), output.Bytes );
	}

	[Fact]
	public async Task RenditionResetRemovesAttributesAndColorsInOrder() {
		TerminalDescription terminal = CreateIndexedColorBuilder( "combined-reset" )
			.SetString( StringCapability.ExitAttributeMode, "Z" )
			.SetString( StringCapability.EnterUnderlineMode, "U" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition current = new(
			TerminalScreenColor.Indexed( 1 ),
			TerminalScreenColor.Indexed( 2 ),
			TerminalTextAttributes.Underline
		);

		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionReset( current )
			?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 2 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Equal( Encoding.Latin1.GetBytes( "ZO" ), output.Bytes );
	}

	[Fact]
	public async Task MissingResetAndSpecificExitDegradesToZeroCostNoOp() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "missing-attribute-reset" )
			.SetString( StringCapability.EnterBoldMode, "B" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Default,
			TerminalScreenColor.Default,
			TerminalTextAttributes.Bold
		);

		Assert.Equal(
			TerminalScreenRendition.Default,
			session.Screen.NormalizeRendition( requested )
		);
		TerminalScreenOperationPlan enter = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			requested
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan reset = session.Screen.PlanRenditionReset( requested )
			?? throw new InvalidOperationException();

		AssertRenditionPlan( enter, 0 );
		AssertRenditionPlan( reset, 0 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, enter, reset );

		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task EqualNormalizedRenditionsProduceZeroCostNoOp() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "rendition-no-op" ).Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			TerminalScreenRendition.Default
		) ?? throw new InvalidOperationException();

		AssertRenditionPlan( plan, 0 );
		Assert.Empty( output.Bytes );

		await CommitAsync( session, plan );

		Assert.Empty( output.Bytes );
	}

	private static void AssertRenditionPlan(
		TerminalScreenOperationPlan plan,
		int expectedByteCount
	) {
		Assert.Equal( TerminalScreenOperationKind.Rendition, plan.Kind );
		Assert.Equal( expectedByteCount, plan.ByteCount );
		Assert.Equal( 1, plan.AffectedLines );
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

	private static TerminalDescriptionBuilder CreateDirectColorBuilder(
		string name
	) {
		return new TerminalDescriptionBuilder( name )
			.SetNumber( NumericCapability.Colors, 16_777_216 )
			.SetExtendedString( "RGB", "8/8/8" )
			.SetString( StringCapability.SetForegroundColor, "<f:%p1%d>" )
			.SetString( StringCapability.SetBackgroundColor, "<b:%p1%d>" )
			.SetString( StringCapability.OriginalColorPair, "<op>" );
	}

	private static TerminalDescriptionBuilder CreateIndexedColorBuilder(
		string name
	) {
		return new TerminalDescriptionBuilder( name )
			.SetNumber( NumericCapability.Colors, 16 )
			.SetString( StringCapability.SetForegroundColor, "F%p1%d" )
			.SetString( StringCapability.SetBackgroundColor, "B%p1%d" )
			.SetString( StringCapability.OriginalColorPair, "O" );
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
