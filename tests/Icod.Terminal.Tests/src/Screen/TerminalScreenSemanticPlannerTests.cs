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

/// <summary>Verifies the Terminal-owned semantic profile and planning families.</summary>
public sealed class TerminalScreenSemanticPlannerTests {
	[Fact]
	public async Task ProfileProjectsCompleteSemanticScreenCapabilities() {
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalOutput()
		);

		TerminalScreenCapabilities capabilities = session.Profile.Screen;

		Assert.Equal( 16, capabilities.IndexedColorCount );
		Assert.False( capabilities.SupportsDirectRgb );
		Assert.True( capabilities.SupportsForegroundColor );
		Assert.True( capabilities.SupportsBackgroundColor );
		Assert.True( capabilities.SupportsDefaultColorRestoration );
		Assert.True( capabilities.SupportsAbsoluteCursorAddressing );
		Assert.True( capabilities.SupportsCursorHidden );
		Assert.True( capabilities.SupportsCursorNormal );
		Assert.True( capabilities.SupportsCursorVeryVisible );
		Assert.Equal(
			TerminalTextAttributes.Bold | TerminalTextAttributes.Underline,
			capabilities.SupportedAttributes
		);
		Assert.Equal(
			TerminalTextAttributes.Bold,
			capabilities.ColorRestrictedAttributes
		);
	}

	[Fact]
	public async Task RenditionNormalizationRejectsUnsafeColorAndAttributeRequests() {
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalOutput()
		);
		TerminalScreenRendition requested = new(
			TerminalScreenColor.Indexed( 3 ),
			TerminalScreenColor.Default,
			TerminalTextAttributes.Bold
				| TerminalTextAttributes.Underline
				| TerminalTextAttributes.Strikeout
		);

		TerminalScreenRendition normalized = session.Screen.NormalizeRendition(
			requested
		);

		Assert.Equal( TerminalScreenColor.Indexed( 3 ), normalized.Foreground );
		Assert.Equal( TerminalScreenColor.Default, normalized.Background );
		Assert.Equal( TerminalTextAttributes.Underline, normalized.Attributes );
		Assert.Equal(
			TerminalScreenRendition.Default,
			session.Screen.NormalizeRendition(
				new TerminalScreenRendition(
					TerminalScreenColor.Indexed( 16 ),
					TerminalScreenColor.Default
				)
			)
		);
	}

	[Fact]
	public async Task RenditionPlanIsOpaqueCostedAndExactlyEmittable() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenRendition target = new(
			TerminalScreenColor.Indexed( 3 ),
			TerminalScreenColor.Default,
			TerminalTextAttributes.Underline
		);
		TerminalScreenOperationPlan plan = session.Screen.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			target
		) ?? throw new InvalidOperationException();

		Assert.Equal( TerminalScreenOperationKind.Rendition, plan.Kind );
		Assert.Equal( 1, plan.AffectedLines );
		Assert.Equal( Encoding.Latin1.GetByteCount( "<f:3><u>" ), plan.ByteCount );

		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.Add( plan );
		await transaction.CommitAsync();

		Assert.Equal( Encoding.Latin1.GetBytes( "<f:3><u>" ), output.Bytes );
	}

	[Fact]
	public async Task AlternateCharacterSetResolutionAndPlansRemainSemantic() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalLineGlyphRepresentation representation = Assert.IsType<TerminalLineGlyphRepresentation>(
			session.Screen.ResolveLineGlyph( TerminalLineGlyph.Horizontal )
		);
		Assert.Equal( "-", representation.Content );
		Assert.True( representation.UsesAlternateCharacterSet );

		TerminalScreenOperationPlan enter = session.Screen.PlanAlternateCharacterSet(
			enabled: true
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.Add( enter );
		transaction.WriteText( representation.Content );
		transaction.Add(
			session.Screen.PlanAlternateCharacterSet( enabled: false )
				?? throw new InvalidOperationException()
		);
		await transaction.CommitAsync();

		Assert.Equal( Encoding.UTF8.GetBytes( "<acs>-</acs>" ), output.Bytes );
	}

	[Fact]
	public async Task EditingPlannerSelectsCheapestSafeCapabilityAndReportsLines() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOperationPlan insert = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Insert,
			2
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan erase = session.Screen.PlanErase(
			TerminalScreenEraseKind.ToEndOfScreen,
			affectedLines: 4
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan region = session.Screen.PlanScrollRegion(
			topRow: 1,
			bottomRow: 8,
			affectedLines: 8
		) ?? throw new InvalidOperationException();

		Assert.Equal( 2, insert.ByteCount );
		Assert.Equal( 1, insert.AffectedLines );
		Assert.Equal( 4, erase.AffectedLines );
		Assert.Equal( 8, region.AffectedLines );

		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.Add( insert );
		transaction.Add( erase );
		transaction.Add( region );
		await transaction.CommitAsync();

		Assert.Equal( Encoding.Latin1.GetBytes( "ii<ed><csr:1,8>" ), output.Bytes );
	}

	[Fact]
	public async Task LineShiftPlannerCoversInsertDeleteAndScrollFamilies() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenLineShiftKind[] kinds = [
			TerminalScreenLineShiftKind.Insert,
			TerminalScreenLineShiftKind.Delete,
			TerminalScreenLineShiftKind.ScrollForward,
			TerminalScreenLineShiftKind.ScrollReverse
		];
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		foreach ( TerminalScreenLineShiftKind kind in kinds ) {
			TerminalScreenOperationPlan plan = session.Screen.PlanLineShift(
				kind,
				2,
				affectedLines: 5
			) ?? throw new InvalidOperationException();
			Assert.Equal( 5, plan.AffectedLines );
			transaction.Add( plan );
		}
		await transaction.CommitAsync();

		Assert.Equal( Encoding.Latin1.GetBytes( "LLDDFFRR" ), output.Bytes );
	}

	[Theory]
	[InlineData( 1_048_577, "<ich:1048577><sf:1048577><right:1048577>" )]
	[InlineData( int.MaxValue, "<ich:2147483647><sf:2147483647><right:2147483647>" )]
	public async Task LargeCountsSelectParameterizedPlansWithoutMaterializingLosingFallbacks(
		int count,
		string expected
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "parameterized-repetition" )
			.SetString( StringCapability.InsertCharacters, "<ich:%p1%d>" )
			.SetString( StringCapability.InsertCharacter, "i" )
			.SetString( StringCapability.ScrollForwardLines, "<sf:%p1%d>" )
			.SetString( StringCapability.ScrollForward, "F" )
			.SetString( StringCapability.CursorRight, "<right:%p1%d>" )
			.SetString( StringCapability.CursorRightOne, "r" )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );
		TerminalScreenOperationPlan character = session.Screen.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Insert, count
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan line = session.Screen.PlanLineShift(
			TerminalScreenLineShiftKind.ScrollForward, count, 5
		) ?? throw new InvalidOperationException();
		TerminalScreenOperationPlan cursor = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 0, 0 ), new TerminalScreenPosition( 0, count )
		) ?? throw new InvalidOperationException();
		Assert.Empty( output.Bytes );

		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.Add( character );
		transaction.Add( line );
		transaction.Add( cursor );
		await transaction.CommitAsync();

		Assert.Equal( expected.Length, character.ByteCount + line.ByteCount + cursor.ByteCount );
		Assert.Equal( Encoding.Latin1.GetBytes( expected ), output.Bytes );
	}

	[Theory]
	[InlineData( "i", 1_048_577 )]
	[InlineData( "$<1>", 1_048_577 )]
	[InlineData( "i", int.MaxValue )]
	[InlineData( "$<1>", int.MaxValue )]
	public async Task ExcessiveFallbackOnlyRepetitionReturnsNoPlan( string literal, int count ) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "repetition-only" )
			.SetString( StringCapability.InsertCharacter, literal )
			.SetString( StringCapability.ScrollForward, literal )
			.SetString( StringCapability.CursorRightOne, literal )
			.Build();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output, terminal );

		Assert.Null( session.Screen.PlanCharacterShift( TerminalScreenCharacterShiftKind.Insert, count ) );
		Assert.Null( session.Screen.PlanLineShift( TerminalScreenLineShiftKind.ScrollForward, count, 5 ) );
		Assert.Null( session.Screen.PlanCursorMove( new TerminalScreenPosition( 0, 0 ), new TerminalScreenPosition( 0, count ) ) );
		Assert.Empty( output.Bytes );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalOutput output,
		TerminalDescription? terminalOverride = null
	) {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "semantic-screen" )
			.SetNumber( NumericCapability.Colors, 16 )
			.SetNumber( NumericCapability.NoColorVideo, 32 )
			.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" )
			.SetString( StringCapability.EnterBoldMode, "<b>" )
			.SetString( StringCapability.EnterUnderlineMode, "<u>" )
			.SetString( StringCapability.ExitAttributeMode, "</>" )
			.SetString( StringCapability.SetForegroundColor, "<f:%p1%d>" )
			.SetString( StringCapability.SetBackgroundColor, "<b:%p1%d>" )
			.SetString( StringCapability.OriginalColorPair, "<op>" )
			.SetString( StringCapability.AlternateCharacterSet, "q-x|l+" )
			.SetString( StringCapability.EnterAlternateCharacterSetMode, "<acs>" )
			.SetString( StringCapability.ExitAlternateCharacterSetMode, "</acs>" )
			.SetString( StringCapability.CursorInvisible, "<hide>" )
			.SetString( StringCapability.CursorNormal, "<normal>" )
			.SetString( StringCapability.CursorVeryVisible, "<very>" )
			.SetString( StringCapability.InsertCharacters, "<ich:%p1%d>" )
			.SetString( StringCapability.InsertCharacter, "i" )
			.SetString( StringCapability.DeleteCharacters, "<dch:%p1%d>" )
			.SetString( StringCapability.DeleteCharacter, "d" )
			.SetString( StringCapability.InsertLines, "<il:%p1%d>" )
			.SetString( StringCapability.InsertLine, "L" )
			.SetString( StringCapability.DeleteLines, "<dl:%p1%d>" )
			.SetString( StringCapability.DeleteLine, "D" )
			.SetString( StringCapability.ScrollForwardLines, "<sf:%p1%d>" )
			.SetString( StringCapability.ScrollForward, "F" )
			.SetString( StringCapability.ScrollReverseLines, "<sr:%p1%d>" )
			.SetString( StringCapability.ScrollReverse, "R" )
			.SetString( StringCapability.ClearToEndOfLine, "<el>" )
			.SetString( StringCapability.ClearToEndOfScreen, "<ed>" )
			.SetString( StringCapability.ClearScreen, "<clear>" )
			.SetString( StringCapability.ChangeScrollRegion, "<csr:%p1%d,%p2%d>" )
			.Build();

		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminalOverride ?? terminal,
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
