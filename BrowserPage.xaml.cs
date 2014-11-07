// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using EO.WebBrowser;

namespace CheckinClient
{
    /// <summary>
    /// Interaction logic for BrowserPage.xaml
    /// </summary>
    public partial class BrowserPage : Page
    {

        int closeClickBuffer = 0;
        DispatcherTimer closeButtonRestartTimer = new DispatcherTimer();
        
        public BrowserPage()
        {
            InitializeComponent();
        }

        private void frmMain_Loaded( object sender, RoutedEventArgs e )
        {
            closeButtonRestartTimer.Tick += new EventHandler( closeButtonRestartTimer_Tick );
            closeButtonRestartTimer.Interval = new TimeSpan( 0, 0, 10 );

            wcWebControl.AllowDrop = false;
            wvWebView.CustomUserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.170 Safari/537.36 -rockwinclient2.0-";
            wvWebView.RegisterJSExtensionFunction( "printLabels", new JSExtInvokeHandler( WebView_PrintLabels ) );
            EO.WebBrowser.Runtime.CachePath = System.AppDomain.CurrentDomain.BaseDirectory + "Cache";
            
            var rockConfig = RockConfig.Load();
            wvWebView.Url = new Uri( rockConfig.CheckinAddress ).ToString();

            puOverlay.IsOpen = true;
        }

        void WebView_PrintLabels( object sender, JSExtInvokeArgs e )
        {
            string tagJson = e.Arguments[0] as string;

            RockLabelPrinter printer = new RockLabelPrinter();
            printer.PrintLabels( tagJson );
        }

        private void btnClose_Click( object sender, RoutedEventArgs e )
        {
            // start a timer to clear the close buffer if the user releases the button
            if ( closeClickBuffer == 0 )
                closeButtonRestartTimer.Start();

            closeClickBuffer++;

            if ( closeClickBuffer > 300 )
                Application.Current.Shutdown();
        }

        // resets the close counter
        private void closeButtonRestartTimer_Tick( object sender, EventArgs e )
        {
            closeClickBuffer = 0;
            closeButtonRestartTimer.Stop();
        }
    }
}
