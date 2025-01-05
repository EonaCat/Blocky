using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Database.Models.Interfaces;
using EonaCat.Dns.Extensions;
using EonaCat.Dns.Models;
using EonaCat.Logger;
using SQLite;

namespace EonaCat.Dns.Database;

internal class SqliteRepository<T> where T : IEntity, new()
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _semaphore = new(1, 1);  // Ensures serialized writes

    internal SqliteRepository(SQLiteAsyncConnection database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public event EventHandler<T> OnEntityInsertedOrUpdated;

    internal AsyncTableQuery<T> Where(Expression<Func<T, bool>> predicate) => Table().Where(predicate);

    internal Task<long> CountAsync(string tableName, string columnName, bool isDistinct = false)
    {
        var query = isDistinct
            ? $"SELECT COUNT(DISTINCT {columnName}) FROM {tableName}"
            : $"SELECT COUNT({columnName}) FROM {tableName}";
        return _database.ExecuteScalarAsync<long>(query);
    }

    internal Task<long> SumAsync(string tableName, string columnName, bool isDistinct = false)
    {
        var query = isDistinct
            ? $"SELECT DISTINCT SUM({columnName}) FROM {tableName}"
            : $"SELECT SUM({columnName}) FROM {tableName}";
        return _database.ExecuteScalarAsync<long>(query);
    }

    private AsyncTableQuery<T> Table() => _database.Table<T>();

    internal Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        => Table().FirstOrDefaultAsync(predicate);

    internal AsyncTableQuery<T> GetAll() => Table();

    internal Task<int> CountAllAsync() => Table().CountAsync();

    internal Task<int> CountAllAsync(Expression<Func<T, bool>> predicate)
        => Table().CountAsync(predicate);

    internal Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        => Table().CountAsync(predicate).ContinueWith(t => t.Result > 0);

    internal Task<int> DeleteAsync(Expression<Func<T, bool>> predicate)
        => _database.DeleteAsync(predicate);

    internal Task<int> DeleteAsync(T entity)
        => _database.DeleteAsync(entity);

    internal async Task<T> InsertOrUpdateAsync(T entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        // Ensure serialized access to the database
        await _semaphore.WaitAsync();
        try
        {
            if (entity.Id > 0)
            {
                await _database.UpdateAsync(entity).ConfigureAwait(false);  // Update if entity exists
            }
            else
            {
                await _database.InsertAsync(entity).ConfigureAwait(false);  // Insert if new
                entity.Id = await _database.ExecuteScalarAsync<long>("SELECT last_insert_rowid()")
                    .ConfigureAwait(false);  // Get the last inserted ID
            }

            OnEntityInsertedOrUpdated?.Invoke(this, entity);
            return entity;
        }
        finally
        {
            _semaphore.Release();  // Release the semaphore
        }
    }

    internal Task<List<TopDomain>> GetLastQueriesAsync(long dateTimeStart)
        => QueryTopDomains(dateTimeStart, "Request", "DateTime DESC");

    internal Task<List<TopDomain>> GetTopDomainsAsync(long dateTimeStart, bool isBlocked = false)
        => QueryTopDomains(dateTimeStart, "Request", "COUNT(Request) DESC", isBlocked);

    internal Task<List<TopClient>> GetTopClientsAsync(long dateTimeStart)
        => QueryTopClients(dateTimeStart, "ClientIp", "COUNT(ClientIp) DESC");

    internal Task<List<TopRecordType>> GetTopRecordTypesAsync(long dateTimeStart)
        => QueryTopRecordTypes(dateTimeStart, "RecordType", "COUNT(RecordType) DESC");

    private Task<List<TopDomain>> QueryTopDomains(long dateTimeStart, string column, string orderBy, bool isBlocked = false)
    {
        var query = $"SELECT {column} as Domain, COUNT({column}) as Total FROM log WHERE DateTime > ?";
        if (isBlocked) query += " AND IsBlocked = 1";
        query += $" GROUP BY {column} ORDER BY {orderBy}";

        return _database.QueryAsync<TopDomain>(query, dateTimeStart);
    }

    private Task<List<TopClient>> QueryTopClients(long dateTimeStart, string column, string orderBy)
    {
        var query = $"SELECT {column} as Client, COUNT({column}) as Total FROM log WHERE DateTime > ?";
        query += $" GROUP BY {column} ORDER BY {orderBy}";

        return _database.QueryAsync<TopClient>(query, dateTimeStart);
    }

    private Task<List<TopRecordType>> QueryTopRecordTypes(long dateTimeStart, string column, string orderBy)
    {
        var query = $"SELECT {column} as RecordType, COUNT({column}) as Total FROM log WHERE DateTime > ?";
        query += $" GROUP BY {column} ORDER BY {orderBy}";

        return _database.QueryAsync<TopRecordType>(query, dateTimeStart);
    }

    internal async Task<long> BulkInsertOrUpdateAsync(IEnumerable<T> entities)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        long insertedCount = 0;

        // Ensure serialized access to the database
        await _semaphore.WaitAsync();
        try
        {
            // Access the underlying SQLiteConnection and execute a transaction
            await Task.Run(() =>
            {
                var connection = _database.GetConnection();
                connection.RunInTransaction(() =>
                {
                    foreach (var entity in entities)
                    {
                        if (entity.Id > 0)
                        {
                            // Update existing entity
                            connection.Update(entity);
                        }
                        else
                        {
                            // Insert new entity
                            connection.Insert(entity);
                            entity.Id = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
                        }

                        insertedCount++;
                        OnEntityInsertedOrUpdated?.Invoke(this, entity);
                    }
                });
            }).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release(); // Release the semaphore
        }

        return insertedCount;
    }


}
