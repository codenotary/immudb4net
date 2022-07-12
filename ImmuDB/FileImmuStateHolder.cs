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
using System.Diagnostics;

public class FileImmuStateHolder : ImmuStateHolder
{
    private readonly string statesFolder;
    private readonly string currentStateFile;
    private string stateHolderFile = "";

    private readonly SerializableImmuStateHolder stateHolder;


    public FileImmuStateHolder(Builder builder)
    {
        statesFolder = builder.StatesFolder;
        if (!File.Exists(statesFolder))
        {
            Directory.CreateDirectory(statesFolder);
        }

        currentStateFile = Path.Combine(stateHolderFile, "current_state");
        if (!File.Exists(currentStateFile))
        {
            using (File.Create(currentStateFile)) { }
        }

        stateHolder = new SerializableImmuStateHolder();
        string lastStateFilename = File.ReadAllText(currentStateFile);

        if (!string.IsNullOrEmpty(lastStateFilename))
        {
            stateHolderFile = Path.Combine(statesFolder, lastStateFilename);

            if (!File.Exists(stateHolderFile))
            {
                throw new InvalidOperationException("Inconsistent current state file");
            }

            stateHolder.ReadFrom(stateHolderFile);
        }
    }

    public ImmuState? GetState(string? serverUuid, string database)
    {
        lock (this)
        {
            return stateHolder.GetState(serverUuid, database);
        }
    }

    public void setState(string serverUuid, ImmuState state)
    {
        lock (this)
        {
            ImmuState? currentState = stateHolder.GetState(serverUuid, state.Database);
            if (currentState != null && currentState.TxId >= state.TxId)
            {
                return;
            }

            stateHolder.setState(serverUuid, state);
            string newStateFile = Path.Combine(statesFolder, "state_" + serverUuid + "_" + state.Database + "_" + Stopwatch.GetTimestamp());

            if (File.Exists(newStateFile))
            {
                throw new InvalidOperationException("Failed attempting to create a new state file. Please retry.");
            }

            try
            {
                stateHolder.WriteTo(newStateFile);
                File.WriteAllText(currentStateFile, newStateFile);
                if (File.Exists(stateHolderFile))
                {
                    File.Delete(stateHolderFile);
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

    public class Builder
    {
        public string StatesFolder { get; private set; }

        private Builder()
        {
            StatesFolder = "states";
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
