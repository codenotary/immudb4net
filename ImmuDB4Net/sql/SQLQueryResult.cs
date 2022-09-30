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

/// <summary>
/// Represents the result data of running an SQL Query statement against ImmuDB
/// </summary>
public class SQLQueryResult
{
    /// <summary>
    /// Gets the column list
    /// </summary>
    /// <returns></returns>
    public List<Column> Columns { get; private set; } = new List<Column>();
    /// <summary>
    /// Gets the row result
    /// </summary>
    /// <returns></returns>
    public List<Dictionary<string, SQLValue>> Rows = new List<Dictionary<string, SQLValue>>();

}

/// <summary>
/// Represents a queryset column
/// </summary>
public class Column
{
    /// <summary>
    /// The column name
    /// </summary>
    public string Name;
    /// <summary>
    /// The column type
    /// </summary>
    public string Type;

    /// <summary>
    /// Creates a new column
    /// </summary>
    /// <param name="name"></param>
    /// <param name="type"></param>
    public Column(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

/// <summary>
/// Represents an SQL value used in <see cref="ImmuClient.SQLExec" /> or <see cref="ImmuClient.SQLQuery" />
/// </summary>
public class SQLValue
{
    /// <summary>
    /// Gets or sets the value
    /// </summary>
    /// <value></value>
    public object Value { get; set; }

    /// <summary>
    /// Gets or sets the value type
    /// </summary>
    /// <value></value>
    public SqlDbType ValueType { get; set; }

    /// <summary>
    /// Creates a new instance of SQL Value
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="valueType">The value type</param>
    public SQLValue(object value, SqlDbType valueType)
    {
        Value = value;
        ValueType = valueType;
    }
}