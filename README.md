[![NuGet](https://img.shields.io/nuget/v/Estrada.OllamaSharp.LameChain.SDK.svg)](https://www.nuget.org/packages/Estrada.OllamaSharp.LameChain.SDK)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0%2B-blue.svg)](https://dotnet.microsoft.com/)

# dotnet-lamechain-sdk
A fluent .NET framework for chaining structured OllamaSharp prompts with sequence and parallel workflows with tools support.

## What is LameChain?

LameChain is a lightweight SDK designed to make chaining LLM instructions easy, while enforcing structured output and clear prompt orchestration.

It provides a fluent experience for building command chains that can:

- execute steps in sequence or in parallel
- pass outputs between commands
- boost prompts with additional contextual sources
- generate a final result from multiple intermediate outputs

### Core features:

- Asynchronous inference using a CQRS-style commands system.
- Structured Output prompts (llm responses parsed to C# POCO classes).
- Configurable workflows using the FluentApi.
- Chain steps system to configure different type of nodes in your workflows, with different features for different purposes.
- Tools support.
- Multi-provider (ollama | anthropic | groq).

### The core concepts

LameChain is built around three main components:

1. **Commands**: define how to build prompts, call Ollama, and parse JSON results into typed C# objects.
2. **Chain Steps**: configure and execute command chains, while managing feeds and context.
3. **Fluent Extensions**: expose a readable API for composing valid chains without manual wiring.
4. **Ollama Tools**: add functions as tools and let the LLM decide which tools to use to generate a more accurate response.

This structure makes it easy to use commands standalone, while also enabling richer workflows when you need multiple LLM requests or mixed result types.

## What’s in the box

- A commands-based system for OllamaSharp with structured output and
  tool execution support.
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


`TResult` can be any basic type (integers, string, Lists...) or plain C# classes that can be annotated
with JsonProperties (recommended, at least the JsonPropertyName) and `Ollama Lame Attributes` (these are 
Custom Attributes that will help the LLM to understand and generate the expected output).

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

#### Vector Search Commands

Vector Search command are commands that are supposed to be used in any type of task that requires an Embeddings Generator, so this are mostly commands that will
perform a similarity search on a vectors database to do some task later with an LLM. Because of that, Vector Search Commands have a constructor for tasks without LLM dependency
that requires only an embeddings generator and a query lambda function, and other constructor expecting an inference service in case you want to do some process with the LLM
before or after the similarity search.

#### Sourceable Commands

Sourceable Commands are commands that return ALWAYS a collection of Texts, usually to use them as source for other processes in the chain. The sources
can be gathered in any way (it might be a web search command, a similarity search or LLM-generated content) so use Sourceable Commands to generalize the output
despite the source / gathering process.

(Note: the main purpose of these kind of commands is to create StashedSteps with reusable sources of data)

#### Storeable Commands

If the purpose of a Sourceable Command is to retrieve data from a source and make it available to the chain, the purpose of Storeable Commands is just the opposite:
store the output of a step in a external source. 

This are generic commands that store a lambda function with this signature: Task<TStored> mthdName(TStored item, string collection) (right now the sdk is designed for documental dbs like mongo or Estrada.ChromaDb.Repos)

A Storeable Command must always return the result of the insert, which should be the stored object with its database id, so it can be passed as context to the next step.

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

1. Create a C# POCO result type for `TResult` (optionally, use the Ollama Lame Attributes to annotate the model)
2. Implement `BasePromptCommand<TResult>` or `DbPromptCommand<TResult>`
3. Optionally create a custom request type inheriting from `PromptCommandRequest`
4. Override `Prompt(PromptCommandRequest request)` to generate prompt logic
5. Optionally override `getDefaultInstruction()` and/or `getPromptInstruction(request)`

### Ollama Lame Attributes

Instead of writting an instruction explaining the LLM what to output for the requested `TResult` or read it
from a DB, you can use the framework's OllamaAttributes to annotate the model at class and/or property level
to compose a more specific prompt to guide the model in its task (the DB approach is recommended though, since you cannot change
the attributes content once the application is compiled and deployed).

There are four types of OllamaAttribute:

- `OllamaJsonOutput`: this defines the expected output at class levels and has two fields available to fill:
      > Title: this should be a short description of the output goal, like ('Evaluation of X task and output of X business model').
      > Description: here you should explain what it expected to LLM to do in terms of output, reasoning steps... here you can be more extensive in the explanation of the task.
        
- `OllamaJsonProperty`: this defines a property of the model, they can be stacked and have two fields:
      > Title: this is a category name to allow the stacking of multiple annotations of the same 'type'. By default you shoud add a 'Description' category, but this is
               made this way so you can build any section to annotate your model according to your needs (you could build an 'Output Examples' section, for example).
      > Prop Description: this is the actual text that will be appended to your category. This should be a description of the field and what to expect.

- `OllamaRequirement` : this are 'special' attributes that will create their own section in the system message enforcing the model to follow specific rules.
                        They can be used at class or property level to add constraints.

- `OllamaJsonHint` : similar to the requirements attribute, this can be used at class or property level to guide the model about a specific part of the task
                     on how to generate a specific field. This are meant to serve as a support for the LLM with hints in the style of 'Review the user input and analyze the sources' 
                     or 'Pay attention to the customer tone to score this 'purchase_satisfaction' field'... use them when you want to add a part of the instruction that does not 
                     describe neither commands the LLM but advices or guides its reasoning process.

*Note: to make use of the Ollama Lame Attributes system your model MUST inherit from `StructuredOutput.cs`, otherwise the annotations won't be parse

Here is an example of an annotated structured output:

``` csharp

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Numerical answer for a query that can be answered with a decimal / float number according to the user intent and/or any specified instructions", Description = @"This schema contains a 'numeric_result' OR NULL in response to a given query. You MUST analyze the 
query to extract the intent, review any given instructions and reason if the query can be answered in numerical terms. In case it is, reason the problem to a numerical value that is logic and consistent with the analyzed intent. 
# IMPORTANT: In case the query CAN'T be answered in numerical terms,you MUST return null.")]
    [OllamaJsonRequirement("Output value MUST be a float number if the query intent can be answered in numerical terms OR NULL in case it can be not")]
    public class NumericResponse : StructuredOutput
    {
        [JsonPropertyName("numeric_result")]
        [OllamaJsonProperty(Title ="Description", PromptDescription = "A decimal or float value with the right logical response when it is possible to answer with a number, else null")]
        [OllamaJsonHint("Review carefully the input to reason if the request can be answered numerically or not to provide an accurate response that is compliant with the schema and the provided details about the expected output")]
        [OllamaJsonRequirement("- The answer MUST BE A FLOAT WHEN the proposed problem CAN be answered with a number. For example: { 'numeric_result' : 89.89 }")]
        [OllamaJsonRequirement("- The answer MUST BE NULL WHEN the proposed problem CANNOT be answered in numerical terms. For example: { 'numeric_result': null } ")]
        public float? Result { get; set; }
    }
}

```

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

##### Junction

Use `.Join(...)` to merge split outputs into a single result.
A `JunctionStep` is itself a `SingleThrowStep`, so it can continue the chain or execute.

> Note: you cannot end a chain on a multi-throw step. Only `SingleThrowStep` and subclasses 
        like Junction step supports execution.

#### Stash

A stash is a type of step that is meant to be to execute some process and store the results as
a texts list to serve as source for other processes in the chain (for example, perform a similarity
search or augment a user query with an LLM)

A stash is not greedy by default (so the next one will read the stash as it would do with any other step) and 
it is isolated from the previous (so recieves no output 
from the previous in its prompt), but you can configure this params according to your needs.

#### Store

A Store is a type of step that is used to persist chain data. It can be the output of the chain or any 
intermediate step whose output you want to store immediately in case the chain fails to complete.

This type of steps will always deserialize the output of the previous step and will try to store it using the 
storing lambda passed on costruction and then forward the insert result to the next step.

#### Conditional

A conditional step inserts the configured step or subchain if a boolean Expression evaluates to true. This steps are useful to add simple branching logic
to your chain without using the LLM. They recieve an `Expression<Func<bool>>` as input param and execute the compiled expression during chain execution
to swap the alternate branch into the mainchain if the condition evaluates to true, else it continues the original main branch.

#### Smart Conditional

A conditional step executes a ScoredBoolEvaluation command to swap or not the configured 'TrueBranch' into the main chain or continueing
with the main chain if evaluates to false, passing along the justification comment to keep the chain context consistency. It is the 
'smart' equivalent to the ConditionalStep but using the LLM to perform the evaluation.

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


### Step features

All steps also support feed configuration, allowing them to:

- pull data from previous steps (`FeedFrom`)
- inject extra context
- boost prompts with external sources

Steps are mutable and can clone or expand themselves via fluent methods.

Current step mutation methods:

```csharp

    public SingleThrowStep ExpandTo(IJsoneable command, StepSettings? stepSettings, string? feedForwardInstruction = null)
        => Activator.CreateInstance(typeof(SingleThrowStep), command, stepSettings, feedForwardInstruction) as SingleThrowStep;

    public SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings? settings, string? feedForwardInstruction = null) where TCommand : BasePromptCommand<TResult>, new()
        => ExpandTo(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

    public TStep ExpandTo<TStep>(IJsoneable command, StepSettings? request, string? feedForwardInstruction = null) where TStep : ChainStep
        => Activator.CreateInstance(typeof(TStep), command, request, feedForwardInstruction) as TStep;
    
    public TStep ExpandTo<TStep>(StepInstruction instruction) where TStep : ChainStep
        => Activator.CreateInstance(typeof(TStep), instruction) as TStep;

    public TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null)
        where TCommand : BasePromptCommand<TResult>, new()
        where TStep : ChainStep
            => ExpandTo<TStep>(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

    public SplitterStep Plug(List<StepInstruction> instructions, StepSettings? request, string? splitterFeedFwd = null)
        => Activator.CreateInstance(typeof(SplitterStep), instructions, request, splitterFeedFwd) as SplitterStep;
    
    public SplitterStep SplitTo(StepInstruction splitted, List<StepInstruction> instructions)
        => Activator.CreateInstance(typeof(SplitterStep),  splitted, instructions) as SplitterStep;

    public StashedStep ToStash(StepInstruction instruction, bool isGreedy = false, bool isIsolated = true)
    {
        if (!instruction.Command.GetType().IsAssignableTo(typeof(SourceableCommand)))
            throw new InvalidOperationException($"{nameof(ChainStep)} >> {nameof(ToStash)} >> INVALID STEP CONFIGURATION >> The configured command type ({instruction.Command.GetType().Name}) is not a {typeof(SourceableCommand)} or any subclass of it");

        return Activator.CreateInstance(typeof(StashedStep), instruction.Command, instruction.StepSettings, isGreedy, isIsolated, instruction.FeedFwdInstruction) as StashedStep;
    }

    public ConditionalStep ToConditional(Expression<Func<bool>> condition, StepSettings stepSettings, string? feedFwd = null)
        => Activator.CreateInstance(typeof(ConditionalStep), condition, stepSettings, feedFwd) as ConditionalStep;

    public StoredStep<TStored> AsStore<TStored>(StepInstruction instruction) where TStored : class
        => Activator.CreateInstance(typeof(StoredStep<TStored>), instruction) as StoredStep<TStored>;

    public SmartConditionalStep ToSmartConditional(StepInstruction instruction)
    {
        if (instruction.Command.GetType() != typeof(ScoredBoolCommand) && !instruction.Command.GetType().IsSubclassOf(typeof(ScoredBoolCommand)))
            throw new InvalidDataException($"{nameof(SmartConditionalStep)} >> {instruction.Command.GetType().Name} >> A SmartConditionalStep command must be a ScoredBoolCommand or a subclass of it");

        return Activator.CreateInstance(typeof(SmartConditionalStep), instruction.Command, instruction.StepSettings, instruction.FeedFwdInstruction) as SmartConditionalStep;
    }

```

## Fluent Extensions

The LameChain fluent API helps you build chains that are valid and composable.
It exposes only the methods that make sense for the current step type.

### Basic chain API

```csharp

.StartWith(StepSettings firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null)
.StartWith<TStep>(StepSettings firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null)
.SubChainWith<TStep>(params object?[]? args)
.Then(this SingleThrowStep step, StepSettings settings)
.ThenIf<TStep>(this SingleThrowStep step, StepSettings evaluator, StepSettings trueInstruction)
.ThenIf(this SingleThrowStep step, StepSettings evaluator, SingleThrowStep trueBranch)
.ThenIf<TStep>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StepSettings trueInstruction)
.ThenIf(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, SingleThrowStep trueBranch)
.Tap(this SingleThrowStep step, List<StepSettings> instructions, StepSettings? plugSettings = null)
.Tap(this SingleThrowStep step, List<SingleThrowStep> subchains, StepSettings? plugSettings = null)
.SplitThrough(this SingleThrowStep step, StepSettings splitted, List<StepSettings> instructions)
.Pipe(this SplitterStep step, StepSettings pipedSettings)
.Join(this SplitterStep step, StepSettings instruction)
.Store<TStored>(this SingleThrowStep step, StepSettings storeInstruction)
.StoreIf<TPrev>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StepSettings trueInstruction)
.StoreIf<TPrev>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StoredStep<TPrev> trueBranch)
.Stash(this SingleThrowStep step, StashSettings stashSettings, out Func<Guid> stashId)
.StashIf(this SingleThrowStep step, StepSettings evaluator, StashSettings stashSettings)
.StashIf(this SingleThrowStep step, StepSettings evaluator, StashedStep trueBranch)
.ForwardFirstType<TStep>(this ChainStep step)
.ExposeThisId(this SingleThrowStep step, out Func<Guid> id)
.ExposeThisId(this SplitterStep step, out Func<Guid> id)
.ExposeThisStep(this SingleThrowStep step, out SingleThrowStep exposed)
.ExposeThisStep(this SplitterStep step, out SplitterStep exposed)
.WithRebujito(this SingleThrowStep step, List<string> sources, string? guidance = null, int? feedDose = null)
.WithRebujito(this SplitterStep step, List<string> sources, string? guidance = null, int? feedDose = null)
.ChainFeedsFrom(this SingleThrowStep step, List<Func<Guid>> steps, string? guidance = null)
.ChainFeedsFrom(this SplitterStep step, List<Func<Guid>> steps, string? guidance = null)
.UseBroadcaster(this SingleThrowStep step, Action<Guid, string, LogLevel> broadcaster)
.ThenExecuteAsync(this SingleThrowStep step, bool withFinalMessage = false, bool withReplay = false, CommandSettings finalMsgSettings = null)
```

### Extension behavior

- `.StartWith(...)` begins a new chain
- `.SubChainWith(...)` begins a new sub chain
- `.UseBroadcaster(...)` adds a C# Action that will recieve any broadcasted event from the chain steps to log them or store
- `.Then(...)` continues a single-output chain
- `.Tap(...)` splits into parallel command branches
- `.SplitThrough(...)` fans one output into several commands
- `.Pipe(...)` runs a command over multiple outputs
- `.Join(...)` merges branch results into one output
- `.Stash(...)` adds a stashed step to use it as feed source in the chain
- `.Store(...)` adds a stored step to persist the previous output outside the chain
- `.ThenIf(...)` adds a conditional or smart conditional step that executes a branch if a boolean condition is met
- `.StashIf(...)` adds a stash step that can be subchained IF a boolean condition is passed, else continues the chain
- `.StoreIf(...)` adds a stored step that will be executed IF a boolean condition is passed
- `.ForwardFirstType<TStep>(...)` sets / returns the first step of a subchain. Use this to 'close' a subchain that starts with one TStep and ends with another type
- `.ExposeThisId(...)` captures step IDs for later feeding
- `.ExposeThisStep(...)` captures step for later feeding
- `.WithRebujito(...)` boosts context with extra sources
- `.ChainFeedsFrom(...)` configures a step feed by passing the step id's to read from
- `.ThenExecuteAsync(...)` finalizes execution and returns `ChainResult`

### Broadcast chain execution:

You surely will want to know what is happening during the chain execution (specially when it fails) and to do so, the framework
uses  'broadcaster'. A broadcaster is a C# Action that encapsulates a method with this signature:

``` csharp
    Task mthd(Guid stepId, string message, LogLevel level);
```

This action is registered and executed in the _runner, recieving the broadcasted information from steps & substeps and executing
whatever logic you want (logging, for example). This is done this way to avoid coupling the sdk with any logging library and to
allow more flexibility when processing the produced chain events.

#### Setup a broadcaster:


1 - Create a method with your chain event processing logic (in this case, logging)

```csharp

private void broadcaster(Guid runnerId, string message, LogLevel level)
{
    string logMessage = $"{level} >> CHAIN STEP {runnerId} >> {message}";

    switch (level)
    {
        case (LogLevel.Error):
        case (LogLevel.Critical):
            _logger.LogError(logMessage);
            break;
        case (LogLevel.Warning):
            _logger.LogWarning(logMessage);
            break;
        case (LogLevel.Information):
        case (LogLevel.Debug):
        case (LogLevel.Trace):
        default:
            _logger.LogInformation(logMessage);
            break;
    }
}

```

2 - Encapsulate it in a C# Action (use a helper method to get the action already configured)

``` csharp

 public Action<Guid, string, LogLevel> GetBroadcastAction()
    => new Action<Guid,string, LogLevel>(broadcaster);

```

3 - Use the Fluent API to register the action in the chain runner

``` csharp

var result = await LameChain
    .StartWith(new StepInstruction(
        _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
        new StepSettings(new ChatCommandRequest(request.Prompt, request.ChatHistory))),
        request.Settings
    )
    .UseBroadcaster(GetBroadcastAction())
    //.Then(...)

```

## Usage example

This example demonstrates how you can use LameChain to split a relatively long processess that might be too heavy (in cognitive terms) for the LLM into
more short and focused tasks that will add their results iteratively to achieve a refined final output that should be better than the one you would achieve
with a single instruction (specially for 'dumb' local models).

The chain simulates a creative process to create character concepts for videogames, but I guess it could be used for a movie or serie script too.
It starts with a simple instruction to build the character in different sequential and paralles processes biased towards the desired styles with web search data


```csharp

    public async Task<ChainResult> ParallelChainExample(ChainedPrompt request)
        => await LameChain
            .StartWith(new StepSettings(
                    _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                        instruction: "Generate a character name and assign it an age (the age might be a specific number or a rough string approximation).", //This should be the 'request.SystemMessage'
                        settings: request.Settings),
                    new PromptCommandRequest(
                        message: "Generate a character for a game ambiented in Spain in the XVI century", // This should be the 'request.Prompt'
                        guidanceMessage: "You are a character concept creator for a videogames company" // bias the output of the first step by assigning it a role that makes it an expert in the topic.
                    )),
                defaultSettings: request.Settings, // default settings for all the chain. If you don't pass individual settings to a command, this will be used instead
                finalSysMessage: "Ensure you output your answer in old castillian spanish style, but be consistent with the provided context data.", //This is just to demonstrate how to use the Final Message. Let's say you are doing some tests about how should the character speak in the game
                chainIntent: "Create game character") // This will be passed to all steps so every LLM request has a clear idea of what is the final task / overall goal
            .UseBroadcaster(GetBroadcastAction())
            .ExposeThisId(out var startId)
            .Then(new StepSettings(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Reduce the previous output to ensure it only contains the requested name and age, remove the rest",
                    request.Settings), // pass indiviual settings for each step if necessary to regulate how focused or creative is the LLM
                feedFwd: "Use the name and the age to develop your part of the character")
            )// Help the next step with its task by passing 'Feed Forward' messages that will be added to the context along with the previous output.
            .ExposeThisId(out var thenId)
            .SplitThrough(
            //This step will split the chain but executing a command first that will feed every plugged substep.
            // The splitted step recieves the previous output (ideally only the name and age after the reduction) and generate some kind of descriptive 'picture' of a possible character that will be passed
            // to the parallel branches for further post-processing.
                new StepSettings(
                    _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                        instruction: "Generate a succinct 'iconic moment' for the character, that is: a description of a typical situation or scene for this character, a moment that should represent its nature and the way it is. Use no more than 50-60 words",
                        settings: request.Settings), // You could pass some very creative settings in this step for example
                    feedFwd: "Use this character typical scene as a character concept to inspire your creations"), 
                [
                        //Based on this little character concept (that inherits the name and age) the other steps will pick a faction, a game location as hometowm and generate an extensive background story to use it as a base for the final result (that will merge the three outputs)
                    new StepSettings(
                        _factory.GetStringChoiceCommand( //You can use different type of Lame Commands in the chain. Here I'm using an AtomicValue Command that selects a single string from the passed list based on the given instruction
                            guidanceMessage: "Select a faction from the available list for the game character you are creating. Use the provided data to select the faction that fits best or choose at random if none stands out."),
                        new StringChoiceRequest(
                            choices: ["Germaners", "Comuneros", "Tercio Imperial" ],
                            message: "Select a game faction for the character", // Enforce the instruction if you get hallucinations with dumb models or maybe the context adds too much noise and misleads the LLM
                            isGuidanceAppend: true, // I'm using the command default instruction so I set this param to true to append the chain context 
                            model: null),
                        feedFwd: "# IMPORTANT: the provided faction name MUST be the character faction") // Help the next worker focus on its task when you start to add too much content to the LLM's context window (for example joining three outputs, like in the next step)
                            .FeedFrom(startId), // This is just to demonstrate an individual feed in a splitted step from a previous process different than the previous
                    new StepSettings(
                        _factory.GetEnumChoiceCommand<EGameLocations>( // You can use other type of AtomicValue command for quick selections based on your app code. values  Here I'm using an AtomicValue command that selects an app enum value based on the instruction.
                            guidanceMessage: "Select a game location from the available list for the game character you are creating. Use the provided data to select the location that fits best with the profile."),
                            new PromptCommandRequest(
                                message: "Select a game location for the character as stated in your instruction", 
                                isGuidanceAppend: true
                            ),
                            feedFwd: "# IMPORTANT: THIS IS THE CHARACTER PLACE OF ORIGIN. You MUST use the selected location as the character's place of birth"), // Every parallel branch can add its own Feed Forward message to help the next step to understand / use the output of its instruction
                    new StepSettings(
                        _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                            instruction:"Generate a background story or profile for the character you are creating. Use the provided data to figure out what kind of character might appropriate in terms of style, mood, vibe..."),
                        new PromptCommandRequest(message: "", isGuidanceAppend : true),
                        feedFwd: "Use this profile as an inspiration for your final character profile, but adapt it to the game lore"
                    )
                    .WithDataBoost( // Steps can be boosted by feeding the output of previous steps but also by adding external string sources.
                    // In this case I'm adding semi-random data to the bias the generated character base profile towards specific styles.
                    // But ideally you would add here well processed sources (in this example it could be real human-made character concepts made by the game studio artists
                        "Use the below data to bias your final response towards that style, ambience, topic or vibe",
                        await _langSearch.SearchWebTexts(new WebSearchRequest("Don Pablo o La vida del buscón. Lazarillo de Tormes, sinopsis.", 5), returnSnippet: false, resultsClamp: 100)),
                        // Guide the refining / summarizing step to leverage the output of the different branches and get more consistent results
                ])
            // Now that the chain has been splitted in 3 branches, it is possible to work on each branch indepently by piping commands that will be executed on each recieved previous output
            // This part of the example tries to demonstrate how to use another step to post-process the generated outputs and get more consistent results by generating
            // some semi-random content that is based on the generated content so far (the goal is to augment the previous results and have more base material to work with in the joining step)
            // Note that now every FeedForward message from the splitter will be 'spent' in this step, you have to use the pipeFeedFwd message to guide the next one
            .Pipe(new StepSettings(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Review the content so far and combine it with the provided sources in a very short story plot (around 50 words)",
                    settings: request.Settings),
                new PromptCommandRequest(message: "", isGuidanceAppend: true),
                feedFwd: "Review all the sources and get a consistent overview of the expected character profile. #IMPORTANT: use the character 'Faction' and game place of origin to generate your profile")
            )
            // You can add rebujitos of data to influence the outputs of each branch of the pipe if you need.
            // In this case I'm simulating more style biasing without filtering it, but ideally you would add business content that you would like to apply on each branch.
            // In this example, it could be more content produced by the game studio staff (like scene scripts or even quest scripts, the idea
            // is to add get results that are aligned with the game lore so the final junction step does not hallucinate and add content from it's training dataset
            //.WithRebujito(
            //    await _langSearch.SearchWebTexts(new WebSearchRequest("Revuelta de los Comuneros. Rebelion de las Germanias", results: 3), returnSnippet: false),
            //    guidance: "Use this data as a source of style references and add merge them in your final response along with the generated game lore. Output your response in always in English despite the source language.", // It is possible to add guidance instruction about the rebujito content to help the lLM to use it
            //    feedDose: 100)
            .Join(new StepSettings(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Generate a full character profile (400-500 words) with the provided data. Review the character Faction and place of origin, you MUST include those in your profile. Include a background story, a psychological profile and a 'usual routines' section"),
                    new PromptCommandRequest(
                        message: "Generate the requested character profile",
                        isGuidanceAppend: true)
                ) // Enforce the instruction (specially if you are using small local models and the context window is relatively filled)
                .FeedFrom(thenId) // Feed the name and age to ensure it uses the one generated at this step (dumb models may have alucinated with the background story and the rebujito feeds)
            )
            .ThenExecuteAsync(request.WithFinalMessage, request.WithReport, request.FinalMessageSettings ?? request.Settings);

            // You can request a final 'user-friendly' message that would make use of the chain output to generate the response, but
            // allows individual guidancemessage and settings allowing more flexibility in the usage
            // For example, if this was the beginning of a game conversation with an NPC and the last Joining step was a CharacterModel.cs with many properties defining a detailed game character
            // then you could make use of the final 'user-friendly' message to start the conversation with the newly created NPC using the rich CharacterModel.cs as context.
            // Because the ChainResult includes both, you could then return the final ChatMessage and store the CharacterModel in DB from the client app.
```

You can also create parallel subchains with multiple substeps. This is a new version of the previous example that includes a bit of everything:

```csharp

    public async Task<ChainResult> ParallelSubChainsExample(ChainedPrompt request)
        => await LameChain
            .StartWith<StashedStep>( // start with a stash of sources from Chroma to set the style of the generated base character. 
                new StashSettings(
                    _factory.GetVectorSearchSourceable(_chromaService.SimilaritySearch),
                    new VectorSearchRequest(
                        index: "game-lore-sources",
                        query: request.Prompt, // This will be used to get the initial sources for the next step
                        embedder: "nomic-embed-text",
                        dimensions: 512,
                        results: 4
                    ),
                    feedFwd:"Use this sources to set the style and ambience of your generated character",
                    isGreedy: false,
                    isIsolated: true),
                defaultSettings: request.Settings, 
                finalSysMessage: "Your response must syntetize the provided profile in a descriptive text (around 400 words) presenting the character (like some kind of teaser or spoiler)", //This is just to demonstrate how to use the Final Message. Let's say you are doing some tests about how should the character speak in the game
                chainIntent: "Create game character")
            .UseBroadcaster(GetBroadcastAction()) // broadcast the chain status
            .Then(new StepSettings(_factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Generate a character name and assign it an age (the age might be a specific number or a rough string approximation).", //This should be the 'request.SystemMessage'
                    settings: request.Settings),
                new PromptCommandRequest(
                    message: string.Empty,
                    guidanceMessage: "You are a character concept creator for a videogames company" // bias the output of the first step by assigning it a role that makes it an expert in the topic.
                    )
                )
            )
            .ExposeThisId(out var startId)
            .Then(new StepSettings(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Reduce the previous output to ensure it only contains the requested name and age, remove the rest",
                    request.Settings),
                feedFwd: "Use the name and the age to develop your part of the character")
            ) 
            .ExposeThisId(out var thenId)
            .Tap([ // Add the subchains
                LameChain.SubChainWith<SingleThrowStep>(new StepSettings(
                    _factory.GetStringChoiceCommand( 
                        guidanceMessage: "Select a faction from the available list for the game character you are creating. Use the provided data to select the faction that fits best or choose at random if none stands out."),
                    new StringChoiceRequest(
                        choices: ["Germaners", "Comuneros", "Tercio Imperial" ],
                        message: "Select a game faction for the character", 
                        isGuidanceAppend: true, 
                        model: null))  
                    .FeedFrom(startId) // Feed a step individually
                    )
                .Then(new StepSettings(_factory.GetMessagePromptCommand("Expand the selected faction with a short description of it (40-50) words. If the faction is based on a real historic faction, then you must be faithful to the historic facts related to the faction history and nature, despite it's ideology or morals"))) //enhance
                .Store<ChatMessage>(new StepSettings( // store the result of a substep if you want. The stored item will be passed as context to the next
                    _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage),
                    new StoreableCommandRequest<ChatMessage>(collectionName: $"parallel-subchain"),
                    feedFwd: "# IMPORTANT: the provided faction name MUST be the character faction")
                ), //You can use a StoredStep anywhere in the chain (like a checkpoint for 'important' parts of the chain you whish to preserve in case of failure)
                LameChain.SubChainWith<StashedStep>(
                    new StashSettings(
                        _factory.GetSourceable<RagExpansionCommand>(), //expand vector search query to get different sources in the next VectorSearch / Stash
                        new RagExpansionRequest(expansions: 1, request.Prompt),
                        feedFwd: null,
                        isGreedy:false,
                        isIsolated: true))
                .Stash(new StashSettings( // retrieve more selected sources from your DB to bias the generated character
                        _factory.GetVectorSearchSourceable(_chromaService.SimilaritySearch), // style bias docs
                        new VectorSearchRequest(index:"game-lore-sources", query: request.Prompt, "nomic-embed-text", dimensions: 512, results: 4),
                        feedFwd: "Use this sources to set the style and ambience of your generated character", 
                        isGreedy: true, 
                        isIsolated: false,
                        withFullContext: false), //Use only the content of the previous sourceable result (a rag expansion in this case)
                    out var stashId),
                LameChain.SubChainWith<SingleThrowStep>(
                    new StepSettings(
                        _factory.GetEnumChoiceCommand<EGameLocations>( 
                            guidanceMessage: "Select a game location from the available list for the game character you are creating. Use the provided data to select the location that fits best with the profile."),
                        new PromptCommandRequest(
                            message: "Select a game location for the character as stated in your instruction",
                            isGuidanceAppend: true)
                    )
                )
                .Then(new StepSettings( // expand selection (this will help in the next step when adding all the subchain outputs to the context window of the junction step)
                    _factory.GetMessagePromptCommand( 
                    systemMessage: "Expand the selected game location with a short description of it. Review any provided information about the game location or, in case the location is fictional, generate a description that is consistent with the game lore and user intent"),
                    new PromptCommandRequest(
                        message: "Describe the selected game location or generate one that fits with the game lore",
                        isGuidanceAppend: true
                    ),
                feedFwd: "# IMPORTANT: THIS IS THE CHARACTER PLACE OF ORIGIN. Use the selected location as the character's place of birth") // Every parallel branch can add its own Feed Forward message to help the next step to understand / use the output of its instruction
            )
                ])
            .Join( // Join all the subchain outputs to generate the final character adapted to the game lore...
                new StepSettings(
                    _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                        instruction: "Generate a full character profile (400-500 words) with the provided data. Review the character Faction and place of origin, you MUST include those in your profile. Include a background story, a psychological profile and a 'usual routines' section"),
                        new PromptCommandRequest(
                            message: "Generate the requested character profile",
                            isGuidanceAppend: true) 
                )
            ) // ...with the classic rebujito to bias the generation towards a desired theme / topic
            .WithRebujito(
                await _langSearch.SearchWebTexts(new WebSearchRequest("Revuelta de los Comuneros. Rebelion de las Germanias", results: 3), returnSnippet: false),
                guidance: "Use this data as a source of style references and add merge them in your final response along with the generated game lore. Output your response in always in English despite the source language.", // It is possible to add guidance instruction about the rebujito content to help the lLM to use it
                feedDose: 300)
            .ChainFeedsFrom([thenId, stashId])
            .ThenExecuteAsync(request.WithFinalMessage, request.WithReport, request.FinalMessageSettings ?? request.Settings);

```

This example demonstrates how can you use LameChain to build a 'smart' rag that will:

- Evaluate the user input to extract the user intent and know what is he trying to do / what is the user querying about
- Evaluate and score if the user intent is related to any of the available DB collections passed as choices
- In case it finds a related collection, creates a parallell branch that will:
    - 'Expand' the rag retrieval by generating an un-grounded answer to the query that will be used to cover more points in the similarity search and stash it.
    - 'Augment' the user query by generating N variants of the input to cover more points in the similarity search and stash it.
    - Perform a 'smart' similarity search that will use the LLM and the output of the scoring evaluator to select the best DB collection(s) to query them
      using the contents of the previous stashes to find more similar documents and / or more focused on the query topic, then stash the results.
- Execute a RagCommand ('configured' for Q&A tasks grounded to optional sources) that will recieve the result of the Similarity Search (if any) OR
  will try to answer the question with the LLM built-in knowledge from the training dataset


```csharp

   public async Task<ChainResult> SmartRagChain(SimpleCommandRequest request)
        => await LameChain
            .StartWith(
                new StepSettings(
                    _factory.GetCommand<UserIntentCommand, ChatMessage>(systemMessage: "", request.Settings),
                    feedFwd: "Use the generated User Intent to guide you in your task",
                    request.Prompt),
                request.Settings, //Default Settings for all the chains
                finalSysMessage: "", // There is no need to fill this since there is no user final message required
                chainIntent: "")
            .UseBroadcaster(GetBroadcastAction())
            .StashIf(
                new StepSettings(
                    _factory.GetCommand<ScoredBoolCommand, ScoredBoolResponse>(systemMessage: $"Your task is to determine whether the provided User Intent is related to any of the Collections of the list below"), 
                    feedFwd: "Use this data as a reliable source to answer the user"
                ).WithDataBoost("AVAILABLE COLLECTIONS:", await getChromaCollectionChoices(withChatCollections: false)),
                trueBranch: LameChain.SubChainWith<StashedStep>(
                    new StashSettings(
                        _factory.GetSourceable<RagExpansionCommand>(),
                        new RagExpansionRequest(expansions: 1, request.Prompt),
                        feedFwd: string.Empty,
                        isGreedy: false,
                        isIsolated: true)
                    )
                .ExposeThisStep(out var ragExpansion)
                .Stash(new StashSettings(
                        _factory.GetSourceable<QueryAugmentationCommand>(),
                        new RagExpansionRequest(expansions: 3, request.Prompt, withFewShot: true, 2),
                        isGreedy: true,
                        isIsolated: true),
                    out var queryAugmentId)
                .Stash(new StashSettings(
                    _factory.GetEmbeddedSourceable<SmartQuerySourceable>(
                        _chromaService.SimilaritySearch,
                        llamaGuidance: "Select ONLY the 'COLLECTION NAME' value of the provided list OR empty list if there are no collections relevant for the user query."
                    ), 
                    new SmartQueryRequest(
                        request.Prompt,
                        collectionChoices: await getChromaCollectionChoices(withChatCollections: false), // add a formated catalogue of DB collections with descriptions and topics to help the LLM decide
                        maxChoices: 1,
                        resultsPerChoice: 3,
                        guidanceMessage: string.Empty, // this is overwritten by prev_ctx so leave it empty
                        dimensions: 512,
                        model: "nomic-embed-text",
                        filters: null),
                    feedFwd: null,
                    isGreedy: true,
                    isIsolated:true,
                    withFullContext: false,
                    withPrevSchema: false
                    ).WithNestedFeed(nameof(MultiChoiceCommand), [ragExpansion.WhoIsPrevious], isForStep: false) as StashSettings, 
                    //This is the only way of feeding a subranch nested COMMAND (not a step substep) from the owning step
                    // A nested feed is a feed that should go to a sub step or a sub command inside a step
                    // In this case Im passing the result of the ScoredBool (with the justification comment) to the MultiChoice command that selects the best available collections.
                    // This will help the LLM decide and will retur more accurate results when there are overlapping collection topics
                    out var smartQueryId)
                // Feed the VectorSearch command (which is the 'main' command of the SmartQueryCommand)
                // with the expansion stashes
                .ChainFeedsFrom([ragExpansion.GetRunnerId, queryAugmentId])
                // At this point you are working with the last subchain step
                // so you have to finish the subchain by passing the first step to be linked properly with the ConditionalStep
                // use this method to do that and cast it to the right type (this feature is still under development)
                .ForwardFirstType<StashedStep>() 
                )
            .Then(new StepSettings(
                _factory.GetCommand<RagQueryCommand, ChatMessage>(systemMessage: request.SystemMessage),
                new ChatCommandRequest(request.Prompt, request.SystemMessage))
            )
            .ChainFeedsFrom([smartQueryId]) // retrieve the output of the SmartQueryCommand that made the query with the expansions
            .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

```

Here are more simple (and conceptual) examples of usage for conditional steps and stores:

```csharp

    public async Task<ChainResult> ConditionalChainExamples(CommandChatRequest request)
    {           
        // Use Store<TPrevious> to retrieve and store the result of a step using a lambda with your required logic
        // Here I'm saving the new generated assistant message in Chroma
        var result = await LameChain
            .StartWith(new StepSettings(
                _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
                new ChatCommandRequest(request.Prompt, request.ChatHistory)),
                request.Settings //Default Settings for all the chains
            )
            .UseBroadcaster(GetBroadcastAction()) // Add an Action with the right signature to process the broadcasted chain events (logging them or storing them)
            .Store<ChatMessage>(
                new StepSettings(
                    _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage),
                    new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}")
                )
            )
            .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

        // Store a ChainStep result IF a boolean Expression is met (no LLM routing)
        result = await LameChain
            .StartWith(new StepSettings(
                _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
                new ChatCommandRequest(request.Prompt, request.ChatHistory)),
                request.Settings, //Default Settings for all the chains
                finalSysMessage: "" // There is no need to fill neither this or the chainIntent since there is no user final message required
            )
            .UseBroadcaster(GetBroadcastAction())
            .ThenIf<StoredStep<ChatMessage>>(() => request.ChatHistory.Count > 10, // This is a simple example of how to use the Expressions to pass conditions that will be evaluated on chain execution to run the 'True' branch or not 
                new StepSettings(), // this are the conditional step settings. You probably won't need them in unless you are using the LLM in the lambda your are passing. You can use them to feed data as usual and use it in the step logic as needed
                new StepSettings(
                    _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage), // In this example I'm just storing a chat conversation using Chroma without too much processing. Pass a function adapted to the chain model and your logic here
                    new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}")
                )
            )
            .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

        // This is a more explicit / readable usage of the Fluent API to work with StoredSteps.
        result = await LameChain
            .StartWith(new StepSettings(
                _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
                new ChatCommandRequest(request.Prompt, request.ChatHistory)),
                request.Settings)
            .UseBroadcaster(GetBroadcastAction())
            .StoreIf<ChatMessage>(() => request.ChatHistory.Count > 10,
                new StepSettings(),
                new StepSettings(
                    _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage),
                    new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}")
                )
            )
            .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

        return result;
    }

```


And a working example of conditional steps:

``` csharp

    public async Task<ChainResult> ConditionalChatChain(CommandChatRequest request)
        => await LameChain
            .StartWith(
                new StepSettings(
                    _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
                    new ChatCommandRequest(request.Prompt, request.ChatHistory), //Note: this is an example. There is no processing or validation of the chat history, it is supposed to be passed in the right order with the system message at first etc
                    feedFwd: request.ChatHistory.Count > 0 && request.ChatHistory.First().Role == ChatRole.System.ToString() ? 
                    $"This is the original system instruction for the chat. Use it to understand the context of the conversation and the any provided assistant instructions:\n{request.ChatHistory.First().Content}" : string.Empty),
                request.Settings, //Default Settings for all the chains
                finalSysMessage: "", // There is no need to fill this since there is no user final message required
                chainIntent: "Enhanced chat") // Add the chainIntent if you want to provide some global context to all steps / LLM requests.
            .UseBroadcaster(GetBroadcastAction())
            .ThenIf(() => request.ChatHistory.Count > 2, // This is just a dummy condition to trigger the subchain (and the subchain is just a dummy chain to sequentiate a few example steps) 
                new StepSettings(),
                LameChain.SubChainWith<StoredStep<ChatMessage>>(
                    new StepSettings(
                        _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage), 
                        new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}")
                    )
                )
                .Then(new StepSettings(_factory.GetMessagePromptCommand("Your task is to generate an alternate version of the provided assistant response"), 
                    new PromptCommandRequest("Generate an alternate version of the provided assistant response without changing the core of the original content", isGuidanceAppend: true), 
                    feedFwd:"Use this content  as a source to generate yours. Do not chat with the user, just return a version of the lyrics as instructed."
                ))
                .Then(new StepSettings(
                        _factory.GetMessagePromptCommand("Your task is to translate the previous output to pirate english"), // suitable for banking environments
                        feedFwd: null,
                    requestPrompt:"Translate the previous output to pirate english without changing the core of the original content", 
                    isGuidanceAppend: true))
                .ForwardFirstType<StoredStep<ChatMessage>>()
            )
            .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

```

## Ollama Tools:

The OllamaInference service includes a tools-resolution loop that allows you to add functions as tools that a LLM with tools support may select
and use to provide more accurate responses (available for the CommandPrompt<T>(ChatRequest request, [...]) and ChatPrompt() methods).

You can convert any function in your application using the SDK OllamaTools.FromMethod(MethodInfo method) static method, like this:

``` csharp

 public object GetToolDefinition()
    => OllamaTools.FromMethod(_toolsService.GetType().GetMethod(nameof(LlamaSharpTools.ChromaSearchTool)));

```

Or, using the SDK Commands system, add the tools to the request by passing the name of the tool and the MethodInfo of the tool function, like this:

``` csharp

    [HttpPost("/commands/tools/rag-example")]
    public async Task<IActionResult> ProviderMessageTest([FromBody] ChatPromptRequestDto request)
    {
        var chatCommandReq = _mapper.Map<ChatPromptRequestDto, CommandChatRequest>(request);
        
        var commandReq = _mapper.Map<CommandChatRequest, ChatCommandRequest>(chatCommandReq);
        
        commandReq.AddTool(nameof(LlamaSharpTools.ChromaCollectionSelector), _toolsService.GetType().GetMethod(nameof(LlamaSharpTools.ChromaCollectionSelector)));
        commandReq.AddTool(nameof(LlamaSharpTools.ChromaSearchTool), _toolsService.GetType().GetMethod(nameof(LlamaSharpTools.ChromaSearchTool)));

        var response = await _ollamaCommands.PromptCommand<MessagePromptCommand, ChatMessage>(commandReq, request.SystemMessage, chatCommandReq.Settings);

        if (response != null)
            return Ok(response);

        return StatusCode((int)HttpStatusCode.InternalServerError);
    }

```

### ToolService:

Because a tool might do many things and then require many different services from the app, all tools in the SDK must have acces to a IServiceProvider that 
will provide any of those services (for example, a tool may need to execute a few Lame Commands, so it will require an scope to the IPromptCommandFactory).

Also, the service provider should be injected and scoped by the DI system so, to delegate the instantiation and scope lifetime the tool repositories and other
dependant services, the SDK makes use of a generic interface, IToolService<T>, where T must be a class inheriting from ToolService.

This way the Ollama Tools can be groupped into 'tool repositories' that will be injected and managed by the .NET DI system, and available during the scope
of the request to be executed in the tool-resolution loop as the LLM request one tool or another.

Here is an example (available in the sample project). It registers a **LlamaToolsService** as tools repository:

```csharp

    builder.Services
        .AddConfigurations(builder.Configuration)
        .ConfigureLameChain(builder.Configuration, ServiceLifetime.Scoped)
        .WithToolsFrom<LlamaSharpTools>(ServiceLifetime.Scoped)
        .ConfigureLangSearch(builder.Configuration)
        .AddChromaConfiguration(builder.Configuration)

```

Then code your tools inside the tools repository and **decorate the method name and the parameters with the Description attributte** so the LLM knows when to use the tool and
what values to pass as arguments. Here is an example:

```csharp

    [Description("Tool to select the best ChromaDB collection to query based on the user input. Use this tool to get the name of the collection that best matches with the user intent.")]
    public async Task<string> ChromaCollectionSelector(
        [Description("User input to analyze to extract the intent and select the best collection")] string userQuery)
    {
        _logger.LogWarning($"USING TOOL: {nameof(ChromaCollectionSelector)}");

        //using var scope = _services.CreateScope(); 

        var commandsFactory = _services.GetRequiredService<IPromptCommandsFactory>();
        var ragService = _services.GetRequiredService<IRagService>();

        var intentCommand = commandsFactory.GetCommand<UserIntentCommand, ChatMessage>();

        var request = new PromptCommandRequest(userQuery);

        var intent = await intentCommand.Prompt(request);

        _logger.LogWarning($"TOOL_CALL >> {nameof(ChromaCollectionSelector)} >> USER INTENT: {intent.Content}");

        var selectorGuidance = $"# IMPORTANT: This is the analysis of the user intent, use it to be more accurate in your selection: {intent.Content}";

        var collectionsCat = await ragService.GetChromaCollectionChoices(withChatCollections: false);

        var choiceCommand = commandsFactory.GetStringChoiceCommand();

        selectorGuidance += "\n# IMPORTANT: Select ONLY the 'COLLECTION NAME' value of the provided list OR empty list if there are no collections relevant for the user query.";

        var selectorPrompt = $"Select the best collection to retrieve data from given this user query: {userQuery}";

        _logger.LogWarning($"TOOL_CALL >> {nameof(ChromaCollectionSelector)} >> SELECTOR PROMPTS:\n>> USER PROMPT: {selectorPrompt}\n>> GUIDANCE: {selectorGuidance}");

        var choiceReq = new StringChoiceRequest(collectionsCat, selectorPrompt, guidance: selectorGuidance, isGuidanceAppend: false, model: null);

        var choice = await choiceCommand.Prompt(choiceReq);

        _logger.LogWarning($"TOOL_CALL >> {nameof(ChromaCollectionSelector)} >> SELECTED: {choice}");

        return choice;
    }

```

## Multiple providers:

The SDK allows you to use different cloud providers (currently **groq** and **anthropic**) by simply preppending the provider name to the 
request models (for example, **groq/llama3.3:70b-versatile** or **anthropic/claude-opus-4.8**) and
adding the required startup and appsettings configuration.  

(Note: if you dont' pass a provider, the SDK will default to 'ollama' as llm provider)

Here is a configuration example to use Groq as provider:

1 - Add the configuration in your appsettings.json

```json

    "GroqSettings": {
        "BaseUrl": "https://api.groq.com",
        "ApiKey": "<your-api-key-here>",
        "Endpoints": {
            "chat": "/openai/v1/chat/completions"
        }
    },
    "ClaudeSettings": {
        "apiKey": "<your-api-key-here>",
        "defaultModel" : "claude-sonnet-4-6",
    }

```

2 - Use the StartupConfiguration extensin to add the provider in the Program.cs

```csharp

    builder.Services
        .AddConfigurations(builder.Configuration)
        .ConfigureGroqSettings(builder.Configuration)
        .AddGroqApiClient(builder.Configuration)
        .ConfigureClaudeApiClient(builder.Configuration)

```

## 🛠️ Sample Implementation

This repository includes a sample project: [**dotnet-llamasharp**](https://github.com/AEstradaGrech/dotnet-llamasharp)

It demonstrates how to use the SDK to gather data and feed a RAG (Retrieval-Augmented Generation) service, showcasing real-world integration.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.