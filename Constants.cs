using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kazama
{
    internal class Constants
    {
        public static string ConfigurationFile { get; set; } = Path.Combine(AppContext.BaseDirectory, "foldersync.toml");
    }
}
