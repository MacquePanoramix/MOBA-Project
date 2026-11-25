using UnityEngine;

namespace Moba.Duel
{
    public sealed partial class DuelWorld
    {
        private sealed class Figure
        {
            public Transform root, facing, body, arm, feet, leftFoot, rightFoot, bar, life;
            public LineRenderer selection, impact, stunRing, healRing;
            public float oldHp = -1f;
            public float hitTime, healTime, step;
            public bool placed;
            public Vector2 lastPosition, visualPos;
            public float visualHeading;
        }

        private Figure MakeHero(bool isOgre)
        {
            var figure = new Figure();
            figure.root = Child(isOgre ? "Ogre" : "Luís",root);
            figure.facing = Child("Facing",figure.root);
            figure.body = Child("Upper body",figure.facing);
            figure.feet = Child("Feet",figure.facing);
            Color accent = isOgre ? OgreTint : LuisTint;
            Color hide = isOgre ? new Color32(153,125,78,255) : new Color32(116,77,54,255);
            Color leather = new Color32(85,59,48,255);
            Color iron = new Color32(68,83,88,255);
            var torso = new DuelGeometry();
            var weapon = new DuelGeometry();
            if (isOgre)
            {
                var skin = new Color32(191,151,89,255);
                var mantle = new Color32(163,76,57,255);
                torso.Column(new Vector3(0,0.32f,0),0.43f,new Vector2(0.53f,0.39f),new Vector2(0.48f,0.34f),8,mantle);
                torso.Oval(new Vector3(0,0.94f,0.03f),new Vector3(0.61f,0.50f,0.40f),9,hide);
                torso.Column(new Vector3(0,0.60f,0.03f),0.13f,new Vector2(0.54f,0.39f),new Vector2(0.54f,0.39f),9,leather);
                torso.Oval(new Vector3(0,0.69f,0.406f),new Vector3(0.14f,0.12f,0.05f),6,Bronze);
                torso.Oval(new Vector3(-0.43f,1.16f,0.02f),new Vector3(0.36f,0.23f,0.33f),7,OgreTint);
                torso.Oval(new Vector3(-0.53f,0.85f,0.24f),new Vector3(0.18f,0.32f,0.20f),7,skin);
                torso.Oval(new Vector3(0.46f,1.18f,-0.02f),new Vector3(0.26f,0.26f,0.28f),8,skin);
                torso.Oval(new Vector3(0,1.44f,0.17f),new Vector3(0.33f,0.32f,0.29f),8,skin);
                torso.Oval(new Vector3(0,1.36f,0.34f),new Vector3(0.29f,0.13f,0.21f),7,hide);
                torso.Oval(new Vector3(0,1.70f,0.04f),new Vector3(0.25f,0.11f,0.22f),6,leather);
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * 0.21f;
                    torso.Beam(new Vector3(x,1.30f,0.43f),new Vector3(x*1.1f,1.53f,0.47f),0.055f,0f,Gold,5);
                    torso.Beam(new Vector3(s*0.24f,1.54f,0.35f),new Vector3(s*0.08f,1.50f,0.43f),0.035f,0.027f,leather,5);
                }
                figure.arm = Child("Club shoulder",figure.body);
                figure.arm.localPosition = new Vector3(0.47f,1.07f,0.10f);
                weapon.Beam(Vector3.zero,new Vector3(0.15f,-0.19f,0.24f),0.16f,0.13f,skin);
                weapon.Oval(new Vector3(0.15f,-0.19f,0.25f),new Vector3(0.16f,0.13f,0.14f),7,hide);
                Vector3 grip = new Vector3(0.15f,-0.18f,0.22f);
                Vector3 cap = new Vector3(0.29f,0.15f,1.02f);
                weapon.Beam(grip,cap,0.068f,0.14f,Timber);
                weapon.Beam(Vector3.Lerp(grip,cap,0.55f),cap,0.14f,0.205f,iron,6);
                weapon.Beam(Vector3.Lerp(grip,cap,0.74f),Vector3.Lerp(grip,cap,0.82f),0.192f,0.199f,Bronze,6);
                weapon.Beam(cap,cap + new Vector3(0.016f,0.03f,0.1f),0.205f,0.14f,iron,6);
            }
            else
            {
                var cape = new Color32(32,109,112,255);
                var shirt = new Color32(70,145,151,255);
                var linen = new Color32(234,219,167,255);
                var skin = new Color32(190,137,94,255);
                torso.Column(new Vector3(0,0.25f,-0.035f),0.71f,new Vector2(0.40f,0.30f),new Vector2(0.25f,0.20f),8,cape);
                torso.Column(new Vector3(0,0.77f,0.005f),0.41f,new Vector2(0.26f,0.20f),new Vector2(0.38f,0.24f),8,shirt);
                torso.Column(new Vector3(0,0.80f,0),0.082f,new Vector2(0.29f,0.226f),new Vector2(0.29f,0.226f),8,Bronze);
                torso.Quad(new Vector3(-0.22f,1.17f,0.17f),new Vector3(-0.10f,1.20f,0.25f),
                    new Vector3(0.14f,0.36f,0.30f),new Vector3(-0.01f,0.27f,0.29f),Vector3.forward,linen);
                torso.Oval(new Vector3(0,1.37f,0.045f),new Vector3(0.22f,0.22f,0.20f),8,skin);
                torso.Oval(new Vector3(0,1.48f,-0.015f),new Vector3(0.235f,0.16f,0.22f),8,leather);
                torso.Oval(new Vector3(0,1.59f,-0.13f),new Vector3(0.16f,0.12f,0.14f),7,leather);
                torso.Beam(new Vector3(-0.30f,1.16f,-0.02f),new Vector3(-0.33f,0.82f,0.18f),0.13f,0.10f,shirt);
                torso.Oval(new Vector3(-0.33f,0.82f,0.18f),new Vector3(0.097f,0.09f,0.10f),6,skin);
                torso.Beam(new Vector3(0.28f,1.12f,0f),new Vector3(0.34f,0.87f,0.17f),0.11f,0.095f,shirt);
                figure.arm = Child("Staff hand",figure.body);
                figure.arm.localPosition = new Vector3(0.34f,0.90f,0.20f);
                weapon.Oval(Vector3.zero,new Vector3(0.11f,0.085f,0.10f),7,skin);
                weapon.Beam(new Vector3(0,-0.66f,-0.03f),new Vector3(0,0.58f,0.12f),0.042f,0.052f,Bronze);
                for (int s = -1; s <= 1; s += 2)
                {
                    weapon.Beam(new Vector3(0,0.55f,0.12f), new Vector3(s*0.14f,0.71f,0.13f),0.035f,0.029f,Gold);
                    weapon.Beam(new Vector3(s*0.14f,0.71f,0.13f),new Vector3(s*0.09f,0.88f,0.15f),0.029f,0.008f,Gold);
                }
                weapon.Column(new Vector3(0,0.63f,0.13f),0.17f,Vector2.zero,Vector2.one*0.09f,5,MintLight);
                weapon.Column(new Vector3(0,0.80f,0.13f),0.13f,Vector2.one*0.09f,Vector2.zero,5,LuisTint);
            }
            Model("Body",figure.body,torso);
            Model("Weapon",figure.arm,weapon);
            float legX = isOgre ? 0.26f : 0.15f;
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = new DuelGeometry();
                float radius = isOgre ? 0.17f : 0.10f;
                leg.Beam(new Vector3(0,0.32f,0),new Vector3(0,0.10f,0.02f),radius,radius*0.87f,leather);
                leg.Oval(new Vector3(0,0.095f,0.075f),new Vector3(radius,0.095f,radius*1.45f),7,leather);
                var foot = Model("Foot",figure.feet,leg);
                foot.localPosition = new Vector3(s * legX,0,0);
                if (s < 0) figure.leftFoot = foot; else figure.rightFoot = foot;
            }
            var shadow = new DuelGeometry();
            shadow.Column(Vector3.up * 0.061f,0.002f,new Vector2(0.56f,0.40f),new Vector2(0.56f,0.40f),16,Shadow);
            Model("Contact shadow",figure.root,shadow);
            figure.selection = Circle("Unit circle",figure.root,0.68f,0.065f,0.047f,accent);
            figure.impact = Circle("Hit",figure.root,0.72f,0.091f,0.060f,Gold);
            figure.stunRing = Circle("Stun halo",figure.root,0.32f,isOgre ? 1.90f : 1.77f,0.054f,Gold);
            figure.healRing = Circle("Heal",figure.root,0.76f,0.14f,0.09f,MintLight);
            figure.healRing.enabled = false;
            figure.bar = Child("Health",figure.root);
            MeshBar("Health frame",figure.bar,new Color32(35,47,45,255),1.22f,0.115f,0);
            figure.life = Child("Remaining health",figure.bar);
            figure.life.localPosition = new Vector3(-0.565f,0f,-0.016f);
            var health = MeshBar("Fill",figure.life,accent,1.13f,0.058f,0);
            health.localPosition = new Vector3(0.565f,0f,0f);
            return figure;
        }

        private Transform MeshBar(string name,Transform parent,Color color,float width,float height,float z)
        {
            var g = new DuelGeometry();
            g.Quad(new Vector3(-width/2,-height/2,z),new Vector3(-width/2,height/2,z),
                new Vector3(width/2,height/2,z),new Vector3(width/2,-height/2,z),Vector3.back,color);
            return Model(name,parent,g,ink);
        }

        private void Pose(Figure f,DuelUnitFrame unit,bool isOgre,bool local,float dt)
        {
            f.root.gameObject.SetActive(true);
            if (!f.placed || Vector2.Distance(f.visualPos,unit.position) >= 1.4f)
            {
                f.visualPos = unit.position;
                f.visualHeading = unit.heading;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-24f * Mathf.Clamp(dt,0f,0.1f));
                f.visualPos = Vector2.Lerp(f.visualPos,unit.position,blend);
                f.visualHeading = Mathf.LerpAngle(f.visualHeading,unit.heading,blend);
            }
            f.root.localPosition = Ground(f.visualPos);
            f.facing.localRotation = Quaternion.Euler(0,f.visualHeading,0);
            float travelled = f.placed ? Vector2.Distance(f.lastPosition,f.visualPos) : 0f;
            if (travelled < 1.5f) f.step += travelled * (isOgre ? 6f : 9f);
            f.lastPosition = f.visualPos;
            f.placed = true;
            float amplitude = unit.moving ? 1f : 0f;
            float step = Mathf.Sin(f.step) * amplitude;
            float bob = Mathf.Abs(step) * 0.047f;
            f.body.localPosition = Vector3.up * bob;
            f.feet.localRotation = Quaternion.Euler(0,step * 6f,0);
            float legX = isOgre ? 0.26f : 0.15f;
            f.leftFoot.localPosition = new Vector3(-legX,Mathf.Max(0,step)*0.11f,step*0.19f);
            f.rightFoot.localPosition = new Vector3(legX,Mathf.Max(0,-step)*0.11f,-step*0.19f);
            float p = Mathf.Clamp01(unit.progress);
            Quaternion rest = isOgre ? Quaternion.Euler(-22f,18f,2f) : Quaternion.Euler(-9f,0f,6f);
            if (unit.action != 0)
            {
                float yaw = Mathf.Atan2(unit.aim.x,unit.aim.y) * Mathf.Rad2Deg;
                f.facing.localRotation = Quaternion.Euler(0,yaw,0);
            }
            if (unit.action == 2)
                f.arm.localRotation = Quaternion.Euler(unit.recovering ? 18f : Mathf.Lerp(-12f,-124f,p),12f,0);
            else if (unit.action == 3)
                f.arm.localRotation = Quaternion.Euler(-9f,unit.recovering ? -77f : Mathf.Lerp(18f,92f,p),0);
            else if (unit.action == 1)
                f.arm.localRotation = Quaternion.Euler(unit.recovering ? 58f : Mathf.Lerp(-9f,-41f,p),0,8f);
            else f.arm.localRotation = Quaternion.Slerp(f.arm.localRotation,rest,Mathf.Clamp01(dt * 14f));
            if (f.oldHp >= 0f && unit.hp < f.oldHp) f.hitTime = 0.30f;
            if (f.oldHp >= 0f && unit.hp > f.oldHp + 0.01f) f.healTime = 0.55f;
            f.oldHp = unit.hp;
            f.hitTime = Mathf.Max(0f,f.hitTime-dt);
            f.impact.enabled = f.hitTime > 0f;
            f.impact.transform.localScale = Vector3.one*(1f + (0.30f - f.hitTime)*2.4f);
            f.healTime = Mathf.Max(0f,f.healTime-dt);
            f.healRing.enabled = f.healTime > 0f;
            if (f.healRing.enabled)
            {
                float healT = 1f - f.healTime / 0.55f;
                f.healRing.transform.localPosition = new Vector3(0f,Mathf.Lerp(0.14f,1.65f,healT),0f);
                float healScale = Mathf.Lerp(1f,1.25f,healT);
                f.healRing.transform.localScale = new Vector3(healScale,1f,healScale);
                Color hc = MintLight;
                hc.a = Mathf.Lerp(1f,0f,healT);
                f.healRing.startColor = f.healRing.endColor = hc;
            }
            f.selection.widthMultiplier = local ? 0.071f : 0.035f;
            Color team = isOgre ? OgreTint : LuisTint;
            f.selection.startColor = f.selection.endColor = local ? Color.Lerp(team,Color.white,0.55f) : team;
            f.stunRing.enabled = unit.stun > 0f;
            f.bar.localPosition = new Vector3(0,isOgre ? 2.04f : 1.94f,0);
            f.bar.rotation = Camera.transform.rotation;
            f.life.localScale = new Vector3(Mathf.Clamp01(unit.hp / Mathf.Max(1f,unit.maxHp)),1,1);
        }
    }
}
