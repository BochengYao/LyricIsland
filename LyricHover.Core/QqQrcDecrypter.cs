/*
 * QRC DES routines are adapted from QQMusicDecoder/DESHelper.cs.
 * MIT License
 * Copyright (c) 2023 WXRIW
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricHover.Core
{
    internal static class QqQrcDecrypter
    {
        private const uint Encrypt = 1;
        private const uint Decrypt = 0;
        private static readonly byte[] QqKey = Encoding.ASCII.GetBytes("!@#)(*$%123ZXC!@!@#)(NHL");
        private static readonly Regex LyricContentPattern = new Regex(
            "<Lyric_1\\b[^>]*\\bLyricContent\\s*=\\s*\"(?<content>.*?)\"\\s*/>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly byte[] SBox1 = {
            14,4,13,1,2,15,11,8,3,10,6,12,5,9,0,7,0,15,7,4,14,2,13,1,10,6,12,11,9,5,3,8,
            4,1,14,8,13,6,2,11,15,12,9,7,3,10,5,0,15,12,8,2,4,9,1,7,5,11,3,14,10,0,6,13
        };
        private static readonly byte[] SBox2 = {
            15,1,8,14,6,11,3,4,9,7,2,13,12,0,5,10,3,13,4,7,15,2,8,15,12,0,1,10,6,9,11,5,
            0,14,7,11,10,4,13,1,5,8,12,6,9,3,2,15,13,8,10,1,3,15,4,2,11,6,7,12,0,5,14,9
        };
        private static readonly byte[] SBox3 = {
            10,0,9,14,6,3,15,5,1,13,12,7,11,4,2,8,13,7,0,9,3,4,6,10,2,8,5,14,12,11,15,1,
            13,6,4,9,8,15,3,0,11,1,2,12,5,10,14,7,1,10,13,0,6,9,8,7,4,15,14,3,11,5,2,12
        };
        private static readonly byte[] SBox4 = {
            7,13,14,3,0,6,9,10,1,2,8,5,11,12,4,15,13,8,11,5,6,15,0,3,4,7,2,12,1,10,14,9,
            10,6,9,0,12,11,7,13,15,1,3,14,5,2,8,4,3,15,0,6,10,10,13,8,9,4,5,11,12,7,2,14
        };
        private static readonly byte[] SBox5 = {
            2,12,4,1,7,10,11,6,8,5,3,15,13,0,14,9,14,11,2,12,4,7,13,1,5,0,15,10,3,9,8,6,
            4,2,1,11,10,13,7,8,15,9,12,5,6,3,0,14,11,8,12,7,1,14,2,13,6,15,0,9,10,4,5,3
        };
        private static readonly byte[] SBox6 = {
            12,1,10,15,9,2,6,8,0,13,3,4,14,7,5,11,10,15,4,2,7,12,9,5,6,1,13,14,0,11,3,8,
            9,14,15,5,2,8,12,3,7,0,4,10,1,13,11,6,4,3,2,12,9,5,15,10,11,14,1,7,6,0,8,13
        };
        private static readonly byte[] SBox7 = {
            4,11,2,14,15,0,8,13,3,12,9,7,5,10,6,1,13,0,11,7,4,9,1,10,14,3,5,12,2,15,8,6,
            1,4,11,13,12,3,7,14,10,15,6,8,0,5,9,2,6,11,13,8,1,4,10,7,9,5,0,15,14,2,3,12
        };
        private static readonly byte[] SBox8 = {
            13,2,8,4,6,15,11,1,10,9,3,14,5,0,12,7,1,15,13,8,10,3,7,4,12,5,6,11,0,14,9,2,
            7,11,4,1,9,12,14,2,0,6,10,13,15,3,5,8,2,1,14,7,4,10,8,13,15,12,9,0,3,5,6,11
        };

        public static bool TryDecrypt(string encryptedLyrics, out string qrcText)
        {
            qrcText = string.Empty;
            if (!TryParseHex(encryptedLyrics, out var encrypted) || encrypted.Length == 0 || encrypted.Length % 8 != 0)
            {
                return false;
            }

            try
            {
                var schedule = CreateSchedule();
                TripleDesKeySetup(QqKey, schedule, Decrypt);
                var decrypted = new byte[encrypted.Length];
                var inputBlock = new byte[8];
                var outputBlock = new byte[8];
                for (var offset = 0; offset < encrypted.Length; offset += 8)
                {
                    Buffer.BlockCopy(encrypted, offset, inputBlock, 0, 8);
                    TripleDesCrypt(inputBlock, outputBlock, schedule);
                    Buffer.BlockCopy(outputBlock, 0, decrypted, offset, 8);
                }

                qrcText = ExtractLyricContent(InflateZlib(decrypted));
                return !string.IsNullOrWhiteSpace(qrcText);
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static string ExtractLyricContent(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return string.Empty;
            }

            // QRC is XML-shaped, but QQ can leave literal quotes inside the
            // LyricContent value.  Anchor on the lyric element terminator instead
            // of treating the first quote in the song text as the attribute end.
            var lyricContent = LyricContentPattern.Match(xml);
            return lyricContent.Success
                ? WebUtility.HtmlDecode(lyricContent.Groups["content"].Value)
                : xml;
        }

        private static bool TryParseHex(string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length % 2 != 0)
            {
                return false;
            }

            bytes = new byte[value.Length / 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                if (!byte.TryParse(
                    value.Substring(index * 2, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out bytes[index]))
                {
                    bytes = Array.Empty<byte>();
                    return false;
                }
            }

            return true;
        }

        private static string InflateZlib(byte[] data)
        {
            // .NET Core 3.1 has DeflateStream but not ZLibStream. QRC is zlib
            // wrapped, so skip its two-byte header; DeflateStream stops at the
            // final deflate block before the Adler-32 trailer and zero padding.
            if (data.Length < 3 || (data[0] & 0x0f) != 8 || ((data[0] << 8) + data[1]) % 31 != 0)
            {
                throw new InvalidDataException("Invalid QRC zlib header.");
            }

            using (var input = new MemoryStream(data, 2, data.Length - 2, false))
            using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                inflater.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static byte[][][] CreateSchedule()
        {
            var schedule = new byte[3][][];
            for (var keyIndex = 0; keyIndex < schedule.Length; keyIndex++)
            {
                schedule[keyIndex] = new byte[16][];
                for (var round = 0; round < schedule[keyIndex].Length; round++)
                {
                    schedule[keyIndex][round] = new byte[6];
                }
            }

            return schedule;
        }

        private static uint BitNumber(byte[] value, int bit, int outputBit)
        {
            return (uint)(((value[bit / 32 * 4 + 3 - bit % 32 / 8] >> (7 - bit % 8)) & 1) << outputBit);
        }

        private static byte BitNumberRight(uint value, int bit, int outputBit)
        {
            return (byte)(((value >> (31 - bit)) & 1) << outputBit);
        }

        private static uint BitNumberLeft(uint value, int bit, int outputBit)
        {
            return ((value << bit) & 0x80000000) >> outputBit;
        }

        private static uint SBoxBit(byte value)
        {
            return (uint)((value & 0x20) | ((value & 0x1f) >> 1) | ((value & 1) << 4));
        }

        private static void KeySchedule(byte[] key, byte[][] schedule, uint mode)
        {
            var shifts = new[] { 1,1,2,2,2,2,2,2,1,2,2,2,2,2,2,1 };
            var permutationC = new[] { 56,48,40,32,24,16,8,0,57,49,41,33,25,17,9,1,58,50,42,34,26,18,10,2,59,51,43,35 };
            var permutationD = new[] { 62,54,46,38,30,22,14,6,61,53,45,37,29,21,13,5,60,52,44,36,28,20,12,4,27,19,11,3 };
            var compression = new[] { 13,16,10,23,0,4,2,27,14,5,20,9,22,18,11,3,25,7,15,6,26,19,12,1,40,51,30,36,46,54,29,39,50,44,32,47,43,48,38,55,33,52,45,41,49,35,28,31 };
            uint c = 0;
            uint d = 0;
            for (var index = 0; index < 28; index++)
            {
                c |= BitNumber(key, permutationC[index], 31 - index);
                d |= BitNumber(key, permutationD[index], 31 - index);
            }

            for (var round = 0; round < 16; round++)
            {
                c = ((c << shifts[round]) | (c >> (28 - shifts[round]))) & 0xfffffff0;
                d = ((d << shifts[round]) | (d >> (28 - shifts[round]))) & 0xfffffff0;
                var targetRound = mode == Decrypt ? 15 - round : round;
                Array.Clear(schedule[targetRound], 0, schedule[targetRound].Length);
                for (var bit = 0; bit < 24; bit++)
                {
                    schedule[targetRound][bit / 8] |= BitNumberRight(c, compression[bit], 7 - bit % 8);
                }
                for (var bit = 24; bit < 48; bit++)
                {
                    schedule[targetRound][bit / 8] |= BitNumberRight(d, compression[bit] - 27, 7 - bit % 8);
                }
            }
        }

        private static void InitialPermutation(uint[] state, byte[] input)
        {
            state[0] = BitNumber(input,57,31)|BitNumber(input,49,30)|BitNumber(input,41,29)|BitNumber(input,33,28)|BitNumber(input,25,27)|BitNumber(input,17,26)|BitNumber(input,9,25)|BitNumber(input,1,24)|
                BitNumber(input,59,23)|BitNumber(input,51,22)|BitNumber(input,43,21)|BitNumber(input,35,20)|BitNumber(input,27,19)|BitNumber(input,19,18)|BitNumber(input,11,17)|BitNumber(input,3,16)|
                BitNumber(input,61,15)|BitNumber(input,53,14)|BitNumber(input,45,13)|BitNumber(input,37,12)|BitNumber(input,29,11)|BitNumber(input,21,10)|BitNumber(input,13,9)|BitNumber(input,5,8)|
                BitNumber(input,63,7)|BitNumber(input,55,6)|BitNumber(input,47,5)|BitNumber(input,39,4)|BitNumber(input,31,3)|BitNumber(input,23,2)|BitNumber(input,15,1)|BitNumber(input,7,0);
            state[1] = BitNumber(input,56,31)|BitNumber(input,48,30)|BitNumber(input,40,29)|BitNumber(input,32,28)|BitNumber(input,24,27)|BitNumber(input,16,26)|BitNumber(input,8,25)|BitNumber(input,0,24)|
                BitNumber(input,58,23)|BitNumber(input,50,22)|BitNumber(input,42,21)|BitNumber(input,34,20)|BitNumber(input,26,19)|BitNumber(input,18,18)|BitNumber(input,10,17)|BitNumber(input,2,16)|
                BitNumber(input,60,15)|BitNumber(input,52,14)|BitNumber(input,44,13)|BitNumber(input,36,12)|BitNumber(input,28,11)|BitNumber(input,20,10)|BitNumber(input,12,9)|BitNumber(input,4,8)|
                BitNumber(input,62,7)|BitNumber(input,54,6)|BitNumber(input,46,5)|BitNumber(input,38,4)|BitNumber(input,30,3)|BitNumber(input,22,2)|BitNumber(input,14,1)|BitNumber(input,6,0);
        }

        private static void InverseInitialPermutation(uint[] state, byte[] output)
        {
            output[3]=(byte)(BitNumberRight(state[1],7,7)|BitNumberRight(state[0],7,6)|BitNumberRight(state[1],15,5)|BitNumberRight(state[0],15,4)|BitNumberRight(state[1],23,3)|BitNumberRight(state[0],23,2)|BitNumberRight(state[1],31,1)|BitNumberRight(state[0],31,0));
            output[2]=(byte)(BitNumberRight(state[1],6,7)|BitNumberRight(state[0],6,6)|BitNumberRight(state[1],14,5)|BitNumberRight(state[0],14,4)|BitNumberRight(state[1],22,3)|BitNumberRight(state[0],22,2)|BitNumberRight(state[1],30,1)|BitNumberRight(state[0],30,0));
            output[1]=(byte)(BitNumberRight(state[1],5,7)|BitNumberRight(state[0],5,6)|BitNumberRight(state[1],13,5)|BitNumberRight(state[0],13,4)|BitNumberRight(state[1],21,3)|BitNumberRight(state[0],21,2)|BitNumberRight(state[1],29,1)|BitNumberRight(state[0],29,0));
            output[0]=(byte)(BitNumberRight(state[1],4,7)|BitNumberRight(state[0],4,6)|BitNumberRight(state[1],12,5)|BitNumberRight(state[0],12,4)|BitNumberRight(state[1],20,3)|BitNumberRight(state[0],20,2)|BitNumberRight(state[1],28,1)|BitNumberRight(state[0],28,0));
            output[7]=(byte)(BitNumberRight(state[1],3,7)|BitNumberRight(state[0],3,6)|BitNumberRight(state[1],11,5)|BitNumberRight(state[0],11,4)|BitNumberRight(state[1],19,3)|BitNumberRight(state[0],19,2)|BitNumberRight(state[1],27,1)|BitNumberRight(state[0],27,0));
            output[6]=(byte)(BitNumberRight(state[1],2,7)|BitNumberRight(state[0],2,6)|BitNumberRight(state[1],10,5)|BitNumberRight(state[0],10,4)|BitNumberRight(state[1],18,3)|BitNumberRight(state[0],18,2)|BitNumberRight(state[1],26,1)|BitNumberRight(state[0],26,0));
            output[5]=(byte)(BitNumberRight(state[1],1,7)|BitNumberRight(state[0],1,6)|BitNumberRight(state[1],9,5)|BitNumberRight(state[0],9,4)|BitNumberRight(state[1],17,3)|BitNumberRight(state[0],17,2)|BitNumberRight(state[1],25,1)|BitNumberRight(state[0],25,0));
            output[4]=(byte)(BitNumberRight(state[1],0,7)|BitNumberRight(state[0],0,6)|BitNumberRight(state[1],8,5)|BitNumberRight(state[0],8,4)|BitNumberRight(state[1],16,3)|BitNumberRight(state[0],16,2)|BitNumberRight(state[1],24,1)|BitNumberRight(state[0],24,0));
        }

        private static uint RoundFunction(uint state, byte[] key)
        {
            var expanded = new byte[6];
            var first = BitNumberLeft(state,31,0)|((state&0xf0000000)>>1)|BitNumberLeft(state,4,5)|BitNumberLeft(state,3,6)|((state&0x0f000000)>>3)|BitNumberLeft(state,8,11)|BitNumberLeft(state,7,12)|((state&0x00f00000)>>5)|BitNumberLeft(state,12,17)|BitNumberLeft(state,11,18)|((state&0x000f0000)>>7)|BitNumberLeft(state,16,23);
            var second = BitNumberLeft(state,15,0)|((state&0x0000f000)<<15)|BitNumberLeft(state,20,5)|BitNumberLeft(state,19,6)|((state&0x00000f00)<<13)|BitNumberLeft(state,24,11)|BitNumberLeft(state,23,12)|((state&0x000000f0)<<11)|BitNumberLeft(state,28,17)|BitNumberLeft(state,27,18)|((state&0x0000000f)<<9)|BitNumberLeft(state,0,23);
            expanded[0]=(byte)(first>>24); expanded[1]=(byte)(first>>16); expanded[2]=(byte)(first>>8);
            expanded[3]=(byte)(second>>24); expanded[4]=(byte)(second>>16); expanded[5]=(byte)(second>>8);
            for (var index = 0; index < expanded.Length; index++) expanded[index] ^= key[index];
            state=(uint)((SBox1[SBoxBit((byte)(expanded[0]>>2))]<<28)|(SBox2[SBoxBit((byte)(((expanded[0]&3)<<4)|(expanded[1]>>4)))]<<24)|
                (SBox3[SBoxBit((byte)(((expanded[1]&15)<<2)|(expanded[2]>>6)))]<<20)|(SBox4[SBoxBit((byte)(expanded[2]&63))]<<16)|
                (SBox5[SBoxBit((byte)(expanded[3]>>2))]<<12)|(SBox6[SBoxBit((byte)(((expanded[3]&3)<<4)|(expanded[4]>>4)))]<<8)|
                (SBox7[SBoxBit((byte)(((expanded[4]&15)<<2)|(expanded[5]>>6)))]<<4)|SBox8[SBoxBit((byte)(expanded[5]&63))]);
            return BitNumberLeft(state,15,0)|BitNumberLeft(state,6,1)|BitNumberLeft(state,19,2)|BitNumberLeft(state,20,3)|BitNumberLeft(state,28,4)|BitNumberLeft(state,11,5)|BitNumberLeft(state,27,6)|BitNumberLeft(state,16,7)|
                BitNumberLeft(state,0,8)|BitNumberLeft(state,14,9)|BitNumberLeft(state,22,10)|BitNumberLeft(state,25,11)|BitNumberLeft(state,4,12)|BitNumberLeft(state,17,13)|BitNumberLeft(state,30,14)|BitNumberLeft(state,9,15)|
                BitNumberLeft(state,1,16)|BitNumberLeft(state,7,17)|BitNumberLeft(state,23,18)|BitNumberLeft(state,13,19)|BitNumberLeft(state,31,20)|BitNumberLeft(state,26,21)|BitNumberLeft(state,2,22)|BitNumberLeft(state,8,23)|
                BitNumberLeft(state,18,24)|BitNumberLeft(state,12,25)|BitNumberLeft(state,29,26)|BitNumberLeft(state,5,27)|BitNumberLeft(state,21,28)|BitNumberLeft(state,10,29)|BitNumberLeft(state,3,30)|BitNumberLeft(state,24,31);
        }

        private static void Crypt(byte[] input, byte[] output, byte[][] key)
        {
            var state = new uint[2];
            InitialPermutation(state, input);
            for (var round = 0; round < 15; round++)
            {
                var temporary = state[1];
                state[1] = RoundFunction(state[1], key[round]) ^ state[0];
                state[0] = temporary;
            }
            state[0] = RoundFunction(state[1], key[15]) ^ state[0];
            InverseInitialPermutation(state, output);
        }

        private static void TripleDesKeySetup(byte[] key, byte[][][] schedule, uint mode)
        {
            var first = new byte[8]; var second = new byte[8]; var third = new byte[8];
            Buffer.BlockCopy(key, 0, first, 0, 8); Buffer.BlockCopy(key, 8, second, 0, 8); Buffer.BlockCopy(key, 16, third, 0, 8);
            if (mode == Encrypt)
            {
                KeySchedule(first, schedule[0], Encrypt); KeySchedule(second, schedule[1], Decrypt); KeySchedule(third, schedule[2], Encrypt);
            }
            else
            {
                KeySchedule(first, schedule[2], Decrypt); KeySchedule(second, schedule[1], Encrypt); KeySchedule(third, schedule[0], Decrypt);
            }
        }

        private static void TripleDesCrypt(byte[] input, byte[] output, byte[][][] schedule)
        {
            Crypt(input, output, schedule[0]);
            Crypt(output, output, schedule[1]);
            Crypt(output, output, schedule[2]);
        }
    }
}
