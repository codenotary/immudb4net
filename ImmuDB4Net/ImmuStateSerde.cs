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

    public string? DeploymentKey { get; set; }
    public string? DeploymentLabel { get; set; }
    public bool DeploymentInfoCheck { get; set; } = true;

    public ImmuState? Read(string fileName, Session? session, string database)
    {
        if (session == null)
            return null;
        string contents = File.ReadAllText(fileName);
        return JsonSerializer.Deserialize<ImmuState>(contents);
    }

    public void Write(string fileName, Session session, ImmuState state)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string contents = JsonSerializer.Serialize(state, options);
        File.WriteAllText(fileName, contents);
    }    
}