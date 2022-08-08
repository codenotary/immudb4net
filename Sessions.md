# How sessions work

The following Sequence diagram describes the interaction between the components that provide the sessions functionality:

``` mermaid
sequenceDiagram
    actor C as Client
    participant SM as SessionManager    
    participant S as Session
    participant CP as ConnectionPool
    participant SRV as Server
    
    C-->CP: acquire connection
    activate CP
    CP-->CP: create / reuse connection
    CP-->CP: provide connection id
    deactivate CP
    CP->>C: new or existing connection id    
    C->>C: check if session is already opened
    C->>SM: open session
    SM->>SRV: get session
    SRV->>SM: session
    SM->>C: session
        
    C->>SRV: run command with session context
    SRV->>C: result
    
    C->>SM: close session
    SM->>SRV:close session
    SRV->>SM: close session result
    SM->>C: close session result
    C->>C: clear session
```