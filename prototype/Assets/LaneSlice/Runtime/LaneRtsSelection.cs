using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public readonly struct ControllableUnitInfo
    {
        public readonly int Id;
        public readonly Vector2 Pos;
        public readonly string Label;
        public readonly bool IsHero;

        public ControllableUnitInfo(int id, Vector2 pos, string label, bool isHero)
        {
            Id = id;
            Pos = pos;
            Label = label;
            IsHero = isHero;
        }
    }

    /// <summary>
    /// Client-side RTS unit selection, drag-box query, and control-group manager (Groups 1..4).
    /// Enforces strict local-team ownership and unit controllability, and prunes dead/expired
    /// units automatically on every snapshot.
    /// </summary>
    public sealed class LaneRtsSelection
    {
        public const int MaxControlGroups = 4;
        public const float DoubleTapWindowSeconds = 0.38f;

        private readonly List<int> selectedUnitIds = new List<int>();
        private readonly List<int>[] controlGroups = new List<int>[MaxControlGroups];
        private readonly HashSet<int> knownAuthorizedIds = new HashSet<int>();
        private readonly List<ControllableUnitInfo> scratchUnits = new List<ControllableUnitInfo>();
        private readonly HashSet<int> scratchAuthSet = new HashSet<int>();

        private int lastRecallGroup = -1;
        private float lastRecallTime = -999f;

        public IReadOnlyList<int> SelectedUnitIds => selectedUnitIds;

        public LaneRtsSelection()
        {
            for (int i = 0; i < MaxControlGroups; i++)
            {
                controlGroups[i] = new List<int>();
            }
        }

        public void Reset()
        {
            selectedUnitIds.Clear();
            knownAuthorizedIds.Clear();
            for (int i = 0; i < MaxControlGroups; i++)
            {
                controlGroups[i].Clear();
            }
            lastRecallGroup = -1;
            lastRecallTime = -999f;
        }

        public void GetAuthorizedUnits(LaneSnapshot snap, LaneTeam localTeam, List<ControllableUnitInfo> results)
        {
            results.Clear();
            if (snap == null) return;

            for (int i = 0; i < snap.heroes.Count; i++)
            {
                HeroSnap h = snap.heroes[i];
                if (h.team == localTeam && h.phase != LanePhase.Dead)
                {
                    results.Add(new ControllableUnitInfo(h.id, new Vector2(h.x, h.y), LaneNames.ShortName(h.heroId), isHero: true));
                }
            }

            for (int i = 0; i < snap.illusions.Count; i++)
            {
                IllusionSnap ill = snap.illusions[i];
                if (ill.team == localTeam && ill.controllable && ill.hp > 0f)
                {
                    results.Add(new ControllableUnitInfo(ill.id, new Vector2(ill.x, ill.y), "Shimmer Decoy", isHero: false));
                }
            }
        }

        public void SyncWithSnapshot(LaneSnapshot snap, LaneTeam localTeam, bool rtsActive, bool autoSelectSummons)
        {
            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            scratchAuthSet.Clear();
            int aliveHeroId = 0;
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                scratchAuthSet.Add(scratchUnits[i].Id);
                if (scratchUnits[i].IsHero && aliveHeroId == 0)
                {
                    aliveHeroId = scratchUnits[i].Id;
                }
            }

            for (int i = selectedUnitIds.Count - 1; i >= 0; i--)
            {
                if (!scratchAuthSet.Contains(selectedUnitIds[i]))
                {
                    selectedUnitIds.RemoveAt(i);
                }
            }

            for (int g = 0; g < MaxControlGroups; g++)
            {
                List<int> grp = controlGroups[g];
                for (int i = grp.Count - 1; i >= 0; i--)
                {
                    if (!scratchAuthSet.Contains(grp[i]))
                    {
                        grp.RemoveAt(i);
                    }
                }
            }

            if (!rtsActive)
            {
                selectedUnitIds.Clear();
                if (aliveHeroId > 0) selectedUnitIds.Add(aliveHeroId);
                SyncKnownSet(scratchAuthSet);
                return;
            }

            if (autoSelectSummons)
            {
                for (int i = 0; i < scratchUnits.Count; i++)
                {
                    ControllableUnitInfo u = scratchUnits[i];
                    if (!u.IsHero && !knownAuthorizedIds.Contains(u.Id) && !selectedUnitIds.Contains(u.Id))
                    {
                        selectedUnitIds.Add(u.Id);
                    }
                }
            }

            SyncKnownSet(scratchAuthSet);

            if (selectedUnitIds.Count == 0 && aliveHeroId > 0)
            {
                selectedUnitIds.Add(aliveHeroId);
            }
        }

        private void SyncKnownSet(HashSet<int> currentAuth)
        {
            knownAuthorizedIds.Clear();
            foreach (int id in currentAuth)
            {
                knownAuthorizedIds.Add(id);
            }
        }

        public bool IsSelected(int unitId)
        {
            for (int i = 0; i < selectedUnitIds.Count; i++)
            {
                if (selectedUnitIds[i] == unitId) return true;
            }
            return false;
        }

        public bool SelectSingleUnit(int unitId, bool additive, LaneSnapshot snap, LaneTeam localTeam)
        {
            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            bool authorized = false;
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                if (scratchUnits[i].Id == unitId)
                {
                    authorized = true;
                    break;
                }
            }
            if (!authorized) return false;

            if (!additive)
            {
                selectedUnitIds.Clear();
                selectedUnitIds.Add(unitId);
            }
            else if (IsSelected(unitId))
            {
                if (selectedUnitIds.Count > 1) selectedUnitIds.Remove(unitId);
            }
            else
            {
                selectedUnitIds.Add(unitId);
            }
            return true;
        }

        public int SelectByPredicate(Func<ControllableUnitInfo, bool> predicate, bool additive, LaneSnapshot snap, LaneTeam localTeam)
        {
            if (predicate == null) return 0;
            GetAuthorizedUnits(snap, localTeam, scratchUnits);

            var matched = new List<int>();
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                if (predicate(scratchUnits[i]))
                {
                    matched.Add(scratchUnits[i].Id);
                }
            }

            if (matched.Count == 0)
            {
                return 0;
            }

            if (!additive)
            {
                selectedUnitIds.Clear();
            }

            for (int i = 0; i < matched.Count; i++)
            {
                if (!selectedUnitIds.Contains(matched[i]))
                {
                    selectedUnitIds.Add(matched[i]);
                }
            }
            return matched.Count;
        }

        public bool SelectHero(LaneSnapshot snap, LaneTeam localTeam)
        {
            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                if (scratchUnits[i].IsHero)
                {
                    selectedUnitIds.Clear();
                    selectedUnitIds.Add(scratchUnits[i].Id);
                    return true;
                }
            }
            return false;
        }

        public int SelectAllControllable(LaneSnapshot snap, LaneTeam localTeam)
        {
            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            if (scratchUnits.Count == 0) return 0;

            selectedUnitIds.Clear();
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                selectedUnitIds.Add(scratchUnits[i].Id);
            }
            return selectedUnitIds.Count;
        }

        public bool CycleSelectedUnit(LaneSnapshot snap, LaneTeam localTeam)
        {
            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            if (scratchUnits.Count == 0) return false;

            int currentPrimary = selectedUnitIds.Count > 0 ? selectedUnitIds[0] : 0;
            int idx = -1;
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                if (scratchUnits[i].Id == currentPrimary)
                {
                    idx = i;
                    break;
                }
            }

            int nextIdx = (idx + 1) % scratchUnits.Count;
            selectedUnitIds.Clear();
            selectedUnitIds.Add(scratchUnits[nextIdx].Id);
            return true;
        }

        public bool AssignControlGroup(int groupSlot, LaneSnapshot snap, LaneTeam localTeam)
        {
            if (groupSlot < 1 || groupSlot > MaxControlGroups) return false;
            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            scratchAuthSet.Clear();
            int heroId = 0;
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                scratchAuthSet.Add(scratchUnits[i].Id);
                if (scratchUnits[i].IsHero && heroId == 0) heroId = scratchUnits[i].Id;
            }

            List<int> grp = controlGroups[groupSlot - 1];
            grp.Clear();
            for (int i = 0; i < selectedUnitIds.Count; i++)
            {
                int id = selectedUnitIds[i];
                if (scratchAuthSet.Contains(id) && !grp.Contains(id))
                {
                    grp.Add(id);
                }
            }
            if (grp.Count == 0 && heroId > 0)
            {
                grp.Add(heroId);
            }
            return grp.Count > 0;
        }

        public bool RecallControlGroup(
            int groupSlot,
            LaneSnapshot snap,
            LaneTeam localTeam,
            float nowTime,
            out bool shouldCenterCamera,
            out Vector2 focusPos)
        {
            shouldCenterCamera = false;
            focusPos = Vector2.zero;
            if (groupSlot < 1 || groupSlot > MaxControlGroups) return false;

            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            var posById = new Dictionary<int, Vector2>(scratchUnits.Count);
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                posById[scratchUnits[i].Id] = scratchUnits[i].Pos;
            }

            List<int> grp = controlGroups[groupSlot - 1];
            for (int i = grp.Count - 1; i >= 0; i--)
            {
                if (!posById.ContainsKey(grp[i]))
                {
                    grp.RemoveAt(i);
                }
            }

            if (grp.Count == 0) return false;

            selectedUnitIds.Clear();
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < grp.Count; i++)
            {
                int id = grp[i];
                selectedUnitIds.Add(id);
                sum += posById[id];
            }
            focusPos = sum / grp.Count;

            shouldCenterCamera = lastRecallGroup == groupSlot && (nowTime - lastRecallTime) <= DoubleTapWindowSeconds;
            lastRecallGroup = groupSlot;
            lastRecallTime = nowTime;
            return true;
        }

        public int GetControlGroupCount(int groupSlot)
        {
            if (groupSlot < 1 || groupSlot > MaxControlGroups) return 0;
            return controlGroups[groupSlot - 1].Count;
        }

        public int[] GetCommandTargetUnitIds(bool rtsActive, LaneSnapshot snap, LaneTeam localTeam)
        {
            if (!rtsActive) return null;

            GetAuthorizedUnits(snap, localTeam, scratchUnits);
            scratchAuthSet.Clear();
            int aliveHeroId = 0;
            for (int i = 0; i < scratchUnits.Count; i++)
            {
                scratchAuthSet.Add(scratchUnits[i].Id);
                if (scratchUnits[i].IsHero && aliveHeroId == 0) aliveHeroId = scratchUnits[i].Id;
            }

            var valid = new List<int>(selectedUnitIds.Count);
            for (int i = 0; i < selectedUnitIds.Count; i++)
            {
                int id = selectedUnitIds[i];
                if (scratchAuthSet.Contains(id) && !valid.Contains(id))
                {
                    valid.Add(id);
                }
            }

            if (valid.Count == 0 && aliveHeroId > 0)
            {
                valid.Add(aliveHeroId);
            }

            return valid.Count > 0 ? valid.ToArray() : null;
        }
    }
}
