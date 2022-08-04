# How sessions work

The following Sequence diagram describes the interaction between the components that provide the sessions functionality:

``` mermaid
sequenceDiagram
    actor C as Client
    participant SM as SessionManager
    participant SP as SessionPool
    participant S as Session
    participant CP as ConnectionPool
    participant SRV as Server

    activate SP
    SP-->SP: start pool lifeycle thread
    deactivate SP
    C->>SM: open session
    SM->>SP: request session
    activate SP
    SP-->SP: check pool
    SP-->SP: create / reuse session handle
    SP-->SP: augment transaction id, transaction type, etc
    SP-->SP: provide new session id
    SP->>S: activate new session
    S->>CP: request connection
    activate CP
    CP-->CP: create / reuse connection
    CP-->CP: provide connection id
    CP->>S: new or existing connection id
    deactivate CP
    S->>SP: new session
    SP->>SM: session
    deactivate SP
    SM->>C: session

    C->>SM: run command
    SM->>S: run command
    activate S
    S-->S: add context
    S->>SRV: run command
    SRV->>S: result
    S->>SM: result
    deactivate S
    activate SM
    SM-->SM: handle result, retries, reconnect, etc
    SM->>C: result
    deactivate SM
```