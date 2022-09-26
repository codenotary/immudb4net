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

namespace ImmuDB.SQL;

using System.Data;

/// <summary>
/// Represents an SQL Parameter used in SQLExec or SQLQuery
/// </summary>
public class SQLParameter
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    /// <value></value>
    public string? Name { get; set; }
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
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The integer value</param>
    public SQLParameter(int value)
    {
        Value = value;
        ValueType = SqlDbType.BigInt;
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The parameter value</param>
    /// <param name="name">The parameter name</param>
    public SQLParameter(int value, string name)
    {
        Name = name;
        Value = value;
        ValueType = SqlDbType.BigInt;
    }

   /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The string value</param>
    public SQLParameter(string value)
    {
        Value = value;
        ValueType = SqlDbType.NVarChar;
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The string value</param>
    /// <param name="name">The parameter name</param>
    public SQLParameter(string value, string name)
    {
        Name = name;
        Value = value;
        ValueType = SqlDbType.NVarChar;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public SQLParameter(DateTime value)
    {
        Value = value;
        ValueType = SqlDbType.DateTime;
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value"></param>
    /// <param name="name"></param>
    public SQLParameter(DateTime value, string name)
    {
        Name = name;
        Value = value;
        ValueType = SqlDbType.DateTime;
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The integer value</param>
    /// <returns>SQL Parameter</returns>
    public static SQLParameter Create(int value)
    {
        return new SQLParameter(value);
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The integer value</param>
    /// <param name="name">The parameter name</param>
    /// <returns></returns>
    public static SQLParameter Create(int value, string name)
    {
        return new SQLParameter(value, name);
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The string value</param>
    /// <returns></returns>
    public static SQLParameter Create(string value)
    {
        return new SQLParameter(value);
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The string value</param>
    /// <param name="name">The parameter name</param>
    /// <returns></returns>
    public static SQLParameter Create(string value, string name)
    {
        return new SQLParameter(value, name);
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The DateTime value</param>
    /// <returns></returns>
    public static SQLParameter Create(DateTime value)
    {
        return new SQLParameter(value);
    }

    /// <summary>
    /// Creates an SQL Parameter
    /// </summary>
    /// <param name="value">The DateTime value</param>
    /// <param name="name">The parameter name</param>
    /// <returns></returns>
    public static SQLParameter Create(DateTime value, string name)
    {
        return new SQLParameter(value, name);
    }
}