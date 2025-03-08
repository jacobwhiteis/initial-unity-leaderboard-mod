using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModNamespace
{
    public static class Constants
    {
        // Application Related static readonlyants
        public const string ModName = "Custom Leaderboard & Replay Mod (CLRM)";
        public const string ModVersion = "2.1.8";
        public const string ModAuthor = "CouldBeACrow";

        // API static readonlyants
        public static readonly string ApiKeyHeader = "x-api-key";
        public static readonly string ApiKey1 = "OHc1V041eTluTDY3MXoy";
        public static readonly string ApiKey2 = "YkJWNzAxNmlnSURnWVFV";
        public static readonly string ApiKey3 = "RUQ1alZDNkJSeA==";
        public static readonly string NewBaseLeaderboardAddress = "https://api.initialunityreborn.com";
        public static readonly string NewGetGhostAddress = NewBaseLeaderboardAddress + "/getGhost/?id=";
        public static readonly string NewGetRecordsAddress = NewBaseLeaderboardAddress + "/getRecords";
        public static readonly string NewGetS3UrlAddress = NewBaseLeaderboardAddress + "/getS3Url";
        public static readonly string NewWriteRecordAddress = NewBaseLeaderboardAddress + "/writeRecord";
        public static readonly string OldGetGhostAddress = "https://initialunity.online/getGhost";
        public static readonly string OldGetRecordsAddress = "https://initialunity.online/getRecords";
        public static readonly string OldWriteRecordAddress = "https://initialunity.online/postRecord";

        // I/O static readonlyants
        public static readonly string OldGhostFolder1 = Path.Combine("Ghosts", "Local");
        public static readonly string OldGhostFolder2 = Path.Combine("Ghosts");
        public static readonly string OldReplayFolder1 = Path.Combine("Replays", "Local");
        public static readonly string OldReplayFolder2 = Path.Combine("Replays");
        public static readonly string NewGhostFolder = Path.Combine("ModGhosts");
        public static readonly string NewReplayFolder = Path.Combine("ModReplays");

    }
}
