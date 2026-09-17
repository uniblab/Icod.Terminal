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

using System.Runtime.ExceptionServices;

/// <summary>Builds one bounded, ordered, session-bound terminal screen output transaction.</summary>
public sealed class TerminalScreenOutputTransaction {
	private const int MaximumItemCount = 65_536;
	private const int MaximumPayloadByteCount = 64 * 1024 * 1024;

	private readonly TerminalSession session;
	private readonly long outputEpoch;
	private readonly bool useSynchronizedOutput;
	private readonly List<OutputItem> items = [];
	private int payloadByteCount;
	private int retainedItemCount;
	private int commitStarted;

	internal TerminalScreenOutputTransaction(
		TerminalSession session,
		long outputEpoch,
		TerminalScreenOutputTransactionOptions options
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( options );
		this.session = session;
		this.outputEpoch = outputEpoch;
		this.useSynchronizedOutput = options.UseSynchronizedOutput;
	}

	/// <summary>Adds one opaque semantic operation plan from the owning session.</summary>
	public void Add(
		TerminalScreenOperationPlan plan
	) {
		this.ThrowIfCommitStarted();
		if ( !plan.IsValid ) {
			throw new ArgumentException( "The default screen-operation plan is not valid.", nameof( plan ) );
		}
		if ( !ReferenceEquals( this.session.Screen, plan.Owner ) ) {
			throw new ArgumentException( "The screen-operation plan belongs to another terminal session.", nameof( plan ) );
		}
		this.AddItem( OutputItem.ForPlan( plan.Segments! ) );
	}

	/// <summary>Adds application text using the owning session's configured encoding.</summary>
	public void WriteText(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		this.ThrowIfCommitStarted();
		byte[] bytes = this.session.EncodeApplicationText( value );
		this.AddPayloadBytes( bytes.Length );
		this.AddItem( OutputItem.ForBytes( bytes ) );
	}

	/// <summary>Adds one bounded strict OSC 8 hyperlink and its application text.</summary>
	public void WriteHyperlink(
		string value,
		string uri,
		string? identifier = null
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( uri );
		this.ThrowIfCommitStarted();
		byte[] begin = OscWriter.EncodeHyperlinkBeginFrame( uri, identifier );
		byte[] text = this.session.EncodeApplicationText( value );
		byte[] end = OscWriter.EncodeHyperlinkEndFrame();
		this.AddPayloadBytes( text.Length );
		this.AddItem( OutputItem.ForHyperlink( begin, text, end ) );
	}

	/// <summary>Adds one opaque raster-placeholder cell owned by this session.</summary>
	public void WriteRasterPlaceholderCell(
		TerminalRasterPlaceholderCell cell
	) {
		this.ThrowIfCommitStarted();
		_ = this.session.ValidateRasterPlaceholderCellForOutput(
			cell,
			nameof( cell )
		);
		this.AddItem( OutputItem.ForRasterCells( [ cell ] ) );
	}

	/// <summary>Adds opaque raster-placeholder cells in caller-supplied order.</summary>
	public void WriteRasterPlaceholderCells(
		ReadOnlyMemory<TerminalRasterPlaceholderCell> cells
	) {
		this.ThrowIfCommitStarted();
		if ( cells.IsEmpty ) {
			throw new ArgumentException(
				"At least one raster-placeholder cell is required.",
				nameof( cells )
			);
		}
		this.EnsureItemCapacity( cells.Length );
		this.session.ValidateRasterPlaceholderCellsForOutput(
			cells,
			nameof( cells )
		);
		this.AddItem( OutputItem.ForRasterCells( cells.ToArray() ), cells.Length );
	}

	/// <summary>Commits this transaction exactly once under the owning session's output gate.</summary>
	/// <remarks>
	/// Hyperlink items and synchronized framing reject conflicting session-owned leases or
	/// pending cleanup before any transaction output. Transactions without those frames
	/// may emit within existing scopes without changing their ownership.
	/// </remarks>
	public async ValueTask CommitAsync(
		CancellationToken cancellationToken = default
	) {
		if ( 0 != Interlocked.Exchange( ref this.commitStarted, 1 ) ) {
			throw new InvalidOperationException( "A screen-output transaction can be committed only once." );
		}
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateRetainedItems();

		// Manager gates precede the output gate everywhere. Reserve in hyperlink,
		// synchronized-output, output order and retain reservations through cleanup.
		using IDisposable? hyperlinkReservation = this.items.Any(
			static item => OutputItemKind.Hyperlink == item.Kind
		) ? await this.session.ReserveScreenHyperlinkOutputAsync( cancellationToken ).ConfigureAwait( false ) : null;
		using IDisposable? synchronizedReservation = this.useSynchronizedOutput
			? await this.session.SynchronizedOutputManager.ReserveScreenOutputAsync( cancellationToken ).ConfigureAwait( false )
			: null;
		using IDisposable outputLease = await this.session.AcquireScreenOutputAsync(
			this.outputEpoch,
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();
		TerminalPersistentRasterPlaceholderState[]?[] rasterStates =
			this.CaptureRasterStatesForCommit();

		List<Exception> failures = [];
		bool synchronizedCleanupRequired = false;
		try {
			if ( this.useSynchronizedOutput ) {
				synchronizedCleanupRequired = true;
				await this.session.Output.WriteAsync(
					CsiWriter.EncodeSynchronizedOutputBeginFrame(),
					CancellationToken.None
				).ConfigureAwait( false );
			}

			for ( int index = 0; index < this.items.Count; ++index ) {
				if ( OutputItemKind.Hyperlink == this.items[ index ].Kind ) {
					int failureCount = failures.Count;
					await this.WriteHyperlinkItemAsync(
						this.items[ index ],
						failures
					).ConfigureAwait( false );
					if ( failureCount != failures.Count ) {
						break;
					}
					continue;
				}
				await this.WriteItemAsync(
					this.items[ index ],
					rasterStates[ index ]
				).ConfigureAwait( false );
			}
		} catch ( Exception exception ) {
			failures.Add( exception );
		}

		if ( synchronizedCleanupRequired ) {
			try {
				await this.session.Output.WriteAsync(
					CsiWriter.EncodeSynchronizedOutputEndFrame(),
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				failures.Add( exception );
			}
		}

		try {
			await this.session.Output.FlushAsync( CancellationToken.None ).ConfigureAwait( false );
		} catch ( Exception exception ) {
			failures.Add( exception );
		}

		ThrowFailures( failures );
	}

	private async ValueTask WriteItemAsync(
		OutputItem item,
		TerminalPersistentRasterPlaceholderState[]? rasterStates
	) {
		switch ( item.Kind ) {
			case OutputItemKind.Plan:
				foreach ( TerminalScreenOutputSegment segment in item.Segments! ) {
					await this.session.WriteTerminalStringCoreAsync(
						segment.Value,
						segment.AffectedLines,
						CancellationToken.None
					).ConfigureAwait( false );
				}
				break;

			case OutputItemKind.Bytes:
				await this.session.Output.WriteAsync(
					item.Bytes!,
					CancellationToken.None
				).ConfigureAwait( false );
				break;

			case OutputItemKind.RasterCells:
				TerminalRasterPlaceholderCell[] cells = item.RasterCells!;
				if ( rasterStates is null || rasterStates.Length != cells.Length ) {
					throw new InvalidOperationException(
						"Committed raster-placeholder state is inconsistent."
					);
				}
				for ( int index = 0; index < cells.Length; ++index ) {
					TerminalRasterPlaceholderCell cell = cells[ index ];
					await this.session.Output.WriteAsync(
						KittyGraphicsPlaceholderCellEncoder.Encode(
							rasterStates[ index ],
							cell.Row,
							cell.Column
						),
						CancellationToken.None
					).ConfigureAwait( false );
				}
				break;

			default:
				throw new InvalidOperationException( "The screen-output item kind is invalid." );
		}
	}

	private async ValueTask WriteHyperlinkItemAsync(
		OutputItem item,
		List<Exception> failures
	) {
		ArgumentNullException.ThrowIfNull( failures );
		bool cleanupRequired = false;
		try {
			cleanupRequired = true;
			await this.session.Output.WriteAsync(
				item.HyperlinkBegin!,
				CancellationToken.None
			).ConfigureAwait( false );
			await this.session.Output.WriteAsync(
				item.Bytes!,
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception exception ) {
			failures.Add( exception );
		}

		if ( cleanupRequired ) {
			try {
				await this.session.Output.WriteAsync(
					item.HyperlinkEnd!,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				failures.Add( exception );
			}
		}
	}

	private void ValidateRetainedItems() {
		foreach ( OutputItem item in this.items ) {
			if ( OutputItemKind.RasterCells == item.Kind ) {
				this.session.ValidateRasterPlaceholderCellsForOutput(
					item.RasterCells!,
					nameof( item.RasterCells )
				);
			}
		}
	}

	private TerminalPersistentRasterPlaceholderState[]?[] CaptureRasterStatesForCommit() {
		TerminalPersistentRasterPlaceholderState[]?[] result =
			new TerminalPersistentRasterPlaceholderState[]?[ this.items.Count ];
		for ( int itemIndex = 0; itemIndex < this.items.Count; ++itemIndex ) {
			OutputItem item = this.items[ itemIndex ];
			if ( OutputItemKind.RasterCells != item.Kind ) {
				continue;
			}

			TerminalRasterPlaceholderCell[] cells = item.RasterCells!;
			TerminalPersistentRasterPlaceholderState[] states =
				new TerminalPersistentRasterPlaceholderState[ cells.Length ];
			for ( int cellIndex = 0; cellIndex < cells.Length; ++cellIndex ) {
				states[ cellIndex ] = this.session.ValidateRasterPlaceholderCellForOutput(
					cells[ cellIndex ],
					nameof( item.RasterCells )
				);
			}
			result[ itemIndex ] = states;
		}
		return result;
	}

	private void AddPayloadBytes(
		int byteCount
	) {
		if ( MaximumPayloadByteCount - this.payloadByteCount < byteCount ) {
			throw new InvalidOperationException( "The screen-output transaction exceeds its application-payload limit." );
		}
		this.payloadByteCount += byteCount;
	}

	private void AddItem(
		OutputItem item,
		int retainedItemCount = 1
	) {
		this.EnsureItemCapacity( retainedItemCount );
		this.items.Add( item );
		this.retainedItemCount += retainedItemCount;
	}

	private void EnsureItemCapacity(
		int additionalItemCount
	) {
		if ( 0 >= additionalItemCount ) {
			throw new ArgumentOutOfRangeException( nameof( additionalItemCount ) );
		}
		if ( MaximumItemCount - this.retainedItemCount < additionalItemCount ) {
			throw new InvalidOperationException( "The screen-output transaction exceeds its item-count limit." );
		}
	}

	private void ThrowIfCommitStarted() {
		if ( 0 != Volatile.Read( ref this.commitStarted ) ) {
			throw new InvalidOperationException( "A screen-output transaction cannot be changed after commit begins." );
		}
		this.session.ThrowIfSessionOutputClosed();
	}

	private static void ThrowFailures(
		IReadOnlyList<Exception> failures
	) {
		if ( 0 == failures.Count ) {
			return;
		}
		if ( 1 == failures.Count ) {
			ExceptionDispatchInfo.Capture( failures[ 0 ] ).Throw();
		}
		throw new AggregateException( failures );
	}

	private enum OutputItemKind {
		Plan,
		Bytes,
		Hyperlink,
		RasterCells
	}

	private sealed record OutputItem(
		OutputItemKind Kind,
		IReadOnlyList<TerminalScreenOutputSegment>? Segments,
		byte[]? Bytes,
		byte[]? HyperlinkBegin,
		byte[]? HyperlinkEnd,
		TerminalRasterPlaceholderCell[]? RasterCells
	) {
		internal static OutputItem ForPlan(
			IReadOnlyList<TerminalScreenOutputSegment> segments
		) => new( OutputItemKind.Plan, segments, null, null, null, null );

		internal static OutputItem ForBytes(
			byte[] bytes
		) => new( OutputItemKind.Bytes, null, bytes, null, null, null );

		internal static OutputItem ForHyperlink(
			byte[] begin,
			byte[] text,
			byte[] end
		) => new( OutputItemKind.Hyperlink, null, text, begin, end, null );

		internal static OutputItem ForRasterCells(
			TerminalRasterPlaceholderCell[] cells
		) => new( OutputItemKind.RasterCells, null, null, null, null, cells );
	}
}
