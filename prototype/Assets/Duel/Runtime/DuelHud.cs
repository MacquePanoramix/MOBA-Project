using System;
using UnityEngine;

namespace Moba.Duel
{
    internal sealed class DuelHud : IDisposable
    {
        private static readonly Color Ink = new Color32(19, 33, 32, 245);
        private static readonly Color Panel = new Color32(29, 49, 47, 249);
        private static readonly Color Fine = new Color32(81, 108, 98, 255);
        private static readonly Color Paper = new Color32(239, 237, 211, 255);
        private static readonly Color Muted = new Color32(183, 199, 183, 255);
        private static readonly Color Gold = new Color32(246, 192, 105, 255);
        private static readonly Color Mint = new Color32(123, 213, 184, 255);
        private readonly DuelScene scene;
        private DuelHero pick = DuelHero.Ogre;
        private string joinAddress = "";
        private bool hostTab;
        private GUIStyle label, center, field;
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1080f, Screen.height / 630f), 0.35f, 1.6f);

        public DuelHud(DuelScene scene)
        {
            this.scene = scene;
        }

        public bool BlocksWorldInput(Vector2 screen)
        {
            float y = (Screen.height - screen.y) / Scale;
            float x = screen.x / Scale;
            float w = Screen.width / Scale, h = Screen.height / Scale;
            return (Mathf.Abs(x - w * 0.5f) < 340 && y < 66) ||
                   (Mathf.Abs(x - w * 0.5f) < 400 && y > h - 76);
        }

        public void Draw()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
                center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                field = new GUIStyle(GUI.skin.textField) { fontSize = 17, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(9, 8, 0, 0) };
            }
            float s = Scale, w = Screen.width / s, h = Screen.height / s;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            if (scene.Frame == null) DrawLobby(w,h);
            else
            {
                DrawMatch(w,h);
                if (scene.MenuOpen || scene.Frame.finished) DrawRoundPanel(w,h);
            }
            GUI.matrix = previous;
        }

        private static void Box(float x,float y,float w,float h,Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x,y,w,h), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void Text(float x,float y,float w,float h,string text,int size,Color color,bool bold = false,bool centered = false)
        {
            var style = centered ? center : label;
            style.normal.textColor = color;
            style.fontSize = size;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            GUI.Label(new Rect(x,y,w,h), text, style);
        }

        private bool Button(float x,float y,float w,float h,string caption,bool primary = false,bool selected = false)
        {
            Rect r = new Rect(x,y,w,h);
            bool hover = r.Contains(Event.current.mousePosition);
            Box(x,y,w,h,primary ? Gold : selected || hover ? Fine : Panel);
            if (!primary) Box(x,y+h-1,w,1,selected ? Gold : Fine);
            if (hover && primary) Box(x,y+h-2,w,2,Paper);
            Text(x,y,w,h,caption,15,primary ? Ink : selected ? Paper : Muted,true,true);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        private void DrawLobby(float w,float h)
        {
            bool waiting = scene.Session == DuelScene.Mode.Host || scene.Session == DuelScene.Mode.Guest;
            float width = 420, height = !waiting ? 486 : scene.Session == DuelScene.Mode.Host ? 290 : 250;
            float x = Mathf.Max(24,w * 0.065f), y = (h-height)*0.5f;
            Box(x,y,width,height,Ink);
            Box(x,y,4,height,Gold);
            x += 25; y += 16;
            Text(x,y,374,54,"Woodland Duel",36,Paper,true);
            Text(x,y+53,370,24,$"{DuelNames.Ogre}  /  {DuelNames.OrbKeeper}",17,Gold);
            if (waiting)
            {
                if (scene.Session == DuelScene.Mode.Host)
                {
                    Text(x,y+90,370,26,$"Listening on port 47777  ({scene.Status})",15,Paper);
                    Text(x,y+120,240,28,scene.HostAddress,20,Mint,true);
                    if (Button(x+251,y+120,117,28,"Copy local IP")) GUIUtility.systemCopyBuffer = scene.HostAddress;
                    Text(x,y+154,370,40,"Internet play: point playit.gg TCP tunnel to 127.0.0.1:47777\nand send your friend the playit address.",12,Muted);
                    if (Button(x,y+214,368,38,"Back to lobby")) scene.ReturnToLobby();
                }
                else
                {
                    Text(x,y+94,370,30,scene.Status,15,Paper);
                    if (Button(x,y+174,368,38,"Back to lobby")) scene.ReturnToLobby();
                }
                return;
            }
            Text(x,y+91,370,20,"Your hero in practice or as host",14,Muted);
            if (Button(x,y+116,177,37,DuelNames.Ogre,false,pick==DuelHero.Ogre)) pick=DuelHero.Ogre;
            if (Button(x+186,y+116,182,37,DuelNames.OrbKeeper,false,pick==DuelHero.Luis)) pick=DuelHero.Luis;
            Text(x,y+155,369,28,pick==DuelHero.Ogre ? "Aim club strikes and deny the orb." : "Collect orbs to heal and strike enemies nearby.",15,Paper);
            if (Button(x,y+194,368,43,"Practice against a bot",true)) scene.Practice(pick);
            Box(x,y+255,368,1,Fine);
            if (Button(x,y+270,177,33,"Host a friend",false,hostTab)) hostTab=true;
            if (Button(x+186,y+270,182,33,"Join a friend",false,!hostTab)) hostTab=false;
            if (hostTab)
            {
                Text(x,y+310,370,22,"Listens on port 47777 for LAN, Tailscale, or playit.gg.",13,Muted);
                if (Button(x,y+336,368,38,"Host on port 47777",true)) scene.Host(pick);
                Text(x,y+382,372,24,"Friend in another country? Use playit.gg (no port forward).",13,Muted);
            }
            else
            {
                Text(x,y+310,370,20,"Host address (playit.gg host:port or IP)",14,Muted);
                Rect addrRect = new Rect(x,y+336,206,36);
                GUI.SetNextControlName("duel-address");
                joinAddress = GUI.TextField(addrRect,joinAddress,96,field);
                if (Button(x+212,y+336,72,36,"Paste")) joinAddress = (GUIUtility.systemCopyBuffer ?? "").Trim();
                if (Button(x+290,y+336,78,36,"Join",true)) scene.Join(joinAddress);
                Text(x,y+382,372,24,"Supports host:port, Tailscale IPv4, or LAN IPv4.",13,Muted);
            }
            Text(x,y+408,370,44,scene.Status,14,Gold);
        }

        private void DrawMatch(float w,float h)
        {
            var f = scene.Frame;
            float x = (w-650)/2;
            Box(x,8,650,52,Ink);
            Side(x+12,12,DuelNames.Ogre, f.ogre,Gold,scene.Hero==DuelHero.Ogre);
            Side(x+406,12,DuelNames.OrbKeeper, f.luis,Mint,scene.Hero==DuelHero.Luis);
            Text(x+250,12,150,23,$"Round {scene.Round}",16,Paper,true,true);
            string modeLabel = scene.Session == DuelScene.Mode.Practice ? "SOLO PRACTICE" : scene.PingMs >= 0 ? $"FRIEND DUEL · {scene.PingMs} ms" : "FRIEND DUEL";
            Text(x+244,35,162,18,modeLabel,11,Muted,true,true);
            float bw = 770; x=(w-bw)*0.5f;
            float y = h-71;
            Box(x,y,bw,63,Ink);
            Box(x,y,4,63,scene.Hero==DuelHero.Ogre ? Gold : Mint);
            if(scene.Hero==DuelHero.Ogre)
            {
                if (Button(x+12,y+8,112,36,Ability("Q  Slam", f.ogre.qCooldown))) scene.Arm(DuelOrder.Slam);
                if (Button(x+131,y+8,119,36,Ability("W  Sweep", f.ogre.wCooldown))) scene.Arm(DuelOrder.Sweep);
                Text(x+13,y+44,253,17,"Aim with cursor   ·   RMB to move",12,Muted);
            }
            else
            {
                Text(x+14,y+6,246,24,"RMB   Move / strike Ogre",16,Mint,true);
                Text(x+14,y+31,248,22,"Walk onto the orb to trigger its pulse.",13,Muted);
            }
            Text(x+268,y+4,491,27,scene.Armed ? "Left-click the target area. Esc cancels." : f.notice,15,Paper,true);
            string orb = f.orbActive ? $"Orb expires in {Mathf.CeilToInt(f.orbTimer)}s" : $"Next orb in {Mathf.CeilToInt(f.orbTimer)}s";
            Text(x+268,y+31,500,24,$"{orb}     ·     {f.orbPickups} collected  /  {f.orbDenials} denied     ·     Esc menu",13,Muted);
        }

        private static string Ability(string name,float cd) => cd > 0.05f ? $"{name}  {cd:0.0}" : name;

        private void Side(float x,float y,string name,DuelUnitFrame unit,Color color,bool controlled)
        {
            Text(x,y,150,23,controlled ? name+"  ·  You" : name,17,color,true);
            Text(x+145,y,85,23,$"{Mathf.CeilToInt(unit.hp)} / {Mathf.CeilToInt(unit.maxHp)}",13,Muted);
            Box(x,y+29,232,6,Fine);
            Box(x,y+29,232*Mathf.Clamp01(unit.hp/Mathf.Max(1,unit.maxHp)),6,color);
        }

        private void DrawRoundPanel(float w,float h)
        {
            bool done = scene.Frame.finished, solo = scene.Session == DuelScene.Mode.Practice;
            float x=(w-380)*.5f,y=(h-210)*.5f;
            Box(x,y,380,210,Ink);
            Box(x,y,380,3,Gold);
            string title = done ? DuelNames.For((DuelHero)scene.Frame.winner)+" wins" : solo ? "Practice paused" : "Duel menu";
            Text(x+20,y+12,342,48,title,29,Paper,true,true);
            if (done)
            {
                string sub = scene.IsReady ? "Waiting for the other player" : solo ? "Play again or try the other hero." : "Both players ready up to swap heroes.";
                Text(x+12,y+57,356,26,sub,15,Muted,false,true);
            }
            else if (Button(x+20,y+57,340,32,"Resume")) scene.Resume();
            if (solo)
            {
                if (Button(x+20,y+98,166,37,"Rematch",true)) scene.Rematch(false);
                if (Button(x+194,y+98,166,37,"Swap heroes")) scene.Rematch(true);
            }
            else if (done && Button(x+20,y+98,340,37,scene.IsReady ? "Ready" : "Ready for rematch",!scene.IsReady))
                scene.Rematch(true);
            if (Button(x+20,y+150,340,34,"Leave to lobby")) scene.ReturnToLobby();
        }

        public void Dispose() { }
    }
}
