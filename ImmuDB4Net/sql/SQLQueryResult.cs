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

namespace ImmuDB.SQL;

public class SQLQueryResult
{
    public List<Column> Columns { get; set; } = new List<Column>();
    public List<Dictionary<string, SQLValue>> Rows = new List<Dictionary<string, SQLValue>>();

}

public class Column
{
    public string Name;
    public string Type;
    public Column(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

public class SQLValue
{
    public object Value { get; set; }
    public SqlDbType ValueType { get; set; }

    public SQLValue(object value, SqlDbType valueType)
    {
        Value = value;
        ValueType = valueType;
    }
}