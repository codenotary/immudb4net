/*
Copyright 2022 CodeNotary, Inc. All rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace ImmuDB;

using ImmuDB.SQL;
using ImmudbProxy;
using static ImmuDB.ImmuClient;

public interface IImmuClient
{
    TimeSpan ConnectionShutdownTimeout { get; set; }
    TimeSpan IdleConnectionCheckInterval { get; }
    string GrpcAddress { get; }
    bool DeploymentInfoCheck { get; set; }

    Task ChangePassword(string user, string oldPassword, string newPassword);
    Task Close();
    Task CompactIndex();
    Task CreateDatabase(string database);
    Task CreateUser(string user, string password, Iam.Permission permission, string database);
    ImmuState CurrentState();
    Task<List<string>> Databases();
    Task<TxHeader> Delete(string key);
    Task<TxHeader> Delete(byte[] key);
    Task FlushIndex(float cleanupPercentage, bool synced);
    Task<Entry> Get(string key, ulong tx);
    Task<Entry> Get(string key);
    Task<List<Entry>> GetAll(List<string> keys);
    Task<Entry> GetAtRevision(string key, long rev);
    Task<Entry> GetAtRevision(byte[] key, long rev);
    Task<Entry> GetAtTx(byte[] key, ulong tx);
    Task<Entry> GetSinceTx(string key, ulong tx);
    Task<Entry> GetSinceTx(byte[] key, ulong tx);
    Task<bool> HealthCheck();
    Task<List<Entry>> History(string key, int limit, ulong offset, bool desc);
    Task<List<Entry>> History(byte[] key, int limit, ulong offset, bool desc);
    bool IsClosed();
    bool IsConnected();
    Task<List<Iam.User>> ListUsers();
    Task Open(string username, string password, string defaultdb);
    Task Reconnect();
    Task<List<Entry>> Scan(byte[] prefix, byte[] seekKey, byte[] endKey, bool inclusiveSeek, bool inclusiveEnd, ulong limit, bool desc);
    Task<List<Entry>> Scan(string prefix);
    Task<List<Entry>> Scan(byte[] prefix);
    Task<List<Entry>> Scan(string prefix, ulong limit, bool desc);
    Task<List<Entry>> Scan(byte[] prefix, ulong limit, bool desc);
    Task<List<Entry>> Scan(string prefix, string seekKey, ulong limit, bool desc);
    Task<List<Entry>> Scan(string prefix, string seekKey, string endKey, ulong limit, bool desc);
    Task<List<Entry>> Scan(byte[] prefix, byte[] seekKey, ulong limit, bool desc);
    Task<List<Entry>> Scan(byte[] prefix, byte[] seekKey, byte[] endKey, ulong limit, bool desc);
    Task<TxHeader> Set(byte[] key, byte[] value);
    Task<TxHeader> Set(string key, byte[] value);
    Task<TxHeader> Set(string key, string value);
    Task<TxHeader> SetAll(List<KVPair> kvList);
    Task<TxHeader> SetReference(byte[] key, byte[] referencedKey, ulong atTx);
    Task<TxHeader> SetReference(string key, string referencedKey, ulong atTx);
    Task<TxHeader> SetReference(string key, string referencedKey);
    Task<TxHeader> SetReference(byte[] key, byte[] referencedKey);
    Task<SQL.SQLExecResult> SQLExec(string sqlStatement, params SQLParameter[] parameters);
    Task<SQL.SQLQueryResult> SQLQuery(string sqlStatement, params SQLParameter[] parameters);
    ImmuState State();
    Task<Tx> TxById(ulong txId);
    Task<List<Tx>> TxScan(ulong initialTxId);
    Task<List<Tx>> TxScan(ulong initialTxId, uint limit, bool desc);
    Task UseDatabase(string database);
    Task<Entry> VerifiedGet(KeyRequest keyReq, ImmuState state);
    Task<Entry> VerifiedGet(string key);
    Task<Entry> VerifiedGet(byte[] key);
    Task<Entry> VerifiedGetAtRevision(string key, long rev);
    Task<Entry> VerifiedGetAtRevision(byte[] key, long rev);
    Task<Entry> VerifiedGetAtTx(string key, ulong tx);
    Task<Entry> VerifiedGetAtTx(byte[] key, ulong tx);
    Task<Entry> VerifiedGetSinceTx(string key, ulong tx);
    Task<Entry> VerifiedGetSinceTx(byte[] key, ulong tx);
    Task<TxHeader> VerifiedSet(string key, byte[] value);
    Task<TxHeader> VerifiedSet(string key, string value);
    Task<TxHeader> VerifiedSet(byte[] key, byte[] value);
    Task<Tx> VerifiedTxById(ulong txId);
    Task<TxHeader> VerifiedZAdd(byte[] set, byte[] key, ulong atTx, double score);
    Task<TxHeader> VerifiedZAdd(string set, string key, double score);
    Task<TxHeader> VerifiedZAdd(byte[] set, byte[] key, double score);
    Task<TxHeader> VerifiedZAdd(string set, string key, ulong atTx, double score);
    Task<TxHeader> ZAdd(byte[] set, byte[] key, ulong atTx, double score);
    Task<TxHeader> ZAdd(string set, string key, double score);
    Task<TxHeader> ZAdd(byte[] set, byte[] key, double score);
    Task<List<ZEntry>> ZScan(string set, ulong limit, bool reverse);
    Task<List<ZEntry>> ZScan(byte[] set, ulong limit, bool reverse);
}
