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
    private string? statesFolder;
    private string? currentStateFile;
    private string stateHolderFile = "";

    private SerializableImmuStateHolder? stateHolder;
    public string? StatesFolder => statesFolder;
    internal bool IsDefaultStateFolder { get; set; } = true;
    public string? Key { get; set; }

    public void Init()
    {
        var folder = statesFolder;
        if (IsDefaultStateFolder)
        {
            folder = Path.Combine(StatesFolder, Key);
        }
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        currentStateFile = Path.Combine(folder, "current_state");
        if (!File.Exists(currentStateFile))
        {
            using (File.Create(currentStateFile)) { }
        }

        stateHolder = new SerializableImmuStateHolder();
        string lastStateFilename = File.ReadAllText(currentStateFile);

        if (!string.IsNullOrEmpty(lastStateFilename))
        {
            stateHolderFile = Path.Combine(folder, lastStateFilename);

            if (!File.Exists(stateHolderFile))
            {
                throw new InvalidOperationException("Inconsistent current state file");
            }

            stateHolder.ReadFrom(stateHolderFile);
        }
    }

    public FileImmuStateHolder(Builder builder)
    {
        statesFolder = builder.StatesFolder;
        IsDefaultStateFolder = builder.IsDefaultStatesFolder;
    }

    public FileImmuStateHolder() : this(NewBuilder())
    {
    }

    public ImmuState? GetState(Session? session, string database)
    {
        lock (this)
        {
            return stateHolder?.GetState(session, database);
        }
    }

    public void SetState(Session session, ImmuState state)
    {
        lock (this)
        {
            if (stateHolder == null)
            {
                throw new InvalidOperationException("you need to call Init before setting state");
            }
            ImmuState? currentState = stateHolder.GetState(session, state.Database);
            if (currentState != null && currentState.TxId >= state.TxId)
            {
                return;
            }

            stateHolder.SetState(session, state);
            string newStateFile = Path.Combine(StatesFolder, string.Format("state_{0}_{1}_{2}_{3}",
                session.ServerUUID,
                state.Database,
                Stopwatch.GetTimestamp(),
                Task.CurrentId ?? 0));

            if (File.Exists(newStateFile))
            {
                throw new InvalidOperationException("Failed attempting to create a new state file. Please retry.");
            }

            try
            {
                stateHolder.WriteTo(newStateFile);
                File.WriteAllText(currentStateFile, Path.GetFileName(newStateFile));
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

    public static Builder NewBuilder()
    {
        return new Builder();
    }

    public class Builder
    {
        internal bool IsDefaultStatesFolder { get; set; } = true;
        public string StatesFolder { get; private set; }

        public Builder()
        {
            StatesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "immudb4net");
        }

        public Builder WithStatesFolder(string statesFolder)
        {
            this.StatesFolder = statesFolder;
            this.IsDefaultStatesFolder = false;
            return this;
        }

        public FileImmuStateHolder build()
        {
            return new FileImmuStateHolder(this);
        }
    }
}
