using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using MuThr.DataModels.BuildActions;

Console.WriteLine("hello world");

Subject<int> sub = new();
IObservable<int> subObv = sub;

subObv.Subscribe(Console.WriteLine);
subObv.Subscribe(Console.WriteLine);

for (int i = 0; i < 10; i++)
{
    sub.OnNext(i);
}

// //lang=json
// string actionSrc = """
// {
//     "$type": "command",
//     "ChildTasks": [
//         {
//             "$type": "command",
//             "Process": "echo",
//             "Arguments": ["hello world \n 111"]
//         }
//     ],
//     "Process": "grep",
//     "Arguments": [
//         "hello"
//     ]
// }
// """;

// BuildAction? action = JsonSerializer.Deserialize<BuildAction>(actionSrc);

// Console.WriteLine(JsonSerializer.Serialize(action));