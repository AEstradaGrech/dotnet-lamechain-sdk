using System.Reflection;
using Microsoft.Extensions.AI;
using Anthropic.Models.Messages;

Console.WriteLine("=== ChatResponse ===");
foreach (var c in typeof(ChatResponse).GetConstructors()) Console.WriteLine("ctor: " + c);
foreach (var p in typeof(ChatResponse).GetProperties()) Console.WriteLine("prop: " + p);

Console.WriteLine("=== ChatMessage ===");
foreach (var c in typeof(ChatMessage).GetConstructors()) Console.WriteLine("ctor: " + c);
foreach (var p in typeof(ChatMessage).GetProperties()) Console.WriteLine("prop: " + p);

Console.WriteLine("=== ChatFinishReason ===");
foreach (var c in typeof(ChatFinishReason).GetConstructors()) Console.WriteLine("ctor: " + c);
foreach (var p in typeof(ChatFinishReason).GetProperties()) Console.WriteLine("prop: " + p);
foreach (var m in typeof(ChatFinishReason).GetMembers(BindingFlags.Public | BindingFlags.Static)) Console.WriteLine("static: " + m);

Console.WriteLine("=== TextContent ===");
foreach (var c in typeof(TextContent).GetConstructors()) Console.WriteLine("ctor: " + c);
foreach (var p in typeof(TextContent).GetProperties()) Console.WriteLine("prop: " + p);

Console.WriteLine("=== TextReasoningContent ===");
foreach (var c in typeof(TextReasoningContent).GetConstructors()) Console.WriteLine("ctor: " + c);
foreach (var p in typeof(TextReasoningContent).GetProperties()) Console.WriteLine("prop: " + p);

Console.WriteLine("=== FunctionCallContent ===");
foreach (var c in typeof(FunctionCallContent).GetConstructors()) Console.WriteLine("ctor: " + c);
foreach (var p in typeof(FunctionCallContent).GetProperties()) Console.WriteLine("prop: " + p);
