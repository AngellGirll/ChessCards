// An isolated adapter for layout/input smoke tests, NOT Unity assemblies.
// It is outside Assets and never participates in the actual Unity project.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Security;
using ChessCards;

namespace UnityEngine
{
    public class MonoBehaviour {}
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Quaternion { public static Quaternion identity=new Quaternion(); }
    public struct Matrix4x4 { public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s){return new Matrix4x4();} }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white=new Color(1,1,1),black=new Color(0,0,0);
        public static Color Lerp(Color a,Color b,float t){return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
        public string Css(){return "rgb("+(int)(r*255)+","+(int)(g*255)+","+(int)(b*255)+")";}
    }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
    }
    public enum TextAnchor {MiddleLeft,MiddleCenter,UpperLeft}
    public enum FontStyle {Normal,Bold}
    public enum ScaleMode {StretchToFill,ScaleToFit}
    public class Font {}
    public class Texture2D { public string Path; public static Texture2D whiteTexture = new Texture2D(); }
    public class GUIContent
    {
        public string text,tooltip;
        public GUIContent(string t){text=t;}
        public GUIContent(string t,string tip){text=t;tooltip=tip;}
        public static GUIContent none=new GUIContent("");
    }
    public class GUIStyleState { public Color textColor=Color.white; }
    public class GUIStyle
    {
        public Font font; public int fontSize; public bool wordWrap; public TextAnchor alignment; public FontStyle fontStyle;
        public GUIStyleState normal=new GUIStyleState();
        public static GUIStyle none=new GUIStyle();
        public GUIStyle(){}
        public GUIStyle(GUIStyle a){font=a.font;fontSize=a.fontSize;wordWrap=a.wordWrap;alignment=a.alignment;normal.textColor=a.normal.textColor;}
        public float CalcHeight(GUIContent c,float width){return GUI.Wrap(c.text,width,fontSize).Count*fontSize*1.3f;}
    }
    public class GUISkin {public GUIStyle label=new GUIStyle(),button=new GUIStyle();}
    public static class Mathf {public static float Min(float a,float b){return Math.Min(a,b);} }
    public static class Screen {public static int width=1280,height=720;public static Rect safeArea{get{return new Rect(0,0,width,height);}}}
    public static class Application {public static int targetFrameRate;}
    public static class Time {public static float unscaledTime;}
    public static class PlayerPrefs
    {
        static Dictionary<string,string> data=new Dictionary<string,string>();
        public static string GetString(string k,string fallback){return data.ContainsKey(k)?data[k]:fallback;}
        public static void SetString(string k,string v){data[k]=v;}
        public static void Save(){}
    }
    public static class JsonUtility
    {
        static JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true};
        public static T FromJson<T>(string s){return JsonSerializer.Deserialize<T>(s,options);}
        public static string ToJson(object o){return JsonSerializer.Serialize(o,options);}
    }
    public static class Resources
    {
        public static T Load<T>(string name) where T:class
        {
            if(typeof(T)==typeof(Font))return new Font() as T;
            string p=System.IO.Path.GetFullPath("Assets/ChessCards/Resources/"+name+".png");
            return File.Exists(p)?new Texture2D{Path=p} as T:null;
        }
    }
    public class ExitGuiException:Exception{}
    public static class GUIUtility {public static void ExitGUI(){throw new ExitGuiException();}}
    public static class GUI
    {
        public static Color color=Color.white,backgroundColor=Color.white,contentColor=Color.white;
        public static bool enabled=true;
        public static Matrix4x4 matrix;
        public static GUISkin skin=new GUISkin();
        static StringBuilder svg;
        static float ox,oy,cx,cy; static bool click;
        static Stack<Vector2> origins=new Stack<Vector2>();
        static int clip;
        public static void Begin(){ox=oy=0;origins.Clear();clip=0;svg=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='"+Screen.width+"' height='"+Screen.height+"'>");}
        public static void Click(float x,float y){cx=x;cy=y;click=true;}
        public static void Save(string path){File.WriteAllText(path,svg.ToString()+"</svg>");click=false;}
        public static bool Button(Rect r,GUIContent content,GUIStyle style)
        {
            bool hit=enabled&&click&&cx>=r.x+ox&&cy>=r.y+oy&&cx<=r.x+ox+r.width&&cy<=r.y+oy+r.height;
            if(hit)click=false;return hit;
        }
        public static void DrawTexture(Rect r,Texture2D texture,ScaleMode mode=ScaleMode.StretchToFill)
        {
            if(texture.Path==null) svg.Append("<rect x='"+(r.x+ox)+"' y='"+(r.y+oy)+"' width='"+r.width+"' height='"+r.height+"' fill='"+color.Css()+"' opacity='"+color.a+"'/>");
            else
            {
                string image="<image x='"+(r.x+ox)+"' y='"+(r.y+oy)+"' width='"+r.width+"' height='"+r.height+"' href='data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(texture.Path))+"'/>";
                if(System.IO.Path.GetFileName(texture.Path)=="Body.png")
                {
                    string id="mask"+(clip++);
                    svg.Append("<defs><mask id='"+id+"'>"+image+"</mask></defs><rect x='"+(r.x+ox)+"' y='"+(r.y+oy)+"' width='"+r.width+"' height='"+r.height+"' fill='"+color.Css()+"' opacity='"+color.a+"' mask='url(#"+id+")'/>");
                }
                else svg.Append("<g opacity='"+color.a+"'>"+image+"</g>");
            }
        }
        public static List<string> Wrap(string t,float width,int size)
        {
            var result=new List<string>();int max=Math.Max(1,(int)(width/(Math.Max(size,1)*.56f)));
            foreach(string para in t.Split('\n'))
            {
                string line="";
                foreach(string word in para.Split(' '))
                {if((line+word).Length>max&&line.Length>0){result.Add(line.TrimEnd());line="";}line+=word+" ";}
                result.Add(line.TrimEnd());
            }
            return result;
        }
        public static void Label(Rect r,string t,GUIStyle style)
        {
            var lines=Wrap(t,r.width,style.fontSize);float lh=style.fontSize*1.25f;
            float y=r.y+oy+(style.alignment==TextAnchor.UpperLeft?0:(r.height-lines.Count*lh)/2)+style.fontSize;
            foreach(string line in lines)
            {
                bool center=style.alignment==TextAnchor.MiddleCenter;
                svg.Append("<text x='"+(r.x+ox+(center?r.width/2:0))+"' y='"+y+"' text-anchor='"+(center?"middle":"start")+"' font-family='DejaVu Sans' font-size='"+style.fontSize+"' font-weight='"+(style.fontStyle==FontStyle.Bold?"bold":"normal")+"' fill='"+style.normal.textColor.Css()+"'>"+SecurityElement.Escape(line)+"</text>");y+=lh;
            }
        }
        public static void BeginGroup(Rect r)
        {
            origins.Push(new Vector2(ox,oy));ox+=r.x;oy+=r.y;
            string id="clip"+(clip++);
            svg.Append("<defs><clipPath id='"+id+"'><rect x='"+ox+"' y='"+oy+"' width='"+r.width+"' height='"+r.height+"'/></clipPath></defs><g clip-path='url(#"+id+")'>");
        }
        public static void EndGroup(){svg.Append("</g>");var p=origins.Pop();ox=p.x;oy=p.y;}
        public static Vector2 BeginScrollView(Rect r,Vector2 p,Rect content){BeginGroup(r);ox-=p.x;oy-=p.y;return p;}
        public static void EndScrollView(){EndGroup();}
    }
}

static class UiSmoke
{
    static object Get(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Set(object o,string name,object v){o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);}
    static void Call(object o,string name)
    {
        try{o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);}
        catch(TargetInvocationException e) {if(!(e.InnerException is UnityEngine.ExitGuiException))throw;}
    }
    static void Draw(ChessCardsApp app,string path)
    {UnityEngine.GUI.Begin();Call(app,"OnGUI");UnityEngine.GUI.Save(path);}
    static void Check(bool condition,string msg){if(!condition)throw new Exception(msg);}
    static void BotTimingRegression()
    {
        var app=new ChessCardsApp();Call(app,"Awake");
        var match=new CardMatch(7);
        Set(app,"match",match);Set(app,"bot",new ChessBot(7));Set(app,"location",false);Set(app,"nextBot",0f);
        UnityEngine.Time.unscaledTime=100f;Call(app,"Update"); // Human has thought for a long time.
        match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));
        Check(match.PlayCard(0)&&match.Move(new ChessMove(12,28)),"Could not prepare human turn");
        match.Hands[1].Clear();match.Hands[1].Add(new Card(match.Top.Color,CardKind.Swap));
        Call(app,"Update");
        Check(match.HumanSide==Side.White && match.Hands[1].Count==1 && match.Phase==MatchPhase.ChooseCard,
            "Bot acted immediately after a long human turn instead of waiting");
        UnityEngine.Time.unscaledTime=100.84f;Call(app,"Update");
        Check((int)Get(app,"pendingBotCard")==-1,"Bot preview ignored turn delay");
        UnityEngine.Time.unscaledTime=100.86f;Call(app,"Update");
        Check((int)Get(app,"pendingBotCard")==0 && match.HumanSide==Side.White && match.Hands[1].Count==1,
            "Swap changed the board before its card announcement");
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        Draw(app,"Validation/Previews/card-announcement-portrait.svg");
        UnityEngine.Time.unscaledTime=101.5f;Call(app,"Update");
        Check(match.HumanSide==Side.White,"Swap preview was too short");
        Set(app,"historyOpen",true);UnityEngine.Time.unscaledTime=200f;Call(app,"Update");
        Check(match.HumanSide==Side.White,"History failed to pause bot");
        Set(app,"historyOpen",false);UnityEngine.Time.unscaledTime=200.01f;Call(app,"Update");
        Check(match.HumanSide==Side.White,"Bot rushed when history closed");
        UnityEngine.Time.unscaledTime=200.86f;Call(app,"Update");
        Check(match.HumanSide==Side.Black && match.Hands[1].Count==0 && match.MovesPlayed==1,
            "Announced swap did not apply once, separately from a chess move");
        UnityEngine.Time.unscaledTime=201f;Call(app,"Update");
        Check(match.MovesPlayed==1,"Bot chess move ignored effect delay");
        UnityEngine.Time.unscaledTime=201.67f;Call(app,"Update");
        Check(match.MovesPlayed==2,"Bot did not resume after announced effect");
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        UnityEngine.GUI.Click(470,48);Draw(app,"Validation/Previews/transition.svg");
        Check((bool)Get(app,"historyOpen"),"History button did not open");
        Draw(app,"Validation/Previews/history-portrait.svg");
        UnityEngine.GUI.Click(350,1200);Draw(app,"Validation/Previews/transition.svg");
        Check(!(bool)Get(app,"historyOpen"),"History button did not close");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        UnityEngine.Time.unscaledTime=0f;
        Console.WriteLine("PASS regression: delayed first bot action, announcement before swap, pause/resume, one action per interval, history UI");
    }
    static void Main()
    {
        Directory.CreateDirectory("Validation/Previews");
        BotTimingRegression();
        var app=new ChessCardsApp();Call(app,"Awake");
        Check(Get(app,"cardBody")!=null && Get(app,"cardBack")!=null,"Card body/back resource missing");
        foreach(var face in (UnityEngine.Texture2D[])Get(app,"cardFaces")) Check(face!=null,"Card face resource missing");
        Draw(app,"Validation/Previews/location-landscape.svg");
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        Draw(app,"Validation/Previews/location-portrait.svg");
        UnityEngine.GUI.Click(150,1160);Draw(app,"Validation/Previews/transition.svg");
        Check(!(bool)Get(app,"location"),"Play click failed");
        var match=(CardMatch)Get(app,"match");
        var playable=match.PlayableCards();
        while(playable.Count==0)
        {
            UnityEngine.GUI.Click(150,1220);Draw(app,"Validation/Previews/transition.svg");
            for(int i=0;i<20&&match.Actor==1;i++){UnityEngine.Time.unscaledTime+=1;Call(app,"Update");}
            playable=match.PlayableCards();
        }
        int card=playable[0];Set(app,"page",card/4);
        UnityEngine.GUI.Click(32+(card%4)*167+70,1010);Draw(app,"Validation/Previews/transition.svg");
        Check(match.Phase==MatchPhase.MovePieces,"Card click failed");
        var move=match.Board.LegalMoves(match.HumanSide)[0];
        bool white=match.HumanSide==Side.White;
        int fromFile=white?move.From%8:7-move.From%8,fromRank=white?7-move.From/8:move.From/8;
        int toFile=white?move.To%8:7-move.To%8,toRank=white?7-move.To/8:move.To/8;
        UnityEngine.GUI.Click(80+fromFile*70+35,210+fromRank*70+35);Draw(app,"Validation/Previews/transition.svg");
        Check((int)Get(app,"selected")==move.From,"Piece click failed");
        Draw(app,"Validation/Previews/board-portrait.svg");
        UnityEngine.GUI.Click(80+toFile*70+35,210+toRank*70+35);Draw(app,"Validation/Previews/transition.svg");
        Check(match.MovesPlayed==1,"Destination click failed");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        Draw(app,"Validation/Previews/board-landscape.svg");
        Set(app,"rules",true);Draw(app,"Validation/Previews/rules-landscape.svg");Set(app,"rules",false);
        // End-to-end: checkmate -> one-time award -> leave board -> paid repair -> reload progress.
        var p=new ChessPosition{CastleRights=0};p.Squares[45]=new Piece(PieceType.King,Side.White);p.Squares[46]=new Piece(PieceType.Queen,Side.White);p.Squares[63]=new Piece(PieceType.King,Side.Black);
        match=new CardMatch(1,p);match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));match.PlayCard(0);match.Move(new ChessMove(46,54));
        Set(app,"match",match);Set(app,"credited",false);Call(app,"CreditResult");Call(app,"CreditResult");
        var progress=(RestorationProgress)Get(app,"progress");Check(progress.Coins==100,"Reward duplicated");
        Set(app,"location",true);UnityEngine.GUI.Click(1100,240);Draw(app,"Validation/Previews/transition.svg");
        Check(progress.Bench&&progress.Coins==40,"Repair click failed");
        Draw(app,"Validation/Previews/repaired-landscape.svg");
        var reloaded=new ChessCardsApp();Call(reloaded,"Awake");var saved=(RestorationProgress)Get(reloaded,"progress");
        Check(saved.Coins==40&&saved.Bench&&saved.Wins==1,"Progress save/reload failed");
        Console.WriteLine("PASS runtime source compilation against isolated adapter; GUI clicks, card selection, board input, mate reward, repair and save/reload");
        Console.WriteLine("Layout previews emitted. This does not replace testing in Unity Editor or a device build.");
    }
}
