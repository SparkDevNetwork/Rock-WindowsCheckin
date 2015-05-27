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
            SetWebBrowserFeatures();
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

        static void SetWebBrowserFeatures()
        {
            // don't change the registry if running in-proc inside Visual Studio
            //if ( LicenseManager.UsageMode != LicenseUsageMode.Runtime )
            //    return;

            var appName = System.IO.Path.GetFileName( System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName );

            var featureControlRegKey = @"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\";

            Registry.SetValue( featureControlRegKey + "FEATURE_BROWSER_EMULATION",
                appName, GetBrowserEmulationMode(), RegistryValueKind.DWord );

            // enable the features which are "On" for the full Internet Explorer browser

            Registry.SetValue( featureControlRegKey + "FEATURE_ENABLE_CLIPCHILDREN_OPTIMIZATION",
                appName, 1, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_AJAX_CONNECTIONEVENTS",
                appName, 1, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_GPU_RENDERING",
                appName, 1, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_WEBOC_DOCUMENT_ZOOM",
                appName, 1, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_NINPUT_LEGACYMODE",
                appName, 0, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_LOCALMACHINE_LOCKDOWN",
                appName, 0, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_BLOCK_LMZ_SCRIPT",
                appName, 0, RegistryValueKind.DWord );

            Registry.SetValue( featureControlRegKey + "FEATURE_BLOCK_LMZ_OBJECT",
                appName, 0, RegistryValueKind.DWord );

            // turn off navigation sounds in IE
            RegistryKey key = Registry.CurrentUser.OpenSubKey( @"AppEvents\Schemes\Apps\Explorer\Navigating\.Current", true );
            key.SetValue( null, "", RegistryValueKind.ExpandString );
        }

        static UInt32 GetBrowserEmulationMode()
        {
            int browserVersion = 0;
            using ( var ieKey = Registry.LocalMachine.OpenSubKey( @"SOFTWARE\Microsoft\Internet Explorer",
                RegistryKeyPermissionCheck.ReadSubTree,
                System.Security.AccessControl.RegistryRights.QueryValues ) )
            {
                var version = ieKey.GetValue( "svcVersion" );
                if ( null == version )
                {
                    version = ieKey.GetValue( "Version" );
                    if ( null == version )
                        throw new ApplicationException( "Microsoft Internet Explorer is required!" );
                }
                int.TryParse( version.ToString().Split( '.' )[0], out browserVersion );
            }

            if ( browserVersion < 7 )
            {
                throw new ApplicationException( "Unsupported version of Microsoft Internet Explorer!" );
            }

            UInt32 mode = 11000; // Internet Explorer 11. Webpages containing standards-based !DOCTYPE directives are displayed in IE11 Standards mode. 

            switch ( browserVersion )
            {
                case 7:
                    mode = 7000; // Webpages containing standards-based !DOCTYPE directives are displayed in IE7 Standards mode. 
                    break;
                case 8:
                    mode = 8000; // Webpages containing standards-based !DOCTYPE directives are displayed in IE8 mode. 
                    break;
                case 9:
                    mode = 9000; // Internet Explorer 9. Webpages containing standards-based !DOCTYPE directives are displayed in IE9 mode.                    
                    break;
                case 10:
                    mode = 10000; // Internet Explorer 10.
                    break;
            }

            return mode;
        }

        
    }
}
