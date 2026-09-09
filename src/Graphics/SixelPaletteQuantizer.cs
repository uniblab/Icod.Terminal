/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

/// <summary>
/// Owns one bounded indexed Sixel-ready raster produced by D174 quantization.
/// </summary>
internal sealed class SixelPaletteImage {
	private readonly byte[] indices;
	private readonly TerminalRasterColor[] palette;
	private readonly byte[] transparentMask;

	internal SixelPaletteImage(
		int width,
		int height,
		byte[] indices,
		TerminalRasterColor[] palette,
		byte[] transparentMask
	) {
		ArgumentNullException.ThrowIfNull( indices );
		ArgumentNullException.ThrowIfNull( palette );
		ArgumentNullException.ThrowIfNull( transparentMask );
		if ( 0 >= width ) {
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}
		if ( 0 >= height ) {
			throw new ArgumentOutOfRangeException( nameof( height ) );
		}

		int pixelCount = checked( width * height );
		if ( pixelCount != indices.Length ) {
			throw new ArgumentException(
				"A Sixel palette image requires exactly one index byte per pixel.",
				nameof( indices )
			);
		}
		if ( 0 != transparentMask.Length && pixelCount != transparentMask.Length ) {
			throw new ArgumentException(
				"A Sixel transparency mask must be empty or contain exactly one byte per pixel.",
				nameof( transparentMask )
			);
		}
		if ( TerminalRasterImage.MaximumPaletteEntries < palette.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( palette ),
				palette.Length,
				$"A Sixel palette cannot contain more than {TerminalRasterImage.MaximumPaletteEntries} entries."
			);
		}
		for ( int index = 0; index < palette.Length; index++ ) {
			if ( byte.MaxValue != palette[ index ].Alpha ) {
				throw new ArgumentException(
					"A Sixel palette entry must be fully opaque; transparency is represented by the separate mask.",
					nameof( palette )
				);
			}
		}

		this.Width = width;
		this.Height = height;
		this.PixelCount = pixelCount;
		this.indices = indices;
		this.palette = palette;
		this.transparentMask = transparentMask;
	}

	internal int Width {
		get;
	}

	internal int Height {
		get;
	}

	internal int PixelCount {
		get;
	}

	internal bool HasTransparency {
		get {
			return 0 != this.transparentMask.Length;
		}
	}

	internal ReadOnlyMemory<byte> Indices {
		get {
			return this.indices;
		}
	}

	internal ReadOnlyMemory<TerminalRasterColor> Palette {
		get {
			return this.palette;
		}
	}

	internal ReadOnlyMemory<byte> TransparentMask {
		get {
			return this.transparentMask;
		}
	}

	internal bool IsTransparent(
		int pixelIndex
	) {
		if ( pixelIndex is < 0 || this.PixelCount <= pixelIndex ) {
			throw new ArgumentOutOfRangeException(
				nameof( pixelIndex ),
				pixelIndex,
				$"A Sixel pixel index must be between 0 and {this.PixelCount - 1}."
			);
		}

		return 0 != this.transparentMask.Length
			&& 0 != this.transparentMask[ pixelIndex ];
	}
}

/// <summary>
/// Converts D173 raster snapshots into bounded deterministic Sixel palettes.
/// </summary>
internal static class SixelPaletteQuantizer {
	internal const int DefaultMaximumColors = 256;

	private const int HistogramChannelBits = 5;
	private const int HistogramChannelSize = 1 << HistogramChannelBits;
	private const int HistogramChannelShift = 8 - HistogramChannelBits;
	private const int HistogramBinCount =
		HistogramChannelSize * HistogramChannelSize * HistogramChannelSize;

	internal static SixelPaletteImage Quantize(
		TerminalRasterImage image,
		int maximumColors = DefaultMaximumColors
	) {
		ArgumentNullException.ThrowIfNull( image );
		if ( maximumColors is < 1 or > DefaultMaximumColors ) {
			throw new ArgumentOutOfRangeException(
				nameof( maximumColors ),
				maximumColors,
				$"A Sixel palette ceiling must be between 1 and {DefaultMaximumColors}."
			);
		}

		if ( TryCreateIndexedPassthrough(
			image,
			maximumColors,
			out SixelPaletteImage? passthrough
		) ) {
			return passthrough;
		}

		int[] histogramCounts = new int[ HistogramBinCount ];
		long[] histogramRedSums = new long[ HistogramBinCount ];
		long[] histogramGreenSums = new long[ HistogramBinCount ];
		long[] histogramBlueSums = new long[ HistogramBinCount ];
		Dictionary<int, byte> exactLookup = new();
		List<TerminalRasterColor> exactColors = new(
			maximumColors
		);
		byte[]? transparentMask = null;
		bool exactOverflow = false;
		int opaquePixelCount = 0;

		for ( int pixelIndex = 0; pixelIndex < image.PixelCount; pixelIndex++ ) {
			TerminalRasterColor color = GetSourceColor(
				image,
				pixelIndex
			);
			if ( 0 == color.Alpha ) {
				transparentMask ??= new byte[ image.PixelCount ];
				transparentMask[ pixelIndex ] = 1;
				continue;
			}
			if ( byte.MaxValue != color.Alpha ) {
				throw new NotSupportedException(
					"Sixel output cannot preserve fractional alpha. Pixels must be fully transparent or fully opaque."
				);
			}

			opaquePixelCount++;
			int packed = PackRgb( color );
			if ( !exactOverflow && !exactLookup.ContainsKey( packed ) ) {
				if ( exactColors.Count < maximumColors ) {
					byte paletteIndex = checked( (byte)exactColors.Count );
					exactLookup.Add(
						packed,
						paletteIndex
					);
					exactColors.Add(
						new TerminalRasterColor(
							color.Red,
							color.Green,
							color.Blue
						)
					);
				} else {
					exactOverflow = true;
				}
			}

			int histogramIndex = GetHistogramIndex( color );
			histogramCounts[ histogramIndex ] = checked(
				histogramCounts[ histogramIndex ] + 1
			);
			histogramRedSums[ histogramIndex ] += color.Red;
			histogramGreenSums[ histogramIndex ] += color.Green;
			histogramBlueSums[ histogramIndex ] += color.Blue;
		}

		byte[] mask = transparentMask ?? Array.Empty<byte>();
		if ( 0 == opaquePixelCount ) {
			return new SixelPaletteImage(
				image.Width,
				image.Height,
				new byte[ image.PixelCount ],
				Array.Empty<TerminalRasterColor>(),
				mask
			);
		}

		if ( !exactOverflow ) {
			return CreateExactPaletteImage(
				image,
				exactColors,
				exactLookup,
				mask
			);
		}

		return CreateReducedPaletteImage(
			image,
			maximumColors,
			histogramCounts,
			histogramRedSums,
			histogramGreenSums,
			histogramBlueSums,
			mask
		);
	}

	private static bool TryCreateIndexedPassthrough(
		TerminalRasterImage image,
		int maximumColors,
		out SixelPaletteImage? result
	) {
		ArgumentNullException.ThrowIfNull( image );
		if ( maximumColors is < 1 or > DefaultMaximumColors ) {
			throw new ArgumentOutOfRangeException( nameof( maximumColors ) );
		}

		result = null;
		if ( TerminalRasterPixelFormat.Indexed8 != image.PixelFormat
			|| maximumColors < image.Palette.Length ) {
			return false;
		}

		ReadOnlySpan<TerminalRasterColor> palette = image.Palette.Span;
		for ( int index = 0; index < palette.Length; index++ ) {
			if ( byte.MaxValue != palette[ index ].Alpha ) {
				return false;
			}
		}

		result = new SixelPaletteImage(
			image.Width,
			image.Height,
			image.PixelBytes.ToArray(),
			palette.ToArray(),
			Array.Empty<byte>()
		);
		return true;
	}

	private static SixelPaletteImage CreateExactPaletteImage(
		TerminalRasterImage image,
		IReadOnlyList<TerminalRasterColor> exactColors,
		IReadOnlyDictionary<int, byte> exactLookup,
		byte[] transparentMask
	) {
		ArgumentNullException.ThrowIfNull( image );
		ArgumentNullException.ThrowIfNull( exactColors );
		ArgumentNullException.ThrowIfNull( exactLookup );
		ArgumentNullException.ThrowIfNull( transparentMask );

		byte[] indices = new byte[ image.PixelCount ];
		for ( int pixelIndex = 0; pixelIndex < image.PixelCount; pixelIndex++ ) {
			if ( 0 != transparentMask.Length
				&& 0 != transparentMask[ pixelIndex ] ) {
				continue;
			}

			TerminalRasterColor color = GetSourceColor(
				image,
				pixelIndex
			);
			indices[ pixelIndex ] = exactLookup[ PackRgb( color ) ];
		}

		return new SixelPaletteImage(
			image.Width,
			image.Height,
			indices,
			exactColors.ToArray(),
			transparentMask
		);
	}

	private static SixelPaletteImage CreateReducedPaletteImage(
		TerminalRasterImage image,
		int maximumColors,
		int[] histogramCounts,
		long[] histogramRedSums,
		long[] histogramGreenSums,
		long[] histogramBlueSums,
		byte[] transparentMask
	) {
		ArgumentNullException.ThrowIfNull( image );
		if ( maximumColors is < 1 or > DefaultMaximumColors ) {
			throw new ArgumentOutOfRangeException( nameof( maximumColors ) );
		}
		ArgumentNullException.ThrowIfNull( histogramCounts );
		ArgumentNullException.ThrowIfNull( histogramRedSums );
		ArgumentNullException.ThrowIfNull( histogramGreenSums );
		ArgumentNullException.ThrowIfNull( histogramBlueSums );
		ArgumentNullException.ThrowIfNull( transparentMask );

		List<HistogramBin> occupiedBins = new();
		for ( int histogramIndex = 0; histogramIndex < HistogramBinCount; histogramIndex++ ) {
			int count = histogramCounts[ histogramIndex ];
			if ( 0 == count ) {
				continue;
			}

			occupiedBins.Add(
				new HistogramBin(
					histogramIndex,
					count,
					histogramRedSums[ histogramIndex ],
					histogramGreenSums[ histogramIndex ],
					histogramBlueSums[ histogramIndex ]
				)
			);
		}
		if ( 0 == occupiedBins.Count ) {
			throw new InvalidOperationException(
				"A reduced Sixel palette requires at least one opaque histogram bin."
			);
		}

		List<ColorBox> boxes = [ new ColorBox( occupiedBins ) ];
		while ( boxes.Count < maximumColors ) {
			int selected = SelectBoxToSplit( boxes );
			if ( 0 > selected ) {
				break;
			}

			SplitBox(
				boxes,
				selected
			);
		}

		List<PaletteBox> paletteBoxes = new( boxes.Count );
		for ( int index = 0; index < boxes.Count; index++ ) {
			ColorBox box = boxes[ index ];
			TerminalRasterColor color = box.GetRepresentativeColor();
			paletteBoxes.Add(
				new PaletteBox(
					box,
					color,
					PackRgb( color )
				)
			);
		}
		paletteBoxes.Sort( ComparePaletteBoxes );

		TerminalRasterColor[] palette = new TerminalRasterColor[ paletteBoxes.Count ];
		for ( int index = 0; index < paletteBoxes.Count; index++ ) {
			palette[ index ] = paletteBoxes[ index ].Color;
		}

		byte[] histogramToPalette = new byte[ HistogramBinCount ];
		for ( int index = 0; index < occupiedBins.Count; index++ ) {
			HistogramBin bin = occupiedBins[ index ];
			histogramToPalette[ bin.HistogramIndex ] = FindNearestPaletteIndex(
				bin.AverageRed,
				bin.AverageGreen,
				bin.AverageBlue,
				palette
			);
		}

		byte[] indices = new byte[ image.PixelCount ];
		for ( int pixelIndex = 0; pixelIndex < image.PixelCount; pixelIndex++ ) {
			if ( 0 != transparentMask.Length
				&& 0 != transparentMask[ pixelIndex ] ) {
				continue;
			}

			TerminalRasterColor color = GetSourceColor(
				image,
				pixelIndex
			);
			indices[ pixelIndex ] = histogramToPalette[ GetHistogramIndex( color ) ];
		}

		return new SixelPaletteImage(
			image.Width,
			image.Height,
			indices,
			palette,
			transparentMask
		);
	}

	private static int SelectBoxToSplit(
		IReadOnlyList<ColorBox> boxes
	) {
		ArgumentNullException.ThrowIfNull( boxes );

		int selected = -1;
		for ( int index = 0; index < boxes.Count; index++ ) {
			ColorBox candidate = boxes[ index ];
			if ( 2 > candidate.Bins.Count ) {
				continue;
			}
			if ( 0 > selected || IsPreferredSplitCandidate(
				candidate,
				boxes[ selected ]
			) ) {
				selected = index;
			}
		}

		return selected;
	}

	private static bool IsPreferredSplitCandidate(
		ColorBox candidate,
		ColorBox current
	) {
		ArgumentNullException.ThrowIfNull( candidate );
		ArgumentNullException.ThrowIfNull( current );

		long candidateScore = checked(
			(long)candidate.MaximumRange * candidate.PixelCount
		);
		long currentScore = checked(
			(long)current.MaximumRange * current.PixelCount
		);
		if ( candidateScore != currentScore ) {
			return candidateScore > currentScore;
		}
		if ( candidate.MaximumRange != current.MaximumRange ) {
			return candidate.MaximumRange > current.MaximumRange;
		}
		if ( candidate.PixelCount != current.PixelCount ) {
			return candidate.PixelCount > current.PixelCount;
		}

		return candidate.MinimumPackedRgb < current.MinimumPackedRgb;
	}

	private static void SplitBox(
		List<ColorBox> boxes,
		int selected
	) {
		ArgumentNullException.ThrowIfNull( boxes );
		if ( selected is < 0 || boxes.Count <= selected ) {
			throw new ArgumentOutOfRangeException( nameof( selected ) );
		}

		ColorBox source = boxes[ selected ];
		if ( 2 > source.Bins.Count ) {
			throw new InvalidOperationException(
				"A Sixel color box must contain at least two bins before it can be split."
			);
		}

		List<HistogramBin> ordered = source.Bins.ToList();
		ColorChannel channel = source.GetSplitChannel();
		ordered.Sort(
			(left, right) => CompareHistogramBins(
				left,
				right,
				channel
			)
		);

		long target = ( source.PixelCount + 1L ) / 2L;
		long cumulative = 0;
		int splitIndex = ordered.Count - 1;
		for ( int index = 0; index < ordered.Count - 1; index++ ) {
			cumulative += ordered[ index ].Count;
			if ( cumulative >= target ) {
				splitIndex = index + 1;
				break;
			}
		}

		ColorBox leftBox = new(
			ordered.GetRange(
				0,
				splitIndex
			)
		);
		ColorBox rightBox = new(
			ordered.GetRange(
				splitIndex,
				ordered.Count - splitIndex
			)
		);
		boxes[ selected ] = leftBox;
		boxes.Insert(
			selected + 1,
			rightBox
		);
	}

	private static int CompareHistogramBins(
		HistogramBin left,
		HistogramBin right,
		ColorChannel channel
	) {
		if ( !Enum.IsDefined( channel ) ) {
			throw new ArgumentOutOfRangeException( nameof( channel ) );
		}

		int comparison;
		switch ( channel ) {
			case ColorChannel.Red:
				comparison = left.AverageRed.CompareTo( right.AverageRed );
				if ( 0 != comparison ) {
					return comparison;
				}
				comparison = left.AverageGreen.CompareTo( right.AverageGreen );
				if ( 0 != comparison ) {
					return comparison;
				}
				comparison = left.AverageBlue.CompareTo( right.AverageBlue );
				break;

			case ColorChannel.Green:
				comparison = left.AverageGreen.CompareTo( right.AverageGreen );
				if ( 0 != comparison ) {
					return comparison;
				}
				comparison = left.AverageRed.CompareTo( right.AverageRed );
				if ( 0 != comparison ) {
					return comparison;
				}
				comparison = left.AverageBlue.CompareTo( right.AverageBlue );
				break;

			case ColorChannel.Blue:
				comparison = left.AverageBlue.CompareTo( right.AverageBlue );
				if ( 0 != comparison ) {
					return comparison;
				}
				comparison = left.AverageRed.CompareTo( right.AverageRed );
				if ( 0 != comparison ) {
					return comparison;
				}
				comparison = left.AverageGreen.CompareTo( right.AverageGreen );
				break;

			default:
				throw new ArgumentOutOfRangeException( nameof( channel ) );
		}
		if ( 0 != comparison ) {
			return comparison;
		}

		return left.HistogramIndex.CompareTo( right.HistogramIndex );
	}

	private static int ComparePaletteBoxes(
		PaletteBox left,
		PaletteBox right
	) {
		int comparison = left.PackedRgb.CompareTo( right.PackedRgb );
		if ( 0 != comparison ) {
			return comparison;
		}

		return left.Box.MinimumPackedRgb.CompareTo(
			right.Box.MinimumPackedRgb
		);
	}

	private static byte FindNearestPaletteIndex(
		byte red,
		byte green,
		byte blue,
		IReadOnlyList<TerminalRasterColor> palette
	) {
		ArgumentNullException.ThrowIfNull( palette );
		if ( 0 == palette.Count ) {
			throw new ArgumentException(
				"A nearest-color search requires a non-empty palette.",
				nameof( palette )
			);
		}

		int bestIndex = 0;
		int bestDistance = GetColorDistanceSquared(
			red,
			green,
			blue,
			palette[ 0 ]
		);
		for ( int index = 1; index < palette.Count; index++ ) {
			int distance = GetColorDistanceSquared(
				red,
				green,
				blue,
				palette[ index ]
			);
			if ( distance < bestDistance ) {
				bestDistance = distance;
				bestIndex = index;
			}
		}

		return checked( (byte)bestIndex );
	}

	private static int GetColorDistanceSquared(
		byte red,
		byte green,
		byte blue,
		TerminalRasterColor paletteColor
	) {
		int redDifference = red - paletteColor.Red;
		int greenDifference = green - paletteColor.Green;
		int blueDifference = blue - paletteColor.Blue;
		return checked(
			redDifference * redDifference
				+ greenDifference * greenDifference
				+ blueDifference * blueDifference
		);
	}

	private static TerminalRasterColor GetSourceColor(
		TerminalRasterImage image,
		int pixelIndex
	) {
		ArgumentNullException.ThrowIfNull( image );
		if ( pixelIndex is < 0 || image.PixelCount <= pixelIndex ) {
			throw new ArgumentOutOfRangeException( nameof( pixelIndex ) );
		}

		ReadOnlySpan<byte> pixels = image.PixelBytes.Span;
		switch ( image.PixelFormat ) {
			case TerminalRasterPixelFormat.Rgb24: {
				int offset = checked( pixelIndex * 3 );
				return new TerminalRasterColor(
					pixels[ offset ],
					pixels[ offset + 1 ],
					pixels[ offset + 2 ]
				);
			}

			case TerminalRasterPixelFormat.Rgba32: {
				int offset = checked( pixelIndex * 4 );
				return new TerminalRasterColor(
					pixels[ offset ],
					pixels[ offset + 1 ],
					pixels[ offset + 2 ],
					pixels[ offset + 3 ]
				);
			}

			case TerminalRasterPixelFormat.Indexed8:
				return image.Palette.Span[ pixels[ pixelIndex ] ];

			default:
				throw new InvalidOperationException(
					"The raster image contains an unknown pixel format."
				);
		}
	}

	private static int GetHistogramIndex(
		TerminalRasterColor color
	) {
		int red = color.Red >> HistogramChannelShift;
		int green = color.Green >> HistogramChannelShift;
		int blue = color.Blue >> HistogramChannelShift;
		return ( red << ( HistogramChannelBits * 2 ) )
			| ( green << HistogramChannelBits )
			| blue;
	}

	private static int PackRgb(
		TerminalRasterColor color
	) {
		return ( color.Red << 16 )
			| ( color.Green << 8 )
			| color.Blue;
	}

	private enum ColorChannel {
		Red,
		Green,
		Blue
	}

	private readonly record struct HistogramBin {
		internal HistogramBin(
			int histogramIndex,
			int count,
			long redSum,
			long greenSum,
			long blueSum
		) {
			if ( histogramIndex is < 0 or >= HistogramBinCount ) {
				throw new ArgumentOutOfRangeException( nameof( histogramIndex ) );
			}
			if ( 0 >= count ) {
				throw new ArgumentOutOfRangeException( nameof( count ) );
			}
			if ( redSum is < 0 || greenSum is < 0 || blueSum is < 0 ) {
				throw new ArgumentOutOfRangeException( nameof( redSum ) );
			}

			this.HistogramIndex = histogramIndex;
			this.Count = count;
			this.RedSum = redSum;
			this.GreenSum = greenSum;
			this.BlueSum = blueSum;
			this.AverageRed = RoundAverage(
				redSum,
				count
			);
			this.AverageGreen = RoundAverage(
				greenSum,
				count
			);
			this.AverageBlue = RoundAverage(
				blueSum,
				count
			);
		}

		internal int HistogramIndex {
			get;
		}

		internal int Count {
			get;
		}

		internal long RedSum {
			get;
		}

		internal long GreenSum {
			get;
		}

		internal long BlueSum {
			get;
		}

		internal byte AverageRed {
			get;
		}

		internal byte AverageGreen {
			get;
		}

		internal byte AverageBlue {
			get;
		}

		internal int PackedAverageRgb {
			get {
				return ( this.AverageRed << 16 )
					| ( this.AverageGreen << 8 )
					| this.AverageBlue;
			}
		}
	}

	private sealed class ColorBox {
		internal ColorBox(
			List<HistogramBin> bins
		) {
			ArgumentNullException.ThrowIfNull( bins );
			if ( 0 == bins.Count ) {
				throw new ArgumentException(
					"A Sixel color box requires at least one histogram bin.",
					nameof( bins )
				);
			}

			this.Bins = bins;
			int minimumRed = byte.MaxValue;
			int minimumGreen = byte.MaxValue;
			int minimumBlue = byte.MaxValue;
			int maximumRed = byte.MinValue;
			int maximumGreen = byte.MinValue;
			int maximumBlue = byte.MinValue;
			int pixelCount = 0;
			int minimumPackedRgb = int.MaxValue;
			for ( int index = 0; index < bins.Count; index++ ) {
				HistogramBin bin = bins[ index ];
				minimumRed = Math.Min( minimumRed, bin.AverageRed );
				minimumGreen = Math.Min( minimumGreen, bin.AverageGreen );
				minimumBlue = Math.Min( minimumBlue, bin.AverageBlue );
				maximumRed = Math.Max( maximumRed, bin.AverageRed );
				maximumGreen = Math.Max( maximumGreen, bin.AverageGreen );
				maximumBlue = Math.Max( maximumBlue, bin.AverageBlue );
				pixelCount = checked( pixelCount + bin.Count );
				minimumPackedRgb = Math.Min(
					minimumPackedRgb,
					bin.PackedAverageRgb
				);
			}

			this.PixelCount = pixelCount;
			this.RedRange = maximumRed - minimumRed;
			this.GreenRange = maximumGreen - minimumGreen;
			this.BlueRange = maximumBlue - minimumBlue;
			this.MaximumRange = Math.Max(
				this.RedRange,
				Math.Max(
					this.GreenRange,
					this.BlueRange
				)
			);
			this.MinimumPackedRgb = minimumPackedRgb;
		}

		internal List<HistogramBin> Bins {
			get;
		}

		internal int PixelCount {
			get;
		}

		internal int RedRange {
			get;
		}

		internal int GreenRange {
			get;
		}

		internal int BlueRange {
			get;
		}

		internal int MaximumRange {
			get;
		}

		internal int MinimumPackedRgb {
			get;
		}

		internal ColorChannel GetSplitChannel() {
			if ( this.RedRange >= this.GreenRange
				&& this.RedRange >= this.BlueRange ) {
				return ColorChannel.Red;
			}
			if ( this.GreenRange >= this.BlueRange ) {
				return ColorChannel.Green;
			}

			return ColorChannel.Blue;
		}

		internal TerminalRasterColor GetRepresentativeColor() {
			long redSum = 0;
			long greenSum = 0;
			long blueSum = 0;
			int count = 0;
			for ( int index = 0; index < this.Bins.Count; index++ ) {
				HistogramBin bin = this.Bins[ index ];
				redSum += bin.RedSum;
				greenSum += bin.GreenSum;
				blueSum += bin.BlueSum;
				count = checked( count + bin.Count );
			}

			return new TerminalRasterColor(
				RoundAverage(
					redSum,
					count
				),
				RoundAverage(
					greenSum,
					count
				),
				RoundAverage(
					blueSum,
					count
				)
			);
		}
	}

	private readonly record struct PaletteBox(
		ColorBox Box,
		TerminalRasterColor Color,
		int PackedRgb
	);

	private static byte RoundAverage(
		long sum,
		int count
	) {
		if ( 0 > sum ) {
			throw new ArgumentOutOfRangeException( nameof( sum ) );
		}
		if ( 0 >= count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}

		return checked(
			(byte)( ( sum + count / 2L ) / count )
		);
	}
}
