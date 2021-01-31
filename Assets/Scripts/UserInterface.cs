using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UserInterface : MonoBehaviour
{
	private enum EditorMode { Edit, Solve, Playback };

	[Header("Prefabs")]
	[SerializeField] private GameObject m_groundPrefab;
	[SerializeField] private GameObject[] m_tilePrefabs;
	[SerializeField] private GameObject m_cursorPrefab;
	[SerializeField] private GameObject m_majorRegionPrefab;
	[SerializeField] private GameObject m_minorRegionPrefab;

	[Header("World Root")]
	[SerializeField] private Transform m_root;

	[Header("UI")]
	[SerializeField] private TMPro.TextMeshProUGUI m_progressText;
	[SerializeField] private Button m_editButton;
	[SerializeField] private Button m_solveButton;
	[SerializeField] private Button m_playbackButton;
	[SerializeField] private Button m_editCurrentButton;

	private Solver m_solver;
	private Scorer m_scorer;
	private Transform[] m_groundTiles;
	private Transform[] m_tiles;
	private Transform m_cursor;

	private EditorMode m_editorMode;
	private int m_cursorX;
	private int m_cursorY;
	private int m_regionStartX = -1; // start corner of the current priority region we're adding; -1 when not drawing
	private int m_regionStartY = -1;
	private bool m_regionIsMajor;
	private Transform m_placingRegionVisuals;
	private Dictionary<Scorer.PriorityRegion,Transform> m_placedRegionVisuals;
	private bool m_layoutChanged;

	private MapLayout m_editLayout; // In edit mode we modify this.
	private MapLayout m_visibleLayout; // A layout representing what we're currently displaying on screen. Identical to m_editLayout in edit mode.

	private List<MapLayout> m_playbackLayouts; // A list of all layouts in playback mode. After solving, this is initialised using the solution we've generated, but the player can assume control and just play a game by hand if they want.
	private int m_playbackIndex = 0;

	private void Start()
	{
		m_solver = GetComponent<Solver>();
		m_scorer = new Scorer( Scorer.DefaultSettings );

		// Create ground tiles:
		m_groundTiles = new Transform[ Constants.EditableWidth * Constants.EditableHeight ];
		m_tiles = new Transform[ m_groundTiles.Length ];
		for ( int y = 0; y < Constants.EditableHeight; ++y )
		{
			for ( int x = 0; x < Constants.EditableWidth; ++x )
			{
				Transform ground = GameObject.Instantiate( m_groundPrefab ).transform;
				ground.parent = m_root;
				ground.localRotation = Quaternion.identity;
				ground.localPosition = new Vector3( x, 0, -y );
				m_groundTiles[x + y*Constants.EditableWidth] = ground;
			}
		}

		// Init cursor:
		m_cursor = GameObject.Instantiate( m_cursorPrefab ).transform;
		m_cursor.position = m_groundTiles[0].position;

		// UI:
		m_editButton.interactable = false;
		m_editCurrentButton.interactable = false;
		UpdateProgressText();

		// Init empty map layout:
		m_editorMode = EditorMode.Edit;
		m_editLayout = new MapLayout();
		m_visibleLayout = new MapLayout();
		m_playbackLayouts = new List<MapLayout>();
		m_placedRegionVisuals = new Dictionary<Scorer.PriorityRegion,Transform>();
		m_layoutChanged = true;

		// TODO - randomise for quick testing
#if UNITY_EDITOR
		for ( int i = 0; i < m_groundTiles.Length; ++i ) { Edit_SetNextTile( Random.Range(1,5) ); }
#endif
	} // Start

	private void Update()
	{
		// Move the cursor:
		if ( m_editorMode == EditorMode.Edit || m_editorMode == EditorMode.Playback )
		{
			// Arrow keys move the cursor:
			if ( Input.GetKeyDown( KeyCode.LeftArrow ) ) { m_cursorX = ( m_cursorX > 0 ? m_cursorX : Constants.EditableWidth ) - 1; UpdateCursorVisuals(); }
			if ( Input.GetKeyDown( KeyCode.RightArrow ) ) { m_cursorX = ( m_cursorX + 1 ) % Constants.EditableWidth; UpdateCursorVisuals(); }
			if ( Input.GetKeyDown( KeyCode.UpArrow ) ) { m_cursorY = ( m_cursorY > 0 ? m_cursorY : Constants.EditableHeight ) - 1; UpdateCursorVisuals(); }
			if ( Input.GetKeyDown( KeyCode.DownArrow ) ) { m_cursorY = ( m_cursorY + 1 ) % Constants.EditableHeight; UpdateCursorVisuals(); }
		}

		if ( m_editorMode == EditorMode.Edit )
		{
			// Pressing 0-4 adds that tile at the current position.
			if ( Input.GetKeyDown( KeyCode.Alpha0 ) || Input.GetKeyDown( KeyCode.Keypad0 ) ) { Edit_SetNextTile(0); }
			if ( Input.GetKeyDown( KeyCode.Alpha1 ) || Input.GetKeyDown( KeyCode.Keypad1 ) ) { Edit_SetNextTile(1); }
			if ( Input.GetKeyDown( KeyCode.Alpha2 ) || Input.GetKeyDown( KeyCode.Keypad2 ) ) { Edit_SetNextTile(2); }
			if ( Input.GetKeyDown( KeyCode.Alpha3 ) || Input.GetKeyDown( KeyCode.Keypad3 ) ) { Edit_SetNextTile(3); }
			if ( Input.GetKeyDown( KeyCode.Alpha4 ) || Input.GetKeyDown( KeyCode.Keypad4 ) ) { Edit_SetNextTile(4); }

			// Presing and holding Shift/Ctrl draws regions (Shift for major, Ctrl for minor).
			if ( Input.GetKeyDown( KeyCode.LeftShift ) || Input.GetKeyDown( KeyCode.RightShift ) ) { Edit_StartDrawingRegion(true); }
			if ( Input.GetKeyUp( KeyCode.LeftShift ) || Input.GetKeyUp( KeyCode.RightShift ) ) { Edit_EndDrawingRegion(); }
			if ( Input.GetKeyDown( KeyCode.LeftControl ) || Input.GetKeyDown( KeyCode.RightControl ) ) { Edit_StartDrawingRegion(false); }
			if ( Input.GetKeyUp( KeyCode.LeftControl ) || Input.GetKeyUp( KeyCode.RightControl ) ) { Edit_EndDrawingRegion(); }

			// Update any region we're placing.
			Edit_UpdatePlacingRegionVisuals();
		}

		if ( m_editorMode == EditorMode.Playback )
		{
			// Pressing Delete on a tile will try to clear it in the same manner as the actual game.
			if ( Input.GetKeyDown( KeyCode.Delete ) ) { Playback_ClearTileUnderCursor(); }

			// PageUp/PageDown/Home/End work through the game history.
			if ( Input.GetKeyDown( KeyCode.PageUp ) ) { Playback_Prev(); }
			if ( Input.GetKeyDown( KeyCode.PageDown ) ) { Playback_Next(); }
			if ( Input.GetKeyDown( KeyCode.Home ) ) { Playback_First(); }
			if ( Input.GetKeyDown( KeyCode.End ) ) { Playback_Last(); }
		}

		// In Solve mode, update the UI as we work.
		else if ( m_editorMode == EditorMode.Solve )
		{
			UpdateProgressText();
			UpdateMapVisuals( m_solver.FinalLayout );
		}
	} // Update

	private void UpdateTileVisuals( int _x, int _y, int _tileColour )
	{
		if ( m_visibleLayout.GetColour(_x,_y) != _tileColour )
		{
			m_visibleLayout.SetColour(_x,_y, _tileColour);

			int index = _x + _y*Constants.EditableWidth;
			if ( m_tiles[index] != null )
			{
				GameObject.Destroy( m_tiles[index].gameObject );
			}
			if ( _tileColour > 0 )
			{
				m_tiles[index] = GameObject.Instantiate( m_tilePrefabs[_tileColour-1] ).transform;
				m_tiles[index].parent = m_groundTiles[index];
				m_tiles[index].localPosition = Vector3.zero;
				m_tiles[index].localRotation = Quaternion.identity;
			}
			else
			{
				m_tiles[index] = null;
			}
		}
	}

	private void Edit_SetNextTile( int _colour )
	{
		m_editLayout.SetColour( m_cursorX, m_cursorY, _colour );
		m_layoutChanged = true; // forces the solver to restart when entering solve mode
		UpdateTileVisuals( m_cursorX, m_cursorY, _colour );
		Edit_MoveCursorToNextTile();
	}

	private void Edit_MoveCursorToNextTile()
	{
		if ( ++m_cursorX == Constants.EditableWidth )
		{
			m_cursorX = 0;
			if ( ++m_cursorY == Constants.EditableHeight )
			{
				m_cursorY = 0;
			}
		}
		UpdateCursorVisuals();
	}

	private void Edit_StartDrawingRegion( bool _isMajor )
	{
		Edit_EndDrawingRegion();
		m_regionIsMajor = _isMajor;
		m_regionStartX = m_cursorX;
		m_regionStartY = m_cursorY;

		m_placingRegionVisuals = GameObject.Instantiate( _isMajor ? m_majorRegionPrefab : m_minorRegionPrefab ).transform;
		m_placingRegionVisuals.position = m_cursor.position;
	}

	private void Edit_EndDrawingRegion()
	{
		if ( m_regionStartX == -1 )
		{
			// Not currently drawing a region.
			return;
		}

		// Tapping shift/ctrl without moving the cursor deletes any region under the cursor.
		if ( m_regionStartX == m_cursorX && m_regionStartY == m_cursorY )
		{
			Scorer.PriorityRegion region = m_scorer.GetPriorityRegionAt( m_cursorX, m_cursorY );
			if ( region != null )
			{
				m_scorer.RemoveRegion( region );
				GameObject.Destroy( m_placedRegionVisuals[region].gameObject );
				m_placedRegionVisuals.Remove( region );
			}
		}
		else
		{
			Scorer.PriorityRegion region = m_scorer.AddRegion( m_cursorX, m_cursorY, m_regionStartX, m_regionStartY, m_regionIsMajor );
			m_placedRegionVisuals.Add( region, m_placingRegionVisuals );
			m_placingRegionVisuals = null;
		}

		if ( m_placingRegionVisuals != null )
		{
			GameObject.Destroy( m_placingRegionVisuals.gameObject );
			m_placingRegionVisuals = null;
		}

		m_regionStartX = -1;
		m_regionStartY = -1;
		m_regionIsMajor = false;
		m_layoutChanged = true;
	}

	private void Edit_UpdatePlacingRegionVisuals()
	{
		if ( m_placingRegionVisuals == null )
		{
			return;
		}

		Vector3 pos0 = m_groundTiles[m_cursorX + m_cursorY*Constants.EditableWidth].position;
		Vector3 pos1 = m_groundTiles[m_regionStartX + m_regionStartY*Constants.EditableWidth].position;
		m_placingRegionVisuals.position = ( pos0 + pos1 ) * 0.5f;
		m_placingRegionVisuals.localScale = new Vector3( Mathf.Abs( pos1.x-pos0.x ), 0.0f, Mathf.Abs( pos1.z-pos0.z ) ) + Vector3.one;
	} // Edit_UpdatePlacingRegionVisuals

	private void UpdateCursorVisuals()
	{
		m_cursor.position = m_groundTiles[m_cursorX + m_cursorY*Constants.EditableWidth].position;
	}

	private void Playback_ClearTileUnderCursor()
	{
		// Clear the history from this point onwards:
		if ( m_playbackIndex < m_playbackLayouts.Count - 1 )
		{
			m_playbackLayouts.RemoveRange( m_playbackIndex + 1, m_playbackLayouts.Count - m_playbackIndex - 1 );
		}

		// Apply the change and story it in the play history:
		MapLayout nextLayout = m_playbackLayouts[ m_playbackIndex ].Duplicate();
		nextLayout.FloodFillAreas();
		int areaID = nextLayout.GetAreaID( m_cursorX, m_cursorY);
		if ( areaID != Constants.Area_Invalid )
		{
			nextLayout.RemoveArea( areaID );
			m_playbackLayouts.Add( nextLayout );
			++m_playbackIndex;
			UpdateMapVisuals( nextLayout );
			UpdatePlaybackText();
		}
	} // Playback_ClearTileUnderCursor

	private void SwitchMode( EditorMode _newMode )
	{
		if ( _newMode == m_editorMode ) { return; }

		switch ( m_editorMode )
		{
			case EditorMode.Edit:
			{
				// If any edits were made to the map then this invalidates our play history.
				if ( m_layoutChanged )
				{
					m_playbackLayouts.Clear();
					m_playbackLayouts.Add( m_editLayout.Duplicate() );
				}

				// Enable UI:
				m_editButton.interactable = true;
				m_editCurrentButton.interactable = true;
				break;
			}
			case EditorMode.Solve:
			{
				m_cursor.gameObject.SetActive( true );
				m_solver.StopSolving();
				UpdateProgressText();

				// Update the playback mode history with what we've generated so far.
				m_playbackLayouts = m_solver.SolutionLayouts;

				// Enable UI:
				m_editCurrentButton.interactable = true;
				m_solveButton.interactable = true;

				break;
			}
			case EditorMode.Playback:
			{
				m_playbackButton.interactable = true;
				break;
			}
		}

		m_editorMode = _newMode;

		switch ( m_editorMode )
		{
			case EditorMode.Edit:
			{
				UpdateMapVisuals( m_editLayout );
				UpdateProgressText();
				m_editButton.interactable = false;
				m_editCurrentButton.interactable = false;
				break;
			}
			case EditorMode.Solve:
			{
				m_cursor.gameObject.SetActive( false );
				if ( m_layoutChanged )
				{
					m_layoutChanged = false;
					m_solver.StartSolving( m_editLayout, m_scorer );
				}
				else
				{
					m_solver.ResumeSolving();
				}
				m_editCurrentButton.interactable = false;
				m_solveButton.interactable = false;
				break;
			}
			case EditorMode.Playback:
			{
				m_playbackIndex = 0;
				UpdatePlaybackText();
				UpdateMapVisuals( m_playbackLayouts[ m_playbackIndex ] );
				m_playbackButton.interactable = false;
				break;
			}
		}
	} // SwitchMode

	private void UpdateMapVisuals( MapLayout _layout )
	{
		for ( int y = 0; y < Constants.EditableHeight; ++y )
		{
			for ( int x = 0; x < Constants.EditableWidth; ++x )
			{
				UpdateTileVisuals( x, y, _layout.GetColour(x,y) );
			}
		}
	} // UpdateMapVisuals

	public void Playback_Prev()
	{
		if ( m_editorMode != EditorMode.Playback ) { return; }
		if ( m_playbackIndex > 0 )
		{
			SetPlaybackIndex( m_playbackIndex - 1 );
		}
	}

	public void Playback_Next()
	{
		if ( m_editorMode != EditorMode.Playback ) { return; }
		if ( m_playbackIndex < m_playbackLayouts.Count - 1 )
		{
			SetPlaybackIndex( m_playbackIndex + 1 );
		}
	}

	public void Playback_First()
	{
		if ( m_editorMode != EditorMode.Playback ) { return; }
		SetPlaybackIndex( 0 );
	}

	public void Playback_Last()
	{
		if ( m_editorMode != EditorMode.Playback ) { return; }
		SetPlaybackIndex( m_playbackLayouts.Count - 1 );
	}

	private void SetPlaybackIndex( int _index )
	{
		Debug.Assert( m_editorMode == EditorMode.Playback );
		m_playbackIndex = _index;
		UpdatePlaybackText();
		UpdateMapVisuals( m_playbackLayouts[m_playbackIndex] );
	}

	// UI:
	public void OnSolveButtonPressed()
	{
		SwitchMode( EditorMode.Solve );
	}

	public void OnEditButtonPressed()
	{
		SwitchMode( EditorMode.Edit );
	}

	public void OnPlaybackButtonPressed()
	{
		SwitchMode( EditorMode.Playback );
	}

	public void OnEditCurrentButtonPressed()
	{
		// Commit what's currently on screen to the editor layout, and then edit that.
		m_editLayout = m_visibleLayout.Duplicate();
		m_layoutChanged = true;
		SwitchMode( EditorMode.Edit );
	} // OnEditCurrentButtonPressed

	private void UpdateProgressText()
	{
		m_progressText.text = $"Attempts: {m_solver.NumIterations}\nEmpty spaces: {m_solver.BestEmptySpaces}/{Constants.EditableWidth*Constants.EditableHeight}\nScore: {Mathf.FloorToInt(m_solver.BestScore)}/{Mathf.FloorToInt(m_scorer.MaxGameScore)}";
	} // UpdateProgressText

	private void UpdatePlaybackText()
	{
		m_progressText.text = $"Turn {m_playbackIndex}/{m_playbackLayouts.Count-1}";
	}

} // UserInterface
