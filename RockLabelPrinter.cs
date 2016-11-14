﻿// <copyright>
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
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.Text;

namespace CheckinClient
{
    /// <summary>
    /// 
    /// </summary>
    [PermissionSet( SecurityAction.Demand, Name = "FullTrust" )]
    public class RockLabelPrinter
    {
        ObjectCache cache;
        bool warnedPrinterError = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="RockCheckinScriptManager"/> class.
        /// </summary>
        /// <param name="p">The p.</param>
        public RockLabelPrinter( )
        {
            cache = MemoryCache.Default;
        }

        /// <summary>
        /// Prints the labels.
        /// </summary>
        /// <param name="labelData">The label data.</param>
        public void PrintLabels( string labelData )
        {
            warnedPrinterError = false;

            var labels = JsonConvert.DeserializeObject<List<LabelItem>>(labelData);

            Dictionary<string, List<LabelItem>> labelsByAddress = SortLabelsByAddress(labels);

            //For each printer 
            foreach ( string labelAddress in labelsByAddress.Keys)
            {
                StringBuilder labelContents = new StringBuilder();

                foreach (LabelItem label in labelsByAddress[labelAddress])
                {
                    // get label file & merge fields
                    labelContents.Append(MergeLabelFields(GetLabelContents(label.LabelFile), label.MergeFields));
                }
                // print label
                PrintLabel( labelContents.ToString(), labelAddress );
            }

            //RawPrinterHelper.SendStringToPrinter( "ZDesigner GX420d (Copy 1)", s );
        }

        /// <summary>
        /// Puts labels into a dictionary 
        /// </summary>
        /// <param name="labels">List of label items.</param>
        /// <returns></returns>
        private Dictionary<string, List<LabelItem>> SortLabelsByAddress(List<LabelItem> labels)
        {
            Dictionary<string, List<LabelItem>> labelsByAddress = new Dictionary<string, List<LabelItem>>();

            foreach(var label in labels)
            {
                if (!labelsByAddress.ContainsKey(label.PrinterAddress))
                {
                    labelsByAddress[label.PrinterAddress] = new List<LabelItem>();
                }

                labelsByAddress[label.PrinterAddress].Add(label);

            }

            return labelsByAddress;
        }

        /// <summary>
        /// Gets the label contents.
        /// </summary>
        /// <param name="labelFile">The label file.</param>
        /// <returns></returns>
        private string GetLabelContents( string labelFile )
        {
            string labelContents = string.Empty;

            if ( cache.Contains( labelFile ) )
            {
                //get an item from the cache  
                labelContents = cache.Get( labelFile ).ToString();
            }
            else
            {
                // get label from site
                using ( WebClient client = new WebClient() )
                {
                    labelContents = client.DownloadString( labelFile );
                }

                var rockConfig = RockConfig.Load();

                CacheItemPolicy cachePolicy = new CacheItemPolicy();
                cachePolicy.AbsoluteExpiration = new DateTimeOffset( DateTime.Now.AddSeconds( rockConfig.CacheLabelDuration ) );
                //add an item to the cache   
                cache.Add( labelFile, labelContents, cachePolicy );
            }

            return labelContents;
        }

        /// <summary>
        /// Merges the label fields.
        /// </summary>
        /// <param name="labelContents">The label contents.</param>
        /// <param name="mergeFields">The merge fields.</param>
        /// <returns></returns>
        private string MergeLabelFields( string labelContents, Dictionary<string, string> mergeFields )
        {
            foreach ( var mergeField in mergeFields )
            {
                if ( !string.IsNullOrWhiteSpace( mergeField.Value ) )
                {
                    labelContents = Regex.Replace( labelContents, string.Format( @"(?<=\^FD){0}(?=\^FS)", mergeField.Key ), mergeField.Value );
                }
                else
                {
                    // Remove the box preceding merge field
                    labelContents = Regex.Replace( labelContents, string.Format( @"\^FO.*\^FS\s*(?=\^FT.*\^FD{0}\^FS)", mergeField.Key ), string.Empty );
                    // Remove the merge field
                    labelContents = Regex.Replace( labelContents, string.Format( @"\^FD{0}\^FS", mergeField.Key ), "^FD^FS" );
                }
            }

            return labelContents;
        }

        /// <summary>
        /// Prints the label.
        /// </summary>
        /// <param name="labelContents">The label contents.</param>
        /// <param name="labelPrinterIp">The label printer ip.</param>
        private void PrintLabel( string labelContents, string labelPrinterIp )
        {
            var rockConfig = RockConfig.Load();
            
            // if IP override
            if ( !string.IsNullOrEmpty(rockConfig.PrinterOverrideIp) )
            {
                PrintViaIp( labelContents, rockConfig.PrinterOverrideIp );
            }
            else if ( !string.IsNullOrEmpty(rockConfig.PrinterOverrideLocal) ) // if printer local
            {
                RawPrinterHelper.SendStringToPrinter( rockConfig.PrinterOverrideLocal, labelContents );
            }
            else if (!string.IsNullOrWhiteSpace(labelPrinterIp)) // else print to given IP
            {
                PrintViaIp( labelContents, labelPrinterIp );
            } else {
                MessageBox.Show( "No printer has been configured.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error );
            }
        }

        /// <summary>
        /// Prints a test label to the chosen hardware printer.
        /// </summary>
        public void TestPrint()
        {
            var text = @"CT~~CD,~CC^~CT~
^XA~TA000~JSN^LT0^MNW^MTD^PON^PMN^LH0,0^JMA^PR4,4~SD15^JUS^LRN^CI0^XZ
^XA
^MMT
^PW609
^LL0406
^LS0
^FT275,210^A0N,28,28^FH\^FDTEST^FS
^PQ1,0,1,Y^XZ
";
            var rockConfig = RockConfig.Load();
            RawPrinterHelper.SendStringToPrinter( rockConfig.PrinterOverrideLocal, text );
        }

        /// <summary>
        /// Prints the via ip.
        /// </summary>
        /// <param name="labelContents">The label contents.</param>
        /// <param name="ipAddress">The ip address.</param>
        private void PrintViaIp( string labelContents, string ipAddress )
        {
            try
            {
                if ( !warnedPrinterError )
                {
                    Socket socket = null;
                    var printerIp = new IPEndPoint( IPAddress.Parse( ipAddress ), 9100 );

                    socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
                    IAsyncResult result = socket.BeginConnect( printerIp, null, null );
                    bool success = result.AsyncWaitHandle.WaitOne( 5000, true );

                    if ( socket.Connected )
                    {
                        var ns = new NetworkStream( socket );
                        byte[] toSend = System.Text.Encoding.ASCII.GetBytes( labelContents );
                        ns.Write( toSend, 0, toSend.Length );
                    }
                    else
                    {

                        MessageBox.Show( String.Format( "Could not connect to the printer {0}.", ipAddress ), "Print Error", MessageBoxButton.OK, MessageBoxImage.Error );
                        warnedPrinterError = true;
                    }

                    if ( socket != null && socket.Connected )
                    {
                        socket.Shutdown( SocketShutdown.Both );
                        socket.Close();
                    }
                }
            }
            catch ( Exception ex )
            {
                MessageBox.Show( String.Format( "Could not connect to the printer {0}. The error was {1}.", ipAddress, ex.Message ), "Print Error", MessageBoxButton.OK, MessageBoxImage.Error );
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class LabelItem
    {
        public int? PrinterDeviceId { get; set; }
        public string PrinterAddress { get; set; }
        public string LabelFile { get; set; }
        public Dictionary<string, string> MergeFields { get; set; }
    }
}