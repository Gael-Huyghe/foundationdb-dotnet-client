﻿#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB.Layers.Collections.Tests
{
	using System;
	using System.Diagnostics;
	using System.Text;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	[Obsolete]
	public class RankedTestFacts : FdbTest
	{
		[Test]
		public async Task Test_Vector_Fast()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "ranked_set");

				var vector = new FdbRankedSet(location);

				await db.ReadWriteAsync(async (tr) =>
				{
					await vector.OpenAsync(tr);
					await PrintRankedSet(vector, tr);
				}, this.Cancellation);

				Log();
				var rnd = new Random();
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < 100; i++)
				{
					Console.Write("\rInserting " + i);
					await db.ReadWriteAsync((tr) => vector.InsertAsync(tr, TuPack.EncodeKey(rnd.Next())), this.Cancellation);
				}
				sw.Stop();
				Log("\rDone in {0:N3} sec", sw.Elapsed.TotalSeconds);

				Log(await db.ReadAsync((tr) => PrintRankedSet(vector, tr), this.Cancellation));
			}
		}

		private static async Task<string> PrintRankedSet(FdbRankedSet rs, IFdbReadOnlyTransaction tr)
		{
			var sb = new StringBuilder();
			for (int l = 0; l < 6; l++)
			{
				sb.AppendFormat("Level {0}:\r\n", l);
				await tr.GetRange(rs.Subspace.Partition.ByKey(l).Keys.ToRange()).ForEachAsync((kvp) =>
				{
					sb.AppendFormat("\t{0} = {1}\r\n", rs.Subspace.Keys.Unpack(kvp.Key), kvp.Value.ToInt64());
				});
			}
			return sb.ToString();
		}

	}

}
