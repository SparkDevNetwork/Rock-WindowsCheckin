using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CheckinClient
{
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisibleAttribute( true )]
    public class ScriptDirector
    {

        Page browserPage;
        ObjectCache cache;
        bool warnedPrinterError = false;
        RockConfig rockConfig = RockConfig.Load();

        public ScriptDirector(Page p)
        {
            this.browserPage = (Page)p;
            cache = MemoryCache.Default;
        }
        public void PrintLabels(string tagJson)
        {
            /* Checks if printer is enabled before printing. */
            if (!rockConfig.IsPrintingDisabled) {
                RockLabelPrinter printer = new RockLabelPrinter();
                printer.PrintLabels( tagJson );
            }
        }

        public void ErrorHandler(string message, string url, string lineNumber) {
            string test = message;
        }
    
    }
}
