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
using System.Security.Cryptography;

public class FileImmuStateHolder : ImmuStateHolder
{
    private readonly String statesFolder;
    private readonly String currentStateFile;
    private String stateHolderFile;

    public FileImmuStateHolder(Builder builder)
    {
        statesFolder = builder.StatesFolder;
        if(!File.Exists(statesFolder)) {
            Directory.CreateDirectory(statesFolder);
        }
        
    }

    public ImmuState getState(string serverUuid, string database)
    {
        throw new NotImplementedException();
    }

    public void setState(string serverUuid, ImmuState state)
    {
        throw new NotImplementedException();
    }

    public class Builder {
        public String StatesFolder {get; private set; }

        private Builder() {
            StatesFolder = "states";
        }

        public Builder WithStatesFolder(String statesFolder) {
            this.StatesFolder = statesFolder;
            return this;
        }

        public FileImmuStateHolder build() {
            return new FileImmuStateHolder(this);
        }
    }
}
