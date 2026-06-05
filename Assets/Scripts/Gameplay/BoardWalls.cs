using System.Collections.Generic;
using UnityEngine;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Builds walls that hug the EXACT contour of the board, including indentations and holes.
    /// For every occupied cell, each of its three "closed" directions - back (-Y/-Z), left (-X),
    /// right (+X) - gets a wall segment if the neighbour in that direction is NOT an occupied cell
    /// (i.e. it's empty, a hole, or off the board). The FRONT (+Y/+Z, toward the camera) is never
    /// walled, matching the Block Jam rule that units exit through the front.
    ///
    /// Wall segments are taken from a small reusable pool (a prefab cube). Nothing is created or
    /// destroyed during gameplay - on each level we just deactivate all segments and reactivate
    /// the ones we need, growing the pool only if a bigger level needs more. This respects the
    /// project's no-runtime-instantiation rule (Instantiate happens only when the pool must grow).
    /// </summary>
    public class BoardWalls : MonoBehaviour
    {
        [Header("Segment prefab (a simple cube). One is placed per closed cell edge.")]
        [SerializeField] private Transform segmentPrefab;

        [Header("Tuning")]
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private float wallThickness = 0.5f;
        [Tooltip("Length of a segment along the cell edge. Usually ~cellSpacing so segments meet.")]
        [SerializeField] private float lengthPadding = 0.05f;

        private readonly List<Transform> _pool = new List<Transform>();
        private int _activeCount;

        /// <summary>
        /// Rebuilds the wall contour for the given board. cellWorld(x,y) must return the world
        /// center of cell (x,y); occupied(x,y) tells whether a cell currently holds the layout.
        /// </summary>
        public void Build(int width, int height, float cellSpacing, Vector3 boardCenter,
                          System.Func<int, int, bool> occupied, System.Func<int, int, Vector3> cellWorld)
        {
            DeactivateAll();
            if (segmentPrefab == null) return;

            float halfCell = cellSpacing * 0.5f;
            float segLen = cellSpacing + lengthPadding;
            float yPos = boardCenter.y + wallHeight * 0.5f;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!occupied(x, y)) continue;
                Vector3 c = cellWorld(x, y);
                c.y = yPos;

                // Back edge (-Y in grid = -Z in world): wall if cell behind is not occupied.
                if (!IsOccupiedSafe(x, y - 1, width, height, occupied))
                    PlaceSegment(c + new Vector3(0f, 0f, -halfCell), new Vector3(segLen, wallHeight, wallThickness));

                // Left edge (-X): wall if cell to the left is not occupied.
                if (!IsOccupiedSafe(x - 1, y, width, height, occupied))
                    PlaceSegment(c + new Vector3(-halfCell, 0f, 0f), new Vector3(wallThickness, wallHeight, segLen));

                // Right edge (+X): wall if cell to the right is not occupied.
                if (!IsOccupiedSafe(x + 1, y, width, height, occupied))
                    PlaceSegment(c + new Vector3(halfCell, 0f, 0f), new Vector3(wallThickness, wallHeight, segLen));

                // Front edge (+Z, toward the camera) is the EXIT direction and is never walled,
                // so units can always walk out forward. Interior holes remain impassable for
                // movement via BlockChecker pathfinding; we simply don't draw front-facing walls,
                // which previously risked sealing the exit when the front row had gaps.
            }
        }

        private bool IsOccupiedSafe(int x, int y, int width, int height, System.Func<int, int, bool> occupied)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return false; // off board = open = needs wall
            return occupied(x, y);
        }

        private void PlaceSegment(Vector3 pos, Vector3 scale)
        {
            Transform seg = GetSegment();
            seg.position = pos;
            seg.localScale = scale;
            seg.rotation = Quaternion.identity;
            seg.gameObject.SetActive(true);
        }

        private Transform GetSegment()
        {
            if (_activeCount < _pool.Count)
                return _pool[_activeCount++];

            // Grow the pool (Instantiate only here, never during gameplay actions).
            Transform seg = Instantiate(segmentPrefab, transform);
            _pool.Add(seg);
            _activeCount++;
            return seg;
        }

        private void DeactivateAll()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
            _activeCount = 0;
        }
    }
}