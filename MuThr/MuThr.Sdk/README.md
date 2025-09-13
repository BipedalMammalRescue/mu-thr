# MuThr.Sdk

Building blocks that can be assembled into a build system using MIT license.

## Usage

Implement ITaskProvider and IMuThrLogger (a simple logger implementation has been included in the SDK).

### ITaskProvider

This interface determines how MuThr discovers potential tasks and maps them into build actions.
To be able to create tasks through ITaskProvider, you'll need to create your own set of concrete types of IDataPoint and its sub-interfaces: ILeafDataPoint, IArrayDataPoint, and IObjDataPoint.
Then, implement the factory method that takes in a string key and creates a data point object and a build action.

Example: imagine a simple asset builder for a game.
Each task takes a *.tres file that's going to be compiled into a binary file for the game engine to consume.
Therefore, the key would naturally be the path to the *.tres file since each asset should only be built once,
and the factory logic can use the content in the *.tres file (such as a special field) to locate a json file that contains a build action somewhere in the assets directory.

### DataPoint

MuThr SDK uses "DataPoint" as the building block to its description of external data. 
Every piece of data that's fed into the build system needs to be able to serialized into an IDataPoint object (or its derived types, see details in the source) so the JSON-based build action scripting can interpret them.

Example workflow: create the C# classes DataPoint, ArrayData, ObjData, and LeafData, each implement the interface of their namesake.
Then make DataPoint a polymorphic JSON class with links to the other three concrete types.
Write your asset description in JSON formats serializable to the aforementioned conrete types.
Finally, implement a ITaskProvider class that deserializes the input data into DataPoint objects.

### Logging

MuThr SDK uses Serilog for its logging purposes, with very minimum addition to its built-in features. 
Work is planned for a dedicated logging package, for now you are encouraged to implement your own logging and formatting and such.
