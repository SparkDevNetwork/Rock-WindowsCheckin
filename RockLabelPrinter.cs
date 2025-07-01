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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

using Newtonsoft.Json;

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

        #region V1 Printing Code

        /// <summary>
        /// Prints the labels.
        /// </summary>
        /// <param name="labelData">The label data.</param>
        /// <param name="hasPrinterCutter">if set to <c>true</c> the printer has a cutter; false otherwise.</param>
        public void PrintLabels( string labelData, bool hasPrinterCutter )
        {
            warnedPrinterError = false;

            var labels = JsonConvert.DeserializeObject<List<LabelItem>>(labelData);

            Dictionary<string, List<LabelItem>> labelsByAddress = SortLabelsByAddress(labels);

            //For each printer 
            foreach ( string labelAddress in labelsByAddress.Keys )
            {
                StringBuilder labelContents = new StringBuilder();

                int labelIndex = 0;
                foreach ( LabelItem label in labelsByAddress[labelAddress] )
                {
                    labelIndex++;
                    
                    // get label file & merge fields
                    var content = MergeLabelFields( GetLabelContents( label.LabelFile ), label.MergeFields ).TrimEnd();
                    
                    // If the "enable label cutting" feature is enabled, then we are going to
                    // control which mode the printer is in. In this case, we will remove any
                    // tear-mode (^MMT) commands from the content and add the cut-mode (^MMC).
                    if ( hasPrinterCutter )
                    {
                        content = content.Replace( "^MMT", string.Empty );

                        // Here we are forcing the printer into cut mode (because
                        // we don't know if it has been put into cut-mode already) even
                        // though we might be suppressing the cut below. This is correct.
                        content = ReplaceIfEndsWith( content, "^XZ", "^MMC^XZ" );

                        // If it's not the last label or a "ROCK_CUT" label, then inject
                        // a suppress back-feed (^XB) command which will also suppress the cut.
                        if ( ! ( labelIndex == labelsByAddress[labelAddress].Count || content.Contains( "ROCK_CUT" ) ) )
                        {
                            content = ReplaceIfEndsWith( content, "^XZ", "^XB^XZ" );
                        }
                    }

                    labelContents.Append( content );
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
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

                // get label from site
                using ( WebClient client = new WebClient() )
                {
                    labelContents = client.DownloadString( labelFile );
                }

                var rockConfig = RockConfig.Load();

                if ( rockConfig.IsCachingEnabled )
                {
                    CacheItemPolicy cachePolicy = new CacheItemPolicy();
                    cachePolicy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds( rockConfig.CacheLabelDuration );
                    //add an item to the cache   
                    cache.Set( labelFile, labelContents, cachePolicy );
                }
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
                // For USB printing we need to conver ^CI28 to ^CI27 inside of the label.
                // Per research from Lee Peterson
                // ^CI27 sets a Zebra printer to expect the code page Windows-1252 data as generated by the Win/USB app rather than
                // UTF -8 as expected with ^CI28, so extended characters print correctly.
                var usbLabelContent = labelContents.Replace( "^CI28", "^CI27" );
                RawPrinterHelper.SendStringToPrinter( rockConfig.PrinterOverrideLocal, usbLabelContent );
            }
            else if (!string.IsNullOrWhiteSpace(labelPrinterIp)) // else print to given IP
            {
                PrintViaIp( labelContents, labelPrinterIp );
            } else {
                MessageBox.Show( "No printer has been configured.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error );
            }
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
                    int printerPort = 9100;
                    var printerIpAddress = ipAddress;

                    // If the user specified in 0.0.0.0:1234 syntax then pull our the IP and port numbers.
                    if ( printerIpAddress.Contains( ":" ) )
                    {
                        var segments = printerIpAddress.Split( ':' );

                        printerIpAddress = segments[0];
                        int.TryParse( segments[1], out printerPort );
                    }

                    var printerEndpoint = new IPEndPoint( IPAddress.Parse( printerIpAddress ), printerPort );

                    Socket socket = null;
                   
                    socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
                    IAsyncResult result = socket.BeginConnect( printerEndpoint, null, null );
                    bool success = result.AsyncWaitHandle.WaitOne( 5000, true );

                    if ( socket.Connected )
                    {
                        var ns = new NetworkStream( socket );
                        byte[] toSend = System.Text.Encoding.UTF8.GetBytes( labelContents );
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

        /// <summary>
        /// Replaces string found at the very end of the content.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="suffix">The suffix.</param>
        /// <returns></returns>
        public static string ReplaceIfEndsWith( string content, string suffix, string replacement )
        {
            if ( content.EndsWith( suffix ) )
            {
                return content.Substring( 0, content.Length - suffix.Length ) + replacement;
            }
            else
            {
                return content;
            }
        }

        #endregion

        #region V2 Printing Code

        /// <summary>
        /// The command sequence to append to make the label cut. This replaces
        /// the normal ^XZ end sequence so we need to append that again.
        /// </summary>
        private static readonly byte[] _cutAndEndLabel = new byte[]
        {
            (byte)'^',
            (byte)'M',
            (byte)'M',
            (byte)'C',
            (byte)'^',
            (byte)'X',
            (byte)'Z',
            (byte)'\r',
            (byte)'\n'
        };

        /// <summary>
        /// The command sequence to append to make not backfeed which also
        /// supresses cutting. This replaces the normal ^XZ end sequence so we
        /// need to append that again.
        /// </summary>
        private static readonly byte[] _supressBackfeedAndEndLabel = new byte[]
        {
            (byte)'^',
            (byte)'X',
            (byte)'B',
            (byte)'^',
            (byte)'X',
            (byte)'Z',
            (byte)'\r',
            (byte)'\n'
        };

        /// <summary>
        /// Print the V2 labels from the JSON encoded list of labels.
        /// </summary>
        /// <param name="labelData">The JSON encoded list of labels.</param>
        /// <returns>A list of error messages.</returns>
        public async Task<List<string>> PrintV2Labels( string labelData )
        {
            var rockConfig = RockConfig.Load();
            var labels = JsonConvert.DeserializeObject<List<ClientLabelBag>>( labelData );

            if ( !string.IsNullOrEmpty( rockConfig.PrinterOverrideIp ) )
            {
                return await PrintV2LabelsToIpAsync( labels, rockConfig.PrinterOverrideIp, rockConfig.HasPrinterCutter );
            }
            else if ( !string.IsNullOrEmpty( rockConfig.PrinterOverrideLocal ) )
            {
                PrintV2LabelsToUsb( labels, rockConfig.PrinterOverrideLocal, rockConfig.HasPrinterCutter );

                return new List<string>();
            }
            else
            {
                var errorMessages = new List<string>();

                foreach ( var labelGroup in labels.GroupBy( l => l.PrinterAddress ) )
                {
                    // If no printer address is specified, then we can't print. Return
                    // one error message for each label in the group.
                    if ( string.IsNullOrWhiteSpace( labelGroup.Key ) )
                    {
                        errorMessages.AddRange( labelGroup.Select( _ => "No printer has been configured." ) );
                    }
                    else
                    {
                        var groupErrorMessages = await PrintV2LabelsToIpAsync( labelGroup.ToList(), labelGroup.Key, rockConfig.HasPrinterCutter );

                        errorMessages.AddRange( groupErrorMessages );
                    }
                }

                return errorMessages;
            }
        }

        /// <summary>
        /// Prints the V2 labels to an IP address.
        /// </summary>
        /// <param name="labels">The labels to be printed.</param>
        /// <param name="labelPrinterIp">The IP address to print the labels to.</param>
        /// <param name="printerHasCutter">Determines if the printer supports cutting.</param>
        /// <returns>A list of error messages.</returns>
        private async Task<List<string>> PrintV2LabelsToIpAsync( List<ClientLabelBag> labels, string labelPrinterIp, bool printerHasCutter )
        {
            var errorMessages = new List<string>();

            for ( int labelIndex = 0; labelIndex < labels.Count; labelIndex++ )
            {
                var label = labels[labelIndex];
                var data = Convert.FromBase64String( label.Data );

                if ( printerHasCutter )
                {
                    data = AmendWithCutCommands( data, labelIndex == labels.Count - 1 );
                }

                var message = await PrintViaIpAsync( data, labelPrinterIp );

                if ( !string.IsNullOrWhiteSpace( message ) )
                {
                    errorMessages.Add( message );
                }
            }

            return errorMessages;
        }

        /// <summary>
        /// Prints the V2 labels to an USB printer.
        /// </summary>
        /// <param name="labels">The labels to be printed.</param>
        /// <param name="printerName">The name of the printer to print the labels to.</param>
        /// <param name="printerHasCutter">Determines if the printer supports cutting.</param>
        /// <returns>A list of error messages.</returns>
        private void PrintV2LabelsToUsb( List<ClientLabelBag> labels, string printerName, bool printerHasCutter )
        {
            for ( int labelIndex = 0; labelIndex < labels.Count; labelIndex++ )
            {
                var label = labels[labelIndex];
                var data = Convert.FromBase64String( label.Data );

                if ( printerHasCutter )
                {
                    data = AmendWithCutCommands( data, labelIndex == labels.Count - 1 );
                }

                var zpl = Encoding.UTF8.GetString( data );

                // For USB printing we need to conver ^CI28 to ^CI27 inside of the label.
                // Per research from Lee Peterson
                // ^CI27 sets a Zebra printer to expect the code page Windows-1252 data as generated by the Win/USB app rather than
                // UTF -8 as expected with ^CI28, so extended characters print correctly.
                zpl = zpl.Replace( "^CI28", "^CI27" );
                RawPrinterHelper.SendStringToPrinter( printerName, zpl );
            }
        }

        /// <summary>
        /// Prints the label via IP address.
        /// </summary>
        /// <param name="labelContents">The label contents.</param>
        /// <param name="ipAddress">The ip address.</param>
        /// <returns>A string that contains an error message if the label failed to print or <c>null</c> otherwise.</returns>
        private async Task<string> PrintViaIpAsync( byte[] labelContents, string ipAddress )
        {
            try
            {
                int printerPort = 9100;
                var printerIpAddress = ipAddress;

                // If the user specified in 0.0.0.0:1234 syntax then pull our the IP and port numbers.
                if ( printerIpAddress.Contains( ":" ) )
                {
                    var segments = printerIpAddress.Split( ':' );

                    printerIpAddress = segments[0];
                    int.TryParse( segments[1], out printerPort );
                }

                var printerEndpoint = new IPEndPoint( IPAddress.Parse( printerIpAddress ), printerPort );
                var socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );

                try
                {
                    var connectTask = socket.ConnectAsync( printerEndpoint );

                    await Task.WhenAny( connectTask, Task.Delay( 5000 ) );

                    if ( socket.Connected )
                    {
                        using ( var ns = new NetworkStream( socket ) )
                        {
                            ns.Write( labelContents, 0, labelContents.Length );
                        }

                        return null;
                    }
                    else
                    {
                        return $"Could not connect to the printer {ipAddress}.";
                    }
                }
                finally
                {
                    if ( socket != null && socket.Connected )
                    {
                        socket.Shutdown( SocketShutdown.Both );
                        socket.Close();
                    }
                }
            }
            catch ( Exception ex )
            {
                return $"Could not connect to the printer {ipAddress}. The error was {ex.Message}.";
            }
        }

        /// <summary>
        /// Amends the print data with the cut commands required for the
        /// operation.
        /// </summary>
        /// <param name="labelContent">The print data.</param>
        /// <param name="isLastLabel"><c>true</c> if this is the last label to be printed.</param>
        /// <returns>A new array of bytes to be printed.</returns>
        private byte[] AmendWithCutCommands( byte[] labelContent, bool isLastLabel )
        {
            var index = Array.LastIndexOf( labelContent, ( byte ) '^' );

            // Ensure we have the expected last command.
            if ( index == -1 || index > labelContent.Length - 3 || labelContent[index + 1] != 'X' || labelContent[index + 2] != 'Z' )
            {
                return labelContent;
            }

            if ( isLastLabel )
            {
                // If this is the last label and it ends in ^XZ then insert the
                // cut command.
                var newContent = new byte[index - 1 + _cutAndEndLabel.Length];

                labelContent.CopyTo( newContent, 0 );
                _cutAndEndLabel.CopyTo( newContent, newContent.Length - _cutAndEndLabel.Length );

                return newContent;
            }
            else
            {
                // If this is not the last label and it ends in ^XZ then insert the
                // supress backfeed command.
                var newContent = new byte[index - 1 + _supressBackfeedAndEndLabel.Length];

                labelContent.CopyTo( newContent, 0 );
                _supressBackfeedAndEndLabel.CopyTo( newContent, newContent.Length - _supressBackfeedAndEndLabel.Length );

                return newContent;
            }
        }

        #endregion
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

    class ClientLabelBag
    {
        public string PrinterAddress { get; set; }

        public string Data { get; set; }
    }
}