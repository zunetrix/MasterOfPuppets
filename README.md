# Master Of Puppets

FFXIV plugin to send custom individual actions (like game macros) with local sync or via chat (party, linkshell, crossworld linkshell)


# Builds
```sh
dotnet build -c Debug
```

```sh
dotnet build -c Release
```

# Tests
```sh
dotnet test ./MasterOfPuppetsTests/
```

TODO:
 - moploop block (start end):
 - add chat watcher init to prevent register on chat game listener if use chat option is disabled
 - refactor macro commands to individual handlers

```
start code no loop

/moploopstart
loop code
/moploopend
```
