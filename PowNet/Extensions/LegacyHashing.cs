using System.Security.Cryptography;
using System.Text;

namespace PowNet.Extensions
{
    /// <summary>
    /// Legacy hashing helpers (MD5 / MD4). ONLY for backward compatibility with existing stored hashes.
    /// Do NOT use for new password storage (prefer PBKDF2).
    /// </summary>
    public static class LegacyHashingExtensions
    {
        public static string HashMd5(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string HashMd4(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var md4 = new Md4Managed();
            var bytes = md4.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // Internal MD4 implementation (RFC 1320)
        private sealed class Md4Managed : HashAlgorithm
        {
            private readonly uint[] _state = new uint[4];
            private readonly uint[] _count = new uint[2];
            private readonly byte[] _buffer = new byte[64];
            private readonly uint[] _x = new uint[16];
            private byte[] _digest = new byte[16];

            public Md4Managed()
            {
                Initialize();
            }

            public override void Initialize()
            {
                _count[0] = _count[1] = 0;
                _state[0] = 0x67452301;
                _state[1] = 0xEFCDAB89;
                _state[2] = 0x98BADCFE;
                _state[3] = 0x10325476;
            }

            protected override void HashCore(byte[] array, int ibStart, int cbSize)
            {
                int i;
                var index = (int)((_count[0] >> 3) & 0x3F);

                if ((_count[0] += (uint)(cbSize << 3)) < (cbSize << 3))
                    _count[1]++;

                _count[1] += (uint)(cbSize >> 29);

                var partLen = 64 - index;
                if (cbSize >= partLen)
                {
                    Buffer.BlockCopy(array, ibStart, _buffer, index, partLen);
                    Transform(_buffer, 0);

                    for (i = partLen; i + 63 < cbSize; i += 64)
                        Transform(array, ibStart + i);

                    index = 0;
                }
                else
                {
                    i = 0;
                }

                Buffer.BlockCopy(array, ibStart + i, _buffer, index, cbSize - i);
            }

            protected override byte[] HashFinal()
            {
                var bits = Encode(_count, 8);

                var index = (int)((_count[0] >> 3) & 0x3f);
                var padLen = (index < 56) ? (56 - index) : (120 - index);

                var padding = new byte[padLen + 8];
                padding[0] = 0x80;
                Buffer.BlockCopy(bits, 0, padding, padLen, 8);
                HashCore(padding, 0, padding.Length);

                _digest = Encode(_state, 16);
                return _digest;
            }

            public override int HashSize => 128;

            private void Transform(byte[] block, int offset)
            {
                for (int i = 0; i < 16; i++)
                {
                    _x[i] = (uint)(block[offset + i * 4] |
                                  (block[offset + i * 4 + 1] << 8) |
                                  (block[offset + i * 4 + 2] << 16) |
                                  (block[offset + i * 4 + 3] << 24));
                }

                uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];

                // Round 1
                FF(ref a, b, c, d, _x[0], 3); FF(ref d, a, b, c, _x[1], 7); FF(ref c, d, a, b, _x[2], 11); FF(ref b, c, d, a, _x[3], 19);
                FF(ref a, b, c, d, _x[4], 3); FF(ref d, a, b, c, _x[5], 7); FF(ref c, d, a, b, _x[6], 11); FF(ref b, c, d, a, _x[7], 19);
                FF(ref a, b, c, d, _x[8], 3); FF(ref d, a, b, c, _x[9], 7); FF(ref c, d, a, b, _x[10], 11); FF(ref b, c, d, a, _x[11], 19);
                FF(ref a, b, c, d, _x[12], 3); FF(ref d, a, b, c, _x[13], 7); FF(ref c, d, a, b, _x[14], 11); FF(ref b, c, d, a, _x[15], 19);

                // Round 2
                GG(ref a, b, c, d, _x[0], 3); GG(ref d, a, b, c, _x[4], 5); GG(ref c, d, a, b, _x[8], 9); GG(ref b, c, d, a, _x[12], 13);
                GG(ref a, b, c, d, _x[1], 3); GG(ref d, a, b, c, _x[5], 5); GG(ref c, d, a, b, _x[9], 9); GG(ref b, c, d, a, _x[13], 13);
                GG(ref a, b, c, d, _x[2], 3); GG(ref d, a, b, c, _x[6], 5); GG(ref c, d, a, b, _x[10], 9); GG(ref b, c, d, a, _x[14], 13);
                GG(ref a, b, c, d, _x[3], 3); GG(ref d, a, b, c, _x[7], 5); GG(ref c, d, a, b, _x[11], 9); GG(ref b, c, d, a, _x[15], 13);

                // Round 3
                HH(ref a, b, c, d, _x[0], 3); HH(ref d, a, b, c, _x[8], 9); HH(ref c, d, a, b, _x[4], 11); HH(ref b, c, d, a, _x[12], 15);
                HH(ref a, b, c, d, _x[2], 3); HH(ref d, a, b, c, _x[10], 9); HH(ref c, d, a, b, _x[6], 11); HH(ref b, c, d, a, _x[14], 15);
                HH(ref a, b, c, d, _x[1], 3); HH(ref d, a, b, c, _x[9], 9); HH(ref c, d, a, b, _x[5], 11); HH(ref b, c, d, a, _x[13], 15);
                HH(ref a, b, c, d, _x[3], 3); HH(ref d, a, b, c, _x[11], 9); HH(ref c, d, a, b, _x[7], 11); HH(ref b, c, d, a, _x[15], 15);

                _state[0] += a;
                _state[1] += b;
                _state[2] += c;
                _state[3] += d;
            }

            private static void FF(ref uint a, uint b, uint c, uint d, uint x, int s)
                => a = RotateLeft(a + F(b, c, d) + x, s);
            private static void GG(ref uint a, uint b, uint c, uint d, uint x, int s)
                => a = RotateLeft(a + G(b, c, d) + x + 0x5A827999, s);
            private static void HH(ref uint a, uint b, uint c, uint d, uint x, int s)
                => a = RotateLeft(a + H(b, c, d) + x + 0x6ED9EBA1, s);

            private static uint F(uint x, uint y, uint z) => (x & y) | (~x & z);
            private static uint G(uint x, uint y, uint z) => (x & y) | (x & z) | (y & z);
            private static uint H(uint x, uint y, uint z) => x ^ y ^ z;

            private static uint RotateLeft(uint x, int n) => (x << n) | (x >> (32 - n));

            private static byte[] Encode(uint[] input, int len)
            {
                var output = new byte[len];
                var i = 0;
                var j = 0;
                while (j < len)
                {
                    output[j] = (byte)(input[i] & 0xff);
                    output[j + 1] = (byte)((input[i] >> 8) & 0xff);
                    output[j + 2] = (byte)((input[i] >> 16) & 0xff);
                    output[j + 3] = (byte)((input[i] >> 24) & 0xff);
                    i++;
                    j += 4;
                }
                return output;
            }
        }
    }

    
}