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

/// <summary>
/// This inner class keeps the global settings that are used across all <see cref="ImmuClient" /> instances
/// </summary>
public class LibraryWideSettings
{
    /// <summary>
    /// Gets or sets the 
    /// </summary>
    /// <value></value>
    public int MaxConnectionsPerServer { get; set; }
    /// <summary>
    /// Gets or sets the idle time before a connection is terminated
    /// </summary>
    /// <value></value>
    public TimeSpan TerminateIdleConnectionTimeout { get; set; }
    /// <summary>
    /// Gets or sets the time interval between checking for idle connections
    /// </summary>
    /// <value></value>
    public TimeSpan IdleConnectionCheckInterval { get; set; }
    internal LibraryWideSettings()
    {
        //these are the default values
        MaxConnectionsPerServer = 2;
        TerminateIdleConnectionTimeout = TimeSpan.FromSeconds(60);
        IdleConnectionCheckInterval = TimeSpan.FromSeconds(6);
    }
}
