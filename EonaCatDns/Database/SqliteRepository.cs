using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EonaCat.Dns.Database.Models.Interfaces;
using EonaCat.Dns.Models;
using SQLite;

namespace EonaCat.Dns.Database;

internal class SqliteRepository<T> where T : IEntity, new()
{
    private readonly SQLiteAsyncConnection _database;
    private readonly object _lock = new();

    internal SqliteRepository(SQLiteAsyncConnection database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public event EventHandler<T> OnEntityInsertedOrUpdated;

    internal AsyncTableQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        return Table().Where(predicate);
    }

    internal async Task<long> CountAsync(string tableName, string columnName, bool isDistinct = false)
    {
        var query = isDistinct
            ? $"SELECT COUNT(DISTINCT {columnName}) FROM {tableName}"
            : $"SELECT COUNT({columnName}) FROM {tableName}";
        return await _database.ExecuteScalarAsync<long>(query).ConfigureAwait(false);
    }

    internal async Task<long> SumAsync(string tableName, string columnName, bool isDistinct = false)
    {
        var query = isDistinct
            ? $"SELECT DISTINCT SUM({columnName}) FROM {tableName}"
            : $"SELECT SUM({columnName}) FROM {tableName}";
        return await _database.ExecuteScalarAsync<long>(query).ConfigureAwait(false);
    }

    private AsyncTableQuery<T> Table()
    {
        return _database.Table<T>();
    }

    internal async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await Table().FirstOrDefaultAsync(predicate).ConfigureAwait(false);
    }

    internal AsyncTableQuery<T> GetAll()
    {
        return Table();
    }

    internal async Task<int> CountAllAsync()
    {
        return await Table().CountAsync().ConfigureAwait(false);
    }

    internal async Task<int> CountAllAsync(Expression<Func<T, bool>> predicate)
    {
        return await Table().CountAsync(predicate).ConfigureAwait(false);
    }

    internal async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await Table().CountAsync(predicate) > 0;
    }

    internal async Task<T> GetByIdAsync(long id)
    {
        return await FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
    }

    internal async Task<T> GetByIdAsync(int id)
    {
        return await FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
    }

    internal async Task<int> DeleteAsync(Expression<Func<T, bool>> predicate)
    {
        return await _database.DeleteAsync(predicate).ConfigureAwait(false);
    }

    internal async Task<int> DeleteAsync(T entity)
    {
        return await _database.DeleteAsync(entity).ConfigureAwait(false);
    }

    internal async Task<T> ExecuteAsync<T>(string query, params object[] args)
    {
        return await _database.ExecuteScalarAsync<T>(query, args).ConfigureAwait(false);
    }

    internal async Task<CreateTableResult> TruncateTableAsync()
    {
        await _database.DropTableAsync<T>().ConfigureAwait(false);
        return await _database.CreateTableAsync<T>().ConfigureAwait(false);
    }

    internal async Task<IEnumerable<T>> BulkInsertOrUpdateAsync(IEnumerable<T> entities)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        await Task.Run(() =>
        {
            lock (_lock)
            {
                _database.RunInTransactionAsync(async x =>
                {
                    foreach (var entity in entities)
                        try
                        {
                            await InsertOrUpdateAsync(entity).ConfigureAwait(false);
                            OnEntityInsertedOrUpdated?.Invoke(null, entity);
                        }
                        catch (Exception exception)
                        {
                            await Logger.LogAsync(exception, "Could not insert or update entity").ConfigureAwait(false);
                        }
                });
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);
        return entities;
    }

    internal async Task<T> InsertOrUpdateAsync(T entity)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        try
        {
            if (entity.Id > 0)
            {
                await _database.UpdateAsync(entity).ConfigureAwait(false);
                OnEntityInsertedOrUpdated?.Invoke(null, entity);
            }
            else
            {
                try
                {
                    await _database.InsertAsync(entity).ConfigureAwait(false);
                    entity.Id = await _database.ExecuteScalarAsync<long>("SELECT last_insert_rowid()")
                        .ConfigureAwait(false);
                    OnEntityInsertedOrUpdated?.Invoke(null, entity);
                }
                catch (NullReferenceException)
                {
                    // Do nothing 
                }
            }

            return entity;
        }
        catch (SQLiteException exception)
        {
            await Logger.LogAsync(exception);
            return default;
        }
    }

    internal async Task<List<TopDomain>> GetLastQueriesAsync(long dateTimeStart)
    {
        return await QueryTopDomains(dateTimeStart, "Request", "DateTime DESC").ConfigureAwait(false);
    }

    internal async Task<List<TopDomain>> GetTopDomainsAsync(long dateTimeStart, bool isBlocked = false)
    {
        return await QueryTopDomains(dateTimeStart, "Request", "COUNT(Request) DESC", isBlocked).ConfigureAwait(false);
    }

    internal async Task<List<TopClient>> GetTopClientsAsync(long dateTimeStart)
    {
        return await QueryTopClients(dateTimeStart, "ClientIp", "COUNT(ClientIp) DESC").ConfigureAwait(false);
    }

    internal async Task<List<TopRecordType>> GetTopRecordTypesAsync(long dateTimeStart)
    {
        return await QueryTopRecordTypes(dateTimeStart, "RecordType", "COUNT(RecordType) DESC").ConfigureAwait(false);
    }

    private async Task<List<TopDomain>> QueryTopDomains(long dateTimeStart, string column, string orderBy,
        bool isBlocked = false)
    {
        var query = $"SELECT {column} as Domain, COUNT({column}) as Total FROM log WHERE DateTime > ?";

        if (isBlocked)
        {
            query += " AND IsBlocked = 1";
        }

        query += $" GROUP BY {column} ORDER BY {orderBy}";

        try
        {
            return await _database.QueryAsync<TopDomain>(query, dateTimeStart).ConfigureAwait(false);
        }
        finally
        {
            await _database.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<List<TopClient>> QueryTopClients(long dateTimeStart, string column, string orderBy,
        bool isBlocked = false)
    {
        var query = $"SELECT {column} as Client, COUNT({column}) as Total FROM log WHERE DateTime > ?";

        if (isBlocked)
        {
            query += " AND IsBlocked = 1";
        }

        query += $" GROUP BY {column} ORDER BY {orderBy}";

        try
        {
            return await _database.QueryAsync<TopClient>(query, dateTimeStart).ConfigureAwait(false);
        }
        finally
        {
            await _database.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<List<TopRecordType>> QueryTopRecordTypes(long dateTimeStart, string column, string orderBy,
        bool isBlocked = false)
    {
        var query = $"SELECT {column} as RecordType, COUNT({column}) as Total FROM log WHERE DateTime > ?";

        if (isBlocked)
        {
            query += " AND IsBlocked = 1";
        }

        query += $" GROUP BY {column} ORDER BY {orderBy}";

        try
        {
            return await _database.QueryAsync<TopRecordType>(query, dateTimeStart).ConfigureAwait(false);
        }
        finally
        {
            await _database.CloseAsync().ConfigureAwait(false);
        }
    }
}