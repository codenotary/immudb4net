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

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImmuDB.Exceptions;

public class FileImmuStateHolder : ImmuStateHolder
{
    internal class DeploymentInfoContent
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }
        [JsonPropertyName("serveruuid")]
        public string? ServerUUID { get; set; }
    }

    private string statesFolder;

    public string StatesFolder => statesFolder;
    public string? DeploymentKey { get; set; }
    public string? DeploymentLabel { get; set; }
    public bool DeploymentInfoCheck { get; set; } = true;
    private DeploymentInfoContent? deploymentInfo;

    public FileImmuStateHolder(Builder builder)
    {
        statesFolder = builder.StatesFolder;
    }

    public FileImmuStateHolder() : this(NewBuilder())
    {
    }

    public ImmuState? GetState(Session? session, string dbname)
    {
        if (DeploymentKey == null)
        {
            throw new InvalidOperationException("you need to set deploymentkey before using GetDeploymentInfo");
        }
        lock (this)
        {
            if (session == null)
            {
                return null;
            }
            if (deploymentInfo == null)
            {
                deploymentInfo = GetDeploymentInfo(session.ServerUUID);
                if (deploymentInfo == null)
                {
                    deploymentInfo = CreateDeploymentInfo(session);
                }
                if ((deploymentInfo.ServerUUID != session.ServerUUID) && DeploymentInfoCheck)
                {
                    var deploymentInfoPath = Path.Combine(statesFolder, DeploymentKey);
                    throw new VerificationException(
                        string.Format("server UUID mismatch. Most likely you connected to a different server instance than previously used at the same address. if you understand the reason and you want to get rid of the problem, you can either delete the folder `{0}` or set CheckDeploymentInfo to false ", deploymentInfoPath));
                }
            }
            var completeStatesFolderPath = Path.Combine(statesFolder, DeploymentKey);
            if (!Directory.Exists(completeStatesFolderPath))
            {
                Directory.CreateDirectory(completeStatesFolderPath);
            }
            string stateFilePath = Path.Combine(completeStatesFolderPath, string.Format("state_{0}", dbname));
            if (!File.Exists(stateFilePath))
            {
                return null;
            }
            string contents = File.ReadAllText(stateFilePath);
            return JsonSerializer.Deserialize<ImmuState>(contents);
        }
    }

    internal DeploymentInfoContent? GetDeploymentInfo(string? serverUUID)
    {
        if (string.IsNullOrEmpty(DeploymentKey))
        {
            throw new InvalidOperationException("you need to set deploymentkey before using GetDeploymentInfo");
        }
        if (string.IsNullOrEmpty(serverUUID))
        {
            return ReadDeploymentKeyBasedDeploymentInfo();
        }
        DeploymentInfoContent? deploymentInfo;
        string? newDeploymentKey;
        if (TryReadDeploymentInfoOnUuid(serverUUID!, out deploymentInfo, out newDeploymentKey))
        {
            DeploymentKey = newDeploymentKey;
            return deploymentInfo;
        }
        return ReadDeploymentKeyBasedDeploymentInfo();
    }

    private bool TryReadDeploymentInfoOnUuid(string serverUUID, out DeploymentInfoContent? deploymentInfo, out string? deploymentKey)
    {
        if (!Directory.Exists(statesFolder))
        {
            deploymentInfo = null;
            deploymentKey = null;
            return false;
        }
        string[] dirs = Directory.GetDirectories(statesFolder);
        foreach (var dir in dirs)
        {
            var deploymentInfoPath = Path.Combine(dir, "deploymentinfo.json");
            if (!File.Exists(deploymentInfoPath))
            {
                continue;
            }
            try
            {
                var loadedDeploymentInfo = JsonSerializer.Deserialize<DeploymentInfoContent>(File.ReadAllText(deploymentInfoPath));
                if (loadedDeploymentInfo == null)
                {
                    continue;
                }
                if (string.Equals(serverUUID, loadedDeploymentInfo.ServerUUID, StringComparison.InvariantCultureIgnoreCase))
                {
                    deploymentInfo = loadedDeploymentInfo;
                    deploymentKey = Path.GetFileName(dir);
                    return true;
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }
        deploymentInfo = null;
        deploymentKey = null;
        return false;
    }

    private DeploymentInfoContent? ReadDeploymentKeyBasedDeploymentInfo()
    {
        var completeStatesFolderPath = Path.Combine(statesFolder, DeploymentKey!);
        var deploymentInfoPath = Path.Combine(completeStatesFolderPath, "deploymentinfo.json");
        if (!File.Exists(deploymentInfoPath))
        {
            return null;
        }
        return JsonSerializer.Deserialize<DeploymentInfoContent>(File.ReadAllText(deploymentInfoPath));
    }

    internal DeploymentInfoContent CreateDeploymentInfo(Session session)
    {
        if (DeploymentKey == null)
        {
            throw new InvalidOperationException("you need to set deploymentkey before using GetDeploymentInfo");
        }
        lock (this)
        {
            var completeStatesFolderPath = Path.Combine(statesFolder, DeploymentKey);
            if (!Directory.Exists(completeStatesFolderPath))
            {
                Directory.CreateDirectory(completeStatesFolderPath);
            }
            var deploymentInfoPath = Path.Combine(completeStatesFolderPath, "deploymentinfo.json");
            var info = new DeploymentInfoContent { Label = DeploymentLabel, ServerUUID = session.ServerUUID };
            string contents = JsonSerializer.Serialize(info);
            File.WriteAllText(deploymentInfoPath, contents);
            return info;
        }
    }

    public void SetState(Session session, ImmuState state)
    {
        if (DeploymentKey == null)
        {
            throw new InvalidOperationException("you need to set deploymentkey before using GetDeploymentInfo");
        }
        lock (this)
        {
            ImmuState? currentState = GetState(session, state.Database);
            if (currentState != null && currentState.TxId >= state.TxId)
            {
                // if the state to save is older than what is save, just skip it
                return;
            }

            string newStateFile = Path.Combine(StatesFolder, string.Format("state_{0}_{1}",
                state.Database,
                Path.GetRandomFileName().Replace(".", "")));
            var options = new JsonSerializerOptions { WriteIndented = true };
            string contents = JsonSerializer.Serialize(state, options);
            File.WriteAllText(newStateFile, contents);
            try
            {
                // I had to use this workaround because File.Move with overwrite is not available in .NET Standard 2.0. Otherwise is't just a one-liner code.
                var stateHolderFile = Path.Combine(StatesFolder, DeploymentKey, string.Format("state_{0}", state.Database));
                var intermediateMoveStateFile = newStateFile + "_";
                if (File.Exists(stateHolderFile))
                {
                    File.Move(stateHolderFile, intermediateMoveStateFile);
                }
                File.Move(newStateFile, stateHolderFile);
                if (File.Exists(intermediateMoveStateFile))
                {
                    File.Delete(intermediateMoveStateFile);
                }
                stateHolderFile = newStateFile;
            }
            catch (IOException e)
            {
                Console.WriteLine($"An IOException occurred: {e.ToString()}");
                throw new InvalidOperationException("Unexpected error " + e);
            }
        }
    }

    public static Builder NewBuilder()
    {
        return new Builder();
    }

    public class Builder
    {
        public string StatesFolder { get; private set; }

        public Builder()
        {
            StatesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "immudb4net");
        }

        public Builder WithStatesFolder(string statesFolder)
        {
            this.StatesFolder = statesFolder;
            return this;
        }

        public FileImmuStateHolder build()
        {
            return new FileImmuStateHolder(this);
        }
    }
}
