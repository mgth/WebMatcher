using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace WebMatcher
{
    class DomParser : ApplicationContext
    {
        private WebBrowser _browser;
        private Uri _url;
        public string Html = "";
        Thread thrd;

        private System.Threading.Timer _timer;

        //public bool Parsed = false;

        public event EventHandler<string> Parsed;

        public DomParser(Uri url, EventHandler<string> handler)
        {
            _url = url;

            Parsed += handler;

            thrd = new Thread(new ThreadStart(
                delegate {
                             _timer = new Timer(TimerTask);

                             _browser = new WebBrowser();
                             _browser.ScriptErrorsSuppressed = true;
                             _browser.DocumentCompleted += _browser_DocumentCompleted;
                             _browser.Navigating += _browser_Navigating;
                             _browser.ProgressChanged += BrowserOnProgressChanged;
                             _timer.Change(10000, 0);
                             _browser.Navigate(_url);
                             Application.Run(this);
                         }));
            thrd.SetApartmentState(ApartmentState.STA);

            thrd.Start();
        }

        private void TimerTask(object stateObj)
        {
            _timer.Dispose();
            //_browser.Dispose();
            this.ExitThread();
            Parsed?.Invoke(this, "");
        }

        private void BrowserOnProgressChanged(object sender, WebBrowserProgressChangedEventArgs args)
        {
            _timer.Change(10000, 0);
        }

        private void _browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            _timer.Dispose();
            Html = _browser?.Document?.Body?.InnerHtml;
            _browser?.Dispose();
            Parsed?.Invoke(this,Html);
        }

        private void _browser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (thrd != null)
            {
                thrd.Abort();
                thrd = null;
                return;
            }

            try
            {
                System.Runtime.InteropServices.Marshal.Release(_browser.Handle);
                _browser.Dispose();
                
            }
            catch(ObjectDisposedException)
            { }

            base.Dispose(disposing);
        }

    }
}
