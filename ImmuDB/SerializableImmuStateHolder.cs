using System.Text.Json;
using System.Linq;

namespace ImmuDB;

public class SerializableImmuStateHolder : ImmuStateHolder
{
     /**
     * Mapping "{serverUuid}_{databaseName}" to the appropriate state.
     */
    private Dictionary<String, ImmuState> statesMap = new Dictionary<string, ImmuState>();

    public void ReadFrom(String fileName) {
        String contents = File.ReadAllText(fileName);
        var deserialized = JsonSerializer.Deserialize<Dictionary<String, ImmuState>>(contents);
        statesMap.Clear();
        if(deserialized == null) {
            return;
        }
        deserialized.ToList().ForEach(pair => statesMap.Add(pair.Key, pair.Value));
    }

    public void WriteTo(String fileName) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        String contents = JsonSerializer.Serialize(statesMap, options);
        File.WriteAllText(fileName, contents);
    }

    public ImmuState GetState(string serverUuid, string database)
    {
        return statesMap[serverUuid + "_" + database];
    }

    public void setState(string serverUuid, ImmuState state)
    {
        statesMap[serverUuid + "_" + state.Database] = state;
    }
}