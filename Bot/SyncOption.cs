using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot
{
    enum SyncOption
    {
        Default = 0,
        SkipDownload = 0b01,
        KeepSyncedAt = 0b10,
    }
}
