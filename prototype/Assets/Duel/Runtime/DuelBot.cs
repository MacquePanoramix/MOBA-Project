using UnityEngine;

namespace Moba.Duel
{
    // The bot sees the same positions, cast telegraphs and orb as the players.
    // It issues ordinary orders at a limited cadence, without reading the opponent's input.
    public sealed class DuelBot
    {
        private readonly DuelHero hero;
        private readonly DuelTuning tuning;
        private float thinkIn;
        private Vector2 lastDestination = Vector2.positiveInfinity;
        private Vector2 lastEnemyPosition;
        private bool sawEnemy;

        public DuelBot(DuelHero hero, DuelTuning tuning)
        {
            this.hero = hero;
            this.tuning = tuning;
            thinkIn = 0.32f;
        }

        public void Tick(DuelMatch game, DuelFrame frame, float dt)
        {
            if (frame.finished) return;
            thinkIn -= dt;
            if (thinkIn > 0f) return;
            const float interval = 0.24f;
            thinkIn = interval;
            var me = hero == DuelHero.Ogre ? frame.ogre : frame.luis;
            var enemy = hero == DuelHero.Ogre ? frame.luis : frame.ogre;
            Vector2 lead = sawEnemy ? Vector2.ClampMagnitude(enemy.position - lastEnemyPosition, 0.65f) : Vector2.zero;
            lastEnemyPosition = enemy.position;
            sawEnemy = true;
            if (me.stun > 0f || me.action != 0) return;

            float gap = Vector2.Distance(me.position, enemy.position);
            float orbGap = Vector2.Distance(me.position, frame.orb);
            if (hero == DuelHero.Ogre)
            {
                bool enemyNearOrb = frame.orbActive && Vector2.Distance(enemy.position, frame.orb) < 1.7f;
                if (me.wCooldown <= 0f && gap <= tuning.sweepRange * 0.90f &&
                    (enemyNearOrb || me.qCooldown > 0.1f))
                    game.Command(hero, DuelOrder.Sweep, enemy.position);
                else if (me.qCooldown <= 0f && gap < tuning.slamRange * 0.95f)
                    game.Command(hero, DuelOrder.Slam, enemy.position + lead);
                else if (frame.orbActive && (orbGap < 5f || gap > tuning.slamRange))
                    Move(game, me, frame.orb);
                else if (gap < 1.6f && me.wCooldown > 0.45f && me.qCooldown > 0.45f)
                    Move(game, me, me.position + (me.position - enemy.position).normalized * 1.8f);
                else
                    Move(game, me, enemy.position);
                return;
            }

            if (enemy.action == 2 && !enemy.recovering)
            {
                Vector2 offset = me.position - enemy.position;
                Vector2 side = new Vector2(-enemy.aim.y, enemy.aim.x);
                float forward = Vector2.Dot(offset, enemy.aim);
                float across = Vector2.Dot(offset, side);
                if (forward >= -0.3f && forward < tuning.slamRange + 0.45f &&
                    Mathf.Abs(across) < tuning.slamHalfWidth + DuelArena.UnitRadius)
                {
                    if (across < 0f || (Mathf.Abs(across) < 0.05f && me.position.y > 0f)) side = -side;
                    Move(game, me, me.position + side * (tuning.slamHalfWidth + 1.25f));
                    return;
                }
            }
            if (frame.orbActive && (orbGap < 3.3f || me.hp < me.maxHp * 0.85f ||
                Vector2.Distance(enemy.position, frame.orb) <= tuning.orbPulseRadius))
                Move(game, me, frame.orb);
            else if (!me.chasing)
                game.Command(hero, DuelOrder.Basic, enemy.position);
        }

        private void Move(DuelMatch game, DuelUnitFrame me, Vector2 target)
        {
            target = new Vector2(Mathf.Clamp(target.x, -DuelArena.HalfWidth + 0.65f, DuelArena.HalfWidth - 0.65f),
                                 Mathf.Clamp(target.y, -DuelArena.HalfDepth + 0.65f, DuelArena.HalfDepth - 0.65f));
            if (!me.chasing && me.moving && (target - lastDestination).sqrMagnitude < 0.07f) return;
            lastDestination = target;
            game.Command(hero, DuelOrder.Move, target);
        }
    }
}
