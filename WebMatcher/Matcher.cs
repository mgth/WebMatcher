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

    public class Matcher : RegistryNotifier
    {
        public MatchersGroup Group
        {
            get { return GetProperty<MatchersGroup>(); }
            set
            {
                var oldGroup = Group;

                if (!SetAndWatch(value)) return;

                oldGroup?.Matchers.Remove(this);
                Group?.Matchers.Add(this);

                GroupName = Group?.Name;
            }
        }

        public Model Model
        {
            get { return GetProperty<Model>(); }
            set { SetAndWatch(value); }
        }

        [DependsOn(nameof(Model))]
        void UpdateModel()
        {
            //oldModel?.Matchers.Remove(this);
            Model?.Matchers.Add(this);

            ModelName = Model?.Name;
        }


        public Token Checking = new Token();
        public Token Queued = new Token();
        public bool AutoRefresh = true;


        public bool IsRunning
        {
            get { return GetProperty<bool>(); }
            set { SetProperty(value); }
        }

        public bool Enqueue()
        {
            return Queued.GetToken() && ThreadPool.QueueUserWorkItem(Check);
        }

        public string Name
        {
            get { return GetOrLoad<string>(); }
            set { SetAndSaveLater(value); }
        }

        public Uri Url
        {
            get { return GetProperty<Uri>(); }
            set
            {
                //if (value?.AbsoluteUri != Url?.AbsoluteUri) value = null;
                SetProperty(value);
            }
        }

        [DependsOn(nameof(Url))]
        private void UpdateUrl()
        {
            Enqueue();
            StringUrl = Url?.AbsoluteUri;
        }

        public string StringUrl
        {
            get { return GetProperty<string>(); }
            set { SetAndSaveLater(value);   }
        }

        [DependsOn(nameof(StringUrl))]
        void UpdateStringUrl()
        {
            try
            {
                if (StringUrl == null)
                {
                    Url = null;
                    return;
                }

                Uri newUri = new Uri(StringUrl);

                if (newUri.AbsoluteUri != Url?.AbsoluteUri)
                {
                    Url = newUri;
                }
            }
            catch (UriFormatException)
            {

            }
        }

        public string Expression
        {
            get { return GetOrLoad<string>(); }
            set
            {
                if (Model == null)
                    SetAndSaveLater(value);
                else
                    Model.Expression = value;
            }
        }

        [DependsOn(nameof(Expression))]
        public void UpdateHtml()
        {
            if (Html != null)
            {
                Value = Result(Expression);
            }
            else Enqueue();
        }

        [DependsOn("Model", "Model.Expression")]
        public void UpdateExpression()
        {
            if (Model != null)
                SetProperty(Model.Expression, "Expression");
        }


        public string Post
        {
            get { return GetOrLoad<string>(); }
            set
            {
                if (SetAndSaveLater(value))
                {
                    Enqueue();
                }
            }
        }

        public string Referer
        {
            get { return GetOrLoad<string>(); }
            set
            {
                if (SetAndSaveLater(value))
                {
                    Enqueue();
                }
            }
        }

        public bool ParseDom
        {
            get { return GetOrLoad<bool>(); }
            set
            {
                if (!SetAndSaveLater(value)) return;

                if (Html != null)
                {
                    Value = Result(Expression);
                }
                else Enqueue();
            }
        }

        public string Html
        {
            get { return GetProperty<string>(); }
            set
            {
                if (!SetProperty(value)) return;

                if (Html != null)
                {
                    Value = Result(Expression);
                }
            }
        }

        public string ParsedHtml
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }

        public string OldValue
        {
            get { return GetOrLoad<string>(); }
            set { SetAndSave(value); }
        }

        public string Value
        {
            get { return GetOrLoad<string>(); }
            private set
            {
                if (value == null) return;

                //Some sites can go backward and forward we dont want notification for that.
                bool setChangedState = (Value != null) && (value != OldValue);
                OldValue = Value;

                if (!SetProperty(value)) return;

                SaveString("Value", Value ?? "");
                LastChanged = DateTime.Now;
                if (setChangedState) ChangedState = true;
            }
        }

        public Image Favicon
        {
            get { return GetProperty<Image>(); }
            private set { SetProperty(value); }
        }


        [DependsOn("Favicon")]
        public System.Windows.Media.ImageSource FaviconSource
        {
            get
            {
                if (Favicon == null) { return null; }

                // Winforms Image we want to get the WPF Image from...
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                MemoryStream memoryStream = new MemoryStream();
                // Save to a memory stream...
                Favicon.Save(memoryStream, ImageFormat.Png);
                // Rewind the stream...
                memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();

                //memoryStream.Dispose();

                return bitmap;
            }
        }

        public string Complement
        {
            get { return GetOrLoad<string>(); }
            private set { SetAndSave(value); }
        }


        public bool IsNew => RegistryKey == null;

        private readonly bool _trayNotification = false;

        public bool ChangedState
        {
            get { return GetOrLoad<bool>(); }
            private set  { SetAndSave(value); }
        }

        [DependsOn(nameof(ChangedState))]
        private void UpdateChangedState()
        {
                if (!_trayNotification) return;
                Group.OnNotify(this);           
        }

        public DateTime LastChecked
        {
            get { return GetOrLoad<DateTime>(); }
            set { SetAndSave(value); }
        }

        public DateTime LastChanged
        {
            get { return GetOrLoad<DateTime>(); }
            set { SetAndSave(value); }
        }

        public State Status
        {
            get { return GetOrLoad<State>(); }
            set { SetAndSave(value); }
        }

        public State RunningStatus
        {
            get { return GetProperty<State>(); }
            private set { SetProperty(value); }
        }

        [DependsOn("Status", "IsRunning")]
        public void UpdateRunningStatus()
        {
            RunningStatus = IsRunning ? State.Running : Status;
        }

        public Matcher(Matchers parent, string key = null)
        {
            Parent = parent;
            ParentNotifier = parent;

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
            _trayNotification = true;
        }




        public string GroupName
        {
            get { return GetProperty<string>(); }
            set { SetAndSaveLater(value); }
        }

        [DependsOn(nameof(GroupName))]
        void UpdateGroupName()
        {
            Group = Parent?.GetGroup(GroupName);
        }


        public string ModelName
        {
            get { return GetProperty<string>(); }
            set { SetAndSaveLater(value); }
        }

        [DependsOn(nameof(ModelName))]
        void UpdateModelName()
        {
            var m = Parent?.GetModel(ModelName);
            if (m?.Count == 0) m.Expression = Expression;
            Model = m;
        }

        public Matchers Parent { get; }

        public void Load(string key = null)
        {
            if (key != null) RegistryKey = key;
            if (RegistryKey == null) return;

            LoadProperty<string>(nameof(Name));
            LoadProperty<string>(nameof(GroupName));
            LoadProperty<string>(nameof(ModelName));
            LoadProperty<string>(nameof(StringUrl));
        }


        string GetNewKey()
        {
            using (RegistryKey k = Parent.GetRegistryKey())
            {
                string[] keys = k.GetSubKeyNames();
                int i = 1;
                while (Array.IndexOf(keys, i.ToString()) > -1) { i++; }
                return i.ToString();
            }
        }



        public
        bool Save(bool saveIfNew = false)
        {
            if (Expanded) return false;

            if (RegistryKey == null)
            {
                if (saveIfNew)
                    RegistryKey = GetNewKey();
                else return false;
            }

            SaveAllProperties();

            return true;
        }

        public void Delete()
        {
            using (RegistryKey k = Parent.GetRegistryKey())
            {
                if (k != null && !string.IsNullOrEmpty(RegistryKey))
                {
                    k.DeleteSubKeyTree(RegistryKey);
                    RegistryKey = null;
                }
            }

            Group = null;
        }


        public void Open()
        {
            if (Url == null) return;

            ChangedState = false;
            System.Diagnostics.Process.Start(Url.AbsoluteUri);
            Save();
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
                        if (s != null)
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
            if (IsNew) return;

            if (!Checking.GetToken()) return;

            IsRunning = true;
            LastChecked = DateTime.Now;

            GetHtml();

            Save();

            if (Favicon == null) GetFavicon();

            IsRunning = false;
            Queued.SetToken();
            Checking.SetToken();
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

        public bool Visible
        {
            get { return true; /*return GetProperty<bool>();*/ }
            //private set { SetProperty(value); }
        }

        [DependsOn("Parent.ViewAll", "IsNew", "ChangedState")]
        public void UpdateVisible()
        {
            //Visible = (Parent.ViewAll ?? false) || IsNew || ChangedState;
        }

        public bool Expanded
        {
            get { return GetProperty<bool>(); }
            set { SetProperty(value); }
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
