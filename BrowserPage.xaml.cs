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
        int closeTouchCount = 0;
        DispatcherTimer closeButtonRestartTimer = new DispatcherTimer();
        
        public BrowserPage()
        {
            InitializeComponent();
        }

        private void frmMain_Loaded( object sender, RoutedEventArgs e )
        {
            closeButtonRestartTimer.Tick += new EventHandler( closeButtonRestartTimer_Tick );
            closeButtonRestartTimer.Interval = new TimeSpan( 0, 0, 10 );

            

            //wcWebControl.AllowDrop = false;
            //wvWebView.CustomUserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.170 Safari/537.36 -rockwinclient2.0-";
            //wvWebView.RegisterJSExtensionFunction( "printLabels", new JSExtInvokeHandler( WebView_PrintLabels ) );
            //EO.WebBrowser.Runtime.CachePath = System.IO.Path.GetTempPath() + @"RockCheckin\Cache"; // System.AppDomain.CurrentDomain.BaseDirectory + "Cache";
            /*EO.WebBrowser.Runtime.AddLicense(
                "9+z29umMQ7Oz/RTinuX39umMQ3Xj7fQQ7azcwp61n1mXpM0X6Jzc8gQQyJ21" +
                "usfftnKtu8XfsHWm8PoO5Kfq6doPvUaBpLHLn3Xj7fQQ7azc6c/nrqXg5/YZ" +
                "8p7cwp61n1mXpM0M66Xm+8+4iVmXpLHLn1mXwPIP41nr/QEQvFu807/u56vm" +
                "8fbNn6/c9gQU7qe0psPZr1uXs8+4iVmXpLHnrprj8AAivUaBpLHLn3Xm9vUQ" +
                "8YLl6gDL45rr6c7NsGqmu8Ddr2qrprEh5Kvq7QAZvFupprHavUaBpLHLn3Xq" +
                "7fgZ4K3s9vbpz5Ors+fw15/I88Aeso3B8sEjxIPI1c7ou2jq7fgZ4K0=" ); */
            var rockConfig = RockConfig.Load();

            wbWebBrowser.AllowDrop = true;
            wbWebBrowser.Navigate( rockConfig.CheckinAddress.ToString() );

            //wvWebView.Url = new Uri( rockConfig.CheckinAddress ).ToString();
            //wvWebView.ZoomFactor = rockConfig.ZoomLevel;

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

            closeTouchCount++;

            if ( closeTouchCount >= 6 )
                Application.Current.Shutdown();
        }

        // resets the close counter
        private void closeButtonRestartTimer_Tick( object sender, EventArgs e )
        {
            closeTouchCount = 0;
            closeClickBuffer = 0;
            closeButtonRestartTimer.Stop();
        }
    }
}
