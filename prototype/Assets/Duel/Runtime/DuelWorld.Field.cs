using UnityEngine;

namespace Moba.Duel
{
    public sealed partial class DuelWorld
    {
        private static readonly Color Pine = new Color32(42, 76, 69, 255);
        private static readonly Color Needle = new Color32(72, 113, 85, 255);
        private static readonly Color Moss = new Color32(111, 142, 96, 255);
        private static readonly Color Rock = new Color32(103, 121, 111, 255);
        private static readonly Color DarkRock = new Color32(65, 82, 78, 255);
        private static readonly Color Timber = new Color32(112, 79, 61, 255);
        private static readonly Color Bronze = new Color32(183, 132, 75, 255);
        private static readonly Color Gold = new Color32(246, 206, 118, 255);
        private static readonly Color OgreTint = new Color32(229, 119, 85, 255);
        private static readonly Color LuisTint = new Color32(73, 207, 186, 255);
        private static readonly Color MintLight = new Color32(198, 255, 216, 255);
        private static readonly Color Shadow = new Color32(71, 89, 76, 255);

        private void BuildField()
        {
            var field = new DuelGeometry();
            float width = DuelArena.HalfWidth;
            float depth = DuelArena.HalfDepth;
            var mossEarth = new Color32(125, 145, 103, 255);
            var meadow = new Color32(117, 139, 98, 255);
            var earth = new Color32(149, 135, 105, 255);
            var path = new Color32(179, 162, 126, 255);
            var verge = new Color32(142, 147, 106, 255);
            var pathLight = new Color32(186, 169, 132, 255);
            field.Quad(new Vector3(-60,-0.8f,-40), new Vector3(-60,-0.8f,40),
                new Vector3(60,-0.8f,40), new Vector3(60,-0.8f,-40), Vector3.up, DarkRock);
            field.Quad(new Vector3(-width-1,0,-depth-1), new Vector3(-width-1,0,depth+1),
                new Vector3(width+1,0,depth+1), new Vector3(width+1,0,-depth-1), Vector3.up, mossEarth);

            // Two playable approaches meet at the old circular paving in the clearing.
            const int sections = 28;
            for (int i = 0; i < sections; i++)
            {
                float x0 = Mathf.Lerp(-width - 0.7f, width + 0.7f, i / (float)sections);
                float x1 = Mathf.Lerp(-width - 0.7f, width + 0.7f, (i + 1f) / sections);
                float bank0 = 2.0f + 0.42f * Mathf.Cos(x0 * 0.72f);
                float bank1 = 2.0f + 0.42f * Mathf.Cos(x1 * 0.72f);
                RoadBand(field, x0, x1, bank0 + 0.62f, bank1 + 0.62f, 0.006f, verge);
                RoadBand(field, x0, x1, bank0 + 0.15f, bank1 + 0.15f, 0.012f, earth);
                RoadBand(field, x0, x1, bank0, bank1, 0.018f, path);
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (depth + 0.48f);
                    float radius = 0.65f + 0.25f * Mathf.Cos(x0 * 7f);
                    field.Oval(new Vector3(x0,-0.18f,z), new Vector3(0.82f,0.47f,radius), 6, Rock);
                    if (i % 3 == 0)
                    {
                        field.Oval(new Vector3(x0+0.25f,0.12f,z-0.12f), new Vector3(0.55f,0.16f,0.4f), 7, meadow);
                    }
                }
            }
            // Broken pairs of inset flags suggest an old road without laying a uniform tile grid.
            for (int sign = -1; sign <= 1; sign += 2)
                for (int i = 0; i < 12; i++)
                {
                    float x = sign * (1.1f + i * 0.76f);
                    float z = 0.72f * Mathf.Sin(i * 2.1f) + (i % 2 == 0 ? 0.36f : -0.36f);
                    field.Column(new Vector3(x,0.025f,z),0.014f,new Vector2(0.27f,0.12f),
                        new Vector2(0.31f,0.14f),5,pathLight,i * 0.92f);
                }

            var centerStone = new Color32(154, 146, 116, 255);
            RingInlay(field,Vector3.up * 0.045f,1.08f,1.17f,16,centerStone);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 axis = new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-axis.z,0f,axis.x);
                Vector3 hub = axis * 0.88f + Vector3.up * 0.049f;
                field.Face(hub - tangent * 0.11f,hub + tangent * 0.11f,hub - axis * 0.36f,Vector3.up,centerStone);
            }

            int treeIndex = 0;
            foreach (Vector2 center in DuelArena.TreeCenters)
            {
                var p = new Vector3(center.x,0.04f,center.y);
                float r = DuelArena.TreeRadius;
                field.Column(p,0.045f,new Vector2(r,r),new Vector2(r*0.93f,r*0.93f),11,Shadow);
                PineTree(field,p,1.48f,r * 0.97f,treeIndex++ * 0.84f);
            }
            // The dense treeline lives outside the traversable ground. Interior trees follow the nav geometry.
            var plants = new System.Random(92);
            for (int i = 0; i < 53; i++)
            {
                float x = -width - 1.2f + (float)plants.NextDouble() * (width + 1.2f) * 2f;
                float z = depth + 1.1f + (float)plants.NextDouble() * 2.2f;
                float scale = 0.6f + (float)plants.NextDouble() * 0.84f;
                float frontDepth = -z - 0.85f;
                PineTree(field,new Vector3(x,-0.69f,z),scale * 2.5f,scale * 0.81f,i);
                if (i % 2 == 0) PineTree(field,new Vector3(-x,-0.69f,frontDepth),scale * 1.4f,scale * 0.62f,i);
            }

            for (int sign = -1; sign <= 1; sign += 2)
            {
                Color accent = sign < 0 ? OgreTint : LuisTint;
                Vector2 spawn = sign < 0 ? DuelArena.OgreSpawn : DuelArena.LuisSpawn;
                var pad = new Vector3(spawn.x,0.047f,spawn.y);
                RingInlay(field,pad,0.95f,1.03f,16,centerStone);
                RingInlay(field,pad,1.08f,1.105f,16,accent);
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = new Vector3(sign*(width+0.77f),0f,side*2.03f);
                    field.Column(p,0.27f,new Vector2(0.55f,0.55f),new Vector2(0.47f,0.47f),6,DarkRock);
                    p.y += 0.27f;
                    field.Column(p,1.10f,new Vector2(0.36f,0.36f),new Vector2(0.26f,0.26f),6,Rock);
                    p.y += 1.10f;
                    field.Column(p,0.13f,new Vector2(0.40f,0.40f),new Vector2(0.40f,0.40f),6,Bronze);
                    p.y += 0.13f;
                    field.Column(p,0.56f,new Vector2(0.23f,0.23f),Vector2.zero,4,accent,0.75f);
                }
            }
            Model("Waystone lane", root, field);
        }

        private static void RoadBand(DuelGeometry field, float x0, float x1, float radius0, float radius1, float y, Color c)
        {
            field.Quad(new Vector3(x0,y,-radius0),new Vector3(x0,y,radius0),new Vector3(x1,y,radius1),
                new Vector3(x1,y,-radius1),Vector3.up,c);
        }

        private static void PineTree(DuelGeometry field, Vector3 ground, float h, float spread, float rotation)
        {
            field.Column(ground,h * 0.65f,Vector2.one * (spread * 0.27f),Vector2.one * (spread * 0.18f),6,Timber,rotation);
            for (int tier = 0; tier < 3; tier++)
            {
                var basePoint = ground + Vector3.up * (h * (0.22f + tier * 0.23f));
                float radius = spread * (1f - tier * 0.24f);
                field.Column(basePoint,h * (0.50f - tier * 0.07f),new Vector2(radius,radius*0.94f),
                    Vector2.zero,7,tier == 0 ? Pine : tier == 1 ? Needle : Moss,rotation + tier * 0.33f);
            }
        }

        private static void RingInlay(DuelGeometry field,Vector3 p,float inner,float outer,int sides,Color color)
        {
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ArcPoint(360f * i / sides);
                Vector3 b = ArcPoint(360f * (i+1) / sides);
                field.Quad(p+a*inner,p+a*outer,p+b*outer,p+b*inner,Vector3.up,color);
            }
        }
    }
}
