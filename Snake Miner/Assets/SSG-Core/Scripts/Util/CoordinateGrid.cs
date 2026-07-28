using System;
using System.Collections.Generic;
using System.Linq;
using Islands;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SSG_Core.Scripts.Util
{
	public class CoordinateGrid
	{
		private readonly List<List<string>> _grid;

		public CoordinateGrid(int2 gridSize)
		{
			_grid = new List<List<string>>();
			for (var i = 0; i < gridSize.x; i++)
			{
				var list = new List<string>();
				for (var j = 0; j < gridSize.y; j++)
				{
					list.Add(string.Empty);					
				}
				_grid.Add(list);
			}
		}
		
		public void PrintGrid()
		{
			var s = "grid print: \n";
			for (int row = 0; row < _grid.Count; row++)
			{
				for (int col = 0; col < _grid[row].Count; col++)
				{
					// Print 'x' for occupied cells, otherwise print a space
					s += string.IsNullOrEmpty(_grid[row][col]) ? " o " : " x ";
				}
				s += "\n";
			}

			Debug.Log(s);
		}

		public string GetIslandIdAtPosition(int2 coord)
		{
			return _grid[coord.x][coord.y];
		}

		public bool TryGetRandomOpenPosition(string islandId, int2 requiredCellOccupationLW, out int2 gridPos)
		{
			var safety = 0;
			var openCoord = int2.zero;
			var hasFoundOpenCoord = false;

			while (!hasFoundOpenCoord && safety < 1000)
			{
				safety++;
				var x = Random.Range(0, _grid.Count);
				var y = Random.Range(0, _grid[x].Count);

				// this only checks blocks
				// this assumes initial position is top-left aligned
				var isOccupationOpen = true;
				if (_grid[x][y] == string.Empty)
				{
					for (var xCell = 0; xCell < requiredCellOccupationLW.x; xCell++)
					{
						if (!isOccupationOpen) continue;
						for (var yCell = 0; yCell < requiredCellOccupationLW.y; yCell++)
						{
							if (!isOccupationOpen) continue;
							if (x + xCell >= _grid.Count)
							{
								isOccupationOpen = false;
								continue;
							}
							if (y + yCell >= _grid[x + xCell].Count)
							{
								isOccupationOpen = false;
								continue;
							}

							if (_grid[x + xCell][y + yCell] != string.Empty)
								isOccupationOpen = false;
						}
					}
					if (!isOccupationOpen) continue;

					// normalize coords with negatives so 0,0 is center
					openCoord = new int2(x - _grid.Count / 2, y - _grid[x].Count / 2);
					hasFoundOpenCoord = true;
					for (var xCell = 0; xCell < requiredCellOccupationLW.x; xCell++)
					{
						for (var yCell = 0; yCell < requiredCellOccupationLW.y; yCell++)
						{
							Debug.Log($"setting grid at ({x + xCell},{y + yCell})");
							PrintGrid();
							_grid[x + xCell][y + yCell] = islandId;
						}
					}
				}
			}

			if (!hasFoundOpenCoord)
			{
				// Debug.LogError("Unable to find open grid position!");
				gridPos = default;
				return false;
			}

			gridPos = openCoord;
			return true;
		}
		
		public List<IslandConnection> GetMissingConnections(int minConnectionLength, int maxConnectionLength, bool shouldFallback, int limitQuantity)
		{
		    var populatedCells = new List<int2>();
		    var connections = new List<IslandConnection>();

		    // Collect all populated cells
		    for (var x = 0; x < _grid.Count; x++)
		    {
		        for (var y = 0; y < _grid[x].Count; y++)
		        {
		            if (_grid[x][y] != string.Empty)
		            {
			            // ensure no duplicate island Ids
			            if (populatedCells.All(p => _grid[p.x][p.y] != _grid[x][y]))
							populatedCells.Add(new int2(x,y));
		            }
		        }
		    }

		    // Identify disconnected groups
		    var groups = FindDisconnectedGroups(populatedCells, connections);

		    var safety = 0;
		    var unchangedGuess = 0; // only fallback if we've tried to fit constraints a few times first
		    var fallbackConnectionLength = 1;
		    // Connect groups until all are merged
		    while (groups.Count > 1 && connections.Count < limitQuantity)
		    {
			    safety++;
			    if (safety > 100)
			    {
				    Debug.LogWarning("safety");
				    break;
			    }

			    var connectionCandidates = FindAllConnectablePairs(groups[0], groups[1], minConnectionLength, maxConnectionLength);
		        var hasConnectionCandidates = connectionCandidates.Count > 0;
		        var isLosClear = false;
		        (int2 cell1, int2 cell2) chosenPair = default;
		        if (hasConnectionCandidates)
		        {
			        chosenPair = connectionCandidates[Random.Range(0, connectionCandidates.Count)];
			        isLosClear = IsLineOfSightClear(chosenPair.cell1, chosenPair.cell2);
		        }

		        if (hasConnectionCandidates && isLosClear)
		        {
			        unchangedGuess = 0;
		            // Add randomness to connection selection
		            var (cell1, cell2) = chosenPair;
		            var direction = GetDirection(cell1, cell2);
		            connections.Add(new IslandConnection(cell1, cell2, direction));
		        }
		        else if (shouldFallback && unchangedGuess > 10)
		        {
			        Debug.Log("fallback");
		            // Fallback to the closest connection between groups within the max length
		            var closestPair = FindClosestPair(groups[0], groups[1], fallbackConnectionLength);
		            if (closestPair != null)
		            {
		                var (cell1, cell2) = closestPair.Value;
		                var direction = GetDirection(cell1, cell2);
		                connections.Add(new IslandConnection(cell1, cell2, direction));
		            }
		            else
		            {
			            fallbackConnectionLength++;
		            }
		        }
		        else
		        {
			        // Debug.Log("missed dist: " + Vector2.Distance(chosenPair.cell1.ToVector2(), chosenPair.cell2.ToVector2()));
		        }

		        unchangedGuess++;

		        // Update groups after merging
		        groups = FindDisconnectedGroups(populatedCells, connections);
		    }

		    return connections;
		}

		// Finds the closest pair of cells between two groups within the max connection length
		private (int2 cell1, int2 cell2)? FindClosestPair(
		    List<int2> group1,
		    List<int2> group2,
		    int maxConnectionLength)
		{
		    int2? bestCell1 = null;
		    int2? bestCell2 = null;
		    var shortestDistance = float.MaxValue;

		    foreach (var cell1 in group1)
		    {
		        foreach (var cell2 in group2)
		        {
		            // var distance = Mathf.Abs(cell1.x - cell2.x) + Mathf.Abs(cell1.y - cell2.y); // Manhattan distance
		            var distance = Mathf.Sqrt(Mathf.Pow(cell1.x - cell2.x, 2) + Mathf.Pow(cell1.y - cell2.y, 2)); // Euclidean distance
		            if (distance <= maxConnectionLength && distance < shortestDistance)
		            {
		                shortestDistance = distance;
		                bestCell1 = cell1;
		                bestCell2 = cell2;
		            }
		        }
		    }

		    if (bestCell1 != null && bestCell2 != null)
		    {
		        return (bestCell1.Value, bestCell2.Value);
		    }

		    return null;
		}

		// Finds all connectable pairs between two groups within the max connection length
		private List<(int2 cell1, int2 cell2)> FindAllConnectablePairs(
		    List<int2> group1,
		    List<int2> group2,
		    int minConnectionLength,
		    int maxConnectionLength)
		{
		    var connectionCandidates = new List<(int2 cell1, int2 cell2)>();

		    foreach (var cell1 in group1)
		    {
		        foreach (var cell2 in group2)
		        {
		            // var distance = Mathf.Abs(cell1.x - cell2.x) + Mathf.Abs(cell1.y - cell2.y); // Manhattan distance
		            var distance = Mathf.Sqrt(Mathf.Pow(cell1.x - cell2.x, 2) + Mathf.Pow(cell1.y - cell2.y, 2)); // Euclidean distance
		            Debug.Log(distance + $" ({minConnectionLength},{maxConnectionLength})");
		            if (distance >= minConnectionLength && distance <= maxConnectionLength)
		            {
		                connectionCandidates.Add((cell1, cell2));
		            }
		        }
		    }

		    return connectionCandidates;
		}

		// Checks if the line of sight between two cells is clear
		private bool IsLineOfSightClear(int2 cell1, int2 cell2)
		{
		    var xDiff = Mathf.Abs(cell1.x - cell2.x);
		    var yDiff = Mathf.Abs(cell1.y - cell2.y);
		    var steps = Mathf.Max(xDiff, yDiff);

		    // todo: should this be direction based? if it uses manhattan dist it might run through an existing island (it does)
		    
		    for (var i = 1; i < steps; i++)
		    {
		        var intermediateX = Mathf.RoundToInt(Mathf.Lerp(cell1.x, cell2.x, (float)i / steps));
		        var intermediateY = Mathf.RoundToInt(Mathf.Lerp(cell1.y, cell2.y, (float)i / steps));

		        if (_grid[intermediateX][intermediateY] != string.Empty)
		        {
			        // same island id is ok i think
			        if (_grid[intermediateX][intermediateY] != _grid[cell1.x][cell1.y]
			            && _grid[intermediateX][intermediateY] != _grid[cell2.x][cell2.y])
							return false; // Line of sight is blocked
		        }
		    }

		    return true;
		}


		// Finds disconnected groups using BFS/DFS
		private List<List<int2>> FindDisconnectedGroups(
		    List<int2> populatedCells,
		    List<IslandConnection> connections)
		{
		    var groups = new List<List<int2>>();
		    var visited = new HashSet<int2>();

		    foreach (var cell in populatedCells)
		    {
		        if (!visited.Contains(cell))
		        {
		            var group = new List<int2>();
		            var queue = new Queue<int2>();
		            queue.Enqueue(cell);

		            while (queue.Count > 0)
		            {
		                var current = queue.Dequeue();
		                if (visited.Contains(current))
		                    continue;

		                visited.Add(current);
		                group.Add(current);

		                foreach (var neighbor in populatedCells)
		                {
		                    if (!visited.Contains(neighbor) &&
		                        IsExplicitlyConnected(current, neighbor, connections))
		                    {
		                        queue.Enqueue(neighbor);
		                    }
		                }
		            }

		            groups.Add(group);
		        }
		    }

		    // randomize return value
		    return groups.OrderBy(_ => new System.Random().Next()).ToList();
		}

	    private bool IsExplicitlyConnected(int2 start, int2 end, List<IslandConnection> connections)
	    {
		    // Check if the connection exists in the list in either direction
		    return connections.Exists(c =>
			    (c.StartCoord.x == start.x && c.StartCoord.y == start.y && c.EndCoord.x == end.x && c.EndCoord.y == end.y) ||
			    (c.StartCoord.x == end.x && c.StartCoord.y == end.y && c.EndCoord.x == start.x && c.EndCoord.y == start.y));
	    }


	    private NSEW GetDirection(int2 start, int2 end)
	    {
		    NSEW? dir = null;
	        var dx = end.x - start.x;
	        var dy = end.y - start.y;

	        if (dx > 0 && dy == 0) dir = NSEW.East;
	        if (dx < 0 && dy == 0) dir = NSEW.West;
	        if (dy > 0 && dx == 0) dir = NSEW.North;
	        if (dy < 0 && dx == 0) dir = NSEW.South;

	        if (!dir.HasValue)
	        {
		        dir = dx > 0
			        ? (dy > 0 ? NSEW.NorthEast : NSEW.SouthEast)
			        : (dy > 0 ? NSEW.NorthWest : NSEW.SouthWest);
	        }

	        Debug.Log($"direction between {start}, {end} is {dir}");

	        return dir.Value;
	    }
	}
	
	public struct IslandConnection
	{
		public int2 StartCoord { get; }
		public int2 EndCoord { get; }
		public NSEW Direction { get; }

		public IslandConnection(int2 startCoord, int2 endCoord, NSEW direction)
		{
			StartCoord = startCoord;
			EndCoord = endCoord;
			Direction = direction;
		}

		public override string ToString()
		{
			return $"From ({StartCoord.x}, {StartCoord.y}) to ({EndCoord.x}, {EndCoord.y}) via {Direction}";
		}
	}
}