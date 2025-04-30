﻿using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Linq;
using Sandbox.UI;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// A split view, with a list of tracks on the left and the dopesheet/curve view on the right
/// </summary>
public partial class TrackListWidget : Widget
{
	public Session Session { get; }

	private SceneEditorSession SceneEditorSession { get; }

	public IEnumerable<TrackWidget> RootTracks => _rootTracks;
	public IEnumerable<TrackWidget> Tracks => RootTracks.SelectMany( EnumerateDescendants );

	private static IEnumerable<TrackWidget> EnumerateDescendants( TrackWidget track ) =>
		[track, ..track.Children.SelectMany( EnumerateDescendants )];

	private ITrackListView? _trackList;
	private readonly SynchronizedList<ITrackView, TrackWidget> _rootTracks;

	private readonly Widget _trackContainer;
	private Widget? _placeholder;

	public TrackListWidget( ListPanel parent, Session session )
		: base( parent )
	{
		Session = session;

		SceneEditorSession = SceneEditorSession.Resolve( Session.Player.Scene );
		SceneEditorSession.Selection.OnItemAdded += OnSelectionAdded;

		AcceptDrops = true;

		_trackContainer = new Widget( this )
		{
			Layout = Layout.Column(),
			FixedWidth = Width
		};

		_trackContainer.Layout.Margin = new Margin( 4f, 0f );

		_rootTracks = new SynchronizedList<ITrackView, TrackWidget>(
			AddRootTrack, RemoveRootTrack, UpdateChildTrack );

		Session.ViewChanged += Session_ViewChanged;

		Load( Session.TrackList );
	}

	private TrackWidget AddRootTrack( ITrackView source ) => _trackContainer.Layout.Add( new TrackWidget( this, null, source ) );
	private void RemoveRootTrack( ITrackView source, TrackWidget item ) => item.Destroy();
	private bool UpdateChildTrack( ITrackView source, TrackWidget item ) => item.UpdateLayout();

	public bool IsPreview( TrackWidget widget ) => _previewTracks?.Contains( widget.View.Track ) ?? false;

	private void OnSelectionAdded( object item )
	{
		if ( Tracks.Any( x => x.IsFocused ) || Session.Editor.DopeSheetPanel?.DopeSheet.IsFocused is not true ) return;
		if ( item is not GameObject go ) return;
		if ( Tracks.FirstOrDefault( x => x.View.Target is ITrackReference<GameObject> { IsBound: true } target && target.Value == go ) is not { } track ) return;
		
		track.Focus( false );

		if ( Parent is ScrollArea scrollArea )
		{
			scrollArea.MakeVisible( track );
		}
	}

	public override void OnDestroyed()
	{
		if ( _trackList is not null )
		{
			_trackList.Changed -= TrackList_Changed;
		}

		Session.ViewChanged -= Session_ViewChanged;
		SceneEditorSession.Selection.OnItemAdded -= OnSelectionAdded;
	}

	protected override void OnWheel( WheelEvent e )
	{
		Session.TrackListScrollPosition -= e.Delta / 5f;
		e.Accept();
	}

	private Vector2 _lastMouseScreenPos;

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		_lastMouseScreenPos = e.ScreenPosition;
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		var delta = e.ScreenPosition - _lastMouseScreenPos;

		if ( e.ButtonState == MouseButtons.Middle )
		{
			Session.TrackListScrollPosition -= delta.y;
			e.Accepted = true;
		}

		_lastMouseScreenPos = e.ScreenPosition;
	}

	private void Load( ITrackListView trackList )
	{
		if ( _trackList == trackList ) return;

		if ( _trackList is not null )
		{
			_trackList.Changed -= TrackList_Changed;
		}

		_trackList = trackList;
		_trackList.Changed += TrackList_Changed;

		TrackList_Changed( trackList );
	}

	private void Session_ViewChanged()
	{
		if ( _rootTracks.Count == 0 )
		{
			_trackContainer.Position = 0f;
			_trackContainer.FixedSize = Size;
			return;
		}

		_trackContainer.Position = new Vector2( 0f, Session.TrackListScrollOffset - Session.TrackListScrollPosition );
		_trackContainer.FixedWidth = Width;
		_trackContainer.FixedHeight = _rootTracks
			.Select( x => x.View.Position + x.View.Height + DopeSheet.RootTrackSpacing )
			.DefaultIfEmpty( 64f )
			.Max();
	}

	private void TrackList_Changed( ITrackListView trackList )
	{
		_placeholder?.Destroy();
		_rootTracks.Update( trackList.RootTracks );

		_trackContainer.Layout.Clear( false );

		foreach ( var track in _rootTracks )
		{
			_trackContainer.Layout.Add( track );
			_trackContainer.Layout.AddSpacingCell( DopeSheet.RootTrackSpacing );
		}

		if ( _rootTracks.Count == 0 )
		{
			CreatePlaceholder();
		}

		Session_ViewChanged();
	}

	private void CreatePlaceholder()
	{
		var row = _trackContainer.Layout.AddRow();

		row.Margin = 32f;

		_placeholder = new Label( "Drag a <b>GameObject</b>, <b>Component</b>, <b>MovieResource</b> or <b>inspector property</b> here to create a track." )
		{
			Alignment = TextFlag.Center | TextFlag.WordWrap,
			WordWrap = true
		};

		row.Add( _placeholder );
	}

	private IReadOnlyList<IProjectTrack>? _previewTracks;

	private IEnumerable<IProjectTrack> GetDraggedTracks( DragEvent ev )
	{
		if ( ev.Data.OfType<GameObject>().FirstOrDefault() is { } go )
		{
			yield return Session.GetOrCreateTrack( go );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.Enabled) );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.LocalPosition) );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.LocalRotation) );

			if ( go.GetComponent<PlayerController>() is { } controller )
			{
				yield return Session.GetOrCreateTrack( controller );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.EyeAngles) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.WishVelocity) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.IsSwimming) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.IsClimbing) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.IsDucking) );
			}


			if ( go.GetComponent<Rigidbody>() is { } rigidBody )
			{
				yield return Session.GetOrCreateTrack( rigidBody );
				yield return Session.GetOrCreateTrack( rigidBody, nameof(Rigidbody.Velocity) );
			}

			yield break;
		}

		if ( ev.Data.OfType<Component>().FirstOrDefault() is { } component )
		{
			yield return Session.GetOrCreateTrack( component );

			if ( component is SkinnedModelRenderer skinnedRenderer )
			{
				if ( skinnedRenderer.Parameters.Graph is { } graph )
				{
					for ( var i = 0; i < graph.ParamCount; ++i )
					{
						var paramName = graph.GetParameterName( i );

						yield return Session.GetOrCreateTrack( component, $"{nameof( SkinnedModelRenderer.Parameters )}.{paramName}" );
					}
				}

				foreach ( var morphName in skinnedRenderer.Morphs.Names )
				{
					yield return Session.GetOrCreateTrack( component, $"{nameof(SkinnedModelRenderer.Morphs)}.{morphName}" );
				}
			}

			yield break;
		}

		if ( ev.Data.OfType<SerializedProperty>().FirstOrDefault() is { } property )
		{
			if ( property.Parent.Targets?.FirstOrDefault() is Component parentComponent )
			{
				yield return Session.GetOrCreateTrack( parentComponent, property.Name );
			}

			yield break;
		}

		if ( ev.Data.Assets.FirstOrDefault( x => x.AssetPath?.EndsWith( ".movie" ) ?? false ) is { } assetData )
		{
			var assetTask = assetData.GetAssetAsync();

			if ( !assetTask.IsCompleted ) yield break;

			if ( assetTask.Result?.LoadResource<MovieResource>() is not { } resource ) yield break;

			yield return Session.GetOrCreateTrack( resource );
		}
	}

	public override void OnDragHover( DragEvent ev )
	{
		if ( _previewTracks is null )
		{
			var knownTracks = Session.Project.Tracks.ToImmutableHashSet();
			var dragged = GetDraggedTracks( ev ).ToImmutableHashSet();

			_previewTracks = dragged.Except( knownTracks ).ToArray();

			Session.TrackList.Update();
		}

		ev.Action = _previewTracks.Count > 0
			? DropAction.Link
			: DropAction.Ignore;
	}

	public override void OnDragLeave()
	{
		if ( _previewTracks is { Count: > 0 } )
		{
			foreach ( var track in _previewTracks )
			{
				track.Remove();
			}

			Session.TrackList.Update();
		}

		_previewTracks = null;
	}

	public override void OnDragDrop( DragEvent ev )
	{
		if ( _previewTracks is null ) return;

		Session.ClipModified();

		_previewTracks = null;
	}
}
