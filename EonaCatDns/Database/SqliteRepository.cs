/*
EonaCatDns
Copyright (C) 2017-2023 EonaCat (Jeroen Saey)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License

*/

using EonaCat.Dns.Database.Models.Interfaces;
using EonaCat.Dns.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EonaCat.Dns.Database;

internal class SqliteRepository<T> where T : IEntity, new()
{
    private SQLiteAsyncConnection Database { get; }
    internal SqliteRepository(SQLiteAsyncConnection database)
    {
        Database = database;
    }

    internal AsyncTableQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        return Table().Where(predicate);
    }

    public async Task<long> CountAsync(string tableName, string columnName, bool isDistinct = false)
    {
        if (isDistinct)
        {
            return await Database.ExecuteScalarAsync<int>($"SELECT COUNT(DISTINCT {columnName}) FROM {tableName}").ConfigureAwait(false);
        }
        return await Database.ExecuteScalarAsync<int>($"SELECT COUNT({columnName}) FROM {tableName}").ConfigureAwait(false);
    }

    public async Task<long> SumAsync(string tableName, string columnName, bool isDistinct = false)
    {
        if (isDistinct)
        {
            return await Database.ExecuteScalarAsync<int>($"SELECT DISTINCT SUM({columnName}) FROM {tableName}").ConfigureAwait(false);
        }
        return await Database.ExecuteScalarAsync<int>($"SELECT SUM({columnName}) FROM {tableName}").ConfigureAwait(false);
    }

    private AsyncTableQuery<T> Table()
    {
        return Database.Table<T>();
    }

    public Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return Table().FirstOrDefaultAsync(predicate);
    }

    public AsyncTableQuery<T> GetAll()
    {
        return Table();
    }

    public Task<int> CountAllAsync()
    {
        return Table().CountAsync();
    }

    public Task<int> CountAllAsync(Expression<Func<T, bool>> predicate)
    {
        return Table().CountAsync(predicate);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await Table().CountAsync(predicate).ConfigureAwait(false) > 0;
    }

    public Task<T> GetByIdAsync(long id)
    {
        return FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<T> GetByIdAsync(int id)
    {
        return FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<int> DeleteAsync(Expression<Func<T, bool>> predicate)
    {
        return Database.DeleteAsync(predicate);
    }

    public Task<int> DeleteAsync(T entity)
    {
        return Database.DeleteAsync(entity);
    }

    public Task<T> ExecuteAsync<T>(string query, params object[] args)
    {
        return Database.ExecuteScalarAsync<T>(query, args);
    }

    public async Task<CreateTableResult> TruncateTableAsync()
    {
        await Database.DropTableAsync<T>().ConfigureAwait(false);
        return await Database.CreateTableAsync<T>().ConfigureAwait(false);
    }

    public async Task<IEnumerable<T>> BulkInsertOrUpdateAsync(IEnumerable<T> entities)
    {
        try
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            var updatedEntities = new List<T>();
            var insertedEntities = new List<T>();

            await Database.RunInTransactionAsync(transaction =>
            {
                var batchEntities = new List<T>();
                foreach (var entity in entities)
                {
                    batchEntities.Add(entity);
                    if (batchEntities.Count != 1000) continue; // Batch size

                    transaction.InsertAll(batchEntities);
                    transaction.UpdateAll(batchEntities);

                    insertedEntities.AddRange(batchEntities); // Track inserted entities
                    updatedEntities.AddRange(batchEntities); // Track updated entities

                    batchEntities.Clear();
                }

                if (batchEntities.Count <= 0) return;

                transaction.InsertAll(batchEntities);
                transaction.UpdateAll(batchEntities);

                insertedEntities.AddRange(batchEntities); // Track inserted entities
                updatedEntities.AddRange(batchEntities); // Track updated entities
            }).ConfigureAwait(false);

            // Set the IDs of the inserted entities
            if (insertedEntities.Any())
            {
                var lastId = await GetLastInsertedRowIdAsync().ConfigureAwait(false);
                foreach (var entity in insertedEntities)
                {
                    entity.Id = lastId++;
                }
            }

            // Return all the entities that were inserted or updated
            return updatedEntities.ToList().Concat(insertedEntities);
        }
        catch (SQLiteException exception)
        {
            Logger.Log(exception);
        }

        return null;
    }

    public async Task<T> InsertOrUpdateAsync(T entity)
    {
        try
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Id > 0)
            {
                await Database.UpdateAsync(entity).ConfigureAwait(false);
            }
            else
            {
                await Database.InsertAsync(entity).ConfigureAwait(false);
                entity.Id = await GetLastInsertedRowIdAsync().ConfigureAwait(false);
            }

            return entity;
        }
        catch (SQLiteException exception)
        {
            Logger.Log(exception);
        }

        return default;
    }

    public async Task<long> GetLastInsertedRowIdAsync()
    {
        var sql = @"select last_insert_rowid()";
        var lastId = await Database.ExecuteScalarAsync<long>(sql).ConfigureAwait(false);
        return lastId;
    }

    public async Task<List<TopDomain>> GetLastQueriesAsync(long dateTimeStart)
    {
        var connection = new SQLiteAsyncConnection(Database.GetConnection().DatabasePath);
        var query = @"SELECT request as Domain, DateTime as Total FROM log WHERE DateTime > ? group by Request ORDER BY DateTime DESC";

        try
        {
            var clients = await connection.QueryAsync<TopDomain>(query, dateTimeStart).ConfigureAwait(false);
            return clients;
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<List<TopDomain>> GetTopDomainsAsync(long dateTimeStart, bool isBlocked = false)
    {
        var connection = new SQLiteAsyncConnection(Database.GetConnection().DatabasePath);
        var query = @"SELECT request as Domain, count(request) as Total FROM log WHERE DateTime > ? AND IsBlocked = ? GROUP BY Request ORDER BY count(Request) DESC";

        try
        {
            var domains = await connection.QueryAsync<TopDomain>(query, dateTimeStart, isBlocked ? 1 : 0).ConfigureAwait(false);
            return domains;
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<List<TopClient>> GetTopClientsAsync(long dateTimeStart)
    {
        var connection = new SQLiteAsyncConnection(Database.GetConnection().DatabasePath);
        var query = @"SELECT ClientIp as Client, count(ClientIp) as Total FROM log WHERE DateTime > ? group by ClientIp ORDER BY count(ClientIp) DESC";

        try
        {
            var clients = await connection.QueryAsync<TopClient>(query, dateTimeStart).ConfigureAwait(false);
            return clients;
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<List<TopRecordType>> GetTopRecordTypesAsync(long dateTimeStart)
    {
        var connection = new SQLiteAsyncConnection(Database.GetConnection().DatabasePath);
        var query = @"SELECT RecordType as RecordType, count(RecordType) as Total FROM log WHERE DateTime > ? group by RecordType ORDER BY count(RecordType) DESC";

        try
        {
            var clients = await connection.QueryAsync<TopRecordType>(query, dateTimeStart).ConfigureAwait(false);
            return clients;
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}