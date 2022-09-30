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
/// Represents the abstraction for the state holder. This allow the creation of custom state holders, such as in a distributed KV or in memory
/// </summary>
public interface IImmuStateHolder
{
    /// <summary>
    /// Gets the deployment key
    /// </summary>
    /// <value></value>
    string? DeploymentKey { get; internal set; }
    /// <summary>
    /// Gets the deployment label, usually the server address
    /// </summary>
    /// <value></value>
    string? DeploymentLabel { get; internal set; }
    /// <summary>
    /// Gets the DeploymentInfoCheck enabled status
    /// </summary>
    /// <value></value>
    bool DeploymentInfoCheck {get; internal set; }
    /// <summary>
    /// Gets the immudb database state
    /// </summary>
    /// <param name="session">The session object</param>
    /// <param name="database">The database name</param>
    /// <returns></returns>
    ImmuState? GetState(Session? session, string database);
    /// <summary>
    /// Sets the state of an immudb database
    /// </summary>
    /// <param name="session">The session object</param>
    /// <param name="state">The state to be stored</param>
    void SetState(Session session, ImmuState state);
}
