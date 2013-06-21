﻿#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

namespace FoundationDb.Layers.Tuples
{
	using FoundationDb.Client;
	using FoundationDb.Client.Utils;
	using System;

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	public class FdbSubspace
	{
		/// <summary>Empty subspace, that does not add any prefix to the keys</summary>
		public static readonly FdbSubspace Empty = new FdbSubspace(FdbTuple.Empty);

		/// <summary>Store a memoized version of the tuple to speed up serialization</summary>
		public FdbMemoizedTuple Tuple { get; private set; }

		#region Constructors...

		public FdbSubspace(string prefix)
		{
			this.Tuple = new FdbTuple<string>(prefix).Memoize();
		}

		public FdbSubspace(IFdbTuple prefix)
		{
			this.Tuple = prefix.Memoize();
		}

		#endregion

		#region Partition...

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo,]).Partition(Bar) is equivalent to Subspace([Foo,Bar,])</remarks>
		/// <example>new FdbSubspace(["Users",]).Partition("Contacts") == new Subspace(["Users","Contacts",])</example>
		public FdbSubspace Partition<T>(T value)
		{
			return new FdbSubspace(this.Tuple.Append<T>(value));
		}

		#endregion

		#region Pack...

		public Slice Pack(Slice keyBlob)
		{
			var writer = OpenBuffer(keyBlob.Count);
			writer.WriteBytes(keyBlob);
			return writer.ToSlice();
		}

		public Slice Pack(IFdbTuple tuple)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(this.Tuple.Packed);
			tuple.PackTo(writer);
			return writer.ToSlice();
		}

		public Slice Pack<T>(T key)
		{
			var writer = OpenBuffer();
			FdbTuplePacker<T>.SerializeTo(writer, key);
			return writer.ToSlice();
		}

		public Slice Pack<T1, T2>(T1 key1, T2 key2)
		{
			var writer = OpenBuffer();
			FdbTuplePacker<T1>.SerializeTo(writer, key1);
			FdbTuplePacker<T2>.SerializeTo(writer, key2);
			return writer.ToSlice();
		}

		public Slice Pack<T1, T2, T3>(T1 key1, T2 key2, T3 key3)
		{
			var writer = OpenBuffer();
			FdbTuplePacker<T1>.SerializeTo(writer, key1);
			FdbTuplePacker<T2>.SerializeTo(writer, key2);
			FdbTuplePacker<T3>.SerializeTo(writer, key3);
			return writer.ToSlice();
		}

		public Slice Pack<T1, T2, T3, T4>(T1 key1, T2 key2, T3 key3, T4 key4)
		{
			var writer = OpenBuffer();
			FdbTuplePacker<T1>.SerializeTo(writer, key1);
			FdbTuplePacker<T2>.SerializeTo(writer, key2);
			FdbTuplePacker<T3>.SerializeTo(writer, key3);
			FdbTuplePacker<T4>.SerializeTo(writer, key4);
			return writer.ToSlice();
		}

		#endregion

		#region Create/Append...

		/// <summary>Append the subspace suffix to a key and return the full path</summary>
		/// <typeparam name="T">Type of the key to append</typeparam>
		/// <param name="value">Value of the key to append</param>
		/// <returns>Tuple that starts with the subspace's suffix, followed by the specified value</returns>
		/// <example>new FdbSubspace(["Users",]).Append(123) => ["Users",123,]</example>
		public FdbLinkedTuple<T> Create<T>(T value)
		{
			return new FdbLinkedTuple<T>(this.Tuple, value);
		}

		/// <summary>Append the subspace suffix to a pair of keys and return the full path</summary>
		/// <typeparam name="T1">Type of the first key to append</typeparam>
		/// <typeparam name="T2">Type of the second key to append</typeparam>
		/// <param name="value1">Value of the first key</param>
		/// <param name="value2">Value of the second key</param>
		/// <returns>Tuple that starts with the subspace's suffix, followed by the first, and second value</returns>
		/// <example>new FdbSubspace(["Users",]).Append("ContactsById", 123) => ["Users","ContactsById",123,]</example>
		public IFdbTuple Create<T1, T2>(T1 value1, T2 value2)
		{
			return this.Tuple.Concat(new FdbTuple<T1, T2>(value1, value2));
		}

		/// <summary>Append the subspace suffix to a triplet of keys and return the full path</summary>
		/// <typeparam name="T1">Type of the first key to append</typeparam>
		/// <typeparam name="T2">Type of the second key to append</typeparam>
		/// <typeparam name="T3">Type of the third key to append</typeparam>
		/// <param name="value1">Value of the first key</param>
		/// <param name="value2">Value of the second key</param>
		/// <param name="value3">Value of the third key</param>
		/// <returns>Tuple that starts with the subspace's suffix, followed by the first, second and third value</returns>
		/// <example>new FdbSubspace(["Users",]).Append("ContactsById", 123, "Bob") => ("Users","ContactsById",123,"Bob")</example>
		public IFdbTuple Create<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return this.Tuple.Concat(new FdbTuple<T1, T2, T3>(value1, value2, value3));
		}

		/// <summary>Append the subspace suffix to a list of keys and return the full path</summary>
		/// <param name="items">Liste of values to append after the subspace</param>
		/// <returns>Tuple that starts with the subspace's suffix, followed by the list of items</returns>
		/// <example>new FdbSubspace(["Users",]).Append("ContactsById", 123, 456, 789) => ("Users","ContactsById",123,456,789,)</example>
		public IFdbTuple Create(params object[] items)
		{
			return this.Tuple.Concat(FdbTuple.Create(items));
		}

		/// <summary>Append the subspace suffix to a triplet of keys and return the full path</summary>
		/// <typeparam name="T1">Type of the first key to append</typeparam>
		/// <typeparam name="T2">Type of the second key to append</typeparam>
		/// <typeparam name="T3">Type of the third key to append</typeparam>
		/// <param name="value1">Value of the first key</param>
		/// <param name="value2">Value of the second key</param>
		/// <param name="value3">Value of the third and last key</param>
		/// <returns>Tuple that starts with the subspace's suffix, followed by the first, second and third value</returns>
		/// <example>new FdbSubspace(["Users",]).Append("User123", "ContactsById", 456) => ("Users","User123","ContactsById",456,)</example>
		public IFdbTuple Append(IFdbTuple value)
		{
			if (value == null) throw new ArgumentNullException("value");

			return this.Tuple.Concat(value);
		}

		#endregion

		#region Unpack...

		/// <summary>Unpack a key into a tuple, with the subspace prefix removed</summary>
		/// <param name="key">Packed version of a key inside this subspace</param>
		/// <returns>Unpacked tuple that are relative to the current subspace</returns>
		/// <example>new Subspace("Foo").Unpack(FdbTuple.Pack("Foo", "Bar", 123)) => ("Bar", 123,) </example>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked tuple is not contained in this subspace</exception>
		public IFdbTuple Unpack(Slice key)
		{
			return FdbTuple.UnpackWithoutPrefix(key, this.Tuple.Packed);
		}

		/// <summary>Unpack an array of keys in tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		public IFdbTuple[] Unpack(Slice[] keys)
		{
			var tuples = new IFdbTuple[keys.Length];
			int p = 0;
			foreach (var key in keys)
			{
				tuples[p++] = FdbTuple.UnpackWithoutPrefix(key, this.Tuple.Packed);
			}
			return tuples;
		}

		#endregion

		internal FdbBufferWriter OpenBuffer(int extraBytes = 0)
		{
			var writer = new FdbBufferWriter();
			if (extraBytes > 0) writer.EnsureBytes(extraBytes + this.Tuple.PackedSize);
			writer.WriteBytes(this.Tuple.Packed);
			return writer;
		}

		public override string ToString()
		{
			return this.Tuple.ToString();
		}

		public override int GetHashCode()
		{
			return this.Tuple.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(obj, this)) return true;
			if (obj is FdbSubspace) return this.Tuple.Equals((obj as FdbSubspace).Tuple);
			if (obj is IFdbTuple) return this.Tuple.Equals(obj as IFdbTuple);
			return false;
		}
	
	}

}