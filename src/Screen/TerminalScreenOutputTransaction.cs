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

/// <summary>Builds one bounded, ordered, session-bound terminal screen output transaction.</summary>
public sealed class TerminalScreenOutputTransaction {
	private const int MaximumItemCount = 65_536;
	private const int MaximumPayloadByteCount = 64 * 1024 * 1024;

	private readonly TerminalSession session;
	private readonly long outputEpoch;
	private readonly List<OutputItem> items = [];
	private int payloadByteCount;
	private int commitStarted;

	internal TerminalScreenOutputTransaction(
		TerminalSession session,
		long outputEpoch
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
		this.outputEpoch = outputEpoch;
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
		this.AddItem( new OutputItem( plan.Segments!, null ) );
	}

	/// <summary>Adds application text using the owning session's configured encoding.</summary>
	public void WriteText(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		this.ThrowIfCommitStarted();
		byte[] bytes = this.session.EncodeApplicationText( value );
		this.payloadByteCount = checked( this.payloadByteCount + bytes.Length );
		if ( MaximumPayloadByteCount < this.payloadByteCount ) {
			throw new InvalidOperationException( "The screen-output transaction exceeds its application-payload limit." );
		}
		this.AddItem( new OutputItem( null, bytes ) );
	}

	/// <summary>Commits this transaction exactly once under the owning session's output gate.</summary>
	public async ValueTask CommitAsync(
		CancellationToken cancellationToken = default
	) {
		if ( 0 != Interlocked.Exchange( ref this.commitStarted, 1 ) ) {
			throw new InvalidOperationException( "A screen-output transaction can be committed only once." );
		}
		cancellationToken.ThrowIfCancellationRequested();

		using IDisposable outputLease = await this.session.AcquireScreenOutputAsync(
			this.outputEpoch,
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		foreach ( OutputItem item in this.items ) {
			if ( item.Segments is not null ) {
				foreach ( TerminalScreenOutputSegment segment in item.Segments ) {
					await this.session.WriteTerminalStringCoreAsync(
						segment.Value,
						segment.AffectedLines,
						CancellationToken.None
					).ConfigureAwait( false );
				}
			} else {
				await this.session.Output.WriteAsync(
					item.Bytes!,
					CancellationToken.None
				).ConfigureAwait( false );
			}
		}
		await this.session.Output.FlushAsync( CancellationToken.None ).ConfigureAwait( false );
	}

	private void AddItem(
		OutputItem item
	) {
		if ( MaximumItemCount <= this.items.Count ) {
			throw new InvalidOperationException( "The screen-output transaction exceeds its item-count limit." );
		}
		this.items.Add( item );
	}

	private void ThrowIfCommitStarted() {
		if ( 0 != Volatile.Read( ref this.commitStarted ) ) {
			throw new InvalidOperationException( "A screen-output transaction cannot be changed after commit begins." );
		}
	}

	private readonly record struct OutputItem(
		IReadOnlyList<TerminalScreenOutputSegment>? Segments,
		byte[]? Bytes
	);
}
