﻿namespace Element.CloudDistributedLock
{
    internal class SessionTokenParser
    {
        private static readonly char[] segmentSeparator = new[] { '#' };
        private static readonly char[] pkRangeSeparator = new[] { ':' };

        public static long Parse(string sessionToken)
        {
            // Simple session token: {pkrangeid}:{globalLSN}
            // Vector session token: {pkrangeid}:{Version}#{GlobalLSN}#{RegionId1}={LocalLsn1}#{RegionId2}={LocalLsn2}....#{RegionIdN}={LocalLsnN}

            var items = sessionToken.Split(pkRangeSeparator, StringSplitOptions.RemoveEmptyEntries);
            var sessionTokenSegments = items[1].Split(segmentSeparator, StringSplitOptions.RemoveEmptyEntries);
            var globalLsnSegmentIndex = sessionTokenSegments.Length == 1 ? 0 : 1;
            return long.Parse(sessionTokenSegments[globalLsnSegmentIndex]);
        }
    }
}
