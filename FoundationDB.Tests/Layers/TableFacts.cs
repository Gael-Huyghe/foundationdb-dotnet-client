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

namespace FoundationDB.Layers.Tables.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	[TestFixture]
	public class TableFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbTable_Read_Write_Delete()
		{

			using (var db = await OpenTestPartitionAsync())
			{

				var location = await GetCleanDirectory(db, "Tables");

				var table = new FdbTable<string, string>("Foos", location.Partition("Foos"), KeyValueEncoders.Values.StringEncoder);

				string secret = "world:" + Guid.NewGuid().ToString();

				// read non existing value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(async () => await table.GetAsync(tr, "hello"), Throws.InstanceOf<KeyNotFoundException>());

					var value = await table.TryGetAsync(tr, "hello");
					Assert.That(value.HasValue, Is.False);
					Assert.That(value.GetValueOrDefault(), Is.Null);
				}

				// write value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					table.Set(tr, "hello", secret);
					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// read value back
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var value = await table.GetAsync(tr, "hello");
					Assert.That(value, Is.EqualTo(secret));

					var opt = await table.TryGetAsync(tr, "hello");
					Assert.That(opt.HasValue, Is.True);
					Assert.That(opt.Value, Is.EqualTo(secret));
				}

				// directly read the value, behind the table's back
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var value = await tr.GetAsync(location.Pack("Foos", "hello"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo(secret));
				}

				// delete the value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					table.Clear(tr, "hello");
					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// verifiy that it is gone
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(async () => await table.GetAsync(tr, "hello"), Throws.InstanceOf<KeyNotFoundException>());

					var value = await table.TryGetAsync(tr, "hello");
					Assert.That(value.HasValue, Is.False);
					
					// also check directly
					var data = await tr.GetAsync(location.Pack("Foos", "hello"));
					Assert.That(data, Is.EqualTo(Slice.Nil));
				}

			}

		}

		[Test]
		public async Task Test_FdbTable_List()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "Tables");

				var table = new FdbTable<string, string>("Foos", location.Partition("Foos"), KeyValueEncoders.Values.StringEncoder);

				// write a bunch of keys
				await db.WriteAsync((tr) =>
				{
					table.Set(tr, "foo", "foo_value");
					table.Set(tr, "bar", "bar_value");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// read them back

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var value = await table.GetAsync(tr, "foo");
					Assert.That(value, Is.EqualTo("foo_value"));

					value = await table.GetAsync(tr, "bar");
					Assert.That(value, Is.EqualTo("bar_value"));

					Assert.That(async () => await table.GetAsync(tr, "baz"), Throws.InstanceOf<KeyNotFoundException>());

					var opt = await table.TryGetAsync(tr, "baz");
					Assert.That(opt.HasValue, Is.False);
				}

			}
		}

	}

}
