using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using NotifyChange;

namespace WebMatcher
{
    public enum State
    {
        NotChecked = 0,
        Ok = 1,
        Running = 2,
        Invalid = 3,
        Unavailable = 4,
        NotFound = 5,
        BadExpression = 6,
    }

    public class Token
    {
        public bool Available { get; private set; } = true;

        public bool GetToken()
        {
            lock (this)
            {
                if (!Available) return false;
                Available = false;
                return true;
            }
        }

        public void SetToken()
        {
            lock (this)
            {
                Available = true;
            }
        }
    }

    public delegate void NotifyHandler(Matcher matcher);

    public class Matcher : Notifier
    {
        MatchersGroup _group;
        public MatchersGroup Group
        {
            get { return _group; }
            set
            {
                var oldGroup = _group;

                if (!SetAndWatch(ref _group, value)) return;

                oldGroup?.Matchers.Remove(this);
                _group?.Matchers.Add(this);
            }
        }

        private Model _model = null;
        public Model Model
        {
            get { return _model; }
            set
            {
                var oldModel = _model;

                if(!SetAndWatch(ref _model, value)) return;

                oldModel?.Matchers.Remove(this);
                _model?.Matchers.Add(this);
            }
        }

        string _value = null;
        string _oldValue = null;
        State _status = State.NotChecked;
        string _name = "";
        string _html = "";
        bool _changedState = false;
        public Token Checking = new Token();
        public Token Queued = new Token();
        public bool AutoRefresh = true;
        Image _favicon;


        public bool IsRunning
        {
            get { return _isRunning; }
            set { SetProperty(ref _isRunning, value); }
        }

        public bool Enqueue()
        {
            if (!Queued.GetToken()) return false;
            return ThreadPool.QueueUserWorkItem(Check);
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public string Key
        {
            get { return _key; }
            private set { SetProperty(ref _key, value); }
        }

        private Uri _url;
        public Uri Url
        {
            get { return _url; }
            set
            {
                if (value?.AbsoluteUri != _url?.AbsoluteUri) _url = null;
                if (SetProperty(ref _url, value))
                {
                    Enqueue();
                }
            }
        }

        [DependsOn("Url")]
        public string StringUrl
        {
            get
            {
                return _url.AbsoluteUri;
            }
            set
            {
                try
                {
                    Uri newUri = new Uri(value);
                    if (newUri.AbsoluteUri  != _url?.AbsoluteUri )
                    {
                        Url = newUri;
                    }
                }
                catch (UriFormatException)
                {

                }
            }
        }

        private string _expression;
        public string Expression
        {
            get { return _expression; }
            set
            {
                if (Model==null) 
                    SetProperty(ref _expression, value);
                else
                    Model.Expression = value;
            }
        }

        [DependsOn("Expression")]
        public void UpdateHtml()
        {
                if (Html != null)
                {
                    Value = Result(_expression);
                }
                else Enqueue();           
        }

        [DependsOn("Model", "Model.Expression")]
        public void UpdateExpression()
        {
            if (Model != null)
                SetProperty(ref _expression, Model.Expression, "Expression");
        }


        private string _post;
        public string Post
        {
            get { return _post; }
            set
            {
                if (SetProperty(ref _post, value))
                {
                    Enqueue();
                }
            }
        }

        private string _referer;
        public string Referer
        {
            get { return _referer; }
            set
            {
                if (SetProperty(ref _referer, value))
                {
                    Enqueue();
                }
            }
        }

        private bool _parseDom = false;
        public bool ParseDom
        {
            get { return _parseDom; }
            set
            {
                if (SetProperty(ref _parseDom, value))
                {
                    if (Html != null)
                    {
                        Value = Result(Expression);
                    }
                    else Enqueue();
                }
            }
        }

        public string Html
        {
            get { return _html; }
            set
            {
                if (SetProperty(ref _html, value))
                {
                    if (Html != null)
                    {
                        Value = Result(Expression);
                    }
                }
            }
        }

        private string _parsedHtml = "";
        public string ParsedHtml
        {
            get { return _parsedHtml; }
            set { SetProperty(ref _parsedHtml, value); }
        }

        public String Value
        {
            get { return _value; }
            private set
            {
                if (value == null) return;

                //Some sites can go backward and forward we dont want notification for that.
                bool setChangedState = (_value != null) && (value != _oldValue);
                _oldValue = _value;

                if (SetProperty(ref _value, value))
                {
                    SaveString("Value", Value ?? "");
                    LastChanged = DateTime.Now;
                    if (setChangedState) ChangedState = true;
                }
            }
        }

        public Image Favicon
        {
            get { return _favicon; }
            private set { SetProperty(ref _favicon, value); }
        }


        [DependsOn("Favicon")]
        public System.Windows.Media.ImageSource FaviconSource
        {
            get
            {
                if (_favicon == null) { return null; }

                // Winforms Image we want to get the WPF Image from...
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                MemoryStream memoryStream = new MemoryStream();
                // Save to a memory stream...
                _favicon.Save(memoryStream, ImageFormat.Png);
                // Rewind the stream...
                memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();

                //memoryStream.Dispose();

                return bitmap;
            }
        }

        private string _complement;
        public String Complement
        {
            get { return _complement; }
            private set { SetProperty(ref _complement, value); }
        }


        public Boolean IsNew => Key == null;

        public bool Notify { get; private set; } = false;

        public bool ChangedState
        {
            get { return _changedState; }
            private set
            {
                if (SetProperty(ref _changedState, value))
                {
                    if (Notify)
                    {
                        SaveString("Changed", ChangedState ? "True" : "False");
                        Group.OnNotify(this);
                    }
                }
            }
        }

        private DateTime _lastCheck = DateTime.MinValue;

        public DateTime LastChecked
        {
            get { return _lastCheck; }
            set
            {
                if (SetProperty(ref _lastCheck, value))
                {
                    SaveString("LastChecked", LastChecked.ToString("dd/MM/yyyy"));
                }
            }
        }

        private DateTime _lastChanged = DateTime.MinValue;
        public DateTime LastChanged
        {
            get { return _lastChanged; }
            set
            {
                if (SetProperty(ref _lastChanged, value))
                {
                    SaveString("LastChanged", LastChanged.ToString("dd/MM/yyyy"));
                }
            }
        }
        public State Status
        {
            get { return _status; }
            set
            {
                if (SetProperty(ref _status, value))
                {
                    SaveString("Status", Status.ToString());
                }
            }
        }

        private State _runningStatus;
        public State RunningStatus
        {
            get { return _runningStatus; }
            private set { SetProperty(ref _runningStatus, value); }
        }

        [DependsOn("Status", "IsRunning")]
        public void UpdateRunningStatus()
        {
            RunningStatus = IsRunning ? State.Running : Status;
        }

        public Matcher(Matchers parent, string key = null)
        {
            Parent = parent;

            if (key == null)
            {
                Name = "<nouveau>";
                //Url = new Uri("");
                Expression = "";
                GroupName = "<nouveau>";
            }
            else
            {
                Load(key);
            }
            Watch(Parent, "Parent");
            Notify = true;
        }


        public String LoadString(RegistryKey k, String key, String defValue = "")
        {
            if (k != null)
            {
                String s = k.GetValue(key, defValue).ToString();
                if (String.IsNullOrEmpty(s)) return null;
                return s;
            }
            return null;
        }

        public void SaveString(string key, string value)
        {
            using (RegistryKey k = GetRegistryKey(true))
            {
                SaveString(k, key, value);
            }
        }
        public static void SaveString(RegistryKey registryKey, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (registryKey.GetSubKeyNames().Contains(key))
                    registryKey.DeleteValue(key);
            }
            else
                registryKey.SetValue(key, value, RegistryValueKind.String);
        }

        [DependsOn("Group")]
        public string GroupName
        {
            get { return Group != null ? Group.Name : ""; }
            set { Group = Parent?.GetGroup(value); }
        }

        [DependsOn("Model")]
        public string ModelName
        {
            get { return (Model!=null)? Model.Name : ""; }
            set
            {
                var m = Parent?.GetModel(value);
                if (m?.Count == 0) m.Expression = Expression;
                Model = m;
            }
        }

        public Matchers Parent { get; }

        public void Load(string key = null)
        {
            if (key != null) Key = key;
            if (Key == null) return;

            using (RegistryKey rk = Matchers.GetRootKey())
            {
                using (RegistryKey k = rk.OpenSubKey(Key))
                {
                    if (k != null)
                    {
                        Name = LoadString(k, "Name");
                        GroupName = LoadString(k, "Group");
                        StringUrl = LoadString(k, "URL", "http://");
                        ModelName = LoadString(k, "Model", "");
                        Expression = LoadString(k, "Expression");
                        Post = LoadString(k, "Post");
                        Referer = LoadString(k, "Referer");
                        ParseDom = LoadString(k, "ParseDom", "False") == "True";
                        Value = LoadString(k, "Value");
                        try
                        {
                            Status = (State)Enum.Parse(typeof(State), LoadString(k, "Status", "NotChecked"));
                        }
                        catch (FormatException) { Status = State.NotChecked; }

                        ChangedState = LoadString(k, "Changed", "False") == "True";

                        LastChecked = DateTime.ParseExact(LoadString(k, "LastCheck", "01/01/0001"), "dd/MM/yyyy", null);
                        LastChanged = DateTime.ParseExact(LoadString(k, "LastChanged", "01/01/0001"), "dd/MM/yyyy", null);
                    }
                }
            }
        }


        static string GetNewKey()
        {
            using (RegistryKey k = Matchers.GetRootKey())
            {
                string[] keys = k.GetSubKeyNames();
                int i = 1;
                while (Array.IndexOf(keys, i.ToString()) > -1) { i++; }
                return i.ToString();
            }
        }


        public RegistryKey GetRegistryKey(bool create = false)
        {
            using (RegistryKey rk = Matchers.GetRootKey())
            {
                if (create)
                    return rk.CreateSubKey(Key);
                else
                    return rk.OpenSubKey(Key);
            }
        }

        public
        bool Save(bool saveIfNew = false)
        {
            if (Expanded) return false;

            if (Key == null)
            {
                if (saveIfNew)
                    Key = GetNewKey();
                else return false;
            }

            using (RegistryKey rk = Matchers.GetRootKey())
            {
                using (RegistryKey k = rk.CreateSubKey(Key))
                {
                    SaveString(k, "Name", Name);
                    SaveString(k, "Group", GroupName);
                    if (!string.IsNullOrEmpty(Url?.AbsoluteUri))
                        SaveString(k, "URL", Url.AbsoluteUri);

                    SaveString(k, "Model", ModelName);
                    SaveString(k, "Expression", Expression);
                    SaveString(k, "Post", Post);
                    SaveString(k, "Referer", Referer);
                    SaveString(k, "ParseDom", ParseDom ? "True" : "False");
                }
            }

            return true;
        }

        public void Delete()
        {
            using (RegistryKey k = Matchers.GetRootKey())
            {
                if (k != null && !string.IsNullOrEmpty(Key))
                {
                    k.DeleteSubKeyTree(Key);
                    Key = null;
                }
            }

            Group = null;
        }


        public void Open()
        {
            if (Url != null)
            {
                ChangedState = false;
                System.Diagnostics.Process.Start(Url.AbsoluteUri);
                Save();
            }
        }

        public static void SetHeader(WebRequest request, string header, string value)
        {
            if (request == null) return;
            // Retrieve the property through reflection.
            PropertyInfo PropertyInfo = request.GetType().GetProperty(header.Replace("-", string.Empty));
            // Check if the property is available.
            if (PropertyInfo != null)
            {
                // Set the value of the header.
                PropertyInfo.SetValue(request, value, null);
            }
            else
            {
                // Set the value of the header.
                request.Headers[header] = value;
            }
        }


        string MatchIcon(string relName)
        {
            Match match = Regex.Match(Html, "<link[^>]*?rel=\"" + relName + "\"[^>]*?href=\"([^\"]*)\"", RegexOptions.Singleline);
            if (!match.Success)
            {
                match = Regex.Match(Html, "<link[^>]*?href=\"([^\"]*)\"[^>]*?rel=\"" + relName + "\"", RegexOptions.Singleline);
            }
            if (match.Success) return match.Groups[1].Value;
            return null;
        }


        public void GetFavicon()
        {
            string url;
            string server = "";

            if (Url == null) return;

            Match match = Regex.Match(Url.AbsoluteUri, "^(http?://.*?)[/$]", RegexOptions.Singleline);
            if (match.Success)
            {
                server = match.Groups[1].Value;
            }

            if (Html == null) GetHtml();
            if (Html != null)
            {

                /*                match = Regex.Match(src, "rel=\"icon\".*?href=\"(.*?)\"", RegexOptions.Singleline);
                                if (!match.Success)
                                {
                                    match = Regex.Match(src, "rel=\"shortcut icon\"[^>]*?href=\"(.*?)\"", RegexOptions.Singleline);
                                }

                                if (!match.Success)
                                {
                                    match = Regex.Match(src, "rel=\"favicon\".*?href=\"(.*?)\"", RegexOptions.Singleline);
                                }

                                if (!match.Success)
                                {
                                    match = Regex.Match(src, "<link href=\"(.*?)\"[^>]*?rel=\"shortcut icon\"", RegexOptions.Singleline);
                                }


                                if (match.Success) url = match.Groups[1].Value;
                 * */

                url = MatchIcon("icon");
                if (url == null) url = MatchIcon("shortcut icon");
                if (url == null) MatchIcon("favicon");

                if (url == null) { url = "/favicon.ico"; }

                if (Regex.Match(url, "^/").Success) url = server + url;

                if (!Regex.Match(url, "^http").Success)
                {
                    match = Regex.Match(Url.AbsoluteUri, "^(.*)[/$]", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        url = match.Groups[1].Value + "/" + url;
                    }
                }

                try
                {
                    WebRequest request = HttpWebRequest.Create(url);
                    request.Method = "GET";
                    SetHeader(request, "User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)");

                    // make request for web page
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    using (Stream s = response.GetResponseStream())
                    {
                        if (s!=null)
                        try
                        {
                            Favicon = Image.FromStream(s);
                        }
                        catch (ArgumentException)
                        {
                            // todo: Bad image format
                        }
                        catch (IOException)
                        {
                            // todo: server exeption
                        }
                    }
                    response.Close();
                }
                catch (WebException)
                {
                }
                catch (UriFormatException)
                {

                }
            }
        }


        private DomParser _parser;

        public void GetDomHtml(String src)
        {
            _parser = new DomParser(Url, _parser_Parsed);
        }

        private void _parser_Parsed(object sender, string e)
        {
            if (!string.IsNullOrEmpty(e))
            {
                Html = e;
            }
            _parser?.Dispose();
            _parser = null;
        }

        public void GetHtml()
        {
            if (Url == null)
            {
                if (Html != null)
                {
                    Html = null;
                    ChangedState = true;
                }
                Status = State.Invalid;
                Complement = "URL invalide";
                return;
            }

            try
            {
                CookieContainer cc = new CookieContainer();

                DateTime date = DateTime.Now.AddDays(365);

                Cookie c = new Cookie
                {
                    Name = "FreedomCookie",
                    Path = "/",
                    Expires = date,
                    Domain = "sourceforge.net",
                    Value = "true"
                };


                cc.Add(c);

                if (!string.IsNullOrEmpty(Referer))
                {
                    /*
                                        HttpWebRequest reqReferer1 = (HttpWebRequest)HttpWebRequest.Create("http://www.realtek.com/downloads/downloadsView.aspx?Langid=1&PNid=14&PFid=24&Level=4&Conn=3");
                                        reqReferer1.CookieContainer = cc;
                                        reqReferer1.Method = "GET";
                                        reqReferer1.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
                                        reqReferer1.GetResponse();
                    */
                    HttpWebRequest reqReferer = (HttpWebRequest)HttpWebRequest.Create(Referer);
                    //                    reqReferer.Referer = reqReferer1.RequestUri.ToString();
                    reqReferer.CookieContainer = cc;
                    reqReferer.Method = "GET";
                    //reqReferer.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
                    reqReferer.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:24.0) Gecko/20100101 Firefox/24.0";
                    HttpWebResponse responseRef = (HttpWebResponse)reqReferer.GetResponse();
                    StreamReader websrcref = new StreamReader(responseRef.GetResponseStream());
                    String srcRef = websrcref.ReadToEnd();
                    /*
                                        Match match = Regex.Match(srcRef, "__VIEWSTATE\" value=\"(.*?)\"", RegexOptions.Singleline);
                                        if (match.Success)
                                        {
                                            ViewState = match.Groups[1].ToString();
                                        }*/
                }

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(Url);


                request.CookieContainer = cc;


                //                request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
                //request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:24.0) Gecko/20100101 Firefox/24.0";
                request.UserAgent = "Mozilla/4.0 (compatible; MSIE 5.01; Windows NT 5.0)";
                //SetHeader(request, "User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)");
                request.AllowAutoRedirect = true;
                //request.PreAuthenticate = true;
                //request.Credentials = CredentialCache.DefaultCredentials;

                request.KeepAlive = true;
                request.Headers["Cache-Control"] = "max-age=0";
                //request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                request.Headers["Accept-Language"] = "fr,fr-fr;q=0.8,en-us;q=0.5,en;q=0.3";
                request.AutomaticDecompression = DecompressionMethods.GZip;

                if (!string.IsNullOrEmpty(Referer))
                    request.Referer = Referer;

                if (!string.IsNullOrEmpty(Post))
                {
                    request.Method = "POST";
                    byte[] array = System.Text.Encoding.UTF8.GetBytes(Post /*+ "&__VIEWSTATE=" + ViewState*/);
                    request.ContentLength = array.Length;
                    request.ContentType = "application/x-www-form-urlencoded";
                    Stream data = request.GetRequestStream();
                    data.Write(array, 0, array.Length);
                    data.Close();
                }
                else
                    request.Method = "GET";

                // make request for web page
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader websrc = new StreamReader(response.GetResponseStream(), System.Text.Encoding.GetEncoding("iso-8859-1"));
                string html = websrc.ReadToEnd();
                response.Close();

                if (ParseDom)
                {
                    GetDomHtml(html);
                }
                else Html = html;

                return;
            }
            catch (UriFormatException)
            {
                Status = State.Invalid;
                Complement = "Format d'URL invalide";
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    Status = State.Unavailable;
                }
                else Status = State.Unavailable;

                Complement = ex.ToString();
            }
            catch (IOException ex)
            {
                Status = State.Unavailable;
                Complement = ex.ToString();
            }
            Html = null;
        }

        public string Result(string expr)
        {
            Complement = "";

            if (!string.IsNullOrEmpty(Html))
            {
                try
                {
                    Regex regex = new Regex(expr, RegexOptions.Singleline, new TimeSpan(0, 0, 0, 10));
                    try
                    {
                        Match match = regex.Match(Html);
                        if (match.Success)
                        {
                            string value = "";
                            Status = State.Ok;

                            for (int i = 1; i < match.Groups.Count; i++)
                            {
                                if (i > 1) value += " - ";
                                value += HttpUtility.HtmlDecode(match.Groups[i].Value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ")); ;
                            }
                            return value;
                        }
                         Status = State.NotFound;
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        Status = State.Invalid;
                        Complement = ex.Message;
                        return null;
                    }
                }
                catch (ArgumentException ex)
                {
                    Status = State.BadExpression;
                    Complement = ex.Message;
                    return null;
                }
            }
            return null;
        }


        public void Check(Object threadContext)
        {
            if (Checking.GetToken())
            {
                IsRunning = true;
                LastChecked = DateTime.Now;

                GetHtml();

                Save();

                if (Favicon == null) GetFavicon();

                IsRunning = false;
                Queued.SetToken();
                Checking.SetToken();
            }
        }

        public Matcher Clone()
        {
            return new Matcher(Parent)
            {
                Name = Name,
                Url = Url,
                Expression = Expression,
                Post = Post,
                Referer = Referer,
                GroupName = GroupName
            };
        }

        private bool _visible = true;
        public bool Visible
        {
            get { return _visible; }
            //private set { SetProperty(ref _visible, value); }
        }

        [DependsOn("Parent.ViewAll", "IsNew", "ChangedState")]
        public void UpdateVisible()
        {
            //Visible = (Parent.ViewAll ?? false) || IsNew || ChangedState;
        }

        private bool _expanded = false;
        private string _key;
        private bool _isRunning;

        public bool Expanded
        {
            get { return _expanded; }
            set { SetProperty(ref _expanded, value); }
        }

        [DependsOn("RunningStatus")]
        public UIElement StateIcon
        {
            get
            {
                switch (RunningStatus)
                {
                    case State.NotChecked:
                        return (UIElement)Application.Current.FindResource("svgNotChecked");
                    case State.Ok:
                        return (UIElement)Application.Current.FindResource("svgOk");
                    case State.Running:
                        return (UIElement)Application.Current.FindResource("svgRunning");
                    case State.Invalid:
                        return (UIElement)Application.Current.FindResource("svgInvalid");
                    case State.Unavailable:
                        return (UIElement)Application.Current.FindResource("svgAnavailable");
                    case State.NotFound:
                        return (UIElement)Application.Current.FindResource("svgNotFound");
                    case State.BadExpression:
                        return (UIElement)Application.Current.FindResource("svgBadExpression");
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return (UIElement)Application.Current.FindResource("svgNotFound");
            }
        }
    }
}
