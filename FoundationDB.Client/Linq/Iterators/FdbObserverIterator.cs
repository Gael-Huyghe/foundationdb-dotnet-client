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

namespace FoundationDB.Linq
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Observe the items of an async sequence</summary>
	/// <typeparam name="TSource">Type of the observed elements</typeparam>
	internal sealed class FdbObserverIterator<TSource> : FdbAsyncFilterIterator<TSource, TSource>
	{

		private readonly AsyncObserverExpression<TSource> m_observer;

		public FdbObserverIterator(IFdbAsyncEnumerable<TSource> source, AsyncObserverExpression<TSource> observer)
			: base(source)
		{
			Contract.Requires(observer != null);
			m_observer = observer;
		}

		protected override FdbAsyncIterator<TSource> Clone()
		{
			return new FdbObserverIterator<TSource>(m_source, m_observer);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (!await m_iterator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
				{ // completed
					return Completed();
				}

				if (cancellationToken.IsCancellationRequested) break;

				TSource current = m_iterator.Current;
				if (!m_observer.Async)
				{
					m_observer.Invoke(current);
				}
				else
				{
					await m_observer.InvokeAsync(current, cancellationToken).ConfigureAwait(false);
				}

				return Publish(current);
			}

			return Canceled(cancellationToken);
		}
	}

}
