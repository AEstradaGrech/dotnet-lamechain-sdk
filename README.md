# dotnet-lamechain-sdk
A fluent .NET framework for chaining structured OllamaSharp prompts with sequence and parallel workflows.

## What is LameChain?

LameChain is a lightweight SDK designed to make chaining LLM instructions easy, while enforcing structured output and clear prompt orchestration.

It provides a fluent experience for building command chains that can:

- execute steps in sequence or in parallel
- pass outputs between commands
- boost prompts with additional contextual sources
- generate a final result from multiple intermediate outputs

### The core concepts

LameChain is built around three main components:

1. **Commands**: define how to build prompts, call Ollama, and parse JSON results into typed C# objects.
2. **Chain Steps**: configure and execute command chains, while managing feeds and context.
3. **Fluent Extensions**: expose a readable API for composing valid chains without manual wiring.

This structure makes it easy to use commands standalone, while also enabling richer workflows when you need multiple LLM requests or mixed result types.

## What’s in the box

- A commands-based system for OllamaSharp with structured output support
- Fluent chain extensions for building readable sequence/parallel flows
- Chain rule classes that enforce valid SDK usage
- Inference and Embeddings services with startup configuration helpers

## How it works

### LameChain Commands

A command is the core unit that:

- creates the system prompt
- sends the request to Ollama
- validates output structure
- deserializes JSON into a typed `TResult`

A command is composed of three generic pieces:

- `TCommand`: command implementation and prompt logic
- `TRequest`: input data for the command
- `TResult`: expected structured response type

Example commands range from simple chat prompts:

```csharp
_commandsFactory.GetCommand<ChatPromptCommand, PromptCommandRequest, ChatMessage>()
```

...to rich business commands like:

```csharp
_commandsFactory.GetCommand<CustomerBriefingCommand, CustomerBriefingRequest, CustomerBriefing>()
```

A command builds a system message from three possible sources:

- `_systemMessage`: optionally provided at construction
- `Core Message`: built from default instruction or DB-retrieved instruction
- `Guidance Message`: passed with `TRequest`

These message parts can be combined in different ways:

- use only `_systemMessage`
- use only `Core Message`
- use only `Guidance Message`
- append/prepend guidance to core instruction
- inject data between sections

This flexible composition model helps you adapt prompts to the needs of your application and the strengths of your model.

### Command types

#### Commands without DB access

Also called `DefaultCommand` or `GuidedPromptCommand`.

These are the simplest commands and often work well with only request guidance like "You are a helpful assistant." Example:

```csharp
var command = _factoryCommands.GetCommand<MessagePromptCommand, ChatMessage>(guidance: null, settings: null);
var request = new PromptCommandRequest {
    Prompt = reqDto.UserPrompt,
    GuidanceMessage = reqDto.SystemMessage
};
var chatMessage = await command.Prompt(request);
```

#### Commands with DB access

If you want to reuse rich system instructions from storage, use `DbCommand`.

A `DbCommand` accepts a retriever function such as:

```csharp
Func<string, string, Task<string>>
```

The function can read instructions from a database, document store, or any custom source.

This command type supports:

- `source`
- `messageName`

and may also support a fallback `DefaultMode` when no DB instruction is available.

#### Core Commands

The framework includes ready-to-use core commands for common use cases, including simple chat, RAG-style prompts, and structured output generation.

##### Atomic Value Commands

Atomic value commands perform small structured requests for values like:

- integers
- booleans
- dates

They are useful when you need the model to compute discrete values that are then used programmatically in your process.

Example:

```csharp
public Task<PurchaseReceipt> GetCustomerDiscount(float appliedDiscount, DateTime daysToNextGift, bool withPurchaseGift);

var receipt = await GetCustomerDiscount(
    await CalculateDiscountCommand("Review purchase history and customer account creation date to calculate...", customerData),
    await CalculateDaysToNextGiftCommand("Based on the data output the minimum date for the next purchase gift etc", customerData),
    await BoolCommand("Based on the provided data, decide if the customer should receive a gift with the purchase", customerData)
);

var customerMessage = await _factory.GetCustomerAnswerCommand(receipt);
```

Atomic commands also support DB-based instructions for models that need stronger guidance.

#### Custom Commands

To build your own custom command:

1. Create a C# POCO result type for `TResult`
2. Implement `BasePromptCommand<TResult>` or `DbPromptCommand<TResult>`
3. Optionally create a custom request type inheriting from `PromptCommandRequest`
4. Override `Prompt(PromptCommandRequest request)` to generate prompt logic
5. Optionally override `getDefaultInstruction()` and/or `getPromptInstruction(request)`

## Chain Steps

A `ChainStep` stores:

- the command to run
- command configuration
- chaining configuration
- feeds and contextual data

It executes the command, processes the result, and passes outputs to the next step.

### Step types

#### `SingleThrowStep`

- the basic unit of work
- consumes one input
- produces one output
- the only step type that can execute the chain
- must end the chain or subchain

#### `MultiThrowStep`

`MultiThrowStep` is used for parallel or branched execution:

- `SplitterStep` (parallel outputs)
- `PipedStep` (same command over multiple inputs)
- `JunctionStep` (rejoin multiple outputs)

##### Splitter

A splitter takes one input and produces many outputs.
It can be used with `.Tap(...)` or `.SplitThrough(...)`.

##### Pipe

After splitting, use `.Pipe(...)` to execute a single command over multiple inputs.

##### Junction

Use `.Join(...)` to merge split outputs into a single result.
A `JunctionStep` is itself a `SingleThrowStep`, so it can continue the chain or execute.

> Note: you cannot end a chain on a multi-throw step. Only `SingleThrowStep` and subclasses 
        like Junction step supports execution.

### Step features

All steps also support feed configuration, allowing them to:

- pull data from previous steps (`FeedFrom`)
- inject extra context
- boost prompts with external sources

Steps are mutable and can clone or expand themselves via fluent methods.

Example mutation methods:

```csharp
// Expand is for SingleThrowSteps
public SingleThrowStep ExpandTo(IJsoneable command, StepSettings? stepSettings, string? feedForwardInstruction = null)
    => Activator.CreateInstance(typeof(SingleThrowStep), command, stepSettings, feedForwardInstruction) as SingleThrowStep;

public SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings? settings, string? feedForwardInstruction = null)
    where TCommand : BasePromptCommand<TResult>, new()
    => ExpandTo(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

public TStep ExpandTo<TStep>(IJsoneable command, StepSettings? request, string? feedForwardInstruction = null)
    where TStep : ChainStep
    => Activator.CreateInstance(typeof(TStep), command, request, feedForwardInstruction) as TStep;

public TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null)
    where TCommand : BasePromptCommand<TResult>, new()
    where TStep : ChainStep
    => ExpandTo<TStep>(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

// for Tap() chained steps
public SplitterStep Plug(List<StepInstruction> instructions, StepSettings? request, string? splitterFeedFwd = null)
  => Activator.CreateInstance(typeof(SplitterStep), instructions, request, splitterFeedFwd) as SplitterStep;

// for SplitThrough() chained steps
public SplitterStep SplitTo(StepInstruction splitted, List<StepInstruction> instructions)
  => Activator.CreateInstance(typeof(SplitterStep), splitted, instructions) as SplitterStep;

// For piped steps
public TStep ExpandTo<TStep>(StepInstruction instruction) where TStep : ChainStep
    => Activator.CreateInstance(typeof(TStep), instruction) as TStep;
```

## Fluent Extensions

The LameChain fluent API helps you build chains that are valid and composable.
It exposes only the methods that make sense for the current step type.

### Basic chain API

```csharp
.StartWith(StepInstruction firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null)
.Then(this SingleThrowStep step, IJsoneable command, StepSettings request, string? feedFwdInstruction = null)
.Tap(this SingleThrowStep step, List<StepInstruction> instructions, StepSettings? plugSettings = null)
.SplitThrough(this SingleThrowStep step, StepInstruction splitted, List<StepInstruction> instructions)
.Pipe(this SplitterStep step, IJsoneable command, StepSettings pipedSettings, string? pipeFeedFwd = null)
.Join(this SplitterStep step, StepInstruction instruction)
.ExposeThisId(this SingleThrowStep step, out Guid id)
.WithRebujito(this SingleThrowStep | SplitterStep step, List<string> sources, string? guidance = null, int? feedDose = null)
.ThenExecuteAsync(this SingleThrowStep step, bool withFinalMessage = false, bool withReplay = false, CommandSettings finalMsgSettings = null)
```

### Extension behavior

- `.StartWith(...)` begins a new chain
- `.Then(...)` continues a single-output chain
- `.Tap(...)` splits into parallel command branches
- `.SplitThrough(...)` fans one output into several commands
- `.Pipe(...)` runs a command over multiple outputs
- `.Join(...)` merges branch results into one output
- `.ExposeThisId(...)` captures step IDs for later feeding
- `.WithRebujito(...)` boosts context with extra sources
- `.ThenExecuteAsync(...)` finalizes execution and returns `ChainResult`

## Example usage

```csharp
return await LameChain
    .StartWith(new StepInstruction(
            _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Generate a character name and assign it an age (the age might be a specific number or a rough string approximation).",
                settings: request.Settings),
            settings: new StepSettings(new PromptCommandRequest(
                message: request.Prompt,
                guidanceMessage: request.SystemMessage
            )),
            feedFwd: ""),
        defaultSettings: request.Settings,
        finalSysMessage: request.FinalSystemMessage,
        chainIntent: "Create game character")
    .ExposeThisId(out var startId)
    .Then(
        _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
            instruction: "Reduce the previous output to ensure it only contains the requested name and age, remove the rest",
            request.Settings),
        new StepSettings(new PromptCommandRequest("")),
        feedFwdInstruction: "Use the name and the age to develop your part of the character")
    .ExposeThisId(out var thenId)
    .SplitThrough(
        new StepInstruction(
            _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Generate a succinct 'iconic moment' for the character, that is: a description of a typical situation or scene for this character, a moment that should represent its nature and the way it is. Use no more than 50-60 words",
                settings: request.Settings),
            settings: new StepSettings(new PromptCommandRequest(message: "")),
            feedFwd: "Use this character typical scene as a character concept to inspire your creations"),
        new List<StepInstruction> {
            new StepInstruction(
                _promptsFactory.GetStringChoiceCommand(
                    guidanceMessage: "Select a faction from the available list for the game character you are creating. Use the provided data to select the faction that fits best or choose at random if none stands out."),
                new StepSettings(new StringChoiceRequest(
                    choices: [ "Game Faction A", "Game Faction B", "Game Faction C" ],
                    message: "Select a game faction for the character",
                    settings: null,
                    model: null))
                    .FeedFrom(startId),
                feedFwd: "Use this game related data to ground your profile to the game lore"),
            new StepInstruction(
                _promptsFactory.GetEnumChoiceCommand<EGameLocations>(
                    guidanceMessage: "Select a game location from the available list for the game character you are creating. Use the provided data to select the location that fits best with the profile."),
                new StepSettings(new PromptCommandRequest(message: "Select a game location for the character as stated in your instruction")),
                feedFwd: "Use the selected location as the character's place of birth"),
            new StepInstruction(
                _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: request.Instructions.Skip(4).First().SystemMessage),
                settings: new StepSettings(new PromptCommandRequest(message: "Generate a background story or profile for the character you are creating. Use the provided data to figure out what kind of character may be appropriate in terms of style, mood, vibe..."))
                    .WithDataBoost(
                        "Use the below data to bias your final response towards that style, ambience, topic or vibe",
                        await _langSearch.SearchRankedTexts(new RankedPageRequest { Count = 6, Query = "Bujias Campanolo, El Dia de la Bestia" }, returnSnippet: true)),
                feedFwd: "Use this profile as an inspiration for your final character profile, but adapt it to the game lore")
        })
    .Pipe(
        _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
            instruction: "Review the content so far and combine it with the provided sources in a very short story plot (around 50 words)",
            settings: request.Settings),
        pipedSettings: new StepSettings(new PromptCommandRequest(message: "")),
        pipeFeedFwd: "Review all the sources and get a consistent overview of the expected character profile.")
    .WithRebujito(
        await _langSearch.SearchRankedTexts(new RankedPageRequest { Count = 3, Query = "H.P Lovecraft stories. Richard Bachman novels" }, returnSnippet: false),
        guidance: "",
        feedDose: 100)
    .Join(
        new StepInstruction(
            _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Generate a full character profile (400-500 words) with the provided data. Include a background story, a psychological profile and a 'usual routines' section"),
            settings: new StepSettings(new PromptCommandRequest(message: "Generate the requested character profile")).FeedFrom(thenId),
            feedFwd: ""))
    .ThenExecuteAsync(request.WithFinalMessage, request.WithReport, request.FinalMessageSettings ?? request.Settings);
```
