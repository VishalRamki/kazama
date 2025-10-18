using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tomlyn;


namespace Kazama.Models
{
    public class FolderSyncConfig
    {
        public string Source { get; set; } = null!;
        public string Destination { get; set; } = null!;
        public bool IncludeSubdirectories { get; set; } = true;
        public bool MirrorDeletions { get; set; } = true;
        public bool OverwriteExisting { get; set; } = true;
    }

    public class FolderSyncRoot
    {
        public List<FolderSyncConfig> FolderSyncs { get; set; } = new List<FolderSyncConfig>();
    }
}
