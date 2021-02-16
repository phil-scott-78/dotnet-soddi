﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Soddi.Services
{
    public static class DotPattern
    {
        /// <summary>
        /// Returns a braille pattern matching the specified dots depending on which characters
        /// 1-8 are in the string
        /// <br/><br/>
        /// ① ④<br/>
        /// ② ⑤<br/>
        /// ③ ⑥<br/>
        /// ⑦ ⑧
        /// </summary>
        /// <param name="pattern">A string containing the dot numbering</param>
        /// <returns></returns>
        public static char Get(string pattern)
        {
            return s_dots[pattern];
        }

        public static char Get(bool[,] pattern)
        {
            if (pattern.Length != 8 || pattern.Rank != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(pattern), "Only 2x4 array sizes supported");
            }

            return Get(new BitArray(new[]
            {
                pattern[0, 0], pattern[1, 0], pattern[2, 0], pattern[0, 1], pattern[1, 1], pattern[2, 1],
                pattern[3, 0], pattern[3, 1]
            }));
        }

        /// <summary>
        /// Returns a braille pattern matching the specified dots based on which bits are true
        /// <br/><br/>
        /// ① ④<br/>
        /// ② ⑤<br/>
        /// ③ ⑥<br/>
        /// ⑦ ⑧
        /// </summary>
        /// <param name="bitArray">A bit array containing the dot numbering</param>
        /// <returns></returns>
        public static char Get(BitArray bitArray)
        {
            if (bitArray.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(bitArray), "Only 8-bit arrays are supported");

            var s = "";
            for (var index = 0; index < bitArray.Count; index++)
            {
                if (bitArray[index])
                {
                    s += (index + 1).ToString();
                }
            }

            return s_dots[s];
        }

        /// <summary>
        /// Returns a braille pattern matching the specified dots based on which bits are true
        /// <br/><br/>
        /// ① ④<br/>
        /// ② ⑤<br/>
        /// ③ ⑥<br/>
        /// ⑦ ⑧
        /// </summary>
        /// <param name="pattern">A byte containing the bits to display</param>
        /// <returns></returns>
        public static char Get(byte pattern)
        {
            return Get(new BitArray(new[] { pattern }));
        }

        /// <summary>
        /// Returns a braille pattern matching the specified dots based on which bits are true
        /// <br/><br/>
        /// ① ④<br/>
        /// ② ⑤<br/>
        /// ③ ⑥<br/>
        /// ⑦ ⑧
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <param name="three"></param>
        /// <param name="four"></param>
        /// <param name="five"></param>
        /// <param name="six"></param>
        /// <param name="seven"></param>
        /// <param name="eight"></param>
        /// <returns></returns>
        public static char Get(bool one, bool two, bool three, bool four, bool five, bool six, bool seven, bool eight)
        {
            return Get(new BitArray(new[] { one, two, three, four, five, six, seven, eight }));
        }

        private static readonly ImmutableDictionary<string, char> s_dots = new Dictionary<string, char>()
        {
            { "", '\u2800' },
            { "1", '\u2801' },
            { "2", '\u2802' },
            { "12", '\u2803' },
            { "3", '\u2804' },
            { "13", '\u2805' },
            { "23", '\u2806' },
            { "123", '\u2807' },
            { "4", '\u2808' },
            { "14", '\u2809' },
            { "24", '\u280A' },
            { "124", '\u280B' },
            { "34", '\u280C' },
            { "134", '\u280D' },
            { "234", '\u280E' },
            { "1234", '\u280F' },
            { "5", '\u2810' },
            { "15", '\u2811' },
            { "25", '\u2812' },
            { "125", '\u2813' },
            { "35", '\u2814' },
            { "135", '\u2815' },
            { "235", '\u2816' },
            { "1235", '\u2817' },
            { "45", '\u2818' },
            { "145", '\u2819' },
            { "245", '\u281A' },
            { "1245", '\u281B' },
            { "345", '\u281C' },
            { "1345", '\u281D' },
            { "2345", '\u281E' },
            { "12345", '\u281F' },
            { "6", '\u2820' },
            { "16", '\u2821' },
            { "26", '\u2822' },
            { "126", '\u2823' },
            { "36", '\u2824' },
            { "136", '\u2825' },
            { "236", '\u2826' },
            { "1236", '\u2827' },
            { "46", '\u2828' },
            { "146", '\u2829' },
            { "246", '\u282A' },
            { "1246", '\u282B' },
            { "346", '\u282C' },
            { "1346", '\u282D' },
            { "2346", '\u282E' },
            { "12346", '\u282F' },
            { "56", '\u2830' },
            { "156", '\u2831' },
            { "256", '\u2832' },
            { "1256", '\u2833' },
            { "356", '\u2834' },
            { "1356", '\u2835' },
            { "2356", '\u2836' },
            { "12356", '\u2837' },
            { "456", '\u2838' },
            { "1456", '\u2839' },
            { "2456", '\u283A' },
            { "12456", '\u283B' },
            { "3456", '\u283C' },
            { "13456", '\u283D' },
            { "23456", '\u283E' },
            { "123456", '\u283F' },
            { "7", '\u2840' },
            { "17", '\u2841' },
            { "27", '\u2842' },
            { "127", '\u2843' },
            { "37", '\u2844' },
            { "137", '\u2845' },
            { "237", '\u2846' },
            { "1237", '\u2847' },
            { "47", '\u2848' },
            { "147", '\u2849' },
            { "247", '\u284A' },
            { "1247", '\u284B' },
            { "347", '\u284C' },
            { "1347", '\u284D' },
            { "2347", '\u284E' },
            { "12347", '\u284F' },
            { "57", '\u2850' },
            { "157", '\u2851' },
            { "257", '\u2852' },
            { "1257", '\u2853' },
            { "357", '\u2854' },
            { "1357", '\u2855' },
            { "2357", '\u2856' },
            { "12357", '\u2857' },
            { "457", '\u2858' },
            { "1457", '\u2859' },
            { "2457", '\u285A' },
            { "12457", '\u285B' },
            { "3457", '\u285C' },
            { "13457", '\u285D' },
            { "23457", '\u285E' },
            { "123457", '\u285F' },
            { "67", '\u2860' },
            { "167", '\u2861' },
            { "267", '\u2862' },
            { "1267", '\u2863' },
            { "367", '\u2864' },
            { "1367", '\u2865' },
            { "2367", '\u2866' },
            { "12367", '\u2867' },
            { "467", '\u2868' },
            { "1467", '\u2869' },
            { "2467", '\u286A' },
            { "12467", '\u286B' },
            { "3467", '\u286C' },
            { "13467", '\u286D' },
            { "23467", '\u286E' },
            { "123467", '\u286F' },
            { "567", '\u2870' },
            { "1567", '\u2871' },
            { "2567", '\u2872' },
            { "12567", '\u2873' },
            { "3567", '\u2874' },
            { "13567", '\u2875' },
            { "23567", '\u2876' },
            { "123567", '\u2877' },
            { "4567", '\u2878' },
            { "14567", '\u2879' },
            { "24567", '\u287A' },
            { "124567", '\u287B' },
            { "34567", '\u287C' },
            { "134567", '\u287D' },
            { "234567", '\u287E' },
            { "1234567", '\u287F' },
            { "8", '\u2880' },
            { "18", '\u2881' },
            { "28", '\u2882' },
            { "128", '\u2883' },
            { "38", '\u2884' },
            { "138", '\u2885' },
            { "238", '\u2886' },
            { "1238", '\u2887' },
            { "48", '\u2888' },
            { "148", '\u2889' },
            { "248", '\u288A' },
            { "1248", '\u288B' },
            { "348", '\u288C' },
            { "1348", '\u288D' },
            { "2348", '\u288E' },
            { "12348", '\u288F' },
            { "58", '\u2890' },
            { "158", '\u2891' },
            { "258", '\u2892' },
            { "1258", '\u2893' },
            { "358", '\u2894' },
            { "1358", '\u2895' },
            { "2358", '\u2896' },
            { "12358", '\u2897' },
            { "458", '\u2898' },
            { "1458", '\u2899' },
            { "2458", '\u289A' },
            { "12458", '\u289B' },
            { "3458", '\u289C' },
            { "13458", '\u289D' },
            { "23458", '\u289E' },
            { "123458", '\u289F' },
            { "68", '\u28A0' },
            { "168", '\u28A1' },
            { "268", '\u28A2' },
            { "1268", '\u28A3' },
            { "368", '\u28A4' },
            { "1368", '\u28A5' },
            { "2368", '\u28A6' },
            { "12368", '\u28A7' },
            { "468", '\u28A8' },
            { "1468", '\u28A9' },
            { "2468", '\u28AA' },
            { "12468", '\u28AB' },
            { "3468", '\u28AC' },
            { "13468", '\u28AD' },
            { "23468", '\u28AE' },
            { "123468", '\u28AF' },
            { "568", '\u28B0' },
            { "1568", '\u28B1' },
            { "2568", '\u28B2' },
            { "12568", '\u28B3' },
            { "3568", '\u28B4' },
            { "13568", '\u28B5' },
            { "23568", '\u28B6' },
            { "123568", '\u28B7' },
            { "4568", '\u28B8' },
            { "14568", '\u28B9' },
            { "24568", '\u28BA' },
            { "124568", '\u28BB' },
            { "34568", '\u28BC' },
            { "134568", '\u28BD' },
            { "234568", '\u28BE' },
            { "1234568", '\u28BF' },
            { "78", '\u28C0' },
            { "178", '\u28C1' },
            { "278", '\u28C2' },
            { "1278", '\u28C3' },
            { "378", '\u28C4' },
            { "1378", '\u28C5' },
            { "2378", '\u28C6' },
            { "12378", '\u28C7' },
            { "478", '\u28C8' },
            { "1478", '\u28C9' },
            { "2478", '\u28CA' },
            { "12478", '\u28CB' },
            { "3478", '\u28CC' },
            { "13478", '\u28CD' },
            { "23478", '\u28CE' },
            { "123478", '\u28CF' },
            { "578", '\u28D0' },
            { "1578", '\u28D1' },
            { "2578", '\u28D2' },
            { "12578", '\u28D3' },
            { "3578", '\u28D4' },
            { "13578", '\u28D5' },
            { "23578", '\u28D6' },
            { "123578", '\u28D7' },
            { "4578", '\u28D8' },
            { "14578", '\u28D9' },
            { "24578", '\u28DA' },
            { "124578", '\u28DB' },
            { "34578", '\u28DC' },
            { "134578", '\u28DD' },
            { "234578", '\u28DE' },
            { "1234578", '\u28DF' },
            { "678", '\u28E0' },
            { "1678", '\u28E1' },
            { "2678", '\u28E2' },
            { "12678", '\u28E3' },
            { "3678", '\u28E4' },
            { "13678", '\u28E5' },
            { "23678", '\u28E6' },
            { "123678", '\u28E7' },
            { "4678", '\u28E8' },
            { "14678", '\u28E9' },
            { "24678", '\u28EA' },
            { "124678", '\u28EB' },
            { "34678", '\u28EC' },
            { "134678", '\u28ED' },
            { "234678", '\u28EE' },
            { "1234678", '\u28EF' },
            { "5678", '\u28F0' },
            { "15678", '\u28F1' },
            { "25678", '\u28F2' },
            { "125678", '\u28F3' },
            { "35678", '\u28F4' },
            { "135678", '\u28F5' },
            { "235678", '\u28F6' },
            { "1235678", '\u28F7' },
            { "45678", '\u28F8' },
            { "145678", '\u28F9' },
            { "245678", '\u28FA' },
            { "1245678", '\u28FB' },
            { "345678", '\u28FC' },
            { "1345678", '\u28FD' },
            { "2345678", '\u28FE' },
            { "12345678", '\u28FF' },
        }.ToImmutableDictionary();
    }
}
