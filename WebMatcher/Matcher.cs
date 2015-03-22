using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web;
using System.Collections.Specialized;
using System.Threading;
using System.Security.Permissions;
using System.Security;

namespace WebMatcher
{
    public delegate void NotifyHandler(Matcher matcher);

    public class Matcher : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void changed(String name)
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
                if (_group != null) _group.Remove(this);
                _group = value;
                _group.Add(this);
                changed("Group");
                changed("GroupName");
            }
        }
        String _key;

        String _value = "";
        String _status = "";
        String _name = "";
        String _html = "";
        Boolean _changedState = false;
        public Boolean Checking = false;
        public Boolean Queued = false;
        public Boolean ForcedCheck = true; // TODO: could be false ?
        public Boolean AutoRefresh = true;
        Image _favicon;

        public String Name
        {
            get { return _name; }
            set { _name = value; changed("Name"); }
        }
        public String Key
        {
            get { return _key; }
        }
        public String URL { get; set; }
        public String Expression { get; set; }
        public String Post { get; set; }
        public String Referer { get; set; }
        public String Html
        {
            get { return _html; }
            set { _html = value; changed("Html"); }
        }
        public String Value
        {
            get { return _value; }
            set { _value = value; changed("Value"); }
        }

        public Image Favicon
        {
            get { return _favicon; }
            set { _favicon = value; changed("FaviconSource"); }
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
        public String Complement { get; set; }

        // TODO : should be done via event here
        /*        public static void SetAppIcon()
                {
                    Icon icn = WebMatcher.Properties.Resources.App;
                    foreach (Matcher m in Matchers) { if (m.ChangedState) icn = WebMatcher.Properties.Resources.AppOk; }
                    if (Notify.Icon != icn) Notify.Icon = icn;
                }
                */

        public Boolean IsNew
        {
            get { return _key == null; }
        }

        public Boolean ChangedState
        {
            get { return _changedState; }
            set
            {
                if (value != _changedState)
                {
                    _changedState = value;
                    /*
                                        foreach(Matcher m in Matchers)
                                        {
                                            if (m.Group == Group) m.changed("GroupExpanded");
                                        }
                                       if (Win != null )
                                        {
                                            Win.Dispatcher.BeginInvoke (
                                                new Action(delegate() 
                                                  {
                                                      if (AutoRefresh)
                                                      Win.lstMatchers_Refresh();
                                                  }
                                               ));
                                        }
                                        SetAppIcon();
                                        */
                    if (_changedState) LastChanged = LastChecked;
                    changed("Changed");
                }
            }
        }

        public DateTime LastChecked { get; set; }
        public DateTime LastChanged { get; set; }
        public String Status
        {
            get { return _status; }
            set
            {
                _status = value;
                changed("Status");
            }
        }

        public bool TimeToCheck
        {
            get
            {
                if (Checking) return false;
                if (Queued) return false;
                if (ForcedCheck) return true;
                if (LastChecked + Parent.Interval < DateTime.Now) return true;

                return false;
            }
        }


        public Matcher(Matchers parent)
        {
            _parent = parent;
            Name = "<nouveau>";
            URL = "http://";
            Expression = "";
            GroupName = "";
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
                try
                {
                    k.DeleteValue(key);
                }
                catch (Exception ex)
                {
                }
            }
            else
                k.SetValue(key, value, RegistryValueKind.String);

        }

        public string GroupName
        {
            get { return Group.Name; }
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

        public void Load(String key)
        {
            _key = key;
            using (RegistryKey rk = Parent.GetRootKey())
            {
                using (RegistryKey k = rk.OpenSubKey(key))
                {
                    if (k != null)
                    {
                        Name = LoadString(k, "Name");
                        GroupName = LoadString(k, "Group");
                        URL = LoadString(k, "URL", "http://");
                        Expression = LoadString(k, "Expression");
                        Post = LoadString(k, "Post");
                        Referer = LoadString(k, "Referer");

                        Value = LoadString(k, "Value");
                        Status = LoadString(k, "Status", "Ok");
                        ChangedState = (LoadString(k, "Changed", "False") == "True") ? true : false;

                        LastChecked = DateTime.ParseExact(LoadString(k, "LastChecked", "01/01/0001"), "dd/MM/yyyy", null);
                        LastChanged = DateTime.ParseExact(LoadString(k, "LastChanged", "01/01/0001"), "dd/MM/yyyy", null);
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

                    SaveString(k, "Status", Status);
                    SaveString(k, "Changed", ChangedState ? "True" : "False");
                    SaveString(k, "LastChecked", LastChecked.ToString("dd/MM/yyyy"));
                    SaveString(k, "LastChanged", LastChanged.ToString("dd/MM/yyyy"));

                    k.Close();
                }
                rk.Close();
            }
        }

        public void delete()
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
                        Favicon = Image.FromStream(s);
                    }
                    response.Close();
                }
                catch (Exception ex)
                {
                }
            }


        }

        public void GetHtml()
        {
            if (Name == "BD-Writer SE506AB")
            { }
            if (URL == null)
            {
                if (Html != null)
                {
                    Html = null;
                    ChangedState = true;
                }
                Status = "Invalid";
                Complement = "URL invalide";
                return;
            }

            try
            {
                String ViewState = "";

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

                // make request for web page
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader websrc = new StreamReader(response.GetResponseStream(), System.Text.Encoding.GetEncoding("iso-8859-1"));
                Html = websrc.ReadToEnd();
                response.Close();
                return;
            }
            catch (UriFormatException ex)
            {
                Status = "Invalid";
                Complement = "URL invalide";
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    // if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    Status = "Unavailable";
                    Complement = ex.ToString();

                    /*                   StreamReader websrc = new StreamReader(ex.Response.GetResponseStream());
                                       String src = websrc.ReadToEnd();

                                       ex.Response.Close();

                                       return src;
                                       */
                }
            }
            catch (Exception ex)
            {
                Status = "Unknown";
                Complement = ex.ToString();
            }
            Html = null;
        }

        public bool GetResult()
        {
            LastChecked = DateTime.Now;
            if (Html != null)
            {
                try
                {
                    Match match = Regex.Match(Html, Expression, RegexOptions.Singleline);

                    if (match.Success)
                    {
                        String tmpValue = "";
                        Status = "Ok";

                        for (int i = 1; i < match.Groups.Count; i++)
                        {
                            if (i > 1) tmpValue += " - ";
                            tmpValue += HttpUtility.HtmlDecode(match.Groups[i].Value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ")); ;
                        }
                        if (tmpValue != Value)
                        {
                            Value = tmpValue;
                            ChangedState = true;
                            return true;
                        }
                    }
                    else
                    {
                        if (Status != "NotFound")
                        {
                            Status = "NotFound";
                            Complement = "Expression non trouvée";
                        }
                    }
                }
                catch (System.ArgumentException ex)
                {
                    if (Value != ex.Message)
                    {
                        Value = ex.Message;
                        return true;
                    }
                }
            }
            return false;
        }
        public void Check(Object threadContext)
        {
            if (Checking) return;
            ForcedCheck = false;
            Checking = true;

            Status = "En cours...";

            GetHtml();
            if (GetResult())
            {
                if (Key != null) Save();
            }

            if (Favicon == null) GetFavicon();

            Checking = false;
            Queued = false;
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

    }
}
