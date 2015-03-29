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
        BadExpression =6,
    }

    public class Token
    {
        bool _token = true;

         public bool GetToken()
        {
            lock(this)
            {
                if (!_token) return false;
                _token = false;
                return true;
            }
        }

        public void SetToken()
        {
            lock (this)
            {
                _token = true ;
            }
        }
    }

    public delegate void NotifyHandler(Matcher matcher);

    public class Matcher : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        //        public static NotifyIcon Notify;
        //        public static MainWindow Win;

        //        static ObservableCollection<Matcher> _matchers = new ObservableCollection<Matcher>();
        //        static ObservableCollection<String> _groups = new ObservableCollection<String>();

        // Nb watchers thread to run concurrently



        MatchersGroup _group;
        public MatchersGroup Group
        {
            get { return _group; }
            set
            {
                if (_group == value) return;
                if (_group != null) _group.Matchers.Remove(this);
                _group = value;
                 if (_group != null) _group.Matchers.Add(this);
                OnPropertyChanged("Group");
                OnPropertyChanged("GroupName");
            }
        }
        String _key;

        String _value = "";
        State _status = State.NotChecked;
        String _name = "";
        String _html = "";
        Boolean _changedState = false;
        public Token Checking = new Token();
        public Token Queued = new Token();
        public Boolean AutoRefresh = true;
        Image _favicon;

        public bool Enqueue()
        {
            if(Queued.GetToken())
            {
                ThreadPool.QueueUserWorkItem(Check);
                return true;
            }
            return false;
        }

        //private bool SetProperty(string name, ref string property, string value)
        //{
        //    if (property != value)
        //    {
        //        property = value;
        //        OnPropertyChanged(name);
        //        return true;
        //    }
        //    else return false;
        //}
        //private bool SetProperty(string name, ref bool property, bool value)
        //{
        //    if (property != value)
        //    {
        //        property = value;
        //        OnPropertyChanged(name);
        //        return true;
        //    }
        //    else return false;
        //}
        private bool SetProperty<T>(string name, ref T property, T value) where T : IComparable
        {
            if (property==null || !property.Equals(value))
            {
                property = value;
                OnPropertyChanged(name);
                return true;
            }
            else return false;
        }

        public String Name
        {
            get { return _name; }
            set { SetProperty("Name",ref _name, value); }
        }
        public String Key
        {
            get { return _key; }
        }

        private string _url;
        public string URL { get { return _url; }
            set {
                if (SetProperty("URL", ref _url, value))
                    Enqueue();
            }
        }

        private string _expression;
        public string Expression { get { return _expression; }
            set {
                if (SetProperty("Expression", ref _expression, value))
                {
                    if (Html != null)
                    {
                        State s;
                        Value = Result(out s);
                        Status = s;
                    }
                    else Enqueue();
                }
           }
        }

        private string _post;
        public string Post { get { return _post; }
            set {
                if (SetProperty("Post", ref _post, value))
                {
                    Enqueue();
                }
            }
        }

        private string _referer;
        public String Referer { get { return _referer; }
            set {
                if (SetProperty("Referrer", ref _referer, value))
                {
                    Enqueue();
                }
            }
        }
        public String Html
        {
            get { return _html; }
            set {
                if ( SetProperty("Html", ref _html, value))
                {
                    Enqueue();
                }
            }
        }
        public String Value
        {
            get { return _value; }
            private set {
                if (value == null) return;
                if (SetProperty("Value", ref _value, value))
                {
                    ChangedState = true;
                    LastChanged = DateTime.Now;
                }
            }
        }

        public Image Favicon
        {
            get { return _favicon; }
            private set { _favicon = value; OnPropertyChanged("FaviconSource"); }
        }
        private bool CheckChanged(string property, ref bool value)
        {
            bool old = value;
            value = (bool)this.GetType().GetProperty(property).GetValue(this);
            if (old != value)
            {
                OnPropertyChanged(property);
                return true;
            }
            return false;
        }

        private double _labelSize = 0;
        public double LabelSize
        {
            get { return _labelSize; }
            set
            {
                if (value != _labelSize)
                {
                    _labelSize = value;
                    OnPropertyChanged("LabelSize");
                    Group.CheckLabelSizeChanged();
                }
            }
        }

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
                return bitmap;
            }
        }

        private string _complement;
        public String Complement {
            get { return _complement; }
            private set { SetProperty("Complement", ref _complement, value);  }
        }


        public Boolean IsNew
        {
            get { return _key == null; }
        }

        public Boolean ChangedState
        {
            get { return _changedState; }
            private set
            {
                if (value != _changedState)
                {
                    _changedState = value;
                    if (_changedState) LastChanged = LastChecked;
                    OnPropertyChanged("ChangedState");
                    CheckChanged("Visible",ref _visible);
                    Group.OnNotify(this);
                }
            }
        }

        private DateTime _lastCheck = DateTime.MinValue;
        public DateTime LastChecked { get { return _lastCheck; } set { SetProperty("LastCheck", ref _lastCheck, value); } }

        private DateTime _lastChanged = DateTime.MinValue;
        public DateTime LastChanged { get { return _lastChanged; } set { SetProperty("LastChanged", ref _lastChanged, value); } }
        public State Status
        {
            get { return _status; }
            set
            {
                if (_status!=value)
                {
                    _status = value;
                    OnPropertyChanged("Status");
                }
            }
        }



        public Matcher(Matchers parent)
        {
            _parent = parent;
            Name = "<nouveau>";
            URL = "http://";
            Expression = "";
            //GroupName = "";

            _parent.PropertyChanged += _parent_PropertyChanged;
        }

        private void _parent_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName=="ViewAll")
                CheckChanged("Visible",ref _visible);
        }

        public String LoadString(RegistryKey k, String key, String defValue = "")
        {
            String s = k.GetValue(key, defValue).ToString();
            if (s == "") return null;
            return s;
        }

        public void SaveString(RegistryKey k, String key, String value)
        {
            if (value == null || value == "")
            {
                if (k.GetSubKeyNames().Contains(key))
                    k.DeleteValue(key);
            }
            else
                k.SetValue(key, value, RegistryValueKind.String);

        }

        public string GroupName
        {
            get {
                if (Group != null) return Group.Name;
                else return "";
            }
            set
            {
                Group = Parent.GetGroup(value);
            }
        }

        Matchers _parent;
        public Matchers Parent
        {
            get { return _parent; }
        }

        public void Load(string key=null)
        {
            if(key!=null && key!=_key)
            {
                _key = key;
                OnPropertyChanged("Key");
            }
            using (RegistryKey rk = Parent.GetRootKey())
            {
                using (RegistryKey k = rk.OpenSubKey(_key))
                {
                    if (k != null)
                    {
                        SetProperty("Name",ref _name, LoadString(k, "Name"));
                        GroupName = LoadString(k, "Group");
                        SetProperty("URL", ref _url, LoadString(k, "URL", "http://"));
                        SetProperty("Expression", ref _expression, LoadString(k, "Expression"));
                        SetProperty("Post", ref _post, LoadString(k, "Post"));
                        SetProperty("Referer", ref _referer, LoadString(k, "Referer"));
                        SetProperty("Value", ref _value, LoadString(k, "Value"));
                        try
                        {
                            SetProperty("Status", ref _status, (State)int.Parse(LoadString(k, "Status", "1")));
                        }
                        catch (FormatException ex)
                        {
                            SetProperty("Status", ref _status, State.NotChecked);
                        }
                        SetProperty("ChangedState", ref _changedState, (LoadString(k, "Changed", "False") == "True") ? true : false);

                        SetProperty("LastCheck", ref _lastCheck, DateTime.ParseExact(LoadString(k, "LastCheck", "01/01/0001"), "dd/MM/yyyy", null));
                        SetProperty("LastChanged", ref _lastChanged, DateTime.ParseExact(LoadString(k, "LastChanged", "01/01/0001"), "dd/MM/yyyy", null));
                        k.Close();
                    }
                }
            }
        }


        String getNewKey()
        {
            using (RegistryKey k = Parent.GetRootKey())
            {
                String[] keys = k.GetSubKeyNames();
                int i = 1;
                while (Array.IndexOf(keys, i.ToString()) > -1) { i++; }
                return i.ToString();
            }
        }

        public void Save()
        {
            if (_key == null) _key = getNewKey();

            using (RegistryKey rk = Parent.GetRootKey())
            {
                using (RegistryKey k = rk.CreateSubKey(_key))
                {
                    SaveString(k, "Name", Name);
                    SaveString(k, "Group", GroupName);
                    SaveString(k, "URL", URL);
                    SaveString(k, "Expression", Expression);
                    SaveString(k, "Post", Post);
                    SaveString(k, "Referer", Referer);
                    SaveString(k, "Value", Value == null ? "" : Value);

                    SaveString(k, "Status", Status.ToString());
                    SaveString(k, "Changed", ChangedState ? "True" : "False");
                    SaveString(k, "LastChecked", LastChecked.ToString("dd/MM/yyyy"));
                    SaveString(k, "LastChanged", LastChanged.ToString("dd/MM/yyyy"));

                    k.Close();
                }
                rk.Close();
            }
        }

        public void Delete()
        {
            using (RegistryKey k = Parent.GetRootKey())
            {
                if (k != null && _key != null && _key != "")
                {
                    k.DeleteSubKeyTree(_key);
                    _key = null;
                }

                k.Close();
            }

            Group = null;
        }


        public void Open()
        {
            ChangedState = false;
            System.Diagnostics.Process.Start(URL);
            Save();
        }

        public static void SetHeader(HttpWebRequest Request, string Header, string Value)
        {
            // Retrieve the property through reflection.
            PropertyInfo PropertyInfo = Request.GetType().GetProperty(Header.Replace("-", string.Empty));
            // Check if the property is available.
            if (PropertyInfo != null)
            {
                // Set the value of the header.
                PropertyInfo.SetValue(Request, Value, null);
            }
            else
            {
                // Set the value of the header.
                Request.Headers[Header] = Value;
            }
        }


        String matchIcon(String relName)
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
            String url;
            String server = "";

            Match match = Regex.Match(URL, "^(http?://.*?)[/$]", RegexOptions.Singleline);
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

                url = matchIcon("icon");
                if (url == null) url = matchIcon("shortcut icon");
                if (url == null) matchIcon("favicon");

                if (url == null) { url = "/favicon.ico"; }

                if (Regex.Match(url, "^/").Success) url = server + url;

                if (!Regex.Match(url, "^http").Success)
                {
                    match = Regex.Match(URL, "^(.*)[/$]", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        url = match.Groups[1].Value + "/" + url;
                    }
                }

                try
                {
                    HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                    request.Method = "GET";
                    SetHeader(request, "User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)");

                    // make request for web page
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    using (Stream s = response.GetResponseStream())
                    {
                        try
                        {
                            Favicon = Image.FromStream(s);
                        }
                        catch (ArgumentException ex)
                        {
                            // todo: Bad image format
                        }
                        catch (IOException ex)
                        {
                            // todo: server exeption
                        }
                    }
                    response.Close();
                }
                catch (WebException ex)
                {
                    
                }
            }


        }

        public void GetHtml()
        {
            if (URL == null)
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
                if (Referer != null && Referer != "")
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

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(URL);
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

                if (Referer != null && Referer != "")
                    request.Referer = Referer;
                /*                else
                                    request.Referer = URL;
                                */
                if (Post != "" && Post != null)
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
                Html = websrc.ReadToEnd();
                response.Close();
                return;
            }
            catch (UriFormatException ex)
            {
                Status = State.Invalid;
                Complement = "URL invalide";
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    // if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    Status = State.Unavailable;
                    Complement = ex.ToString();

                    /*                   StreamReader websrc = new StreamReader(ex.Response.GetResponseStream());
                                       String src = websrc.ReadToEnd();

                                       ex.Response.Close();

                                       return src;
                                       */
                }
            }
            //catch (Exception ex)
            //{
            //    Status = "Unknown";
            //    Complement = ex.ToString();
            //}
            Html = null;
        }

        public String Result(out State status)
        {
            if (Html != null)
            {
                try
                {
                    Regex regex = new Regex(Expression, RegexOptions.Singleline, new TimeSpan(0, 0, 0, 1));
                    try
                    {
                        Match match = regex.Match(Html);
                        if (match.Success)
                        {
                            String value = "";
                            status = State.Ok;

                            for (int i = 1; i < match.Groups.Count; i++)
                            {
                                if (i > 1) value += " - ";
                                value += HttpUtility.HtmlDecode(match.Groups[i].Value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ")); ;
                            }
                            return value;
                        }
                        else
                        {
                                status = State.NotFound;
                        }
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        status = State.Invalid;
                        return ex.Message;
                    }

                }
                catch (System.ArgumentException ex)
                {
                        status = State.Invalid;
                        return ex.Message;
                }
            }
            status = State.NotChecked;
            return null;
        }
        public void Check(Object threadContext)
        {
            if (!Checking.GetToken()) return;

            LastChecked = DateTime.Now;

            Status = State.Running;

            GetHtml();

            State s;
            Value = Result(out s);
            Status = s;

            if (Key != null) Save();

            if (Favicon == null) GetFavicon();

             Queued.SetToken();
            Checking.SetToken();
        }

        public Matcher Clone()
        {

            Matcher m = new Matcher(Parent);
            m.Name = Name;
            m.URL = URL;
            m.Expression = Expression;
            m.Post = Post;
            m.Referer = Referer;
            m.GroupName = GroupName;

            //m._html = m.GetHtml();

            return m;
        }

        private bool _visible = true;
        public bool Visible
        {
            get
            {
                if ((Parent.ViewAll ?? false)) return true;
                if (IsNew) return true;
                if (ChangedState) return true;
                return false;
            }
        }

    }
}
