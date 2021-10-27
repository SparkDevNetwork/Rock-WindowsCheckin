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
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace CheckinClient
{
    /// <summary>
    /// Interaction logic for BrowserPage.xaml
    /// </summary>
    /// 

    /* 
        JME 1/8/2021
        Updated this to use the new WebView2 from Microsoft which gives us a Chromium browser. Couple of things to note:
        1. This does require that the WebView2 runtime is installed. https://docs.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
        2. Finding a way to tell the browser that it's running inside of this application proved tricky. While you can update request headers for
           each request, JavaScript can't read these headers when the request is over. I tried to update the "user-agent" header to add a string
           noting that this was the windows check-in client. That works, but navigator.userAgent doesn't use this new value :(
           There is talk about Microsoft allowing you to edit the useragent in the future. I left the code to update request headers in in case
           we wanted that in the future. For now the client JavaScript will look for the existence of the method 'window.chrome.webview.postMessage'.
        3. The event from the client uses a POCO. This will allow for other types of events in the future. Technically, we don't need this now, but if
           we wanted to add a new type of event in the future we have support for that.
        4. If we want to hide the context menu in the future you can with this code. https://docs.microsoft.com/en-us/microsoft-edge/webview2/howto/js#scenario--removing-the-context-menu
           I left it in for now as it is really nice to debug with. Maybe a future setting to enable / disable.
    */

    public partial class BrowserPage : Page
    {

        private int _closeClickBuffer = 0;
        private int _closeTouchCount = 0;
        private DispatcherTimer _closeButtonRestartTimer = new DispatcherTimer();
        private RockConfig _rockConfig = null;

        private const string RUNTIME_DOWNLOAD_LOCATION = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        /// <summary>
        /// Initializes a new instance of the <see cref="BrowserPage"/> class.
        /// </summary>
        public BrowserPage()
        {
            InitializeComponent();
            InitializeWebBrowserAsync();

            _rockConfig = RockConfig.Load();
        }

        /// <summary>
        /// Handles the Loaded event of the frmMain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void frmMain_Loaded( object sender, RoutedEventArgs e )
        {
            _closeButtonRestartTimer.Tick += new EventHandler( closeButtonRestartTimer_Tick );
            _closeButtonRestartTimer.Interval = new TimeSpan( 0, 0, 10 );

            // Set the close button overlay panel to open
            puOverlay.IsOpen = true;
        }

        /// <summary>
        /// Initializes the WebBrowser asynchronous.
        /// </summary>
        async void InitializeWebBrowserAsync()
        {
            // Wait for the control to load
            try
            {
                var checkinClientUserDataFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "Spark_Development_Network", "CheckinClient" );
                Directory.CreateDirectory( checkinClientUserDataFolder );

                // specify the userFolder to something the user has access to
                CoreWebView2Environment coreWebView2Environment = null;
                await CoreWebView2Environment.CreateAsync( userDataFolder: checkinClientUserDataFolder )
                    .ContinueWith( a => coreWebView2Environment = a.Result );

                await wbWebBrowser.EnsureCoreWebView2Async( coreWebView2Environment );

                // Setup event to receive messages back from the content's JavaScript.
                wbWebBrowser.WebMessageReceived += WbWebBrowser_WebMessageReceived;

                // Setup event to allow for us to inject a request header that allows the content to know that it's running inside of the check-in windows host
                wbWebBrowser.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                wbWebBrowser.CoreWebView2.AddWebResourceRequestedFilter( "*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Document );
                wbWebBrowser.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;

                // Navigate to the configured start page
                wbWebBrowser.CoreWebView2.Navigate( _rockConfig.CheckinAddress );
            }
            catch( Exception )
            {
                var result = MessageBox.Show( $"We were not able to initialize the embedded web browser component. Please ensure that the Microsoft Edge WebView2 run-time is installed. You can download it from the address below: \n\n {RUNTIME_DOWNLOAD_LOCATION} \n\n Would you like to download the run-time now?", "Rock Check-in", MessageBoxButton.YesNo, MessageBoxImage.Information );

                switch ( result )
                {
                    case MessageBoxResult.Yes:
                        System.Diagnostics.Process.Start( RUNTIME_DOWNLOAD_LOCATION );
                        break;
                }

                Application.Current.Shutdown();
            }            
        }

        /// <summary>
        /// Handles the PermissionRequested event of the CoreWebView2 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CoreWebView2PermissionRequestedEventArgs"/> instance containing the event data.</param>
        private void CoreWebView2_PermissionRequested( object sender, CoreWebView2PermissionRequestedEventArgs e )
        {
            if ( e.PermissionKind == CoreWebView2PermissionKind.Camera )
            {
                // Allow use of Camera thru browser
                e.State = CoreWebView2PermissionState.Allow;
            }
        }

        /// <summary>
        /// Handles the WebResourceRequested event of the CoreWebView2 control. This adds a header that allows the content to know it's running inside of the check-in windows host
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs"/> instance containing the event data.</param>
        private void CoreWebView2_WebResourceRequested( object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs e )
        {
            //e.Request.Headers.SetHeader( "user-agent", e.Request.Headers.GetHeader( "user-agent" ) + " (Rock-Checkin-Client-v4)" );  // see engineering note above before removing this
        }

        /// <summary>
        /// Handles the WebMessageReceived event of the WbWebBrowser control. This is the event that javascript will call to make requests into the host application.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs"/> instance containing the event data.</param>
        private void WbWebBrowser_WebMessageReceived( object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e )
        {
            // Process the event from the client app
            try
            {
                var clientEvent = JsonConvert.DeserializeObject<BrowserEvent>( e.WebMessageAsJson );

                switch ( clientEvent.EventName )
                {
                    case "PRINT_LABELS":
                        {
                            if ( !_rockConfig.IsPrintingDisabled )
                            {
                                RockLabelPrinter printer = new RockLabelPrinter();
                                printer.PrintLabels( clientEvent.EventData, _rockConfig.HasPrinterCutter );
                            }
                            break;
                        }
                }
            }
            catch ( Exception )
            {
                MessageBox.Show( "An invalid request was recieved from the client.", "Rock Check-in", MessageBoxButton.OK, MessageBoxImage.Warning );
            }
        }

        /// <summary>
        /// Handles the Click event of the btnClose control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnClose_Click( object sender, RoutedEventArgs e )
        {
            // start a timer to clear the close buffer if the user releases the button
            if ( _closeClickBuffer == 0 )
            {
                _closeButtonRestartTimer.Start();
            }

            _closeTouchCount++;

            if ( _closeTouchCount >= 6 )
            {
                Application.Current.Shutdown();
            }
        }

        // Resets the close counter
        private void closeButtonRestartTimer_Tick( object sender, EventArgs e )
        {
            _closeTouchCount = 0;
            _closeClickBuffer = 0;
            _closeButtonRestartTimer.Stop();
        }

        /// <summary>
        /// POCO for handeling browser events
        /// </summary>
        private class BrowserEvent
        {
            /// <summary>
            /// Gets or sets the name of the event.
            /// </summary>
            /// <value>
            /// The name of the event.
            /// </value>
            [JsonProperty("eventName")]
            public string EventName { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the event data.
            /// </summary>
            /// <value>
            /// The event data.
            /// </value>
            [JsonProperty( "eventData" )]
            public string EventData { get; set; } = string.Empty;
        }
    }
}
