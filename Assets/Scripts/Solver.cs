using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Solver : MonoBehaviour
{
	// Types:
	private class Solution
	{
		public List<int> AreaIDs = new List<int>();
		public MapLayout FinalLayout = new MapLayout();
		public int EmptySpaces = 0;
		public float Score = 0; // The score the solver has given to this solution, which may not actually correspond to the score in DRV3 (e.g. we may apply artificial bonuses/penalties to help bias towards what we consider helpful configurations, such as contiguous empty spaces).
		public float GameScore = 0; // The score this solution should actually earn in-game.
	} // Solution

	// Most recent request:
	private MapLayout m_initialLayout = null;
	private Scorer m_scorer = null;

	// Progress (for UI feedback):
	private Coroutine m_solveCo = null;
	private int m_numIterations = 0;
	private Solution m_bestSolution = new Solution();

	public void StartSolving( MapLayout _initialLayout, Scorer _scorer )
	{
		StopSolving();

		m_initialLayout = _initialLayout.Duplicate();
		m_initialLayout.FloodFillAreas();
		m_scorer = _scorer;
		m_numIterations = 0;
		m_bestSolution = new Solution(){
			EmptySpaces = m_initialLayout.NumEmptySpaces,
			Score = 0,
			GameScore = 0,
		};
		m_scorer.ScoreLayout( m_initialLayout, out m_bestSolution.Score, out m_bestSolution.GameScore );

		m_solveCo = StartCoroutine( RunSolve_MonteCarlo() );
	} // Start

	public void ResumeSolving()
	{
		if ( m_solveCo == null )
		{
			Debug.Assert( m_initialLayout != null );
			m_solveCo = StartCoroutine( RunSolve_MonteCarlo() );
		}
	} // ResumeSolving

	public void StopSolving()
	{
		if ( m_solveCo != null )
		{
			StopCoroutine( m_solveCo );
			m_solveCo = null;
		}
	} // StopSolving

	public bool IsSolving { get { return m_solveCo != null; } }
	public int NumIterations { get { return m_numIterations; } }
	public float BestScore { get { return m_bestSolution.GameScore; } }
	public int BestEmptySpaces { get { return m_bestSolution.EmptySpaces; } }

	public MapLayout InitialLayout { get { return m_initialLayout.Duplicate(); } }
	public MapLayout FinalLayout { get { return m_bestSolution.FinalLayout.Duplicate(); } }
	public List<MapLayout> SolutionLayouts { get {
		List<MapLayout> results = new List<MapLayout>( m_bestSolution.AreaIDs.Count + 1 );
		MapLayout solutionLayout = InitialLayout;
		results.Add( solutionLayout );
		foreach ( int areaID in m_bestSolution.AreaIDs )
		{
			solutionLayout = solutionLayout.Duplicate();
			solutionLayout.RemoveArea( areaID );
			results.Add( solutionLayout );
		}
		return results;
	} } // SolutionLayouts

	////////////////////////////////////////////////////////////////////////////////
	// Most straightforward algorithm - just guess random solutions and keep the best one.
	private IEnumerator RunSolve_MonteCarlo()
	{
		// const int c_solutionsPerFrame = 100;
		const int c_microsecondsPerFrame = 30 * 1000;
		long targetTicks = ( System.Diagnostics.Stopwatch.Frequency * c_microsecondsPerFrame ) / 1000000L;

		while ( true )
		{
			System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			do
			{
				Solution newSolution = TryRandomSolution();
				if ( newSolution.Score > m_bestSolution.Score )
				{
					m_bestSolution = newSolution;
				}
				++m_numIterations;
			}
			while ( stopwatch.ElapsedTicks < targetTicks );
			stopwatch.Stop();

			yield return null;
		}
	} // RunSolve_MonteCarlo

	private Solution TryRandomSolution()
	{
		Solution solution = new Solution();
		MapLayout layout = m_initialLayout.Duplicate();
		while ( layout.NumAreas > 0 )
		{
			int areaToRemove = Random.Range( 0, layout.NumAreas );
			layout.RemoveArea( areaToRemove );
			solution.AreaIDs.Add( areaToRemove );
		}

		// Score the solution somehow:
		solution.FinalLayout = layout;
		solution.EmptySpaces = layout.NumEmptySpaces;
		m_scorer.ScoreLayout( layout, out solution.Score, out solution.GameScore );

		return solution;
	} // TryRandomSolution
} // Solver