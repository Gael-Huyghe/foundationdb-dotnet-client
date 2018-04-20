﻿#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	[DebuggerDisplay("[{ToString()}]")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public struct Uuid64 : IFormattable, IEquatable<Uuid64>, IComparable<Uuid64>
	{
		public static readonly Uuid64 Empty = default(Uuid64);

		private readonly ulong m_value;

		public Uuid64(ulong value)
		{
			m_value = value;
		}

		public Uuid64(long value)
		{
			m_value = (ulong)value;
		}

		public Uuid64(byte[] value)
		{
			if (value == null) throw new ArgumentNullException("value");
			if (value.Length != 8) throw new ArgumentException("Value must be 8 bytes long", "value");

			m_value = Read(value, 0);
		}

		public Uuid64(Slice value)
		{
			if (value == null) throw new ArgumentNullException("value");
			if (value.Count != 8) throw new ArgumentException("Value must be 8 bytes long", "value");

			m_value = Read(value.Array, value.Offset);
		}

		public Uuid64(string value)
		{
			if (!TryParse(value, out m_value))
			{
				throw new FormatException("Invalid Uuid64 format");
			}
		}

		/// <summary>Generate a new random 64-bit UUID, using a global source of randomness.</summary>
		/// <returns>Instance of a new Uuid64 that is random.</returns>
		/// <remarks>
		/// <p>If you need sequential uuids, you should use a different generator (ex: FlakeID, ...)</p>
		/// <p>This method uses a cryptographic RNG under a lock to generate 8 bytes of randomness, which can be slow. If you must generate a large number of unique ids, you should use a different source.</p>
		/// </remarks>
		public static Uuid64 NewUuid()
		{
			//Note: we chould use Guid.NewGuid() as a source of randomness, but even though a guid is "guaranteed" to be unique, a substring of a guid is not.. or is it?
			return Uuid64RandomGenerator.Default.NewUuid();
		}

		#region Parsing...

		public static Uuid64 Parse([NotNull] string s)
		{
			if (s == null) throw new ArgumentNullException("s");
			ulong value;
			if (!TryParse(s, out value))
			{
				throw new FormatException("Invalid Uuid64 format");
			}
			return new Uuid64(value);
		}

		public static bool TryParse([NotNull] string s, out Uuid64 result)
		{
			if (s == null) throw new ArgumentNullException("s");
			ulong value;
			if (!TryParse(s, out value))
			{
				result = default(Uuid64);
				return false;
			}
			result = new Uuid64(value);
			return true;
		}

		private static bool TryParse(string s, out ulong result)
		{
			Contract.Requires(s != null);

			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			result = 0;
			switch (s.Length)
			{
				case 19:
				{ // {xxxxxxxx-xxxxxxxx}
					if (s[0] != '{' || s[18] != '}')
					{
						return false;
					}
					return TryDecode16(s.ToCharArray(), 1, true, out result);
				}
				case 18:
				{ // {xxxxxxxxxxxxxxxx}
					if (s[0] != '{' || s[17] != '}')
					{
						return false;
					}
					return TryDecode16(s.ToCharArray(), 1, false, out result);
				}
				case 17:
				{ // xxxxxxxx-xxxxxxxx
					if (s[8] != '-') return false;
					return TryDecode16(s.ToCharArray(), 0, true, out result);
				}
				case 16:
				{ // xxxxxxxxxxxxxxxx
					return TryDecode16(s.ToCharArray(), 0, false, out result);
				}
			}

			// only base62 is allowed
			if (s.Length <= 11)
			{
				return TryDecode62(s.ToCharArray(), out result);
			}

			return false;
		}

		#endregion

		#region Casting...

		public static implicit operator Uuid64(ulong value)
		{
			return new Uuid64(value);
		}

		public static explicit operator ulong(Uuid64 value)
		{
			return value.m_value;
		}

		public static implicit operator Uuid64(long value)
		{
			return new Uuid64(value);
		}

		public static explicit operator long(Uuid64 value)
		{
			return (long)value.m_value;
		}

		#endregion

		#region IFormattable...

		public long ToInt64()
		{
			return (long)m_value;
		}

		public ulong ToUInt64()
		{
			return m_value;
		}

		public Slice ToSlice()
		{
			return Slice.FromFixedU64BE(m_value);
		}

		public byte[] ToByteArray()
		{
			var bytes = Slice.FromFixedU64BE(m_value).Array;
			Contract.Assert(bytes != null && bytes.Length == 8); // HACKHACK: for perf reasons, we rely on the fact that Slice.FromFixedU64BE() allocates a new 8-byte array that we can return without copying
			return bytes;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format "xxxxxxxx-xxxxxxxx", where 'x' is a lower-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString(null, null);
		}

		/// <summary>Returns a string representation of the value of this <see cref="Uuid64"/> instance, according to the provided format specifier.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "B", "X", "G", "Z" or "N". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid64"/>, using the specified format.</returns>
		/// <remarks>See <see cref="ToString(string, IFormatProvider)"/> for a description of the different formats</remarks>
		public string ToString(string format)
		{
			return ToString(format, null);
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <param name="formatProvider">This argument is ignored</param>
		/// <returns>String using the format "xxxxxxxx-xxxxxxxx", where 'x' is a lower-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public string ToString(IFormatProvider formatProvider)
		{
			return ToString("D", null);
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid64"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid64"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as two groups of 8 hexadecimal digits, separated by an hyphen: "01234567-89abcdef" (17 characters).</p>
		/// <p>The <b>X</b> format encodes the value as a single group of 16 hexadecimal digits: "0123456789abcdef" (16 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with '{' and '}': "{01234567-89abcdef}" (19 characters).</p>
		/// <p>The <b>R</b> format encodes the value as a decimal number "1234567890" (1 to 20 characters) which can be parsed as an UInt64 without loss.</p>
		/// <p>The <b>C</b> format uses a compact base-62 encoding that preserves lexicographical ordering, composed of digits, uppercase alpha and lowercase alpha, suitable for compact representation that can fit in a querystring.</p>
		/// <p>The <b>Z</b> format is equivalent to the <b>C</b> format, but with extra padding so that the string is always 11 characters long.</p>
		/// </example>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = "D";

			switch(format)
			{
				case "D":
				{ // Default format is "xxxxxxxx-xxxxxxxx"
					return Encode16(m_value, separator: true, quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxxxxxx-xxxxxxxx"
					return Encode16(m_value, separator: true, quotes: false, upper: false);
				}

				case "C":
				case "c":
				{ // base 62, compact, no padding
					return Encode62(m_value, padded: false);
				}
				case "Z":
				case "z":
				{ // base 62, padded with '0' up to 11 chars
					return Encode62(m_value, padded: true);
				}

				case "R":
				case "r":
				{ // Integer: "1234567890"
					return m_value.ToString(null, formatProvider ?? CultureInfo.InvariantCulture);
				}

				case "X": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "N":
				{ // "XXXXXXXXXXXXXXXX"
					return Encode16(m_value, separator: false, quotes: false, upper: true);
				}
				case "x": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "n":
				{ // "xxxxxxxxxxxxxxxx"
					return Encode16(m_value, separator: false, quotes: false, upper: false);
				}

				case "B":
				{ // "{xxxxxxxx-xxxxxxxx}"
					return Encode16(m_value, separator: true, quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxxxxxx-xxxxxxxx}"
					return Encode16(m_value, separator: true, quotes: true, upper: false);
				}
			}
			throw new FormatException("Invalid Uuid64 format specification.");
		}

		#endregion

		#region IEquatable / IComparable...

		public override bool Equals(object obj)
		{
			if (obj is Uuid64) return Equals((Uuid64)obj);
			if (obj is ulong) return m_value == (ulong)obj;
			if (obj is long) return m_value == (ulong)(long)obj;
			//TODO: string format ? Slice ?
			return false;
		}

		public override int GetHashCode()
		{
			return ((int)m_value) ^ (int)(m_value >> 32);
		}

		public bool Equals(Uuid64 other)
		{
			return m_value == other.m_value;
		}

		public int CompareTo(Uuid64 other)
		{
			return m_value.CompareTo(other.m_value);
		}

		#endregion

		#region Base16 encoding...

		private static char HexToLowerChar(int a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'a') : (char)(a + '0');
		}

		private static unsafe char* HexsToLowerChars(char* ptr, int a)
		{
			Contract.Requires(ptr != null);
			ptr[0] = HexToLowerChar(a >> 28);
			ptr[1] = HexToLowerChar(a >> 24);
			ptr[2] = HexToLowerChar(a >> 20);
			ptr[3] = HexToLowerChar(a >> 16);
			ptr[4] = HexToLowerChar(a >> 12);
			ptr[5] = HexToLowerChar(a >> 8);
			ptr[6] = HexToLowerChar(a >> 4);
			ptr[7] = HexToLowerChar(a);
			return ptr + 8;
		}

		private static char HexToUpperChar(int a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'A') : (char)(a + '0');
		}

		private static unsafe char* HexsToUpperChars(char* ptr, int a)
		{
			Contract.Requires(ptr != null);
			ptr[0] = HexToUpperChar(a >> 28);
			ptr[1] = HexToUpperChar(a >> 24);
			ptr[2] = HexToUpperChar(a >> 20);
			ptr[3] = HexToUpperChar(a >> 16);
			ptr[4] = HexToUpperChar(a >> 12);
			ptr[5] = HexToUpperChar(a >> 8);
			ptr[6] = HexToUpperChar(a >> 4);
			ptr[7] = HexToUpperChar(a);
			return ptr + 8;
		}

		private unsafe static string Encode16(ulong value, bool separator, bool quotes, bool upper)
		{
			int size = 16 + (separator ? 1 : 0) + (quotes ? 2 : 0);
			char* buffer = stackalloc char[24]; // max 19 mais on arrondi a 24

			char* ptr = buffer;
			if (quotes) *ptr++ = '{';
			ptr = upper 		
				? HexsToUpperChars(ptr, (int)(value >> 32))
				: HexsToLowerChars(ptr, (int)(value >> 32));
			if (separator) *ptr++ = '-';
			ptr = upper
				? HexsToUpperChars(ptr, (int)(value & 0xFFFFFFFF))
				: HexsToLowerChars(ptr, (int)(value & 0xFFFFFFFF));
			if (quotes) *ptr++ = '}';

			Contract.Assert(ptr == buffer + size);
			return new string(buffer, 0, size);
		}

		private const int INVALID_CHAR = -1;

		private static int CharToHex(char c)
		{
			if (c <= '9')
			{
				return c >= '0' ? (c - 48) : INVALID_CHAR;
			}
			if (c <= 'F')
			{
				return c >= 'A' ? (c - 55) : INVALID_CHAR;
			}
			if (c <= 'f')
			{
				return c >= 'a' ? (c - 87) : INVALID_CHAR;
			}
			return INVALID_CHAR;
		}

		private static bool TryCharsToHexs(char[] chars, int offset, out uint result)
		{
			int word = 0;
			for (int i = 0; i < 8; i++)
			{
				int a = CharToHex(chars[offset++]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (uint)word;
			return true;
		}

		private static bool TryDecode16(char[] chars, int offset, bool separator, out ulong result)
		{
			uint a, b;

			if ((!separator || chars[offset + 8] == '-')
				&& TryCharsToHexs(chars, offset, out a) 
				&& TryCharsToHexs(chars, offset + (separator ? 9 : 8), out b))
			{
				result = ((ulong)a << 32) | (ulong)b;
				return true;
			}

			result = 0;
			return false;
		}

		#endregion

		#region Base62 encoding...

		//NOTE: this version of base62 encoding puts the digits BEFORE the letters, to ensure that the string representation of a UUID64 is in the same order as its byte[] or ulong version.
		// => This scheme use the "0-9A-Za-z" ordering, while most other base62 encoder use "a-zA-Z0-9"

		private static readonly char[] Base62LexicographicChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
		private static readonly int[] Base62Values = new int[3 * 32]
		{ 
			/* 32.. 63 */ -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, -1, -1, -1, -1, -1,
			/* 64.. 95 */ -1, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, -1, -1, -1, -1, -1,
			/* 96..127 */ -1, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1,
		};

		/// <summary>Encode a 64-bit value into a base-62 string</summary>
		/// <param name="value">64-bit value to encode</param>
		/// <param name="padded">If true, keep the leading '0' to return a string of length 11. If false, discards all extra leading '0' digits.</param>
		/// <returns>String that contains only digits, lower and upper case letters. The string will be lexicographically ordered, which means that sorting by string will give the same order as sorting by value.</returns>
		/// <sample>
		/// Encode62(0, false) => "0"
		/// Encode62(0, true) => "00000000000"
		/// Encode62(0xDEADBEEF) => ""
		/// </sample>
		private static string Encode62(ulong value, bool padded)
		{
			// special case for default(Uuid64) which may be more frequent than others
			if (value == 0) return padded ? "00000000000" : "0";

			// encoding a 64 bits value in Base62 yields 10.75 "digits", which is rounded up to 11 chars.
			const int MAX_SIZE = 11;

			unsafe
			{
				// The maximum size is 11 chars, but we will allocate 64 bytes on the stack to keep alignment.
				char* chars = stackalloc char[16];
				char[] bc = Base62LexicographicChars;

				// start from the last "digit"
				char* pc = chars + (MAX_SIZE - 1);

				while (pc >= chars)
				{
					ulong r = value % 62L;
					value /= 62L;
					*pc-- = bc[(int)r];
					if (!padded && value == 0)
					{ // the rest will be all zeroes
						break;
					}
				}

				++pc;
				int count = MAX_SIZE - (int)(pc - chars);
				Contract.Assert(count > 0 && count <= 11);
				return count <= 0 ? String.Empty : new string(pc, 0, count);
			}
		}

		private static bool TryDecode62(char[] s, out ulong value)
		{
			if (s == null || s.Length == 0 || s.Length > 11)
			{ // fail: too small/too big
				value = 0;
				return false;
			}

			// we know that the original value is exactly 64bits, and any missing digit is '0'
			ulong factor = 1UL;
			ulong acc = 0UL;
			int p = s.Length - 1;
			int[] bv = Base62Values;
			while (p >= 0)
			{
				// read digit
				int a = s[p];
				// decode base62 digit
				a = a >= 32 && a < 128 ? bv[a - 32] : -1;
				if (a == -1)
				{ // fail: invalid character
					value = 0;
					return false;
				}
				// accumulate, while checking for overflow
				acc = checked(acc + ((ulong)a * factor));
				if (p-- > 0) factor *= 62;
			}
			value = acc;
			return true;
		}

		#endregion

		#region Fast I/O...

		internal static ulong Read(byte[] buffer, int offset)
		{
			Contract.Requires(buffer != null && offset >= 0 && offset + 7 < buffer.Length);
			// buffer contains the bytes in Big Endian
			ulong res = buffer[offset + 7];
			res |= ((ulong)buffer[offset + 6]) << 8;
			res |= ((ulong)buffer[offset + 5]) << 16;
			res |= ((ulong)buffer[offset + 4]) << 24;
			res |= ((ulong)buffer[offset + 3]) << 32;
			res |= ((ulong)buffer[offset + 2]) << 40;
			res |= ((ulong)buffer[offset + 1]) << 48;
			res |= ((ulong)buffer[offset + 0]) << 56;
			return res;
		}

		internal unsafe static ulong Read(byte* src)
		{
			ulong tmp;

			if (BitConverter.IsLittleEndian)
			{ // Intel ?
				byte* ptr = (byte*)&tmp;
				// big endian
				ptr[0] = src[7];
				ptr[1] = src[6];
				ptr[2] = src[5];
				ptr[3] = src[4];
				ptr[4] = src[3];
				ptr[5] = src[2];
				ptr[6] = src[1];
				ptr[7] = src[0];
			}
			else
			{ // ARM ?
				tmp = *((ulong*)src);
			}

			return tmp;
		}

		internal unsafe static void Write(ulong value, byte* ptr)
		{
			if (BitConverter.IsLittleEndian)
			{ // Intel ?
				byte* src = (byte*)&value;
				ptr[0] = src[7];
				ptr[1] = src[6];
				ptr[2] = src[5];
				ptr[3] = src[4];
				ptr[4] = src[3];
				ptr[5] = src[2];
				ptr[6] = src[1];
				ptr[7] = src[0];
			}
			else
			{ // ARM ?
				*((ulong*)ptr) = value;
			}

		}

		internal unsafe void WriteTo(byte* ptr)
		{
			Write(m_value, ptr);
		}

		#endregion

		#region Operators...

		public static bool operator ==(Uuid64 left, Uuid64 right)
		{
			return left.m_value == right.m_value;
		}

		public static bool operator !=(Uuid64 left, Uuid64 right)
		{
			return left.m_value != right.m_value;
		}

		public static bool operator >(Uuid64 left, Uuid64 right)
		{
			return left.m_value > right.m_value;
		}

		public static bool operator >=(Uuid64 left, Uuid64 right)
		{
			return left.m_value >= right.m_value;
		}

		public static bool operator <(Uuid64 left, Uuid64 right)
		{
			return left.m_value < right.m_value;
		}

		public static bool operator <=(Uuid64 left, Uuid64 right)
		{
			return left.m_value <= right.m_value;
		}

		// Comparing an Uuid64 to a 64-bit integer can have sense for "if (id == 0)" or "if (id != 0)" ?

		public static bool operator ==(Uuid64 left, long right)
		{
			return left.m_value == (ulong)right;
		}

		public static bool operator ==(Uuid64 left, ulong right)
		{
			return left.m_value == right;
		}

		public static bool operator !=(Uuid64 left, long right)
		{
			return left.m_value != (ulong)right;
		}

		public static bool operator !=(Uuid64 left, ulong right)
		{
			return left.m_value != right;
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid64 operator +(Uuid64 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			ulong v = (ulong)right;
			return new Uuid64(checked(left.m_value + v));
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid64 operator +(Uuid64 left, ulong right)
		{
			return new Uuid64(checked(left.m_value + right));
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid64 operator -(Uuid64 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			ulong v = (ulong)right;
			return new Uuid64(checked(left.m_value - v));
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid64 operator -(Uuid64 left, ulong right)
		{
			return new Uuid64(checked(left.m_value - right));
		}

		/// <summary>Increments the value of this instance</summary>
		public static Uuid64 operator ++(Uuid64 value)
		{
			return new Uuid64(checked(value.m_value + 1));
		}

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid64 operator --(Uuid64 value)
		{
			return new Uuid64(checked(value.m_value - 1));
		}

		#endregion

	}

	/// <summary>Helper class for generating 64-bit UUIDs from a secure random number generator</summary>
	public sealed class Uuid64RandomGenerator
	{

		/// <summary>Default instance of a random generator</summary>
		/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
		public static readonly Uuid64RandomGenerator Default = new Uuid64RandomGenerator();

		private readonly System.Security.Cryptography.RandomNumberGenerator m_rng;
		private readonly byte[] m_scratch = new byte[8];

		/// <summary>Create a new instance of a random UUID generator</summary>
		public Uuid64RandomGenerator()
			: this(null)
		{ }

		/// <summary>Create a new instance of a random UUID generator, using a specific random number generator</summary>
		public Uuid64RandomGenerator(System.Security.Cryptography.RandomNumberGenerator generator)
		{
			m_rng = generator ?? System.Security.Cryptography.RandomNumberGenerator.Create();
		}

		/// <summary>Return a new random 64-bit UUID</summary>
		/// <returns>Uuid64 that contains 64 bits worth of randomness.</returns>
		/// <remarks>
		/// <p>This methods needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each threads.</p>
		/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
		/// </remarks>
		public Uuid64 NewUuid()
		{
			lock (m_rng)
			{
				// get 8 bytes of randomness (0 allowed)
				m_rng.GetBytes(m_scratch);
				return new Uuid64(m_scratch);
			}
		}

	}

}
