using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.MovementLab
{
    public sealed class MovementCourse
    {
        private const int SamplesPerTree = 16;
        private const float DetourPadding = 0.03f;

        private readonly float walkableHalfWidth;
        private readonly float walkableHalfDepth;
        private readonly float clearanceRadiusSquared;
        private readonly Vector2[] treeCenters;
        private readonly List<Vector2> detourPoints = new List<Vector2>();
        private readonly List<int>[] detourNeighbors;

        public IReadOnlyList<Vector2> Trees { get; }

        public MovementCourse(float halfWidth, float halfDepth, Vector2[] trees, float treeRadius, float unitRadius)
        {
            treeCenters = (Vector2[])trees.Clone();
            Trees = Array.AsReadOnly(treeCenters);
            walkableHalfWidth = halfWidth - unitRadius;
            walkableHalfDepth = halfDepth - unitRadius;
            float clearanceRadius = treeRadius + unitRadius;
            clearanceRadiusSquared = clearanceRadius * clearanceRadius;

            // The polygon sides stay outside the tree and unit clearance circle.
            float detourRadius = clearanceRadius / Mathf.Cos(Mathf.PI / SamplesPerTree) + DetourPadding;
            foreach (Vector2 tree in treeCenters)
            {
                for (int sample = 0; sample < SamplesPerTree; sample++)
                {
                    float angle = sample * (2f * Mathf.PI / SamplesPerTree);
                    Vector2 point = tree + detourRadius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    if (IsWalkable(point))
                        detourPoints.Add(point);
                }
            }

            detourNeighbors = new List<int>[detourPoints.Count];
            for (int index = 0; index < detourPoints.Count; index++)
                detourNeighbors[index] = new List<int>();

            for (int first = 0; first < detourPoints.Count; first++)
            {
                for (int second = first + 1; second < detourPoints.Count; second++)
                {
                    if (!HasClearSegment(detourPoints[first], detourPoints[second]))
                        continue;

                    detourNeighbors[first].Add(second);
                    detourNeighbors[second].Add(first);
                }
            }
        }

        public bool IsWalkable(Vector2 point)
        {
            if (!(Mathf.Abs(point.x) <= walkableHalfWidth && Mathf.Abs(point.y) <= walkableHalfDepth))
                return false;

            foreach (Vector2 tree in treeCenters)
            {
                if ((point - tree).sqrMagnitude <= clearanceRadiusSquared)
                    return false;
            }

            return true;
        }

        // Each route leg has clearance for the unit's full radius.
        public bool TryFindRoute(Vector2 start, Vector2 destination, List<Vector2> route)
        {
            route.Clear();
            if (!IsWalkable(start) || !IsWalkable(destination))
                return false;

            if (HasClearSegment(start, destination))
            {
                route.Add(destination);
                return true;
            }

            int pointCount = detourPoints.Count;
            var costs = new float[pointCount];
            var estimates = new float[pointCount];
            var previous = new int[pointCount];
            for (int point = 0; point < pointCount; point++)
            {
                previous[point] = -1;
                costs[point] = HasClearSegment(start, detourPoints[point])
                    ? Vector2.Distance(start, detourPoints[point])
                    : float.PositiveInfinity;
                estimates[point] = costs[point] + Vector2.Distance(detourPoints[point], destination);
            }

            // A* on the visible segments. Straight-line distance is exact once the goal is visible.
            while (true)
            {
                int current = -1;
                float bestEstimate = float.PositiveInfinity;
                for (int point = 0; point < pointCount; point++)
                {
                    if (estimates[point] < bestEstimate)
                    {
                        current = point;
                        bestEstimate = estimates[point];
                    }
                }

                if (current < 0)
                    return false;

                Vector2 currentPoint = detourPoints[current];
                if (HasClearSegment(currentPoint, destination))
                {
                    route.Add(destination);
                    for (int point = current; point >= 0; point = previous[point])
                        route.Add(detourPoints[point]);
                    route.Reverse();
                    return true;
                }

                estimates[current] = float.PositiveInfinity;
                foreach (int neighbor in detourNeighbors[current])
                {
                    float candidateCost = costs[current] + Vector2.Distance(currentPoint, detourPoints[neighbor]);
                    if (candidateCost >= costs[neighbor])
                        continue;

                    costs[neighbor] = candidateCost;
                    estimates[neighbor] = candidateCost + Vector2.Distance(detourPoints[neighbor], destination);
                    previous[neighbor] = current;
                }
            }
        }

        private bool HasClearSegment(Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared == 0f)
                return true;

            foreach (Vector2 tree in treeCenters)
            {
                float projection = Mathf.Clamp01(Vector2.Dot(tree - start, segment) / lengthSquared);
                Vector2 closestPoint = start + projection * segment;
                if ((tree - closestPoint).sqrMagnitude <= clearanceRadiusSquared)
                    return false;
            }

            return true;
        }
    }
}
