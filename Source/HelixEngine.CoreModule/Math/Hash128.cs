using System;
using System.Text;

namespace HelixEngine
{
    // A 128 bit hash: SpookyHash V2 of the data appended (Bob Jenkins' public domain algorithm); as text 32 hex digits,
    // the first half's bytes from the lowest, then the second's.
    public partial struct Hash128 : IComparable, IComparable<Hash128>, IEquatable<Hash128>
    {
        public Hash128(uint u32_0, uint u32_1, uint u32_2, uint u32_3)
        {
            u64_0 = ((ulong)u32_1 << 32) | u32_0;
            u64_1 = ((ulong)u32_3 << 32) | u32_2;
        }

        public Hash128(ulong u64_0, ulong u64_1)
        {
            this.u64_0 = u64_0;
            this.u64_1 = u64_1;
        }

        public bool isValid => u64_0 != 0 || u64_1 != 0;

        public int CompareTo(Hash128 rhs) => u64_0 != rhs.u64_0 ? u64_0.CompareTo(rhs.u64_0) : u64_1.CompareTo(rhs.u64_1);
        public int CompareTo(object obj) => obj is Hash128 h ? CompareTo(h) : 1;
        public bool Equals(Hash128 obj) => u64_0 == obj.u64_0 && u64_1 == obj.u64_1;
        public override bool Equals(object obj) => obj is Hash128 h && Equals(h);
        public override int GetHashCode() => u64_0.GetHashCode() ^ u64_1.GetHashCode();
        public static bool operator ==(Hash128 hash1, Hash128 hash2) => hash1.Equals(hash2);
        public static bool operator !=(Hash128 hash1, Hash128 hash2) => !hash1.Equals(hash2);
        public static bool operator <(Hash128 x, Hash128 y) => x.CompareTo(y) < 0;
        public static bool operator >(Hash128 x, Hash128 y) => x.CompareTo(y) > 0;

        public override string ToString()
        {
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
            {
                ulong half = i < 8 ? u64_0 : u64_1;
                sb.Append(((byte)(half >> ((i % 8) * 8))).ToString("x2"));
            }
            return sb.ToString();
        }

        public static Hash128 Parse(string hashString)
        {
            ulong a = 0, b = 0;
            if (hashString == null || hashString.Length != 32)
                return new Hash128();
            for (int i = 0; i < 32; i++)
            {
                int v = Convert.ToInt32(hashString[i].ToString(), 16);
                int bytes = i / 2;
                int shift = (bytes % 8) * 8 + (i % 2 == 0 ? 4 : 0);
                if (bytes < 8)
                    a |= (ulong)v << shift;
                else
                    b |= (ulong)v << shift;
            }
            return new Hash128(a, b);
        }

        // ---------------------------------------------------------------------------------------------- computing

        public static Hash128 Compute(string data)
        {
            var h = new Hash128();
            h.Append(data);
            return h;
        }

        public static Hash128 Compute(int val) => Compute(BitConverter.GetBytes(val));
        public static Hash128 Compute(float val) => Compute(BitConverter.GetBytes(val));
        public static Hash128 Compute(byte[] data) => Compute(data, 0, data?.Length ?? 0);

        public static Hash128 Compute(byte[] data, int start, int count)
        {
            var h = new Hash128();
            h.Append(data, start, count);
            return h;
        }

        public static Hash128 Compute<T>(T val) where T : unmanaged
        {
            var h = new Hash128();
            h.Append(ref val);
            return h;
        }

        public void Append(string data) => Append(data == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(data));
        public void Append(int val) => Append(BitConverter.GetBytes(val));
        public void Append(float val) => Append(BitConverter.GetBytes(val));
        public void Append(byte[] data) => Append(data, 0, data?.Length ?? 0);

        public void Append(byte[] data, int start, int count)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (start < 0 || count < 0 || start + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(start));
            Spooky.Hash(new ReadOnlySpan<byte>(data, start, count), ref u64_0, ref u64_1);
        }

        public unsafe void Append<T>(ref T val) where T : unmanaged
        {
            fixed (T* p = &val)
                Spooky.Hash(new ReadOnlySpan<byte>(p, sizeof(T)), ref u64_0, ref u64_1);
        }

        static class Spooky
        {
            const ulong Const = 0xdeadbeefdeadbeefUL;
            const int Vars = 12, Block = 96, Buffer = 192;

            static ulong Rot(ulong x, int k) => (x << k) | (x >> (64 - k));
            static ulong U64(ReadOnlySpan<byte> s, int at) => BitConverter.ToUInt64(s.Slice(at, 8));
            static ulong U32(ReadOnlySpan<byte> s, int at) => BitConverter.ToUInt32(s.Slice(at, 4));

            // The hash of the data seeded by the two halves (the halves become the hash).
            public static void Hash(ReadOnlySpan<byte> m, ref ulong hash1, ref ulong hash2)
            {
                if (m.Length < Buffer)
                {
                    Short(m, ref hash1, ref hash2);
                    return;
                }
                var h = new ulong[Vars];
                h[0] = h[3] = h[6] = h[9] = hash1;
                h[1] = h[4] = h[7] = h[10] = hash2;
                h[2] = h[5] = h[8] = h[11] = Const;
                int blocks = m.Length / Block;
                var data = new ulong[Vars];
                for (int b = 0; b < blocks; b++)
                {
                    for (int i = 0; i < Vars; i++)
                        data[i] = U64(m, b * Block + i * 8);
                    Mix(data, h);
                }
                int done = blocks * Block, remainder = m.Length - done;
                var last = new byte[Block];
                m.Slice(done, remainder).CopyTo(last);
                last[Block - 1] = (byte)remainder;
                for (int i = 0; i < Vars; i++)
                    data[i] = BitConverter.ToUInt64(last, i * 8);
                for (int i = 0; i < Vars; i++)
                    h[i] += data[i];
                for (int r = 0; r < 3; r++)
                    EndPartial(h);
                hash1 = h[0];
                hash2 = h[1];
            }

            static void Mix(ulong[] d, ulong[] s)
            {
                s[0] += d[0]; s[2] ^= s[10]; s[11] ^= s[0]; s[0] = Rot(s[0], 11); s[11] += s[1];
                s[1] += d[1]; s[3] ^= s[11]; s[0] ^= s[1]; s[1] = Rot(s[1], 32); s[0] += s[2];
                s[2] += d[2]; s[4] ^= s[0]; s[1] ^= s[2]; s[2] = Rot(s[2], 43); s[1] += s[3];
                s[3] += d[3]; s[5] ^= s[1]; s[2] ^= s[3]; s[3] = Rot(s[3], 31); s[2] += s[4];
                s[4] += d[4]; s[6] ^= s[2]; s[3] ^= s[4]; s[4] = Rot(s[4], 17); s[3] += s[5];
                s[5] += d[5]; s[7] ^= s[3]; s[4] ^= s[5]; s[5] = Rot(s[5], 28); s[4] += s[6];
                s[6] += d[6]; s[8] ^= s[4]; s[5] ^= s[6]; s[6] = Rot(s[6], 39); s[5] += s[7];
                s[7] += d[7]; s[9] ^= s[5]; s[6] ^= s[7]; s[7] = Rot(s[7], 57); s[6] += s[8];
                s[8] += d[8]; s[10] ^= s[6]; s[7] ^= s[8]; s[8] = Rot(s[8], 55); s[7] += s[9];
                s[9] += d[9]; s[11] ^= s[7]; s[8] ^= s[9]; s[9] = Rot(s[9], 54); s[8] += s[10];
                s[10] += d[10]; s[0] ^= s[8]; s[9] ^= s[10]; s[10] = Rot(s[10], 22); s[9] += s[11];
                s[11] += d[11]; s[1] ^= s[9]; s[10] ^= s[11]; s[11] = Rot(s[11], 46); s[10] += s[0];
            }

            static void EndPartial(ulong[] h)
            {
                h[11] += h[1]; h[2] ^= h[11]; h[1] = Rot(h[1], 44);
                h[0] += h[2]; h[3] ^= h[0]; h[2] = Rot(h[2], 15);
                h[1] += h[3]; h[4] ^= h[1]; h[3] = Rot(h[3], 34);
                h[2] += h[4]; h[5] ^= h[2]; h[4] = Rot(h[4], 21);
                h[3] += h[5]; h[6] ^= h[3]; h[5] = Rot(h[5], 38);
                h[4] += h[6]; h[7] ^= h[4]; h[6] = Rot(h[6], 33);
                h[5] += h[7]; h[8] ^= h[5]; h[7] = Rot(h[7], 10);
                h[6] += h[8]; h[9] ^= h[6]; h[8] = Rot(h[8], 13);
                h[7] += h[9]; h[10] ^= h[7]; h[9] = Rot(h[9], 38);
                h[8] += h[10]; h[11] ^= h[8]; h[10] = Rot(h[10], 53);
                h[9] += h[11]; h[0] ^= h[9]; h[11] = Rot(h[11], 42);
                h[10] += h[0]; h[1] ^= h[10]; h[0] = Rot(h[0], 54);
            }

            static void ShortMix(ref ulong h0, ref ulong h1, ref ulong h2, ref ulong h3)
            {
                h2 = Rot(h2, 50); h2 += h3; h0 ^= h2;
                h3 = Rot(h3, 52); h3 += h0; h1 ^= h3;
                h0 = Rot(h0, 30); h0 += h1; h2 ^= h0;
                h1 = Rot(h1, 41); h1 += h2; h3 ^= h1;
                h2 = Rot(h2, 54); h2 += h3; h0 ^= h2;
                h3 = Rot(h3, 48); h3 += h0; h1 ^= h3;
                h0 = Rot(h0, 38); h0 += h1; h2 ^= h0;
                h1 = Rot(h1, 37); h1 += h2; h3 ^= h1;
                h2 = Rot(h2, 62); h2 += h3; h0 ^= h2;
                h3 = Rot(h3, 34); h3 += h0; h1 ^= h3;
                h0 = Rot(h0, 5); h0 += h1; h2 ^= h0;
                h1 = Rot(h1, 36); h1 += h2; h3 ^= h1;
            }

            static void ShortEnd(ref ulong h0, ref ulong h1, ref ulong h2, ref ulong h3)
            {
                h3 ^= h2; h2 = Rot(h2, 15); h3 += h2;
                h0 ^= h3; h3 = Rot(h3, 52); h0 += h3;
                h1 ^= h0; h0 = Rot(h0, 26); h1 += h0;
                h2 ^= h1; h1 = Rot(h1, 51); h2 += h1;
                h3 ^= h2; h2 = Rot(h2, 28); h3 += h2;
                h0 ^= h3; h3 = Rot(h3, 9); h0 += h3;
                h1 ^= h0; h0 = Rot(h0, 47); h1 += h0;
                h2 ^= h1; h1 = Rot(h1, 54); h2 += h1;
                h3 ^= h2; h2 = Rot(h2, 32); h3 += h2;
                h0 ^= h3; h3 = Rot(h3, 25); h0 += h3;
                h1 ^= h0; h0 = Rot(h0, 63); h1 += h0;
            }

            static void Short(ReadOnlySpan<byte> m, ref ulong hash1, ref ulong hash2)
            {
                int length = m.Length, remainder = length % 32, at = 0;
                ulong a = hash1, b = hash2, c = Const, d = Const;
                if (length > 15)
                {
                    int end = length / 32 * 32;
                    for (; at < end; at += 32)
                    {
                        c += U64(m, at);
                        d += U64(m, at + 8);
                        ShortMix(ref a, ref b, ref c, ref d);
                        a += U64(m, at + 16);
                        b += U64(m, at + 24);
                    }
                    if (remainder >= 16)
                    {
                        c += U64(m, at);
                        d += U64(m, at + 8);
                        ShortMix(ref a, ref b, ref c, ref d);
                        at += 16;
                        remainder -= 16;
                    }
                }
                d += (ulong)length << 56;
                ReadOnlySpan<byte> r = m.Slice(at);
                switch (remainder)
                {
                case 15: d += (ulong)r[14] << 48; goto case 14;
                case 14: d += (ulong)r[13] << 40; goto case 13;
                case 13: d += (ulong)r[12] << 32; goto case 12;
                case 12: d += U32(r, 8); c += U64(r, 0); break;
                case 11: d += (ulong)r[10] << 16; goto case 10;
                case 10: d += (ulong)r[9] << 8; goto case 9;
                case 9: d += r[8]; goto case 8;
                case 8: c += U64(r, 0); break;
                case 7: c += (ulong)r[6] << 48; goto case 6;
                case 6: c += (ulong)r[5] << 40; goto case 5;
                case 5: c += (ulong)r[4] << 32; goto case 4;
                case 4: c += U32(r, 0); break;
                case 3: c += (ulong)r[2] << 16; goto case 2;
                case 2: c += (ulong)r[1] << 8; goto case 1;
                case 1: c += r[0]; break;
                case 0: c += Const; d += Const; break;
                }
                ShortEnd(ref a, ref b, ref c, ref d);
                hash1 = a;
                hash2 = b;
            }
        }
    }
}
