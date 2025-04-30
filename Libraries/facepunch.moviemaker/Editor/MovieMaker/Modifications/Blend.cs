﻿using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public class BlendModification() : PerTrackModification<BlendOptions>( BlendOptions.Default, false )
{
	public override MovieTimeRange? SourceTimeRange => Options.SourceDuration is { } duration
		? (Options.Offset, Options.Offset + duration)
		: null;

	public void SetFromClipboard( ClipboardData clipboard, MovieTime offset, MovieProject project )
	{
		Options = Options with
		{
			SourceDuration = clipboard.Selection.TotalTimeRange.Duration,
			Offset = offset
		};

		foreach ( var (id, blocks) in clipboard.Tracks )
		{
			if ( blocks.Count == 0 ) continue;
			if ( project.GetTrack( id ) is not IProjectPropertyTrack track ) continue;

			var state = GetOrCreateTrackModificationPreview( track );

			state.Modification = blocks.Select( x => x.Shift( -clipboard.Selection.TotalStart ) ).AsModification();
		}
	}

	public override void AddControls( ToolbarGroup group )
	{
		group.AddToggle( "Additive", "layers",
			() => Options.IsAdditive,
			state => Options = Options with { IsAdditive = state } );
	}

	public bool PreChange( IProjectPropertyTrack track, ITrackProperty property )
	{
		if ( GetTrackModificationPreview( track ) is not null )
		{
			return false;
		}

		var preview = GetOrCreateTrackModificationPreview( track );

		// We create modifications in PreChange so we can capture the pre-change value,
		// used for additive blending

		preview.Modification = property.Value.AsSignal( property.TargetType ).AsModification();

		return true;
	}

	public bool PostChange( IProjectPropertyTrack track, ITrackProperty property )
	{
		if ( GetTrackModificationPreview( track ) is not { Modification: ISignalBlendModification blend } preview )
		{
			return false;
		}

		preview.Modification = blend.WithSignal( property.Value.AsSignal( property.TargetType ) );
		return true;
	}
}

public record ClipboardData( TimeSelection Selection, IReadOnlyDictionary<Guid, IReadOnlyList<IProjectPropertyBlock>> Tracks );

public record BlendOptions( bool IsAdditive, MovieTime Offset, MovieTime? SourceDuration ) : ITranslatableOptions
{
	public static BlendOptions Default { get; } = new( false, default, default );

	public ITranslatableOptions WithOffset( MovieTime offset ) => this with { Offset = offset };
}

public abstract class BlendTrackModification<T> : ITrackModification<T, BlendOptions>
{
	protected PropertyBlock<T> Blend( PropertySignal<T>? original, PropertySignal<T>? overlay,
		PropertySignal<T> relativeTo, MovieTimeRange timeRange, TimeSelection selection, BlendOptions options )
	{
		if ( original is null && overlay is null )
		{
			throw new ArgumentNullException( nameof( overlay ), "Expected at least one signal." );
		}

		if ( overlay is not null )
		{
			overlay += options.Offset;
		}

		if ( original is null || overlay is null )
		{
			return new PropertyBlock<T>( (original ?? overlay)!.Reduce( timeRange ), timeRange );
		}

		if ( options.IsAdditive )
		{
			overlay = overlay - relativeTo + original;
		}

		return new PropertyBlock<T>( original.CrossFade( overlay, selection ).Reduce( timeRange ), timeRange );
	}

	public abstract IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, BlendOptions options );
}

public interface ISignalBlendModification : ITrackModification
{
	ISignalBlendModification WithSignal( IPropertySignal signal );
}

public sealed class SignalBlendModification<T>( PropertySignal<T> signal, PropertySignal<T> relativeTo ) : BlendTrackModification<T>, ISignalBlendModification
{
	public override IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, BlendOptions options )
	{
		var timeRange = selection.TotalTimeRange;

		// Fill in gaps between blocks in original track with AsSignal()

		if ( original.AsSignal() is not { } originalSignal )
		{
			yield return new PropertyBlock<T>( signal, timeRange );
			yield break;
		}

		yield return Blend( originalSignal, signal, relativeTo, timeRange, selection, options );
	}

	public ISignalBlendModification WithSignal( IPropertySignal newSignal ) =>
		new SignalBlendModification<T>( (PropertySignal<T>)newSignal, relativeTo );
}

public sealed class ClipboardBlendModification<T>( ImmutableArray<PropertyBlock<T>> sourceBlocks ) : BlendTrackModification<T>
{
	public ClipboardBlendModification( IEnumerable<IProjectPropertyBlock> blocks )
		: this( [..blocks.Cast<PropertyBlock<T>>()] )
	{

	}

	public override IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, BlendOptions options )
	{
		var blocks = sourceBlocks;

		var timeRanges = original.Select( x => x.TimeRange )
			.Union( blocks.Select( x => x.TimeRange + options.Offset ) );

		PropertySignal<T> relativeTo = blocks[0].GetValue( blocks[0].TimeRange.Start );

		foreach ( var timeRange in timeRanges )
		{
			var originalSignal = original
				.Where( x => timeRange.Contains( x.TimeRange ) )
				.AsSignal();

			var overlaySignal = blocks
				.Where( x => timeRange.Contains( x.TimeRange + options.Offset ) )
				.AsSignal();

			var clamped = timeRange.Clamp( selection.TotalTimeRange );

			yield return Blend( originalSignal, overlaySignal, relativeTo, clamped, selection, options );
		}
	}
}

public static class TrackModificationExtensions
{
	public static ITrackModification AsModification( this IPropertySignal signal )
	{
		var modificationType = typeof( SignalBlendModification<> ).MakeGenericType( signal.PropertyType );

		return (ITrackModification)Activator.CreateInstance( modificationType, signal, signal )!;
	}

	public static ITrackModification AsModification( this IEnumerable<IProjectPropertyBlock> sourceBlocks )
	{
		var untypedArray = sourceBlocks.ToArray();

		if ( untypedArray.Length == 0 ) throw new ArgumentException( "Expected at least one block.", nameof( sourceBlocks ) );

		var propertyType = untypedArray[0].PropertyType;
		var modificationType = typeof( ClipboardBlendModification<> ).MakeGenericType( propertyType );

		return (ITrackModification)Activator.CreateInstance( modificationType, [untypedArray] )!;
	}
}
