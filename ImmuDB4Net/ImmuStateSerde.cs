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

using System.Text.Json;
using System.Linq;

namespace ImmuDB;

public class ImmuStateSerde
{
    /**
    * Mapping "{serverUuid}_{databaseName}" to the appropriate state.
    */
    private Dictionary<string, ImmuState> statesMap = new Dictionary<string, ImmuState>();

    public string? DeploymentKey { get; set; }
    public string? DeploymentLabel { get; set; }
    public bool DeploymentInfoCheck { get; set; } = true;

    public ImmuState? Read(string fileName, Session? session, string database)
    {
        if (session == null)
            return null;
        string contents = File.ReadAllText(fileName);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, ImmuState>>(contents);
        statesMap.Clear();
        if (deserialized == null)
        {
            return null;
        }
        deserialized.ToList().ForEach(pair => statesMap.Add(pair.Key, pair.Value));
        string key = session.ServerUUID + "_" + database;
        if (statesMap.TryGetValue(key, out var state))
        {
            return state;
        }
        return null;
    }

    public void Write(string fileName, Session session, ImmuState state)
    {
        statesMap[session.ServerUUID + "_" + state.Database] = state;
        var options = new JsonSerializerOptions { WriteIndented = true };
        string contents = JsonSerializer.Serialize(statesMap, options);
        File.WriteAllText(fileName, contents);
    }    
}