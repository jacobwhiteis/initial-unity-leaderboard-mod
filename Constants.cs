using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModNamespace
{
    public static class Constants
    {
        // Application Related Constants
        public const string ModName = "Custom Leaderboard & Replay Mod (CLRM)";
        public const string ModVersion = "1.0.0";
        public const string ModAuthor = "CouldBeACrow";

        // API Constants
        public const string ApiKeyHeader = "x-api-key";
        public const string ApiKey = "yNrGPe5fnx1sMuNGXRV4o7JUyTonjAoH2G1ky6X3";
        public const string NewBaseLeaderboardAddress = "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod";
        public const string NewGetGhostAddress = NewBaseLeaderboardAddress + "/getGhost/?id=";
        public const string NewGetRecordsAddress = NewBaseLeaderboardAddress + "/getRecords";
        public const string NewWriteRecordAddress = NewBaseLeaderboardAddress + "/writeRecord";
        public const string OldGetGhostAddress = "https://initialunity.online/getGhost";
        public const string OldGetRecordsAddress = "https://initialunity.online/getRecords";
        public const string OldWriteRecordAddress = "https://initialunity.online/postRecord";

    }
}
