# immudb4net [![License](https://img.shields.io/github/license/codenotary/immudb4net)](LICENSE)


### The Official [immudb] Client for .NET

[immudb]: https://immudb.io/


## Contents

- [immudb4net ![License](LICENSE)](#immudb4net-)
    - [The Official [immudb] Client for .NET](#the-official-immudb-client-for-net)
  - [Contents](#contents)
  - [Introduction](#introduction)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
    - [How to use immudb4net packages from Github Packages](#how-to-use-immudb4net-packages-from-github-packages)
  - [Supported Versions](#supported-versions)
  - [Quickstart](#quickstart)
  - [Step-by-step Guide](#step-by-step-guide)
    - [Creating a Client](#creating-a-client)
    - [User Sessions](#user-sessions)
    - [Creating a Database](#creating-a-database)
    - [Setting the Active Database](#setting-the-active-database)
    - [Standard Read and Write](#standard-read-and-write)
    - [Verified or Safe Read and Write](#verified-or-safe-read-and-write)
    - [Multi-key Read and Write](#multi-key-read-and-write)
    - [Closing the client](#closing-the-client)
  - [Contributing](#contributing)

## Introduction

immudb4net implements a [gRPC] immudb client, based on [immudb's official protobuf definition].<br/>
It exposes a minimal and simple to use API for applications, while the cryptographic verifications and state update protocol implementation 
are fully implemented internally by this client.

The latest validated immudb state may be kept in the local file system using default `FileImmuStateHolder`.<br/>
Please read [immudb Research Paper] for details of how immutability is ensured by [immudb].

[gRPC]: https://grpc.io/
[immudb Research Paper]: https://immudb.io/
[immudb]: https://immudb.io/
[immudb's official protobuf definition](https://github.com/codenotary/immudb/blob/master/pkg/api/schema/schema.proto)

## Prerequisites

immudb4net assumes you have access to a running immudb server.<br/>
Running `immudb` on your system is very simple, please refer to this [immudb QuickStart](https://docs.immudb.io/master/quickstart.html) page.

## Installation

Include immudb4net as a dependency in your project via Nuget package:

TODO

`immudb4net` is currently hosted on [NuGet.Org]

[NuGet.Org]: https://nuget.org

## Build locally from sources

Use ```dotnet build``` to build locally the ImmuDB client assembly.

### How to use immudb4net packages from Github Packages

## Supported Versions

immudb4net supports the [latest immudb server] release, that is 1.3.0 at the time of updating this document.

[latest immudb server]: https://github.com/codenotary/immudb/releases/tag/v1.3.0

## Quickstart

[Hello Immutable World!] example can be found in `immudb-client-examples` repo.

[Hello Immutable World!]: https://github.com/codenotary/immudb-client-examples/tree/master/c#

Follow its [README](https://github.com/codenotary/immudb-client-examples/blob/master/c#/README.md) to build and run it.

## Step-by-step Guide

### Creating a Client

The following code snippets show how to create a client.

Using default configuration:

``` C#
    ImmuClient immuClient = ImmuClient.newBuilder().build();
```

Setting `immudb` url and port:

``` C#
    ImmuClient immuClient = ImmuClient.newBuilder()
                                .withServerUrl("localhost")
                                .withServerPort(3322)
                                .build();
```

Customizing the `State Holder`:

``` C#
    FileImmuStateHolder stateHolder = FileImmuStateHolder.newBuilder()
                                        .withStatesFolder("./my_immuapp_states")
                                        .build();

    ImmuClient immuClient = ImmuClient.newBuilder()
                                      .withStateHolder(stateHolder)
                                      .build();
```

### User Sessions

Use `login` and `logout` methods to initiate and terminate user sessions:

``` C#
    immuClient.login("usr1", "pwd1");

    // Interact with immudb using logged-in user.
    //...

    immuClient.logout();
```

### Creating a Database

Creating a new database is quite simple:

``` C#
    immuClient.createDatabase("db1");
```

### Setting the Active Database

Specify the active database with:

``` C#
    immuClient.useDatabase("db1");
```

### Standard Read and Write

immudb provides standard read and write operations that behave as in a standard
key-value store i.e. no cryptographic verification is involved. Such operations
may be used when validations can be postponed.

``` C#
    client.set("k123", new byte[]{1, 2, 3});
    
    byte[] v = client.get("k123").getValue();
```

### Verified or Safe Read and Write

immudb provides built-in cryptographic verification for any entry. The client
implements the mathematical validations while the application uses as a standard
read or write operation:

``` C#
    try {
        client.verifiedSet("k123", new byte[]{1, 2, 3});
    
        byte[] v = client.verifiedGet("k123").getValue();

    } (catch VerificationException e) {

        // Check if it is a data tampering detected case!

    }
```

### Multi-key Read and Write

Transactional multi-key read and write operations are supported by immudb and immudb4net.

Atomic multi-key write (all entries are persisted or none):

``` C#
        final List<KVPair> kvs = KVListBuilder.newBuilder()
            .add(new KVPair("sga-key1", new byte[] {1, 2}))
            .add(new KVPair("sga-key2", new byte[] {3, 4}))
            .entries();

        try {
            immuClient.setAll(kvs);
        } catch (CorruptedDataException e) {
            // ...
        }
```

Atomic multi-key read (all entries are retrieved or none):

``` C#
    List<string> keys = Arrays.asList(key1, key2, key3);
    List<Entry> result = immuClient.getAll(keys);

    for (Entry entry : result) {
        byte[] key = entry.getKey();
        byte[] value = entry.getValue();
        // ...
    }
```

### Closing the client

Apart from the `logout`, for closing the connection with immudb server use the `shutdown` operation:

``` C#
    immuClient.shutdown();
```

Note: After the shutdown, a new client needs to be created to establish a new connection.

## Contributing

We welcome contributions. Feel free to join the team!

To report bugs or get help, use [GitHub's issues].

[GitHub's issues]: https://github.com/codenotary/immudb4net/issues
