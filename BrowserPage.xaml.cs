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
using System.Diagnostics;
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
using Microsoft.Win32;

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
            SetBrowserCompatibilityMode();
            InitializeComponent();
        }

        private void frmMain_Loaded( object sender, RoutedEventArgs e )
        {
            closeButtonRestartTimer.Tick += new EventHandler( closeButtonRestartTimer_Tick );
            closeButtonRestartTimer.Interval = new TimeSpan( 0, 0, 10 );

            var rockConfig = RockConfig.Load();

            ScriptDirector scriptDirector = new ScriptDirector( this );
            wbWebBrowser.ObjectForScripting = scriptDirector;
            wbWebBrowser.AllowDrop = true;

            wbWebBrowser.Navigate( rockConfig.CheckinAddress.ToString() );

            puOverlay.IsOpen = true;
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

        

        private void SetBrowserCompatibilityMode()
        {
            // http://msdn.microsoft.com/en-us/library/ee330720(v=vs.85).aspx

            // FeatureControl settings are per-process
            var fileName = System.IO.Path.GetFileName( Process.GetCurrentProcess().MainModule.FileName );

            if ( String.Compare( fileName, "devenv.exe", true ) == 0 ) // make sure we're not running inside Visual Studio
                return;

            using ( var key = Registry.CurrentUser.CreateSubKey( @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION",
                RegistryKeyPermissionCheck.ReadWriteSubTree ) )
            {
                // Webpages containing standards-based !DOCTYPE directives are displayed in IE10 Standards mode.
                UInt32 mode = 11000; // 11000 = IE11, 10000 = IE10, 9000 = IE9, 8000 = IE8, 7000 = IE7; 
                key.SetValue( fileName, mode, RegistryValueKind.DWord );
            }

            using ( var key = Registry.CurrentUser.CreateSubKey( @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_ZONE_ELEVATION",
                RegistryKeyPermissionCheck.ReadWriteSubTree ) )
            {
                // disable zone elevation prevention
                UInt32 mode = 0;
                key.SetValue( fileName, mode, RegistryValueKind.DWord );
            }

            using ( var key = Registry.CurrentUser.CreateSubKey( @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BLOCK_LMZ_SCRIPT",
                RegistryKeyPermissionCheck.ReadWriteSubTree ) )
            {
                // enable <scripts> in local machine zone
                UInt32 mode = 0;
                key.SetValue( fileName, mode, RegistryValueKind.DWord );
            }

            using ( var key = Registry.CurrentUser.CreateSubKey( @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_NINPUT_LEGACYMODE",
                RegistryKeyPermissionCheck.ReadWriteSubTree ) )
            {
                // disable Legacy Input Model
                UInt32 mode = 0;
                key.SetValue( fileName, mode, RegistryValueKind.DWord );
            }

        }
    }
}
