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
    public class MonoBehaviour
    {
        public GameObject gameObject = new GameObject();
        protected static T FindAnyObjectByType<T>() where T : class { return null; }
    }
    public class GameObject { public T AddComponent<T>() where T : new() { return new T(); } }
    public class AudioListener {}
    public class AudioClip {}
    public class AudioSource
    {
        public AudioClip clip;
        public float volume;
        public bool loop, playOnAwake, isPlaying;
        public int oneShots;
        public void Play() { isPlaying = true; }
        public void Stop() { isPlaying = false; }
        public void PlayOneShot(AudioClip sound) { oneShots++; }
    }
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
        public bool Contains(Vector2 point){return point.x>=x && point.x<x+width && point.y>=y && point.y<y+height;}
    }
    public enum EventType { Repaint }
    public class Event
    {
        public static Event current = new Event();
        public EventType type = EventType.Repaint;
        public Vector2 mousePosition = new Vector2(-1,-1);
    }
    public enum TextAnchor {MiddleLeft,MiddleCenter,UpperLeft}
    public enum FontStyle {Normal,Bold}
    public enum ScaleMode {StretchToFill,ScaleToFit,ScaleAndCrop}
    public class Font {}
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public class Texture2D
    {
        public string Path;
        public int width = 1, height = 1;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Color[] Colors;
        public Texture2D() {}
        public Texture2D(int w, int h, TextureFormat format, bool mipmaps) { width = w; height = h; }
        public void SetPixels(Color[] colors) { Colors = colors; }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) {}
        public static Texture2D whiteTexture = new Texture2D();
    }
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
        public void Draw(Rect r, GUIContent content, bool hover, bool active, bool on, bool focus)
        {
            if (hover || active || on || focus) throw new Exception("Static text must use its normal style");
            GUI.DrawLabel(r, content.text, this);
        }
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
        public static void FromJsonOverwrite(string json, object target)
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (doc.RootElement.TryGetProperty(field.Name, out var value))
                    field.SetValue(target, JsonSerializer.Deserialize(value.GetRawText(), field.FieldType, options));
        }
        public static string ToJson(object o){return JsonSerializer.Serialize(o,options);}
    }
    public static class Resources
    {
        public static T Load<T>(string name) where T:class
        {
            if(typeof(T)==typeof(Font))return new Font() as T;
            string basePath=System.IO.Path.GetFullPath("Assets/ChessCards/Resources/"+name);
            if(typeof(T)==typeof(AudioClip))return File.Exists(basePath+".ogg")?new AudioClip() as T:null;
            string jpg=basePath+".jpg";
            if(File.Exists(jpg))return new Texture2D{Path=jpg,width=1360,height=765} as T;
            string png=basePath+".png";
            return File.Exists(png)?new Texture2D{Path=png} as T:null;
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
        public static void Hover(float x,float y){Event.current.mousePosition=new Vector2(x,y);}
        public static void Click(float x,float y){Hover(x,y);cx=x;cy=y;click=true;}
        public static void Save(string path){File.WriteAllText(path,svg.ToString()+"</svg>");click=false;}
        public static bool Button(Rect r,GUIContent content,GUIStyle style)
        {
            bool hit=enabled&&click&&cx>=r.x+ox&&cy>=r.y+oy&&cx<=r.x+ox+r.width&&cy<=r.y+oy+r.height;
            if(hit)click=false;return hit;
        }
        public static void DrawTexture(Rect r,Texture2D texture,ScaleMode mode,bool alphaBlend,float aspect,Color tint,float border,float radius)
        {
            string fill = tint.Css();
            if (texture.Colors != null)
            {
                string id = "gradient" + (clip++);
                svg.Append("<defs><linearGradient id='" + id + "' x2='0%' y2='100%'>");
                int stops = texture.width == 1 ? 4 : 1;
                for (int i = 0; i <= stops; i++)
                {
                    int sample = (texture.height - 1) * (stops - i) / stops * texture.width;
                    Color shade = texture.Colors[sample];
                    svg.Append("<stop offset='" + i * 100 / stops + "%' stop-color='" + shade.Css() +
                        "' stop-opacity='" + shade.a + "'/>");
                }
                svg.Append("</linearGradient></defs>");
                fill = "url(#" + id + ")";
            }
            svg.Append("<rect x='" + (r.x+ox) + "' y='" + (r.y+oy) + "' width='" + r.width + "' height='" +
                r.height + "' rx='" + radius + "' fill='" + fill + "' opacity='" + tint.a + "'/>");
        }
        public static void DrawTextureWithTexCoords(Rect r, Texture2D texture, Rect uv)
        {
            BeginGroup(r);
            bool flipX = uv.width < 0, flipY = uv.height < 0;
            if (flipX || flipY)
            {
                float tx = flipX ? 2 * ox + r.width : 0;
                float ty = flipY ? 2 * oy + r.height : 0;
                svg.Append("<g transform='translate(" + tx + " " + ty + ") scale(" +
                    (flipX ? -1 : 1) + " " + (flipY ? -1 : 1) + ")'>");
            }
            float ux = flipX ? uv.x + uv.width : uv.x, uy = flipY ? uv.y + uv.height : uv.y;
            float width = r.width / Math.Abs(uv.width), height = r.height / Math.Abs(uv.height);
            DrawTexture(new Rect(-ux * width, -(1 - uy - Math.Abs(uv.height)) * height, width, height), texture);
            if (flipX || flipY) svg.Append("</g>");
            EndGroup();
        }
        static readonly Dictionary<string,string> textureData = new Dictionary<string,string>();
        public static void DrawTexture(Rect r,Texture2D texture,ScaleMode mode=ScaleMode.StretchToFill)
        {
            if(texture.Path==null) svg.Append("<rect x='"+(r.x+ox)+"' y='"+(r.y+oy)+"' width='"+r.width+"' height='"+r.height+"' fill='"+color.Css()+"' opacity='"+color.a+"'/>");
            else
            {
                if (!textureData.TryGetValue(texture.Path,out string data))
                {
                    string mime = texture.Path.EndsWith(".jpg") ? "jpeg" : "png";
                    data = "data:image/" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(texture.Path));
                    textureData.Add(texture.Path,data);
                }
                string fit = mode == ScaleMode.ScaleAndCrop ? "xMidYMid slice" : mode == ScaleMode.ScaleToFit ? "xMidYMid meet" : "none";
                string image="<image x='"+(r.x+ox)+"' y='"+(r.y+oy)+"' width='"+r.width+"' height='"+r.height+"' preserveAspectRatio='"+fit+"' href='"+data+"'/>";
                if(System.IO.Path.GetFileName(texture.Path)=="Body.png")
                {
                    string id="tint"+(clip++);
                    string matrix=FormattableString.Invariant($"{color.r} 0 0 0 0  0 {color.g} 0 0 0  0 0 {color.b} 0 0  0 0 0 1 0");
                    svg.Append("<defs><filter id='"+id+"' color-interpolation-filters='sRGB'><feColorMatrix values='"+matrix+
                        "'/></filter></defs><g opacity='"+color.a+"' filter='url(#"+id+")'>"+image+"</g>");
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
        public static void DrawLabel(Rect r,string t,GUIStyle style)
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
    static void CheckersModeSmoke()
    {
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        var app=new ChessCardsApp();Call(app,"Awake");
        UnityEngine.GUI.Click(150,1160);Draw(app,"Validation/Previews/transition.svg");
        Check((bool)Get(app,"modeSelection"),"Checkers selection did not open");
        UnityEngine.GUI.Click(300,880);Draw(app,"Validation/Previews/transition.svg");
        Check(!(bool)Get(app,"modeSelection") && (bool)Get(app,"location"),"Mode cancel should stay in courtyard");
        UnityEngine.GUI.Click(150,1160);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(500,520);Draw(app,"Validation/Previews/transition.svg");
        var match=(CardMatch)Get(app,"match");
        Check(match.Mode==GameMode.Checkers && !(bool)Get(app,"location"),"Checkers choice did not start checkers");
        var pieces=(UnityEngine.Texture2D[,])Get(app,"pieces");
        Check(pieces[0,7]!=null && pieces[1,7]!=null && pieces[0,8]!=null && pieces[1,8]!=null,"Checker resources missing");
        match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));
        UnityEngine.GUI.Click(100,1010);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(255,595);Draw(app,"Validation/Previews/transition.svg");
        Check((int)Get(app,"selected")==18,"Checker selection click failed");
        UnityEngine.GUI.Click(325,525);Draw(app,"Validation/Previews/transition.svg");
        Check(match.MovesPlayed==1 && match.Board.Squares[27].Type==PieceType.Checker,"Checker destination click failed");
        Draw(app,"Validation/Previews/checkers-portrait.svg");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        Draw(app,"Validation/Previews/checkers-landscape.svg");
        Set(app,"location",true);Set(app,"modeSelection",true);Draw(app,"Validation/Previews/mode-selection-landscape.svg");
        Set(app,"modeSelection",false);Set(app,"location",false);
        var board=new ChessPosition(GameMode.Checkers){CastleRights=0};
        board.Squares[18]=new Piece(PieceType.Checker,Side.White);
        board.Squares[27]=new Piece(PieceType.Checker,Side.Black);
        board.Squares[45]=new Piece(PieceType.Checker,Side.Black);
        board.Squares[63]=new Piece(PieceType.Checker,Side.Black);
        match=new CardMatch(3,board);Set(app,"match",match);
        match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        UnityEngine.GUI.Click(100,1010);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(255,595);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(395,455);Draw(app,"Validation/Previews/transition.svg");
        Check((int)Get(app,"selected")==36 && match.Board.ForcedCaptureFrom==36 && match.Remaining==1,"Capture continuation not auto-selected");
        Draw(app,"Validation/Previews/checkers-capture-portrait.svg");
        UnityEngine.GUI.Click(535,315);Draw(app,"Validation/Previews/transition.svg");
        Check(match.MovesPlayed==1 && match.Actor==1 && match.Board.PendingCaptured==0,"UI chain did not complete as one move");
        Set(app,"rules",true);Draw(app,"Validation/Previews/checkers-rules-portrait.svg");Set(app,"rules",false);
        var won=new ChessPosition(GameMode.Checkers){CastleRights=0};won.Squares[18]=new Piece(PieceType.Checker,Side.White);
        match=new CardMatch(3,won);match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));match.PlayCard(0);match.Move(new ChessMove(18,27));
        Check(match.Phase==MatchPhase.Finished,"Could not prepare checkers replay");
        Set(app,"match",match);Set(app,"credited",true);
        UnityEngine.GUI.Click(550,1220);Draw(app,"Validation/Previews/transition.svg");
        Check(((CardMatch)Get(app,"match")).Mode==GameMode.Checkers,"Replay lost selected mode");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        Console.WriteLine("PASS game mode selector, cancel, checkers input, capture continuation, rules and same-mode replay");
    }
    static void GoDominoModeSmoke()
    {
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        var app=new ChessCardsApp();Call(app,"Awake");
        UnityEngine.GUI.Click(150,1160);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(300,700);Draw(app,"Validation/Previews/transition.svg");
        var match=(CardMatch)Get(app,"match");
        Check(match.Mode==GameMode.Go && match.HumanSide==Side.Black,"Go selector starts the human as Black");
        match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,2));
        UnityEngine.GUI.Click(100,1010);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(360,490);Draw(app,"Validation/Previews/transition.svg");
        Check(match.Go.StoneAt(40)==Side.Black && match.Remaining==1,"Go center intersection click");
        Draw(app,"Validation/Previews/go-portrait.svg");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        Draw(app,"Validation/Previews/go-landscape.svg");
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        UnityEngine.GUI.Click(370,1220);Draw(app,"Validation/Previews/transition.svg");
        Check(match.Actor==1 && match.Go.LastPass==Side.Black,"Go pass button hands over");
        for (int point=0;point<12;point++) Check(match.Go.Place(Side.White,point,out _),"Go bot pass fixture");
        match.Go.Pass(Side.Black);
        int botCards = match.Hands[1].Count, discarded = match.DiscardCount;
        Call(app,"Update");
        UnityEngine.Time.unscaledTime+=10;Call(app,"Update");
        Check(match.Phase==MatchPhase.GoScoring && match.Hands[1].Count==botCards && match.DiscardCount==discarded,
            "Bot answers a pass directly without playing a card");
        UnityEngine.GUI.Click(360,490);Draw(app,"Validation/Previews/transition.svg");
        Check(match.IsGoStoneMarked(40),"Go scoring group click");
        Draw(app,"Validation/Previews/go-scoring-portrait.svg");
        UnityEngine.GUI.Click(190,1220);Draw(app,"Validation/Previews/transition.svg");
        Check(match.Phase==MatchPhase.ChooseCard && match.Actor==0 && !match.IsGoStoneMarked(40),"Go resume click");
        Check(match.PassGo() && match.PassGo(),"Go score confirmation fixture");
        UnityEngine.GUI.Click(530,1220);Draw(app,"Validation/Previews/transition.svg");
        Check(match.Phase==MatchPhase.Finished,"Go score confirmation click");
        Set(app,"location",true);
        UnityEngine.GUI.Click(150,1160);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(500,700);Draw(app,"Validation/Previews/transition.svg");
        match=(CardMatch)Get(app,"match");
        Check(match.Mode==GameMode.Domino,"Domino choice starts a domino match");
        match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));
        UnityEngine.GUI.Click(100,1010);Draw(app,"Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(131,586);Draw(app,"Validation/Previews/transition.svg");
        Check((int)Get(app,"selected")==0,"Domino rack click selects first tile");
        UnityEngine.GUI.Click(220,460);Draw(app,"Validation/Previews/transition.svg");
        Check(match.Domino.Chain.Count==1 && match.Domino.Rack(match.HumanSide).Count==6,"Domino end button places selected tile");
        Draw(app,"Validation/Previews/domino-portrait.svg");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        Draw(app,"Validation/Previews/domino-landscape.svg");
        Console.WriteLine("PASS Go and domino selection, Go intersection and pass, domino rack and chain controls");
    }
    static void LandscapeGameplay()
    {
        UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
        var app = new ChessCardsApp(); Call(app, "Awake");
        UnityEngine.GUI.Click(400, 615); Draw(app, "Validation/Previews/transition.svg");
        Check((bool)Get(app, "modeSelection"), "Landscape Play opens mode selector");
        UnityEngine.GUI.Click(855, 430); Draw(app, "Validation/Previews/transition.svg");
        var match = (CardMatch)Get(app, "match");
        Check(match.Mode == GameMode.Domino && !(bool)Get(app, "portrait"), "Landscape mode choice");
        match.Hands[0].Clear();
        match.Hands[0].Add(new Card(match.Top.Color, 1));
        UnityEngine.GUI.Click(665, 390); Draw(app, "Validation/Previews/transition.svg");
        Check(match.Phase == MatchPhase.MovePieces, "Landscape card hitbox");
        UnityEngine.GUI.Click(95, 565); Draw(app, "Validation/Previews/transition.svg");
        Check((int)Get(app, "selected") == 0, "Landscape rack hitbox");
        UnityEngine.GUI.Click(174, 443); Draw(app, "Validation/Previews/transition.svg");
        Check(match.Domino.Chain.Count == 1, "Landscape domino placement");
        Console.WriteLine("PASS horizontal play, mode selection, card input and board actions");
    }

    static string HoverFrame(ChessCardsApp app, float x, float y)
    {
        const string path = "Validation/Previews/hover.svg";
        UnityEngine.GUI.Hover(x, y);
        Draw(app, path);
        return File.ReadAllText(path);
    }

    static void HoverAndRulesSmoke()
    {
        UnityEngine.Screen.width = 720; UnityEngine.Screen.height = 1280;
        var app = new ChessCardsApp(); Call(app, "Awake");
        string courtyard = HoverFrame(app, -1, -1);
        Check(!courtyard.Contains(">Правила<"), "Rules should only be available during a match");
        Check(courtyard == HoverFrame(app, 200, 50), "Courtyard heading must not change on hover");
        Check(courtyard != HoverFrame(app, 350, 1160), "Enabled Play button shows hover feedback");
        Check(courtyard == HoverFrame(app, 580, 935), "Unaffordable repair must not change on hover");

        UnityEngine.GUI.Click(650, 50); Draw(app, "Validation/Previews/transition.svg");
        Check((bool)Get(app, "settingsOpen"), "Open Settings for modal hover test");
        string settings = HoverFrame(app, -1, -1);
        Check(settings == HoverFrame(app, 350, 1160), "Settings must block hover on Play underneath");
        Check(settings == HoverFrame(app, 650, 50), "Settings must block hover on the gear underneath");
        Check(settings == HoverFrame(app, 300, 321), "Settings heading must not change on hover");
        Check(settings != HoverFrame(app, 350, 516), "An enabled option in Settings shows hover feedback");
        UnityEngine.GUI.Click(350, 1160); Draw(app, "Validation/Previews/transition.svg");
        Check((bool)Get(app, "settingsOpen") && !(bool)Get(app, "modeSelection"), "Settings must block clicks underneath");
        UnityEngine.GUI.Click(340, 950); Draw(app, "Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(350, 1160); Draw(app, "Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(500, 520); Draw(app, "Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(530, 48); Draw(app, "Validation/Previews/transition.svg");
        Check((bool)Get(app, "rules") && HoverFrame(app, -1, -1).Contains("Русские шашки на доске"),
            "Rules button in a match shows the active game's rules");
        Console.WriteLine("PASS hover on enabled buttons only, static headings, modal input blocking and match-specific rules");
    }

    static void PortraitCourtyardRepair()
    {
        const string saveKey = "ChessCards.Restoration.v1";
        string saved = UnityEngine.PlayerPrefs.GetString(saveKey, "{}");
        try
        {
            UnityEngine.Screen.width = 720; UnityEngine.Screen.height = 1280;
            var app = new ChessCardsApp(); Call(app, "Awake");
            var progress = (RestorationProgress)Get(app, "progress");
            Check(progress.Bench && progress.Coins == 40, "Load repaired courtyard state in portrait");
            Check(Get(app,"yardCrown") != null && Get(app,"yardCrest") != null && Get(app,"yardCorner") != null,
                "Missing courtyard ornaments");
            Draw(app, "Validation/Previews/repaired-portrait.svg");
            string before = File.ReadAllText("Validation/Previews/repaired-portrait.svg");
            Check(before.Contains(">1 из 3<") && before.Contains(">✓ Готово<"), "Show progress and repaired state");
            UnityEngine.GUI.Click(585, 936); Draw(app, "Validation/Previews/transition.svg");
            Check(!progress.Lamp && progress.Coins == 40, "Unaffordable portrait repair is inactive");

            progress.Coins = 100;
            UnityEngine.GUI.Click(585, 936); Draw(app, "Validation/Previews/transition.svg");
            Check(progress.Lamp && progress.Coins == 0, "Portrait repair costs the correct amount");
            Check(HoverFrame(app, -1, -1).Contains(">2 из 3<"), "Portrait progress updates immediately");
            Console.WriteLine("PASS full-bleed courtyard controls, portrait repair affordability and progress");
        }
        finally
        {
            UnityEngine.PlayerPrefs.SetString(saveKey, saved);
        }
    }

    static void SettingsSmoke()
    {
        UnityEngine.Screen.width = 720; UnityEngine.Screen.height = 1280;
        var app = new ChessCardsApp(); Call(app, "Awake");
        var prefs = (GamePreferences)Get(app, "preferences");
        Check(prefs.Language == GameLanguage.Russian && prefs.Difficulty == BotDifficulty.Normal &&
            prefs.Selection == ModeSelection.Manual && prefs.Music > 0, "Settings defaults");
        Check(!((UnityEngine.AudioSource)Get(app, "musicSource")).isPlaying, "Music should wait for input");

        UnityEngine.GUI.Click(650, 50); Draw(app, "Validation/Previews/transition.svg");
        Check((bool)Get(app, "settingsOpen") && ((UnityEngine.AudioSource)Get(app, "musicSource")).isPlaying,
            "Settings button and first audio gesture");
        UnityEngine.GUI.Click(530, 430); Draw(app, "Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(550, 515); Draw(app, "Validation/Previews/transition.svg");
        UnityEngine.GUI.Click(530, 605); Draw(app, "Validation/Previews/transition.svg");
        for (int i = 0; i < 4; i++)
        {
            UnityEngine.GUI.Click(85, 710); Draw(app, "Validation/Previews/transition.svg");
        }
        UnityEngine.GUI.Click(190, 795); Draw(app, "Validation/Previews/settings-portrait.svg");
        Check(prefs.Language == GameLanguage.English && prefs.Difficulty == BotDifficulty.Hard &&
            prefs.Selection == ModeSelection.Random, "Language, bot and random options respond to clicks");
        Check(prefs.Music == 0 && Math.Abs(prefs.Effects - .2f) < .01f &&
            !((UnityEngine.AudioSource)Get(app, "musicSource")).isPlaying &&
            Math.Abs(((UnityEngine.AudioSource)Get(app, "effectsSource")).volume - .2f) < .01f, "Music mute and effects volume");
        Check(File.ReadAllText("Validation/Previews/settings-portrait.svg").Contains(">Settings<"), "Language applies without restart");
        UnityEngine.GUI.Click(340, 950); Draw(app, "Validation/Previews/transition.svg");
        Check(!(bool)Get(app, "settingsOpen"), "Settings Done closes the overlay");

        var reloaded = new ChessCardsApp(); Call(reloaded, "Awake");
        prefs = (GamePreferences)Get(reloaded, "preferences");
        Check(prefs.Language == GameLanguage.English && prefs.Difficulty == BotDifficulty.Hard &&
            prefs.Selection == ModeSelection.Random && prefs.Music == 0 &&
            Math.Abs(prefs.Effects - .2f) < .01f, "All settings reload");

        UnityEngine.GUI.Click(150, 1160); Draw(reloaded, "Validation/Previews/transition.svg");
        var match = (CardMatch)Get(reloaded, "match");
        Check(match != null && !(bool)Get(reloaded, "modeSelection") && !(bool)Get(reloaded, "location"),
            "Random mode starts directly");
        Check(((ChessBot)Get(reloaded, "bot")).Difficulty == BotDifficulty.Hard, "Selected difficulty reaches actual bot");
        match.Hands[0].Clear();
        match.Hands[0].Add(new Card(match.Top.Color, 1));
        Draw(reloaded, "Validation/Previews/card-english.svg");
        Check(File.ReadAllText("Validation/Previews/card-english.svg").Contains(">MOVE<"), "Card face caption is localized");


        Set(reloaded, "historyOpen", true); Draw(reloaded, "Validation/Previews/history-english.svg");
        Check(File.ReadAllText("Validation/Previews/history-english.svg").Contains("Choose a card of the same color or value."),
            "Current game history is localized");
        Set(reloaded, "historyOpen", false);
        Set(reloaded, "rules", true); Draw(reloaded, "Validation/Previews/rules-english.svg");
        string rules = File.ReadAllText("Validation/Previews/rules-english.svg");
        Check(rules.Contains("How to play") && !rules.Contains("ЦЕЛЬ"), "English rules");
        Set(reloaded, "rules", false);
        match.Resign();
        Call(reloaded, "Update");
        UnityEngine.GUI.Click(550, 1220); Draw(reloaded, "Validation/Previews/transition.svg");
        Check(!ReferenceEquals(match, Get(reloaded, "match")) && !(bool)Get(reloaded, "modeSelection"),
            "Random replay starts a fresh game directly");
        UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
        UnityEngine.GUI.Click(1190, 50); Draw(reloaded, "Validation/Previews/settings-landscape.svg");
        Check((bool)Get(reloaded, "settingsOpen"), "Landscape settings open from a game");
        Console.WriteLine("PASS settings input, two languages, audio levels, difficulty, random start/replay and persistence");
    }

    static void Main()
    {
        Directory.CreateDirectory("Validation/Previews");
        BotTimingRegression();
        var app=new ChessCardsApp();Call(app,"Awake");
        Check(Get(app,"cardBody")!=null && Get(app,"cardFrame")!=null && Get(app,"cardBack")!=null,"Card artwork resource missing");
        foreach(var face in (UnityEngine.Texture2D[])Get(app,"cardFaces")) Check(face!=null,"Card face resource missing");
        Draw(app,"Validation/Previews/location-landscape.svg");
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        Draw(app,"Validation/Previews/location-portrait.svg");
        UnityEngine.GUI.Click(150,1160);Draw(app,"Validation/Previews/transition.svg");
        Check((bool)Get(app,"modeSelection") && (bool)Get(app,"location"),"Play must open mode selection");
        Draw(app,"Validation/Previews/mode-selection-portrait.svg");
        UnityEngine.GUI.Click(300,520);Draw(app,"Validation/Previews/transition.svg");
        Check(!(bool)Get(app,"location") && ((CardMatch)Get(app,"match")).Mode==GameMode.Chess,"Chess choice failed");
        var match=(CardMatch)Get(app,"match");
        match.Hands[0].Clear();
        match.Hands[0].AddRange(new[] { new Card(0,1), new Card(1,2), new Card(2,3), new Card(3,CardKind.Undo) });
        Draw(app,"Validation/Previews/cards-royal-portrait.svg");
        UnityEngine.Screen.width=1280;UnityEngine.Screen.height=720;
        Draw(app,"Validation/Previews/cards-royal-landscape.svg");
        UnityEngine.Screen.width=720;UnityEngine.Screen.height=1280;
        match.Hands[0].Clear();match.Hands[0].Add(new Card(match.Top.Color,1));
        UnityEngine.GUI.Click(100,1010);Draw(app,"Validation/Previews/transition.svg");
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
        Set(app,"location",true);UnityEngine.GUI.Click(1176,291);Draw(app,"Validation/Previews/transition.svg");
        Check(progress.Bench&&progress.Coins==40,"Repair click failed");
        Draw(app,"Validation/Previews/repaired-landscape.svg");
        var reloaded=new ChessCardsApp();Call(reloaded,"Awake");var saved=(RestorationProgress)Get(reloaded,"progress");
        Check(saved.Coins==40&&saved.Bench&&saved.Wins==1,"Progress save/reload failed");
        CheckersModeSmoke();
        GoDominoModeSmoke();
        LandscapeGameplay();
        HoverAndRulesSmoke();
        PortraitCourtyardRepair();
        SettingsSmoke();
        Console.WriteLine("PASS runtime source compilation against isolated adapter; GUI clicks, card selection, board input, mate reward, repair and save/reload");
        Console.WriteLine("Layout previews emitted. This does not replace testing in Unity Editor or a device build.");
    }
}
