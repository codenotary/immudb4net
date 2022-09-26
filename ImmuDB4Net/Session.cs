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
/// Represents the transaction kind that will be supported in future versions
/// </summary>
public enum TransactionKind {
    /// <summary>
    /// Read mode
    /// </summary>
    Read,
    /// <summary>
    /// Read and Write mode
    /// </summary>
    ReadWrite
}

/// <summary>
/// Represents a session that has been established after connecting to the server
/// </summary>
public class Session
{
    /// <summary>
    /// Gets or sets the transaction kind
    /// </summary>
    public TransactionKind Kind;
    /// <summary>
    /// Gets the session ID
    /// </summary>
    /// <value></value>
    public string Id { get; private set; }
    /// <summary>
    /// Gets the Server UUID
    /// </summary>
    /// <value></value>
    public string ServerUUID { get; private set; }
    internal string? TransactionId { get; set; }

    /// <summary>
    /// Creates a new session
    /// </summary>
    /// <param name="id">The session ID</param>
    /// <param name="serverUUID">The server UUID</param>
    public Session(string id, string serverUUID) {
        Id = id; 
        ServerUUID = serverUUID;
    }
}