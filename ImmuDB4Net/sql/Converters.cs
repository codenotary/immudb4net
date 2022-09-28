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

using System.Data;
using Google.Protobuf.WellKnownTypes;
using ImmudbProxy;

namespace ImmuDB.SQL;

internal static class Converters
{
    internal static ImmudbProxy.SQLValue CreateSQLValue(SQLParameter? parameter)
    {
        if (parameter == null)
        {
            return new ImmudbProxy.SQLValue { Null = NullValue.NullValue };
        }
        switch (parameter.ValueType)
        {
            case SqlDbType.SmallInt:
            case SqlDbType.Int:
            case SqlDbType.BigInt:
                return new ImmudbProxy.SQLValue { N = (long)parameter.Value };
            case SqlDbType.Date:
            case SqlDbType.DateTime:
                DateTime dt = (DateTime)parameter.Value;
                long timeMicroseconds = new DateTimeOffset(dt).ToUnixTimeMilliseconds() * 1000;
                return new ImmudbProxy.SQLValue { Ts = timeMicroseconds };
            case SqlDbType.Text:
            case SqlDbType.NText:
            case SqlDbType.Char:
            case SqlDbType.NChar:
            case SqlDbType.VarChar:
            case SqlDbType.NVarChar:
            case SqlDbType.Xml:
                return new ImmudbProxy.SQLValue { S = (string)parameter.Value };
            default:
                throw new NotSupportedException(string.Format("The SQL type {0} is not supported", parameter.ValueType));
        }
    }

    internal static SQL.SQLValue FromProxySQLValue(ImmudbProxy.SQLValue proxyValue)
    {
        switch (proxyValue.ValueCase)
        {
            case ImmudbProxy.SQLValue.ValueOneofCase.Null:
                return new SQL.SQLValue(proxyValue.Null, SqlDbType.Int);
            case ImmudbProxy.SQLValue.ValueOneofCase.N:
                return new SQL.SQLValue(proxyValue.N, SqlDbType.Int);
            case ImmudbProxy.SQLValue.ValueOneofCase.S:
                return new SQL.SQLValue(proxyValue.S, SqlDbType.NVarChar);
            case ImmudbProxy.SQLValue.ValueOneofCase.Ts:
                var dateTimeArg = DateTimeOffset.FromUnixTimeMilliseconds((long)proxyValue.Ts / 1000);
                return new SQL.SQLValue(dateTimeArg, SqlDbType.NVarChar);
            default:
                throw new NotSupportedException(string.Format("The proxyvalue type {0} is not supported", proxyValue.ValueCase));
        }
    }

    internal static List<NamedParam> ToNamedParams(SQLParameter[] parameters)
    {
        List<NamedParam> namedParams = new List<NamedParam>();
        int paramNameCounter = 1;
        foreach (var entry in parameters)
        {
            if (entry == null)
            {
                namedParams.Add(new NamedParam { Name = string.Format("param{0}", paramNameCounter++), Value = CreateSQLValue(null) });
                continue;
            }
            var namedParam = new NamedParam
            {
                Name = string.IsNullOrEmpty(entry.Name) ? string.Format("param{0}", paramNameCounter++) : entry.Name,
                Value = CreateSQLValue(entry)
            };
            namedParams.Add(namedParam);
        }
        return namedParams;
    }
}