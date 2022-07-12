using System.Text.Json;
using System.Linq;

namespace ImmuDB;

public class SerializableImmuStateHolder : ImmuStateHolder
{
     /**
     * Mapping "{serverUuid}_{databaseName}" to the appropriate state.
     */
    private Dictionary<string, ImmuState> statesMap = new Dictionary<string, ImmuState>();

    public void ReadFrom(string fileName) {
        string contents = File.ReadAllText(fileName);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, ImmuState>>(contents);
        statesMap.Clear();
        if(deserialized == null) {
            return;
        }
        deserialized.ToList().ForEach(pair => statesMap.Add(pair.Key, pair.Value));
    }

    public void WriteTo(string fileName) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string contents = JsonSerializer.Serialize(statesMap, options);
        File.WriteAllText(fileName, contents);
    }

    public ImmuState? GetState(string? serverUuid, string database)
    {
        if(serverUuid == null)
            return null;
        return statesMap[serverUuid + "_" + database];
    }

    public void setState(string serverUuid, ImmuState state)
    {
        statesMap[serverUuid + "_" + state.Database] = state;
    }
}