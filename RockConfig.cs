﻿using System;
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
using System.Configuration;

namespace CheckinClient
{
    /// <summary>
    /// 
    /// </summary>
    internal sealed partial class RockConfig : ApplicationSettingsBase
    {
        /// <summary>
        /// The default instance
        /// </summary>
        private static RockConfig defaultInstance = ( (RockConfig)( ApplicationSettingsBase.Synchronized( new RockConfig() ) ) );

        /// <summary>
        /// Gets the default.
        /// </summary>
        /// <value>
        /// The default.
        /// </value>
        public static RockConfig Default
        {
            get
            {
                return defaultInstance;
            }
        }

        /// <summary>
        /// Gets or sets the checkin address.
        /// </summary>
        /// <value>
        /// The checkin address.
        /// </value>
        [DefaultSettingValueAttribute( "" )]
        [UserScopedSetting]
        public string CheckinAddress
        {
            get
            {
                string checkinAddress = this["CheckinAddress"] as string ?? string.Empty;

                // If a URL was provided in the Command Line, use it instead
                string[] args = Environment.GetCommandLineArgs();

                foreach (string arg in args)
                {
                    if (arg.StartsWith("/U:"))
                    {
                        checkinAddress = arg.Replace("/U:", "");
                    }
                }

                return checkinAddress;
            }

            set
            {
                this["CheckinAddress"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the printer override ip.
        /// </summary>
        /// <value>
        /// The printer override ip.
        /// </value>
        [DefaultSettingValueAttribute( "" )]
        [UserScopedSetting]
        public string PrinterOverrideIp
        {
            get
            {
                return ( this["PrinterOverrideIp"] as string ) ?? "";
            }

            set
            {
                this["PrinterOverrideIp"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the printer override local.
        /// </summary>
        /// <value>
        /// The printer override local.
        /// </value>
        [DefaultSettingValueAttribute( "" )]
        [UserScopedSetting]
        public string PrinterOverrideLocal
        {
            get
            {
                return ( this["PrinterOverrideLocal"] as string ) ?? "";
            }

            set
            {
                this["PrinterOverrideLocal"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the duration of the cache label.
        /// </summary>
        /// <value>
        /// The duration of the cache label.
        /// </value>
        [DefaultSettingValueAttribute( "" )]
        [UserScopedSetting]
        public int CacheLabelDuration
        {
            get
            {
                int? cacheLabelDuration = this["CacheLabelDuration"] as int?;
                if (!cacheLabelDuration.HasValue || cacheLabelDuration < 0)
                {
                    cacheLabelDuration = 1440;
                }
                
                return cacheLabelDuration.Value;
            }

            set
            {
                this["CacheLabelDuration"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the zoom level.
        /// </summary>
        /// <value>
        /// The zoom level of the web view (1 = 100%, max value = 500, min .25) .
        /// </value>
        [DefaultSettingValueAttribute( "" )]
        [UserScopedSetting]
        public double ZoomLevel
        {
            get
            {
                double minValue = .25 ;
                double maxValue = 500;

                double? zoomLevel = this["ZoomLevel"] as double?;
                if ( !zoomLevel.HasValue || zoomLevel.Value < minValue || zoomLevel.Value > maxValue )
                {
                    zoomLevel = 1;
                }

                return zoomLevel.Value;
            }

            set
            {
                this["ZoomLevel"] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is caching enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is caching enabled; otherwise, <c>false</c>.
        /// </value>
        [DefaultSettingValueAttribute( "true" )]
        [UserScopedSetting]
        public bool IsCachingEnabled
        {
            get
            {
                bool? isCachingEnabled = this["IsCachingEnabled"] as bool?;
                if ( !isCachingEnabled.HasValue  )
                {
                    isCachingEnabled = true;
                }

                return isCachingEnabled.Value;
            }
            set
            {
                this["IsCachingEnabled"] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the printer has a cutter or not.
        /// </summary>
        /// <value>
        /// <c>false</c> if no cutter; otherwise, <c>true</c>.
        /// </value>
        [DefaultSettingValueAttribute( "false" )]
        [UserScopedSetting]
        public bool HasPrinterCutter
        {
            get
            {
                bool? hasPrinterCutter = this["HasPrinterCutter"] as bool?;
                if ( !hasPrinterCutter.HasValue )
                {
                    hasPrinterCutter = true;
                }

                return hasPrinterCutter.Value;
            }
            set
            {
                this["HasPrinterCutter"] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the printer is disabled or not.
        /// </summary>
        /// <value>
        /// <c>false</c> printing disabled; otherwise, <c>true</c>.
        /// </value>
        [DefaultSettingValueAttribute("false")]
        [UserScopedSetting]
        public bool IsPrintingDisabled
        {
            get
            {
                bool? isPrintingDisabled = this["IsPrintingDisabled"] as bool?;
                if (!isPrintingDisabled.HasValue)
                {
                    isPrintingDisabled = true;
                }

                return isPrintingDisabled.Value;
            }
            set
            {
                this["IsPrintingDisabled"] = value;
            }
        }

        /// <summary>
        /// Loads this instance.
        /// </summary>
        /// <returns></returns>
        public static RockConfig Load()
        {
            return RockConfig.Default;
        }
    }
}
