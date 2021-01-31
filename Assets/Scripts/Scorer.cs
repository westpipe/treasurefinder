using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Scorer
{
	public class Settings
	{
		// Each empty cell is worth, at most, this many points (and at least 0).
		public float EmptyCellScore;
		// An empty cell with all its adjacent neighbours blocked is worth this many fewer points (and if it has e.g. 2 neighbours blocked then it takes 50% of this penalty).
		public float BlockedCardinalAdjacentMalus;
		// An empty cell with all its diagonal neighbours blocked is worth this many fewer points (and if it has e.g. 2 neighbours blocked then it takes 50% of this penalty).
		public float BlockedDiagonalAdjacentMalus;

		// Score bonus for Major/Minor regions.
		public float MajorRegionScore;
		public float MinorRegionScore;
	}

	public static readonly Settings DefaultSettings = new Settings(){
		EmptyCellScore = 5.0f,
		BlockedCardinalAdjacentMalus = 2.0f,
		BlockedDiagonalAdjacentMalus = 1.0f,

		MajorRegionScore = 1000.0f,
		MinorRegionScore = 500.0f,
	};

	// These are the settings that DRV3 actually uses (i.e. no artificial penalty on blocked adjacent cells).
	// We don't use this for comparing solutions but it's a more natural score to show the user as it should
	// be what they'll actually get in-game for executing a solution.
	public static readonly Settings ActualGameSettings = new Settings(){
		EmptyCellScore = 5.0f,
		BlockedCardinalAdjacentMalus = 0.0f,
		BlockedDiagonalAdjacentMalus = 0.0f,

		MajorRegionScore = 1000.0f,
		MinorRegionScore = 500.0f,
	};

	public class PriorityRegion
	{
		public int XMin; // inclusive
		public int YMin;
		public int XMax; // inclusive
		public int YMax;
		public bool IsMajor;
	}

	private readonly Settings m_settings;
	private float m_maxScore;
	private float m_maxGameScore;
	private readonly List<PriorityRegion> m_priorityRegions;

	public Scorer( Settings _settings )
	{
		m_settings = _settings;
		m_priorityRegions = new List<PriorityRegion>();
		ScoreLayout( new MapLayout(), out m_maxScore, out m_maxGameScore );
	}

	public PriorityRegion AddRegion( int _x0, int _y0, int _x1, int _y1, bool _isMajor )
	{
		PriorityRegion region = new PriorityRegion(){
			XMin = Mathf.Min( _x0, _x1 ),
			XMax = Mathf.Max( _x0, _x1 ),
			YMin = Mathf.Min( _y0, _y1 ),
			YMax = Mathf.Max( _y0, _y1 ),
			IsMajor = _isMajor,
		};
		m_priorityRegions.Add( region );
		ScoreLayout( new MapLayout(), out m_maxScore, out m_maxGameScore );
		return region;
	} // AddRegion

	public PriorityRegion GetPriorityRegionAt( int _x, int _y )
	{
		for ( int r = 0; r < m_priorityRegions.Count; ++r )
		{
			PriorityRegion region = m_priorityRegions[r];
			if ( region.XMin <= _x && _x <= region.XMax && region.YMin <= _y && _y <= region.YMax )
			{
				return region;
			}
		}
		return null;
	} // GetPriorityRegionAt

	public void RemoveRegion( PriorityRegion _region )
	{
		m_priorityRegions.Remove( _region );
		ScoreLayout( new MapLayout(), out m_maxScore, out m_maxGameScore );
	} // RemoveRegion

	public IReadOnlyCollection<PriorityRegion> PriorityRegions { get { return m_priorityRegions; } }

	public void ScoreLayout( MapLayout _layout, out float o_score, out float o_gameScore )
	{
		float totalScore = 0.0f;
		float totalGameScore = 0.0f;

		// Score individual cells:
		for ( int y = 0; y < Constants.EditableHeight; ++y )
		{
			for ( int x = 0; x < Constants.EditableWidth; ++x )
			{
				// We reduce the score of tiles adjacent to bedrock as well as non-empty tiles; empty spaces
				// around the edge of the board are considered less valuable as they're less likely to contain
				// buried items.
				if ( _layout.GetColour(x,y) == Constants.Colour_Empty )
				{
					int emptyCardinalNeighbours = ( _layout.GetColour(x+1,y) == Constants.Colour_Empty ? 1 : 0 )
												+ ( _layout.GetColour(x-1,y) == Constants.Colour_Empty ? 1 : 0 )
												+ ( _layout.GetColour(x,y+1) == Constants.Colour_Empty ? 1 : 0 )
												+ ( _layout.GetColour(x,y-1) == Constants.Colour_Empty ? 1 : 0 );
					int emptyDiagonalNeighbours = ( _layout.GetColour(x+1,y+1) == Constants.Colour_Empty ? 1 : 0 )
												+ ( _layout.GetColour(x-1,y+1) == Constants.Colour_Empty ? 1 : 0 )
												+ ( _layout.GetColour(x+1,y-1) == Constants.Colour_Empty ? 1 : 0 )
												+ ( _layout.GetColour(x-1,y-1) == Constants.Colour_Empty ? 1 : 0 );
					float cellScore = m_settings.EmptyCellScore;
					float malus = m_settings.BlockedCardinalAdjacentMalus * 0.25f * ( 4 - emptyCardinalNeighbours )
								+ m_settings.BlockedDiagonalAdjacentMalus * 0.25f * ( 4 - emptyDiagonalNeighbours );
					cellScore = Mathf.Max( 0.0f, cellScore - malus );

					float cellGameScore = ActualGameSettings.EmptyCellScore;
					float gameMalus = ActualGameSettings.BlockedCardinalAdjacentMalus * 0.25f * ( 4 - emptyCardinalNeighbours )
									+ ActualGameSettings.BlockedDiagonalAdjacentMalus * 0.25f * ( 4 - emptyDiagonalNeighbours );
					cellGameScore = Mathf.Max( 0.0f, cellGameScore - gameMalus );

					totalScore += cellScore;
					totalGameScore += cellGameScore;
				}
			}
		}

		// Score regions:
		for ( int r = 0; r < m_priorityRegions.Count; ++r )
		{
			PriorityRegion region = m_priorityRegions[r];
			bool obstructed = false;
			for ( int y = region.YMin; ( y <= region.YMax ) && !obstructed; ++y )
			{
				for ( int x = region.XMin; x <= region.XMax; ++x )
				{
					if ( _layout.GetColour(x,y) != Constants.Colour_Empty )
					{
						obstructed = true;
						break;
					}
				} // for every x
			} // for every y
			if ( !obstructed )
			{
				totalScore += region.IsMajor ? m_settings.MajorRegionScore : m_settings.MinorRegionScore;
				totalGameScore += region.IsMajor ? ActualGameSettings.MajorRegionScore : ActualGameSettings.MinorRegionScore;
			}
		} // for every region

		o_score = totalScore;
		o_gameScore = totalGameScore;
	} // ScoreLayout

	public float MaxScore { get { return m_maxScore; } }
	public float MaxGameScore { get { return m_maxGameScore; } }

} // Scorer
