# MuThr Data Build System

MuThr is a data-oriented, data-driven build system SDK that provides a simple, unified configuration interface for complex data build tasks.

## Behavior as Data

A task in MuThr is made of a tree of executed actions, where each action is treated as a simple, abstract process with standard input and standard output.
User can then specify transformations for input and output to further customize the behavior of each action.
Other than the input and output streams, the build system doesn't observe any effect of the actions.

With such design, MuThr minimizes the potential damage caused by side effects and make error handling trivial, as failure just becomes the absense of output, and interrupting any individual task will not damage outside state (note: since the current design uses files as the means to funnel data between actions and I/O components, interruptions would cause a trail of temp files left over), making crash recovery extremely simple.

### Stateless Error Handling

MuThr's error handling is implemented using the optional pattern, albeit a heavily modified one.
This pattern allows error and output data to be passed in the same code path, and the system never crashes due to a task failure; instead, error data is returned to the observing parties, which in turn causes future tasks that depend on an errored task to be canceled.
Due to the stateless nature of MuThr's task coordination, this pattern allows simple yet always graceful error recovery for any individual task failure.

## Reactive Parallelism

MuThr uses Linq and ReativeX as the main framework. 
The combination of these frameworks, combined with the data-oriented design, allows MuThr to achieve high degree of dependency-free parallelism and efficient scheduling of individual sub-tasks during build execution.

## Sample Usage

Refer to ./MuThr/MuThr.csproj for a sample executable that uses the SDK.

## TODO

Adaptation mechanism used to allow externally defined semantic to be converted into the initial build input.
