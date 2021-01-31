using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
	public const int EditableWidth = 22;
	public const int EditableHeight = 11;
	public const int TotalWidth = EditableWidth + 2;
	public const int TotalHeight = EditableHeight + 2;

	public const int NumColours = 4;
	public const int Colour_Empty = 0;
	public const int Colour_Bedrock = NumColours + 1;
	public const int Area_Invalid = -1;
}

public class MapLayout
{
	// 0: empty
	// 1-4: tile
	// 5: bedrock around map edges
	// editable map area is [1,Width] * [1,Height] (note - not 0-based)
	private int[] Colours;

	private int[] AreaIDs;
	private List<int> TilesForAreas;

	public MapLayout()
	{
		Colours = new int[ Constants.TotalWidth * Constants.TotalHeight ];
		for ( int x = -1; x < Constants.EditableWidth + 1; ++x )
		{
			Colours[IndexForXY(x,-1)] = Constants.Colour_Bedrock;
			Colours[IndexForXY(x,Constants.EditableHeight)] = Constants.Colour_Bedrock;
		}
		for ( int y = -1; y < Constants.EditableHeight + 1; ++y )
		{
			Colours[IndexForXY(-1,y)] = Constants.Colour_Bedrock;
			Colours[IndexForXY(Constants.EditableWidth,y)] = Constants.Colour_Bedrock;
		}

		AreaIDs = new int[ Colours.Length ];
		TilesForAreas = new List<int>();
		for ( int i = 0; i < AreaIDs.Length; ++i ) { AreaIDs[i] = Constants.Area_Invalid; }
	} // constructor

	public int GetColour( int _x, int _y ) { return Colours[ IndexForXY( _x, _y ) ]; }
	public int GetAreaID( int _x, int _y ) { return AreaIDs[ IndexForXY( _x, _y ) ]; }

	public void SetColour( int _x, int _y, int _colour ) { Colours[ IndexForXY(_x,_y) ] = _colour; }

	public int NumAreas { get { return TilesForAreas.Count; } }

	public int NumEmptySpaces { get {
		int count = 0;
		for ( int i = 0; i < Colours.Length; ++i )
		{
			if ( Colours[i] == Constants.Colour_Empty )
			{
				++count;
			}
		}
		return count;
	} }

	// Fills out AreaIDs and TilesForAreas. Contiguous (destroyable) groups get area IDs starting at 0; single tiles (and the bedrock border) get an ID of -1.
	public void FloodFillAreas()
	{
		const int Area_Unassigned = -2; // -2 here indicates 'unknown'

		int nextAreaID = 0;
		for ( int i = 0; i < AreaIDs.Length; ++i ) { AreaIDs[i] = Area_Unassigned; }
		TilesForAreas.Clear();

		for ( int y = -1; y < Constants.EditableHeight + 1; ++y )
		{
			for ( int x = -1; x < Constants.EditableWidth + 1; ++x )
			{
				int index = IndexForXY(x,y);
				int colour = Colours[index];
				int existingAreaID = AreaIDs[index];
				if ( existingAreaID != -2 ) { continue; } // this tile's area has already been filled in

				// Empty tiles/bedrock get filled in with an invalid area.
				if ( colour == Constants.Colour_Empty || colour == Constants.Colour_Bedrock )
				{
					AreaIDs[index] = Constants.Area_Invalid;
					continue;
				}

				// Check if any adjacent tiles have the same colour. If so, start a new area and flood fill it.
				int northIndex = IndexForXY(x,y-1); // guaranteed safe as the borders are all bedrock
				int southIndex = IndexForXY(x,y+1);
				int westIndex = IndexForXY(x-1,y);
				int eastIndex = IndexForXY(x+1,y);
				if ( Colours[northIndex] == colour || Colours[southIndex] == colour || Colours[westIndex] == colour || Colours[eastIndex] == colour )
				{
					FloodFillSingleArea_Recursive( x, y, colour, Area_Unassigned, colour, nextAreaID );
					TilesForAreas.Add( index );
					Debug.Assert( AreaIDs[index] == nextAreaID );
					Debug.Assert( AreaIDs[northIndex] == nextAreaID || AreaIDs[southIndex] == nextAreaID || AreaIDs[westIndex] == nextAreaID || AreaIDs[eastIndex] == nextAreaID );
					++nextAreaID;
				}
				else
				{
					// This is a single tile not adjacent to anything matching it.
					AreaIDs[index] = Constants.Area_Invalid;
				}
			} // For each column...
		} // For each row...
	} // FloodFillAreas

	private void FloodFillSingleArea_Recursive( int _x, int _y, int _requiredColour, int _requiredAreaID, int _newColour, int _newAreaID )
	{
		Debug.Assert( _requiredColour != _newColour || _requiredAreaID != _newAreaID ); // we must be making *some* change to the grid or this function will recurse indefinitely
		int index = IndexForXY(_x,_y);
		if ( Colours[index] == _requiredColour && AreaIDs[index] == _requiredAreaID )
		{
			Colours[index] = _newColour;
			AreaIDs[index] = _newAreaID;
			FloodFillSingleArea_Recursive( _x-1, _y, _requiredColour, _requiredAreaID, _newColour, _newAreaID );
			FloodFillSingleArea_Recursive( _x+1, _y, _requiredColour, _requiredAreaID, _newColour, _newAreaID );
			FloodFillSingleArea_Recursive( _x, _y-1, _requiredColour, _requiredAreaID, _newColour, _newAreaID );
			FloodFillSingleArea_Recursive( _x, _y+1, _requiredColour, _requiredAreaID, _newColour, _newAreaID );
		}
	} // FloodFillSingleArea_Recursive

	public MapLayout Duplicate()
	{
		MapLayout clone = new MapLayout();
		clone.Colours = (int[])Colours.Clone();
		clone.AreaIDs = (int[])AreaIDs.Clone();
		clone.TilesForAreas = new List<int>( TilesForAreas );
		return clone;
	} // Duplicate

	public void RemoveArea( int _areaID )
	{
		Debug.Assert( _areaID >= 0 && _areaID < TilesForAreas.Count );
		int index = TilesForAreas[_areaID];
		XYForIndex( index, out int x, out int y );
		Debug.Assert( AreaIDs[index] == _areaID );

		HashSet<int> neighbours = new HashSet<int>();
		RemoveArea_Recursive( x, y, _areaID, neighbours );
		Debug.Assert( AreaIDs[index] == Constants.Area_Invalid );

		foreach ( int neighbour in neighbours )
		{
			int colour = Colours[neighbour];
			Debug.Assert( colour != Constants.Colour_Empty && colour != Constants.Colour_Bedrock );
			Colours[neighbour] = ( colour % Constants.NumColours ) + 1;
		}

		FloodFillAreas();
	} // RemoveArea

	private void RemoveArea_Recursive( int _x, int _y, int _requiredAreaID, HashSet<int> o_neighbours )
	{
		int index = IndexForXY( _x, _y );
		int area = AreaIDs[index];
		if ( area == _requiredAreaID )
		{
			// Clear tile:
			Colours[index] = Constants.Colour_Empty;
			AreaIDs[index] = Constants.Area_Invalid;

			// Flood fill outwards:
			RemoveArea_Recursive( _x-1, _y, _requiredAreaID, o_neighbours );
			RemoveArea_Recursive( _x+1, _y, _requiredAreaID, o_neighbours );
			RemoveArea_Recursive( _x, _y-1, _requiredAreaID, o_neighbours );
			RemoveArea_Recursive( _x, _y+1, _requiredAreaID, o_neighbours );
		}
		else if ( Colours[index] != Constants.Colour_Empty && Colours[index] != Constants.Colour_Bedrock )
		{
			// Add the tile to the list of neighbours to be flipped later.
			o_neighbours.Add( index );
		}
	} // RemoveArea_Recursive

	private int IndexForXY( int _x, int _y )
	{
		Debug.Assert( _x >= -1 && _x < Constants.TotalWidth-1 );
		Debug.Assert( _y >= -1 && _y < Constants.TotalHeight-1 );
		return (_x+1) + ( (_y+1) * Constants.TotalWidth );
	} // IndexForXY

	private void XYForIndex( int _index, out int o_x, out int o_y )
	{
		Debug.Assert( _index >= 0 && _index < Colours.Length );
		o_y = _index / Constants.TotalWidth;
		o_x = _index - ( o_y * Constants.TotalWidth );
		--o_y;
		--o_x;
	} // XYForIndex
} // MapLayout
