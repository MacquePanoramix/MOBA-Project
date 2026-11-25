using System.Collections.Generic;
using Moba.MovementLab;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Symmetric 1v1 woodland lane geometry, side tree alcoves, tower obstacles, and visibility-graph routing.
    /// Reuses MovementCourse directly rather than duplicating A* pathfinding.
    /// </summary>
    public sealed class LaneMap
    {
        public const float TreeRadius = 0.56f;
        public const float UnitRadius = 0.48f;

        private readonly LaneTuning tuning;
        private readonly Vector2[] staticTrees;
        private MovementCourse courseAllStructures;
        private MovementCourse courseNoBlueTower;
        private MovementCourse courseNoRedTower;
        private MovementCourse courseNoTowers;

        public float HalfLength => tuning.laneHalfLength;
        public float HalfWidth => tuning.laneHalfWidth;
        public IReadOnlyList<Vector2> Trees => staticTrees;

        public LaneMap(LaneTuning tuning)
        {
            this.tuning = tuning;
            staticTrees = BuildSymmetricTrees();
            RebuildCourses();
        }

        public Vector2 FountainPos(LaneTeam team) =>
            team == LaneTeam.Blue ? tuning.blueFountainPos : tuning.redFountainPos;

        public Vector2 SpawnPos(LaneTeam team) =>
            team == LaneTeam.Blue ? tuning.blueSpawnPos : tuning.redSpawnPos;

        public Vector2 TowerPos(LaneTeam team) =>
            team == LaneTeam.Blue ? tuning.blueTowerPos : tuning.redTowerPos;

        public bool IsInFountain(LaneTeam team, Vector2 pos)
        {
            return (pos - FountainPos(team)).sqrMagnitude <= tuning.fountainRadius * tuning.fountainRadius;
        }

        public MovementCourse CourseFor(bool blueTowerAlive, bool redTowerAlive)
        {
            if (blueTowerAlive && redTowerAlive) return courseAllStructures;
            if (!blueTowerAlive && redTowerAlive) return courseNoBlueTower;
            if (blueTowerAlive && !redTowerAlive) return courseNoRedTower;
            return courseNoTowers;
        }

        public Vector2 ClampInsideLane(Vector2 point, float margin = 0.55f)
        {
            float maxX = Mathf.Max(1f, tuning.laneHalfLength - margin);
            float maxZ = Mathf.Max(1f, tuning.laneHalfWidth - margin);
            return new Vector2(
                Mathf.Clamp(point.x, -maxX, maxX),
                Mathf.Clamp(point.y, -maxZ, maxZ));
        }

        public bool IsWalkable(Vector2 point, bool blueTowerAlive, bool redTowerAlive)
        {
            return CourseFor(blueTowerAlive, redTowerAlive).IsWalkable(point);
        }

        public Vector2 NearestWalkable(Vector2 point, bool blueTowerAlive, bool redTowerAlive)
        {
            Vector2 clamped = ClampInsideLane(point);
            MovementCourse course = CourseFor(blueTowerAlive, redTowerAlive);
            if (course.IsWalkable(clamped)) return clamped;

            for (int rStep = 1; rStep <= 10; rStep++)
            {
                float r = rStep * 0.42f;
                for (int i = 0; i < 16; i++)
                {
                    float rad = i * Mathf.PI * 2f / 16f;
                    Vector2 candidate = ClampInsideLane(clamped + new Vector2(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r));
                    if (course.IsWalkable(candidate)) return candidate;
                }
            }
            return Vector2.zero;
        }

        public Vector2 PushOut(Vector2 start, Vector2 delta, bool blueTowerAlive, bool redTowerAlive)
        {
            float dist = delta.magnitude;
            if (dist < 0.0001f) return start;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / 0.25f), 1, 24);
            Vector2 step = delta / steps;
            Vector2 cur = start;
            MovementCourse course = CourseFor(blueTowerAlive, redTowerAlive);

            for (int i = 0; i < steps; i++)
            {
                Vector2 next = ClampInsideLane(cur + step);
                if (course.IsWalkable(next))
                {
                    cur = next;
                }
                else
                {
                    Vector2 tryX = ClampInsideLane(new Vector2(next.x, cur.y));
                    if (course.IsWalkable(tryX))
                    {
                        cur = tryX;
                        continue;
                    }
                    Vector2 tryY = ClampInsideLane(new Vector2(cur.x, next.y));
                    if (course.IsWalkable(tryY))
                    {
                        cur = tryY;
                        continue;
                    }
                    break;
                }
            }
            return cur;
        }

        public bool TryFindRoute(Vector2 start, Vector2 dest, bool blueTowerAlive, bool redTowerAlive, List<Vector2> route)
        {
            Vector2 safeStart = NearestWalkable(start, blueTowerAlive, redTowerAlive);
            Vector2 safeDest = NearestWalkable(dest, blueTowerAlive, redTowerAlive);
            return CourseFor(blueTowerAlive, redTowerAlive).TryFindRoute(safeStart, safeDest, route);
        }

        private void RebuildCourses()
        {
            courseAllStructures = BuildCourse(includeBlueTower: true, includeRedTower: true);
            courseNoBlueTower = BuildCourse(includeBlueTower: false, includeRedTower: true);
            courseNoRedTower = BuildCourse(includeBlueTower: true, includeRedTower: false);
            courseNoTowers = BuildCourse(includeBlueTower: false, includeRedTower: false);
        }

        private MovementCourse BuildCourse(bool includeBlueTower, bool includeRedTower)
        {
            var blockers = new List<Vector2>(staticTrees.Length + 6);
            blockers.AddRange(staticTrees);
            if (includeBlueTower) AddTowerFootprint(blockers, tuning.blueTowerPos);
            if (includeRedTower) AddTowerFootprint(blockers, tuning.redTowerPos);
            return new MovementCourse(tuning.laneHalfLength, tuning.laneHalfWidth, blockers.ToArray(), TreeRadius, UnitRadius);
        }

        private static void AddTowerFootprint(List<Vector2> blockers, Vector2 towerPos)
        {
            // Clean circular footprint around tower center (clearance radius = 1.04u)
            blockers.Add(towerPos);
        }

        private static Vector2[] BuildSymmetricTrees()
        {
            // Open central lane corridor (~y in [-4.5, +4.5]) with tactical side tree pockets
            // for jukes, orb contests, and Miravel ambush angles.
            var pairs = new List<Vector2>();
            AddMirror(pairs, new Vector2(5.2f, 5.4f));
            AddMirror(pairs, new Vector2(6.5f, 6.2f));
            AddMirror(pairs, new Vector2(14.5f, 5.6f));
            AddMirror(pairs, new Vector2(15.7f, 6.4f));

            AddMirror(pairs, new Vector2(5.2f, -5.4f));
            AddMirror(pairs, new Vector2(6.5f, -6.2f));
            AddMirror(pairs, new Vector2(14.5f, -5.6f));
            AddMirror(pairs, new Vector2(15.7f, -6.4f));

            // Center-flank lone trees (wide enough for crisp pathing around)
            pairs.Add(new Vector2(0f, 5.1f));
            pairs.Add(new Vector2(0f, -5.1f));
            return pairs.ToArray();
        }

        private static void AddMirror(List<Vector2> list, Vector2 pos)
        {
            list.Add(pos);
            list.Add(new Vector2(-pos.x, pos.y));
        }
    }
}
