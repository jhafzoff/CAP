﻿using DotNetCore.CAP;
using DotNetCore.CAP.Transport;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCore.CAP
{
    public class GoogleSpannerCapTransaction : CapTransactionBase
    {
        public GoogleSpannerCapTransaction(IDispatcher dispatcher) : base(dispatcher)
        {
        }

        public override void Commit()
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Commit();
                    break;
                case IDbContextTransaction dbContextTransaction:
                    dbContextTransaction.Commit();
                    break;
            }

            Flush();
        }

        public override async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Commit();
                    break;
                case IDbContextTransaction dbContextTransaction:
                    await dbContextTransaction.CommitAsync(cancellationToken);
                    break;
            }

            Flush();
        }

        public override void Rollback()
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Rollback();
                    break;
                case IDbContextTransaction dbContextTransaction:
                    dbContextTransaction.Rollback();
                    break;
            }
        }

        public override async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Rollback();
                    break;
                case IDbContextTransaction dbContextTransaction:
                    await dbContextTransaction.RollbackAsync(cancellationToken);
                    break;
            }
        }

        public override void Dispose()
        {
            (DbTransaction as IDbTransaction)?.Dispose();
            DbTransaction = null;

            GC.SuppressFinalize(this);
        }
    }

    public static class CapTransactionExtensions
    {
        public static ICapTransaction Begin(this ICapTransaction transaction,
            IDbTransaction dbTransaction, bool autoCommit = false)
        {
            transaction.DbTransaction = dbTransaction;
            transaction.AutoCommit = autoCommit;

            return transaction;
        }

        public static ICapTransaction Begin(this ICapTransaction transaction,
            IDbContextTransaction dbTransaction, bool autoCommit = false)
        {
            transaction.DbTransaction = dbTransaction;
            transaction.AutoCommit = autoCommit;

            return transaction;
        }

        /// <summary>
        /// Start the CAP transaction
        /// </summary>
        /// <param name="dbConnection">The <see cref="IDbConnection" />.</param>
        /// <param name="publisher">The <see cref="ICapPublisher" />.</param>
        /// <param name="autoCommit">Whether the transaction is automatically committed when the message is published</param>
        /// <returns>The <see cref="ICapTransaction" /> object.</returns>
        public static ICapTransaction BeginTransaction(this IDbConnection dbConnection,
            ICapPublisher publisher, bool autoCommit = false)
        {
            if (dbConnection.State == ConnectionState.Closed)
            {
                dbConnection.Open();
            }

            IDbTransaction dbTransaction = dbConnection.BeginTransaction();
            publisher.Transaction.Value = publisher.ServiceProvider.GetRequiredService<ICapTransaction>();
            return publisher.Transaction.Value.Begin(dbTransaction, autoCommit);
        }

        /// <summary>
        /// Start the CAP transaction
        /// </summary>
        /// <param name="database">The <see cref="DatabaseFacade" />.</param>
        /// <param name="publisher">The <see cref="ICapPublisher" />.</param>
        /// <param name="autoCommit">Whether the transaction is automatically committed when the message is published</param>
        /// <returns>The <see cref="IDbContextTransaction" /> of EF DbContext transaction object.</returns>
        public static IDbContextTransaction BeginTransaction(this DatabaseFacade database,
            ICapPublisher publisher, bool autoCommit = false)
        {
            IDbContextTransaction trans = database.BeginTransaction();
            publisher.Transaction.Value = publisher.ServiceProvider.GetRequiredService<ICapTransaction>();
            ICapTransaction capTrans = publisher.Transaction.Value.Begin(trans, autoCommit);
            return new CapEFDbTransaction(capTrans);
        }
    }
}
