using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hl18
{
    public static class Mapper
    {
        public static int MIN_DIRECT = 1;
        public static int MAX_DIRECT = 1_330_000;
        public static int TOP_START = MAX_DIRECT+1;

        // id mapping: external to internal, create if not found
        public static int ExtIdToIntIdCreate(int extId)
        {
            if (extId >= MIN_DIRECT && extId <= MAX_DIRECT)
                return extId; // easy translation
            if (extIdToIntIdMap.TryGetValue(extId, out int intId))
                return intId; // existing mapping 

            // new mapping
            intId = TOP_START + extIdToIntIdMap.Count;
            if (intId >= Storage.MAX_ACCOUNTS)
                return -1; // error, shouldn't happen
            extIdToIntIdMap.Add(extId, intId);
            intIdToExtIdMap.Add(intId, extId);
            return intId;
        }


        // id mapping: external to internal, return false if not found
        public static bool ExtIdToIntId(int extId, out int intId)
        {
            if (extId >= MIN_DIRECT && extId <= MAX_DIRECT)
            {
                intId = extId; // easy translation
                return true;
            }
            if (extIdToIntIdMap.TryGetValue(extId, out intId))
                return true; // existing mapping found
            return false; // existing mapping not found
        }


        public static int IntIdToExtId(int intId)
        {
            if (intId >= MIN_DIRECT && intId <= MAX_DIRECT)
                return intId; // easy translation
            if (intIdToExtIdMap.TryGetValue(intId, out int extId))
                return extId; // existing translation 
            return -intId; // error, shouldn't happen
        }

        public static void DisplayStats()
        {
            Console.WriteLine("ExtToIntMapCount: {0}", extIdToIntIdMap.Count);
        }

        private static Dictionary<int, int> extIdToIntIdMap = new Dictionary<int, int>();
        private static Dictionary<int, int> intIdToExtIdMap = new Dictionary<int, int>();
    }
}
