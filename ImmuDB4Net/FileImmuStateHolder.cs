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
    private string stateHolderFile = "";

    private readonly SerializableImmuStateHolder stateHolder;


    public FileImmuStateHolder(Builder builder)
    {
        statesFolder = builder.StatesFolder;
        if (!Directory.Exists(statesFolder))
        {
            Directory.CreateDirectory(statesFolder);
        }

        stateHolder = new SerializableImmuStateHolder();
        string lastStateFilename = GetOldestStateFile();

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

    private string GetOldestStateFile()
    {
        var stateFiles = Directory.GetFiles(statesFolder);
        if(stateFiles.Length == 0) 
        {
            return "";
        }
        List<Tuple<string, DateTime>> stateFilesData = new List<Tuple<string, DateTime>>(stateFiles.Length);
        var sortedFileInfo = stateFiles.Select(x => new Tuple<string, DateTime>(x, File.GetLastWriteTimeUtc(x))).OrderBy(x => x.Item2).ToList();
        return sortedFileInfo[0].Item1;
    }

    public ImmuState? GetState(Session? session, string database)
    {
        lock (this)
        {
            return stateHolder.GetState(session, database);
        }
    }

    public void SetState(Session session, ImmuState state)
    {
        lock (this)
        {
            ImmuState? currentState = stateHolder.GetState(session, state.Database);
            if (currentState != null && currentState.TxId >= state.TxId)
            {
                return;
            }

            stateHolder.SetState(session, state);
            string newStateFile = Path.Combine(statesFolder, string.Format("state_{0}_{1}_{2}_{3}",
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
        public string StatesFolder { get; private set; }

        public Builder()
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
