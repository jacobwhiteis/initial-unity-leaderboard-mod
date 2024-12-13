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
        public const string ApiKey = "ZTp93l4D6XaL3FZVH11Kd36CWbsrB5Y8aACFCfwv";
        public const string NewBaseLeaderboardAddress = "https://api.initialunityreborn.com";
        public const string NewGetGhostAddress = NewBaseLeaderboardAddress + "/getGhost/?id=";
        public const string NewGetRecordsAddress = NewBaseLeaderboardAddress + "/getRecords";
        public const string NewWriteRecordAddress = NewBaseLeaderboardAddress + "/writeRecord";
        public const string OldGetGhostAddress = "https://initialunity.online/getGhost";
        public const string OldGetRecordsAddress = "https://initialunity.online/getRecords";
        public const string OldWriteRecordAddress = "https://initialunity.online/postRecord";

        // I/O Constants
        public const string OldGhostFolder = "Ghosts\\Local";
        public const string OldReplayFolder = "Replays\\Local";
        public const string NewGhostFolder = "ModGhosts\\Local";
        public const string NewReplayFolder = "ModReplays\\Local";

    }
}
