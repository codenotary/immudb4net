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

public class SQLParameter
{
    public string? Name { get; set; }
    public object Value { get; set; }
    public SqlDbType ValueType { get; set; }

    public SQLParameter(int value)
    {
        Value = value;
        ValueType = SqlDbType.BigInt;
    }

    public SQLParameter(int value, string name)
    {
        Name = name;
        Value = value;
        ValueType = SqlDbType.BigInt;
    }

    public SQLParameter(string value)
    {
        Value = value;
        ValueType = SqlDbType.NVarChar;
    }

    public SQLParameter(string value, string name)
    {
        Name = name;
        Value = value;
        ValueType = SqlDbType.NVarChar;
    }

    public SQLParameter(DateTime value)
    {
        Value = value;
        ValueType = SqlDbType.DateTime;
    }

    public SQLParameter(DateTime value, string name)
    {
        Name = name;
        Value = value;
        ValueType = SqlDbType.DateTime;
    }

    public static SQLParameter Create(int value)
    {
        return new SQLParameter(value);
    }

    public static SQLParameter Create(int value, string name)
    {
        return new SQLParameter(value, name);
    }

    public static SQLParameter Create(string value)
    {
        return new SQLParameter(value);
    }

    public static SQLParameter Create(string value, string name)
    {
        return new SQLParameter(value, name);
    }
    public static SQLParameter Create(DateTime value)
    {
        return new SQLParameter(value);
    }

    public static SQLParameter Create(DateTime value, string name)
    {
        return new SQLParameter(value, name);
    }
}