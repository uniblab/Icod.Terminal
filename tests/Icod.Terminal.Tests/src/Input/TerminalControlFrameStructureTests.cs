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
namespace Icod.Terminal.Tests.Input;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies N153 structural preservation above normalized family framing.
/// </summary>
public sealed class TerminalControlFrameStructureTests {
	[Fact]
	public void CsiStructurePreservesRawParameterAndIntermediateBytes() {
		byte[] frame = [
			0x1B,
			(byte)'[',
			(byte)'?',
			(byte)'1',
			(byte)';',
			(byte)';',
			(byte)'2',
			(byte)':',
			(byte)'3',
			(byte)' ',
			(byte)'q'
		];

		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			frame,
			TerminalControlFamily.Csi
		);

		Assert.Equal( TerminalControlFamily.Csi, structure.Family );
		Assert.False( structure.UsesEightBitIntroducer );
		Assert.Equal( 2, structure.IntroducerLength );
		Assert.Equal(
			new byte[] {
				(byte)'?',
				(byte)'1',
				(byte)';',
				(byte)';',
				(byte)'2',
				(byte)':',
				(byte)'3'
			},
			structure.ParameterBytes.ToArray()
		);
		Assert.Equal(
			new byte[] { (byte)' ' },
			structure.IntermediateBytes.ToArray()
		);
		Assert.Equal( (byte)'q', structure.FinalByte );
		Assert.Empty( structure.PayloadBytes.ToArray() );
		Assert.Equal( TerminalStringTerminatorKind.None, structure.TerminatorKind );
		Assert.Equal( 0, structure.TerminatorLength );
	}

	[Fact]
	public void EightBitCsiStructureRetainsIntroducerIdentity() {
		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			new byte[] { 0x9B, (byte)'>', (byte)'0', (byte)';', (byte)'1', (byte)'c' },
			TerminalControlFamily.Csi
		);

		Assert.True( structure.UsesEightBitIntroducer );
		Assert.Equal( 1, structure.IntroducerLength );
		Assert.Equal(
			new byte[] { (byte)'>', (byte)'0', (byte)';', (byte)'1' },
			structure.ParameterBytes.ToArray()
		);
		Assert.Empty( structure.IntermediateBytes.ToArray() );
		Assert.Equal( (byte)'c', structure.FinalByte );
	}

	[Fact]
	public void DcsStructureSeparatesHeaderSelectorPayloadAndTerminator() {
		byte[] frame = [
			0x1B,
			(byte)'P',
			(byte)'1',
			(byte)'$',
			(byte)'r',
			(byte)'0',
			(byte)';',
			(byte)'m',
			0x1B,
			(byte)'\\'
		];

		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			frame,
			TerminalControlFamily.Dcs
		);

		Assert.Equal( new byte[] { (byte)'1' }, structure.ParameterBytes.ToArray() );
		Assert.Equal( new byte[] { (byte)'$' }, structure.IntermediateBytes.ToArray() );
		Assert.Equal( (byte)'r', structure.FinalByte );
		Assert.Equal(
			new byte[] { (byte)'0', (byte)';', (byte)'m' },
			structure.PayloadBytes.ToArray()
		);
		Assert.Equal( TerminalStringTerminatorKind.SevenBitSt, structure.TerminatorKind );
		Assert.Equal( 2, structure.TerminatorLength );
	}

	[Fact]
	public void DcsStructurePreservesReleasedMixedStCompatibility() {
		TerminalControlFrameStructure sevenBitWithC1St = TerminalControlFrameStructure.Parse(
			new byte[] { 0x1B, (byte)'P', (byte)'q', (byte)'x', 0x9C },
			TerminalControlFamily.Dcs
		);
		Assert.False( sevenBitWithC1St.UsesEightBitIntroducer );
		Assert.Equal(
			TerminalStringTerminatorKind.EightBitSt,
			sevenBitWithC1St.TerminatorKind
		);

		TerminalControlFrameStructure eightBitWithSevenBitSt = TerminalControlFrameStructure.Parse(
			new byte[] { 0x90, (byte)'q', (byte)'x', 0x1B, (byte)'\\' },
			TerminalControlFamily.Dcs
		);
		Assert.True( eightBitWithSevenBitSt.UsesEightBitIntroducer );
		Assert.Equal(
			TerminalStringTerminatorKind.SevenBitSt,
			eightBitWithSevenBitSt.TerminatorKind
		);
	}

	[Fact]
	public void OscStructureRetainsOpaquePayloadAndBellTermination() {
		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			new byte[] {
				0x1B,
				(byte)']',
				(byte)'5',
				(byte)'2',
				(byte)';',
				(byte)'c',
				(byte)';',
				(byte)'?',
				0x07
			},
			TerminalControlFamily.Osc
		);

		Assert.Empty( structure.ParameterBytes.ToArray() );
		Assert.Empty( structure.IntermediateBytes.ToArray() );
		Assert.Null( structure.FinalByte );
		Assert.Equal(
			new byte[] {
				(byte)'5',
				(byte)'2',
				(byte)';',
				(byte)'c',
				(byte)';',
				(byte)'?'
			},
			structure.PayloadBytes.ToArray()
		);
		Assert.Equal( TerminalStringTerminatorKind.Bell, structure.TerminatorKind );
	}

	[Fact]
	public void ApcStructureDoesNotInterpretKittyControlData() {
		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			new byte[] {
				0x1B,
				(byte)'_',
				(byte)'G',
				(byte)'a',
				(byte)'=',
				(byte)'T',
				(byte)',',
				(byte)'f',
				(byte)'=',
				(byte)'3',
				(byte)'2',
				(byte)';',
				(byte)'Q',
				(byte)'Q',
				(byte)'=',
				(byte)'=',
				0x1B,
				(byte)'\\'
			},
			TerminalControlFamily.Apc
		);

		Assert.Equal( TerminalControlFamily.Apc, structure.Family );
		Assert.Null( structure.FinalByte );
		Assert.Equal(
			"Ga=T,f=32;QQ==",
			System.Text.Encoding.ASCII.GetString( structure.PayloadBytes.Span )
		);
		Assert.Equal( TerminalStringTerminatorKind.SevenBitSt, structure.TerminatorKind );
	}

	[Fact]
	public void StructureRejectsWrongFamilyAndMalformedHeader() {
		Assert.Throws<FormatException>(
			() => TerminalControlFrameStructure.Parse(
				new byte[] { 0x1B, (byte)'_', (byte)'G', 0x1B, (byte)'\\' },
				TerminalControlFamily.Osc
			)
		);

		Assert.Throws<FormatException>(
			() => TerminalControlFrameStructure.Parse(
				new byte[] { 0x1B, (byte)'[', (byte)'1', (byte)'a', (byte)'c' },
				TerminalControlFamily.Csi
			)
		);
	}

	[Fact]
	public void ResponseFrameAdapterUsesReleasedFrameKind() {
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Csi,
			[ 0x1B, (byte)'[', (byte)'?', (byte)'1', (byte)'c' ]
		);

		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse( frame );

		Assert.Equal( TerminalControlFamily.Csi, structure.Family );
		Assert.Equal(
			new byte[] { (byte)'?', (byte)'1' },
			structure.ParameterBytes.ToArray()
		);
		Assert.Equal( (byte)'c', structure.FinalByte );
	}
}
